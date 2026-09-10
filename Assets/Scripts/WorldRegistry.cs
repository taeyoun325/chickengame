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

    /// <summary>바닥에 굴러다니는 치킨만 주울 수 있다.
    ///
    /// 임자가 있는 것은 빼야 한다. 남의 손에 든 것을 옆에서 뽑아 가거나,
    /// 튀김기에 올려둔 것을 스테이션 절차를 건너뛰고 집어 가면 안 된다.</summary>
    private static bool CanBePickedUp(FoodItem food)
    {
        return food != null
               && food.state != FoodState.Frying
               && !food.IsHeld
               && food.RestingStation == null;
    }

    /// <summary>반경 안에서 지금 쓰려는 스테이션을 고른다. 거리만 보면 두 스테이션
    /// 사이에 섰을 때 등 뒤의 것이 잡히므로, 바라보는 방향에 가산점을 준다.</summary>
    public static Station NearestStation(Vector3 position, float maxDistance, Vector3 facing = default)
    {
        bool useFacing = facing.sqrMagnitude > 0.01f;
        Vector3 forward = useFacing ? facing.normalized : Vector3.zero;

        Station best = null;
        float bestScore = float.MaxValue;
        for (int index = 0; index < stations.Count; index++)
        {
            Station station = stations[index];
            if (station == null || !station.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 delta = station.transform.position - position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > maxDistance)
            {
                continue;
            }

            float score = distance;
            if (useFacing && distance > 0.01f)
            {
                // 정면이면 최대 1.2m 만큼 가깝게 친다.
                score -= Vector3.Dot(forward, delta / distance) * 1.2f;
            }

            if (score < bestScore)
            {
                best = station;
                bestScore = score;
            }
        }

        return best;
    }

    public static FoodItem NearestLooseFood(Vector3 position, float maxDistance)
    {
        FoodItem closest = null;
        float closestDistance = maxDistance;
        for (int index = 0; index < foods.Count; index++)
        {
            FoodItem food = foods[index];
            if (!CanBePickedUp(food))
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
