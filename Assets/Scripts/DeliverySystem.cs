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

    /// <summary>이번 배달이 사고로 끝나는지. 출발할 때 정해진다.</summary>
    public bool crashed;

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

    /// <summary>먼 집일수록 사고가 잦다. 이게 없으면 먼 주문이 배달비만 비싼
    /// 공짜 이득이 되어 가까운 주문을 받을 이유가 사라진다.
    /// 스쿠터 튜닝은 시간뿐 아니라 위험도 함께 줄여준다.</summary>
    public float CrashChance =>
        Mathf.Clamp(0.04f + distance * 0.03f, 0f, 0.25f) * GameTuning.DeliverySpeedMultiplier;
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
    private bool forceCrash;

    public int PendingCount => pending.Count;

    /// <summary>대기 중인 배달 주문.</summary>
    public IReadOnlyList<DeliveryOrder> Pending => pending;

    /// <summary>자동 검증에서 배달 주문을 바로 하나 만들 때 쓴다.</summary>
    public void ForceRequest()
    {
        CreateRequest();
    }
    public int RidingCount => riding.Count;
    public int CompletedDeliveries { get; private set; }
    public int MissedDeliveries { get; private set; }
    public int CrashedDeliveries { get; private set; }

    /// <summary>자동 검증에서 다음 출발을 반드시 사고로 만든다.
    /// 확률에 기대면 검증이 어떤 날은 통과하고 어떤 날은 실패한다.</summary>
    public void ForceNextRideCrash()
    {
        forceCrash = true;
    }

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
        // 배달 시뮬레이션은 호스트에서만 돈다.
        if (game == null || !KitchenNetwork.IsHostSide)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        requestTimer -= deltaTime;
        if (requestTimer <= 0f)
        {
            CreateRequest();
            requestTimer = Difficulty.DeliveryInterval(RequestInterval, game.Day);
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
            if (order.crashed)
            {
                CrashedDeliveries++;
                game.ReportDeliveryCrashed(order);
                continue;
            }

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
        order.crashed = forceCrash || Random.value < order.CrashChance;
        forceCrash = false;

        // 사고가 나면 중간에서 돌아오므로 스쿠터가 더 빨리 보인다.
        float ride = order.RideTime * GameTuning.DeliverySpeedMultiplier;
        order.rideTimer = order.crashed ? ride * 0.5f : ride;
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
