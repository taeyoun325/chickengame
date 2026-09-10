using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>손님 한 명이 들고 있는 주문.</summary>
public sealed class RestaurantOrder
{
    public readonly int number;
    public readonly MenuRecipe recipe;
    public readonly Customer customer;
    public readonly float patience;
    public readonly int quantity;
    public readonly CustomerMood mood;
    public int delivered;
    public float remainingTime;

    public RestaurantOrder(int orderNumber, MenuRecipe orderRecipe, Customer customerObject, float orderPatience, int orderQuantity, CustomerMood customerMood)
    {
        mood = customerMood ?? CustomerMood.Normal;
        number = orderNumber;
        recipe = orderRecipe;
        customer = customerObject;
        patience = orderPatience;
        quantity = Mathf.Max(1, orderQuantity);
        remainingTime = orderPatience;
    }

    /// <summary>아직 받지 못한 수량.</summary>
    public int Remaining => quantity - delivered;
}

public sealed partial class RestaurantGame : MonoBehaviour
{
    public static RestaurantGame Instance { get; private set; }

    public const int TargetRevenue = 10_000_000;
    private const float DefaultDayLength = 600f;
    private const float BaseOrderInterval = 14f;
    private const float OrderPatience = 45f;
    private const float BaseFryTime = 5f;

    /// <summary>익은 뒤 탈 때까지의 여유. 튀김기를 업그레이드해도 이 여유는 유지된다.
    /// 예전에는 burnTime 이 fryTime 에 비례해서, 최대 업그레이드 시 여유가 1.8초까지 줄었다.</summary>
    private const float BurnGrace = 3f;
    private const int MaxWaitingOrders = 5;
    private const int MaxReputation = 100;

    /// <summary>재기할 때마다 매출에서 떼는 비율. 거듭할수록 아프게 한다.</summary>
    private int ReopenPenaltyPercent => Mathf.Min(60, 20 + reopenCount * 15);
    private static readonly Vector3 DoorPoint = new Vector3(0f, 0.9f, -8f);
    private static readonly Vector3 ExitPoint = new Vector3(0f, 0.9f, -10f);

    private readonly List<RestaurantOrder> activeOrders = new List<RestaurantOrder>();
    private int revenue;
    private int day = 1;
    private int totalOrders;
    private int successfulOrders;
    private int failedOrders;
    private int burntChicken;
    private int spending;
    private int wastedFood;
    private int reputation = MaxReputation;
    private int streak;
    private int bestStreak;
    private int bestDayRevenue;
    private int bestDay;
    private bool finished;
    private int reopenCount;
    private HazardSystem hazards;
    private int dayStartNetRevenue;
    private int dayStartOrders;
    private int dayStartSuccess;
    private int dayStartFailed;
    private float dayTimer;
    private float playSeconds;

    /// <summary>영업한 시간의 합. 목표 달성까지 걸린 시간이 곧 이 게임의 기록이다.</summary>
    public float PlaySeconds => playSeconds;

    /// <summary>기록은 초가 아니라 분:초로 읽어야 감이 온다.</summary>
    public static string Clock(float seconds)
    {
        int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{whole / 60:00}:{whole % 60:00}";
    }
    private float orderTimer = 3f;
    private float messageTimer;
    private float hudTimer;
    private float tagTimer;
    private Text revenueLabel;
    private Text dayLabel;
    private Text ordersLabel;
    private Text messageLabel;
    private Text statsLabel;
    private Text deliveryLabel;
    private Text upgradeLabel;
    private DeliverySystem delivery;
    private UpgradeSystem upgrades;
    private readonly List<GameObject> reserveFryers = new List<GameObject>();
    private float fryTime = BaseFryTime;
    private float burnTime = BaseFryTime + BurnGrace;
    private float orderInterval = BaseOrderInterval;
    private float orderIntervalMultiplier = 1f;
    private float patienceMultiplier = 1f;
    private float deliveryFeeMultiplier = 1f;
    private bool powerOn = true;
    private RandomEventSystem events;
    private Text eventLabel;
    private Text networkLabel;

