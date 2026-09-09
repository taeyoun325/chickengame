using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>배달 주문 하나. 매장 손님과 달리 포장만 넘기면 스쿠터가 알아서 다녀온다.</summary>
public sealed class DeliveryOrder
{
    public readonly int number;
    public readonly MenuRecipe recipe;
    public readonly string address;
    public readonly float distance;
    public float remainingTime;
    public bool dispatched;
    public float rideTimer;

    public DeliveryOrder(int orderNumber, MenuRecipe orderRecipe, string deliveryAddress, float deliveryDistance, float acceptWindow)
    {
        number = orderNumber;
        recipe = orderRecipe;
        address = deliveryAddress;
        distance = deliveryDistance;
        remainingTime = acceptWindow;
    }

    /// <summary>먼 집일수록 배달비가 비싸다.</summary>
    public int DeliveryFee => 3_000 + Mathf.RoundToInt(distance * 800f);

    public float RideTime => 6f + distance * 2.5f;
}

/// <summary>배달대에 포장을 올리면 스쿠터가 출발하고, 돌아오면 매출이 들어온다.</summary>
public sealed class DeliverySystem : MonoBehaviour
{
    private const float RequestInterval = 20f;
    private const float AcceptWindow = 60f;
    private const int MaxPendingRequests = 3;

    private static readonly string[] Addresses =
    {
        "행복아파트 101동", "역전 원룸촌", "중앙시장 2층", "벚꽃빌라 302호", "강변타워 15층"
    };

    private readonly List<DeliveryOrder> pending = new List<DeliveryOrder>();
    private readonly List<DeliveryOrder> riding = new List<DeliveryOrder>();
    private RestaurantGame game;
    private Transform scooter;
    private Vector3 scooterHome;
    private float requestTimer = 25f;
    private int nextNumber = 1;

    public int PendingCount => pending.Count;
    public int RidingCount => riding.Count;
    public int CompletedDeliveries { get; private set; }
    public int MissedDeliveries { get; private set; }

    public void Initialise(RestaurantGame restaurantGame, Transform scooterTransform)
    {
        game = restaurantGame;
        scooter = scooterTransform;
        if (scooter != null)
        {
            scooterHome = scooter.position;
        }
    }

    private void Update()
    {
        if (game == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        requestTimer -= deltaTime;
        if (requestTimer <= 0f)
        {
            CreateRequest();
            requestTimer = RequestInterval;
        }

        for (int index = pending.Count - 1; index >= 0; index--)
        {
            DeliveryOrder order = pending[index];
            order.remainingTime -= deltaTime;
            if (order.remainingTime <= 0f)
            {
                pending.RemoveAt(index);
                MissedDeliveries++;
                game.ReportDeliveryMissed(order);
            }
        }

        TickRides(deltaTime);
    }

    private void TickRides(float deltaTime)
    {
        for (int index = riding.Count - 1; index >= 0; index--)
        {
            DeliveryOrder order = riding[index];
            order.rideTimer -= deltaTime;
            if (order.rideTimer > 0f)
            {
                continue;
            }

            riding.RemoveAt(index);
            CompletedDeliveries++;
            game.ReportDeliveryComplete(order);
        }

        MoveScooter(deltaTime);
    }

    /// <summary>스쿠터는 배달을 나가면 문 밖으로 사라졌다가 돌아온다.</summary>
    private void MoveScooter(float deltaTime)
    {
        if (scooter == null)
        {
            return;
        }

        Vector3 target = riding.Count > 0 ? scooterHome + new Vector3(0f, 0f, -6f) : scooterHome;
        scooter.position = Vector3.MoveTowards(scooter.position, target, 5f * deltaTime);
    }

    /// <summary>배달대에 올린 포장이 어떤 배달 주문에 맞는지 찾는다.</summary>
    public DeliveryOrder MatchPending(MenuKind kind)
    {
        DeliveryOrder best = null;
        foreach (DeliveryOrder order in pending)
        {
            if (order.recipe.kind != kind)
            {
                continue;
            }

            if (best == null || order.remainingTime < best.remainingTime)
            {
                best = order;
            }
        }

        return best;
    }

    public void Dispatch(DeliveryOrder order)
    {
        pending.Remove(order);
        order.dispatched = true;
        order.rideTimer = order.RideTime * GameTuning.DeliverySpeedMultiplier;
        riding.Add(order);
    }

    private void CreateRequest()
    {
        if (pending.Count >= MaxPendingRequests || game == null)
        {
            return;
        }

        MenuRecipe recipe = MenuDatabase.RandomFor(game.Day);
        string address = Addresses[Random.Range(0, Addresses.Length)];
        float distance = Random.Range(1f, 4f);
        DeliveryOrder order = new DeliveryOrder(nextNumber++, recipe, address, distance, AcceptWindow);
        pending.Add(order);
        game.ShowMessage($"배달 주문! {address} {recipe.displayName}");
    }

    public string BuildStatusText()
    {
        if (pending.Count == 0 && riding.Count == 0)
        {
            return "배달 대기 없음";
        }

        StringBuilder text = new StringBuilder("DELIVERY\n");
        foreach (DeliveryOrder order in pending)
        {
            text.AppendLine($"[{order.address}] {order.recipe.displayName}  {Mathf.CeilToInt(order.remainingTime)}s  +₩{order.DeliveryFee:N0}");
        }

        foreach (DeliveryOrder order in riding)
        {
            text.AppendLine($"배달 중... {order.address}  {Mathf.CeilToInt(order.rideTimer)}s");
        }

        return text.ToString();
    }
}
