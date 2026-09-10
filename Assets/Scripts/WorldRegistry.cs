using System.Collections.Generic;
using UnityEngine;

/// <summary>매 프레임 FindObjectsByType 을 돌리는 대신 스테이션과 음식을 등록해 둔다.
/// 네트워크 RPC 가 스테이션을 가리킬 때 쓰는 안정적인 인덱스도 여기서 나온다.</summary>
public static class WorldRegistry
{
    private static readonly List<Station> stations = new List<Station>();
    private static readonly List<FoodItem> foods = new List<FoodItem>();
    private static readonly List<PlayerInteraction> players = new List<PlayerInteraction>();

    public static IReadOnlyList<Station> Stations => stations;
    public static IReadOnlyList<FoodItem> Foods => foods;
    public static IReadOnlyList<PlayerInteraction> Players => players;

    public static void Clear()
    {
        stations.Clear();
        foods.Clear();
        players.Clear();
    }

    public static void Register(Station station)
    {
        if (station == null || stations.Contains(station))
        {
            return;
        }

        station.Index = stations.Count;
        stations.Add(station);
    }

    public static void Unregister(Station station)
    {
        stations.Remove(station);
    }

    public static Station StationAt(int index)
    {
        return index >= 0 && index < stations.Count ? stations[index] : null;
    }

    public static void Register(FoodItem food)
    {
        if (food != null && !foods.Contains(food))
        {
            foods.Add(food);
        }
    }

    public static void Unregister(FoodItem food)
    {
        foods.Remove(food);
    }

    public static void Register(PlayerInteraction player)
    {
        if (player != null && !players.Contains(player))
        {
            players.Add(player);
        }
    }

    public static void Unregister(PlayerInteraction player)
    {
        players.Remove(player);
    }

    /// <summary>주어진 위치에서 반경 안에 있는 가장 가까운 스테이션.</summary>
    public static Station NearestStation(Vector3 position, float maxDistance)
    {
        Station closest = null;
        float closestDistance = maxDistance;
        for (int index = 0; index < stations.Count; index++)
        {
            Station station = stations[index];
            if (station == null || !station.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = Vector3.Distance(position, station.transform.position);
            if (distance < closestDistance)
            {
                closest = station;
                closestDistance = distance;
            }
        }

        return closest;
    }

    public static FoodItem NearestLooseFood(Vector3 position, float maxDistance)
    {
        FoodItem closest = null;
        float closestDistance = maxDistance;
        for (int index = 0; index < foods.Count; index++)
        {
            FoodItem food = foods[index];
            if (food == null || food.state == FoodState.Frying)
            {
                continue;
            }

            float distance = Vector3.Distance(position, food.transform.position);
            if (distance < closestDistance)
            {
                closest = food;
                closestDistance = distance;
            }
        }

        return closest;
    }
}