    public int Day => day;
    public int Revenue => revenue;
    public int Reputation => reputation;
    public int Streak => streak;

    /// <summary>현재 튀김 시간. 검증 하네스가 대기 시간을 맞추는 데 쓴다.</summary>
    public float FryTime => fryTime;

    /// <summary>업그레이드로 쓴 돈까지 포함한 실제 벌어들인 총액.</summary>
    public int GrossEarned => revenue + spending;
    public int SuccessfulOrders => successfulOrders;
    public int FailedOrders => failedOrders;

    /// <summary>대기 중인 매장 주문. HUD 와 자동 검증에서 읽는다.</summary>
    public IReadOnlyList<RestaurantOrder> ActiveOrders => activeOrders;

    /// <summary>자동 검증 중에는 손님이 저절로 들어오지 않게 잠시 끈다.</summary>
    public bool AutoOrdersEnabled { get; set; } = true;

    private float dayLength = DefaultDayLength;

    private void Awake()
    {
        Instance = this;
        GameTuning.Reset();
        GameSpeed.Resume();
        ReadQaArguments();
    }

    /// <summary>-daylength 60, -startday 5 처럼 검증용으로 진행 상태를 지정할 수 있다.</summary>
    private void ReadQaArguments()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "-daylength" && float.TryParse(args[index + 1], out float seconds) && seconds >= 10f)
            {
                dayLength = seconds;
                Debug.Log($"[Day] 하루 길이를 {seconds}초로 설정");
            }
            else if (args[index] == "-startday" && int.TryParse(args[index + 1], out int startDay) && startDay >= 1)
            {
                day = startDay;
                Debug.Log($"[Day] DAY {startDay} 부터 시작");
            }
            else if (args[index] == "-startrevenue" && int.TryParse(args[index + 1], out int startRevenue) && startRevenue >= 0)
            {
                // 목표가 ₩10,000,000 이라 승리 화면까지 정상 플레이로 가려면 수백 DAY 가 걸린다.
                // 승리 경로를 실제로 밟아 보려면 출발점을 옮겨줄 수밖에 없다.
                revenue = startRevenue;
                Debug.Log($"[Day] 매출 ₩{startRevenue:N0} 에서 시작");
            }
        }
    }

    public void ConnectHud(Text revenueText, Text dayText, Text ordersText, Text messageText, Text statsText)
    {
        revenueLabel = revenueText;
        dayLabel = dayText;
        ordersLabel = ordersText;
        messageLabel = messageText;
        statsLabel = statsText;
        UpdateHud();
    }

    private void Update()
    {
        if (GameFlow.Instance != null && !GameFlow.Instance.IsPlaying)
        {
            return;
        }

        // 접속한 손님 화면은 호스트가 보내주는 값만 그린다.
        if (!KitchenNetwork.IsHostSide)
        {
            UpdateHudFromNetwork();
            return;
        }

        dayTimer += Time.deltaTime;
        orderTimer -= Time.deltaTime;
        messageTimer -= Time.deltaTime;

        // 이 게임의 목표는 버티기가 아니라 ₩10,000,000 을 얼마나 빨리 찍느냐다.
        // 그래서 DAY 마다 되감기는 dayTimer 와 별개로, 영업한 시간만 계속 쌓는다.
        // 일시정지와 결산 화면은 위에서 걸러지므로 여기 시간에 들어가지 않는다.
        playSeconds += Time.deltaTime;

        if (orderTimer <= 0f)
        {
            int createdQuantity = 1;
            if (AutoOrdersEnabled)
            {
                createdQuantity = CreateOrder();
            }

            // 세트 주문이 나오면 그만큼 다음 손님을 늦게 보낸다. 수량만 두 배가 되고
            // 간격이 그대로면 DAY 3 에서 수요가 하루아침에 두 배로 뛴다.
            orderTimer = Difficulty.OrderInterval(orderInterval, day)
                         * orderIntervalMultiplier
                         * Difficulty.ReputationOrderScale(reputation)
                         * Difficulty.QuantitySpacing(createdQuantity);
        }

        tagTimer -= Time.deltaTime;
        bool refreshTags = tagTimer <= 0f;
        if (refreshTags)
        {
            tagTimer = 0.2f;
        }

        bool queueChanged = false;
        for (int index = activeOrders.Count - 1; index >= 0; index--)
        {
            RestaurantOrder order = activeOrders[index];
            order.remainingTime -= Time.deltaTime;
            if (order.customer != null)
            {
                order.customer.ShowPatience(order.remainingTime / order.patience);
            }

            if (order.customer != null && refreshTags)
            {
                string moodMark = order.mood.displayName.Length > 0 ? $"[{order.mood.displayName}] " : string.Empty;
                order.customer.ShowTag(order.quantity > 1
                    ? $"{moodMark}{order.recipe.displayName} {order.delivered}/{order.quantity}  {Mathf.CeilToInt(order.remainingTime)}s"
                    : $"{moodMark}{order.recipe.displayName}  {Mathf.CeilToInt(order.remainingTime)}s");
            }

            if (order.remainingTime <= 0f)
            {
                failedOrders++;
                streak = 0;
                ChangeReputation(-order.mood.FailurePenalty(6));
                RushRemainingCustomers(order);
                SendCustomerHome(order);
                activeOrders.RemoveAt(index);
                queueChanged = true;
                PlaySound(GameSound.Fail);
                ShowMessage($"주문 #{order.number} 취소! 손님이 화나서 떠났습니다");
            }
        }

        if (queueChanged)
        {
            ReflowQueue();
        }

        if (dayTimer >= dayLength)
        {
            EndDay();
        }

        if (messageTimer <= 0f && messageLabel != null)
        {
            messageLabel.text = string.Empty;
        }

        TickAllFood(Time.deltaTime);
        CheckVictory();
        UpdateHud();
        PublishNetworkState();
    }

    private void PublishNetworkState()
    {
        if (KitchenNetwork.Online)
        {
            KitchenNetwork.Instance.PublishState(revenue, day, reputation, BuildOrdersText());
        }
    }

    /// <summary>클라이언트 HUD: 호스트가 보내준 값으로만 채운다.</summary>
    private void UpdateHudFromNetwork()
    {
        if (!KitchenNetwork.Online)
        {
            return;
        }

        KitchenNetwork net = KitchenNetwork.Instance;
        if (revenueLabel != null)
        {
            revenueLabel.text = $"REVENUE  ₩{net.Revenue:N0} / ₩{TargetRevenue:N0}";
        }

        if (dayLabel != null)
        {
            dayLabel.text = $"DAY {net.Day}";
        }

        if (ordersLabel != null)
        {
            ordersLabel.text = net.Orders;
        }

        if (statsLabel != null)
        {
            statsLabel.text = $"평판 {net.Reputation}/{MaxReputation}";
        }

        if (networkLabel != null && NetworkSession.Instance != null)
        {
            networkLabel.text = NetworkSession.Instance.BuildStatusText();
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.unscaledDeltaTime;
        }
        else if (messageLabel != null)
        {
            messageLabel.text = string.Empty;
        }
    }

    public void InteractWithStation(PlayerInteraction actor, Station station)
    {
        switch (station.stationType)
        {
            case StationType.Fridge:
                TakeRawChicken(actor);
                break;
            case StationType.Fryer:
                UseFryer(actor, station);
                break;
            case StationType.Sauce:
                ApplySauce(actor, station);
                break;
            case StationType.Packing:
                PackChicken(actor);
                break;
            case StationType.Checkout:
                CompleteOrder(actor);
                break;
            case StationType.Delivery:
                HandOverDelivery(actor);
                break;
            case StationType.Upgrade:
                ShowMessage("1~5 키로 업그레이드를 구매하세요");
                break;
            case StationType.Trash:
                ThrowAway(actor);
                break;
            case StationType.Extinguisher:
                ToggleExtinguisher(actor);
                break;
        }
    }

    public void ConnectNetworkLabel(Text label)
    {
        networkLabel = label;
    }

    public void ConnectHazards(HazardSystem system)
    {
        hazards = system;
    }

    public DeliverySystem Delivery => delivery;

    public void ConnectDelivery(DeliverySystem system, Text deliveryText)
    {
        delivery = system;
        deliveryLabel = deliveryText;
    }

    public void ConnectUpgrades(UpgradeSystem system, Text upgradeText)
    {
        upgrades = system;
        upgradeLabel = upgradeText;
        ApplyUpgrades();
    }

    public void ConnectEvents(RandomEventSystem system, Text eventText)
    {
        events = system;
        eventLabel = eventText;
    }

    public static void PlaySound(GameSound sound)
    {
        if (GameAudio.Instance != null)
        {
            GameAudio.Instance.Play(sound);
        }
    }

    public void ShowMessage(string message)
    {
        ShowLocalMessage(message);

        // 호스트가 처리한 결과는 접속한 손님들에게도 알린다.
        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastNotice(message);
        }
    }

    public void ShowLocalMessage(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message;
        }

        messageTimer = 3f;
    }

    private string BuildOrdersText()
    {
        if (activeOrders.Count == 0)
        {
            return "주문을 기다리는 중...";
        }

        StringBuilder text = new StringBuilder("ORDERS\n");
        foreach (RestaurantOrder order in activeOrders)
        {
            string progress = order.quantity > 1 ? $"{order.delivered}/{order.quantity}" : "x1";
            string mark = order.mood.displayName.Length > 0 ? $" [{order.mood.displayName}]" : string.Empty;
            text.AppendLine($"#{order.number}  {order.recipe.displayName} {progress}{mark}  {Mathf.CeilToInt(order.remainingTime)}s");
        }

        return text.ToString();
    }

    /// <summary>HUD 는 매 프레임 다시 만들 필요가 없다. 문자열 할당을 줄이려고 10Hz 로 제한한다.</summary>
    private void UpdateHud()
    {
        hudTimer -= Time.unscaledDeltaTime;
        if (hudTimer > 0f)
        {
            return;
        }

        hudTimer = 0.1f;

        if (revenueLabel != null)
        {
            revenueLabel.text = $"REVENUE  ₩{revenue:N0} / ₩{TargetRevenue:N0}";
        }

        if (dayLabel != null)
        {
            dayLabel.text = $"DAY {day}   {Mathf.CeilToInt(dayLength - dayTimer):00}s\n{Clock(playSeconds)}";
        }

        if (ordersLabel != null)
        {
            ordersLabel.text = BuildOrdersText();
        }

        if (networkLabel != null && NetworkSession.Instance != null)
        {
            networkLabel.text = NetworkSession.Instance.BuildStatusText();
        }

        if (eventLabel != null && events != null)
        {
            eventLabel.text = events.BuildStatusText();
        }

        if (upgradeLabel != null && upgrades != null)
        {
            upgradeLabel.text = upgrades.BuildShopText();
        }

        if (deliveryLabel != null && delivery != null)
        {
            deliveryLabel.text = delivery.BuildStatusText();
        }

        if (statsLabel != null)
        {
            statsLabel.text = $"평판 {reputation} {Difficulty.ReputationGrade(reputation)}   콤보 x{Difficulty.ComboMultiplier(streak):0.0}   주문 {totalOrders}  성공 {successfulOrders}  실패 {failedOrders}  탄 치킨 {burntChicken}" + (delivery != null ? $"  배달 {delivery.CompletedDeliveries}" : string.Empty);
        }
    }
}
