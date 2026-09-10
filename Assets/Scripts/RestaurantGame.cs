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
    public float remainingTime;

    public RestaurantOrder(int orderNumber, MenuRecipe orderRecipe, Customer customerObject, float orderPatience)
    {
        number = orderNumber;
        recipe = orderRecipe;
        customer = customerObject;
        patience = orderPatience;
        remainingTime = orderPatience;
    }
}

public sealed partial class RestaurantGame : MonoBehaviour
{
    public static RestaurantGame Instance { get; private set; }

    public const int TargetRevenue = 10_000_000;
    private const float DefaultDayLength = 600f;
    private const float BaseOrderInterval = 12f;
    private const float OrderPatience = 45f;
    private const float BaseFryTime = 5f;
    private const int MaxWaitingOrders = 5;
    private const int MaxReputation = 100;
    private static readonly Vector3 DoorPoint = new Vector3(0f, 0.9f, -8f);
    private static readonly Vector3 ExitPoint = new Vector3(0f, 0.9f, -10f);
    private static readonly Color NormalAmbient = new Color(0.55f, 0.48f, 0.38f);

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
    private bool finished;
    private HazardSystem hazards;
    private int dayStartNetRevenue;
    private int dayStartOrders;
    private int dayStartSuccess;
    private int dayStartFailed;
    private float dayTimer;
    private float orderTimer = 3f;
    private float messageTimer;
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
    private float burnTime = BaseFryTime * 1.6f;
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

    /// <summary>대기 중인 매장 주문. HUD 와 자동 검증에서 읽는다.</summary>
    public IReadOnlyList<RestaurantOrder> ActiveOrders => activeOrders;

    private float dayLength = DefaultDayLength;

    private void Awake()
    {
        Instance = this;
        GameTuning.Reset();
        Time.timeScale = 1f;
        ReadDayLengthArgument();
    }

    /// <summary>-daylength 60 처럼 하루 길이를 줄여 결산까지 빠르게 확인할 수 있다.</summary>
    private void ReadDayLengthArgument()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "-daylength" && float.TryParse(args[index + 1], out float seconds) && seconds >= 10f)
            {
                dayLength = seconds;
                Debug.Log($"[Day] 하루 길이를 {seconds}초로 설정");
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

        if (orderTimer <= 0f)
        {
            CreateOrder();
            orderTimer = Difficulty.OrderInterval(orderInterval, day) * orderIntervalMultiplier;
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

            if (order.remainingTime <= 0f)
            {
                failedOrders++;
                streak = 0;
                ChangeReputation(-8);
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
            text.AppendLine($"#{order.number}  {order.recipe.displayName} x1  {Mathf.CeilToInt(order.remainingTime)}s");
        }

        return text.ToString();
    }

    private void UpdateHud()
    {
        if (revenueLabel != null)
        {
            revenueLabel.text = $"REVENUE  ₩{revenue:N0} / ₩{TargetRevenue:N0}";
        }

        if (dayLabel != null)
        {
            dayLabel.text = $"DAY {day}   {Mathf.CeilToInt(dayLength - dayTimer):00}s";
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
            statsLabel.text = $"평판 {reputation}/{MaxReputation}   콤보 x{Difficulty.ComboMultiplier(streak):0.0}   주문 {totalOrders}  성공 {successfulOrders}  실패 {failedOrders}  탄 치킨 {burntChicken}" + (delivery != null ? $"  배달 {delivery.CompletedDeliveries}" : string.Empty);
        }
    }
}
