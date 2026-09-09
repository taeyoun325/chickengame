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

public sealed class RestaurantGame : MonoBehaviour
{
    public static RestaurantGame Instance { get; private set; }

    public const int TargetRevenue = 10_000_000;
    private const float DayLength = 600f;
    private const float BaseOrderInterval = 12f;
    private const float OrderPatience = 45f;
    private const float BaseFryTime = 5f;
    private const int MaxWaitingOrders = 5;
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
    private HazardSystem hazards;
    private GameObject settlementPanel;
    private Text settlementText;
    private bool settlementOpen;
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

    public int Day => day;
    public int Revenue => revenue;

    private void Awake()
    {
        Instance = this;
        GameTuning.Reset();
        Time.timeScale = 1f;
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
        if (settlementOpen)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                StartNextDay();
            }

            return;
        }

        dayTimer += Time.deltaTime;
        orderTimer -= Time.deltaTime;
        messageTimer -= Time.deltaTime;

        if (orderTimer <= 0f)
        {
            CreateOrder();
            orderTimer = orderInterval * orderIntervalMultiplier;
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
                SendCustomerHome(order);
                activeOrders.RemoveAt(index);
                queueChanged = true;
                ShowMessage($"주문 #{order.number} 취소! 손님이 화나서 떠났습니다");
            }
        }

        if (queueChanged)
        {
            ReflowQueue();
        }

        if (dayTimer >= DayLength)
        {
            EndDay();
        }

        if (messageTimer <= 0f && messageLabel != null)
        {
            messageLabel.text = string.Empty;
        }

        UpdateHud();
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

    public void ConnectHazards(HazardSystem system)
    {
        hazards = system;
    }

    public void ConnectSettlement(GameObject panel, Text panelText)
    {
        settlementPanel = panel;
        settlementText = panelText;
        if (settlementPanel != null)
        {
            settlementPanel.SetActive(false);
        }
    }

    /// <summary>하루가 끝나면 게임을 멈추고 결산을 보여준 뒤 저장한다.</summary>
    private void EndDay()
    {
        dayTimer = 0f;
        settlementOpen = true;
        Time.timeScale = 0f;

        int dayRevenue = revenue + spending - dayStartNetRevenue;
        int dayOrders = totalOrders - dayStartOrders;
        int daySuccess = successfulOrders - dayStartSuccess;
        int dayFailed = failedOrders - dayStartFailed;
        float progress = revenue / (float)TargetRevenue * 100f;

        StringBuilder text = new StringBuilder();
        text.AppendLine($"DAY {day} 영업 종료");
        text.AppendLine();
        text.AppendLine($"오늘 매출        ₩{dayRevenue:N0}");
        text.AppendLine($"주문 {dayOrders}건   성공 {daySuccess}   실패 {dayFailed}");
        text.AppendLine($"탄 치킨 {burntChicken}   버린 음식 {wastedFood}");
        if (hazards != null)
        {
            text.AppendLine($"미끄러짐 {hazards.SlipCount}회   화재 {hazards.FireCount}회");
        }

        text.AppendLine($"업그레이드 지출  ₩{spending:N0}");
        text.AppendLine();
        text.AppendLine($"누적 매출        ₩{revenue:N0} / ₩{TargetRevenue:N0}  ({progress:0.00}%)");
        text.AppendLine();
        text.AppendLine("SPACE 를 눌러 다음 DAY 시작");

        if (settlementText != null)
        {
            settlementText.text = text.ToString();
        }

        if (settlementPanel != null)
        {
            settlementPanel.SetActive(true);
        }

        SaveProgress();
    }

    private void StartNextDay()
    {
        settlementOpen = false;
        Time.timeScale = 1f;
        if (settlementPanel != null)
        {
            settlementPanel.SetActive(false);
        }

        day++;
        dayStartNetRevenue = revenue + spending;
        dayStartOrders = totalOrders;
        dayStartSuccess = successfulOrders;
        dayStartFailed = failedOrders;
        ShowMessage($"DAY {day} 시작!");
    }

    public void SaveProgress()
    {
        SaveData data = new SaveData
        {
            day = day,
            revenue = revenue,
            totalOrders = totalOrders,
            successfulOrders = successfulOrders,
            failedOrders = failedOrders,
            burntChicken = burntChicken,
            wastedFood = wastedFood,
            spending = spending,
            upgradeLevels = upgrades != null ? upgrades.ExportLevels() : System.Array.Empty<int>()
        };

        SaveSystem.Save(data);
    }

    /// <summary>세이브가 있으면 이어서 시작한다.</summary>
    public void LoadProgress()
    {
        SaveData data = SaveSystem.Load();
        if (data == null)
        {
            return;
        }

        day = Mathf.Max(1, data.day);
        revenue = data.revenue;
        totalOrders = data.totalOrders;
        successfulOrders = data.successfulOrders;
        failedOrders = data.failedOrders;
        burntChicken = data.burntChicken;
        wastedFood = data.wastedFood;
        spending = data.spending;
        if (upgrades != null)
        {
            upgrades.ImportLevels(data.upgradeLevels);
            ApplyUpgrades();
        }

        dayStartNetRevenue = revenue + spending;
        dayStartOrders = totalOrders;
        dayStartSuccess = successfulOrders;
        dayStartFailed = failedOrders;
        ShowMessage($"DAY {day} 이어하기 (저장 {data.savedAt})");
    }

    public bool TryExtinguishNearby(Vector3 position)
    {
        if (hazards == null || !hazards.TryExtinguish(position))
        {
            return false;
        }

        ShowMessage("불을 껐습니다!");
        return true;
    }

    private void ThrowAway(PlayerInteraction actor)
    {
        if (actor.HeldFood == null)
        {
            ShowMessage("버릴 것이 없습니다");
            return;
        }

        string description = actor.HeldFood.Describe();
        Destroy(actor.HeldFood.gameObject);
        actor.ClearHeldFood();
        wastedFood++;
        ShowMessage($"{description}을 버렸습니다");
    }

    private void ToggleExtinguisher(PlayerInteraction actor)
    {
        if (actor.CarryingExtinguisher)
        {
            actor.ReturnExtinguisher();
            ShowMessage("소화기를 제자리에 두었습니다");
            return;
        }

        if (!actor.HandsFree)
        {
            ShowMessage("손을 비우고 오세요");
            return;
        }

        actor.TakeExtinguisher();
        ShowMessage("소화기를 들었습니다. 불 앞에서 E");
    }

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

    /// <summary>증설 업그레이드로 열리는 예비 튀김기는 처음에는 꺼져 있다.</summary>
    public void RegisterReserveFryer(GameObject fryer)
    {
        reserveFryers.Add(fryer);
        fryer.SetActive(false);
    }

    public bool TrySpend(int amount)
    {
        if (revenue < amount)
        {
            return false;
        }

        revenue -= amount;
        spending += amount;
        return true;
    }

    /// <summary>구매한 업그레이드 레벨을 실제 게임 값에 반영한다.</summary>
    public void ApplyUpgrades()
    {
        if (upgrades == null)
        {
            return;
        }

        fryTime = BaseFryTime * Mathf.Pow(0.88f, upgrades.LevelOf(UpgradeKind.FryerSpeed));
        burnTime = fryTime * 1.6f;
        orderInterval = BaseOrderInterval * Mathf.Pow(0.9f, upgrades.LevelOf(UpgradeKind.Marketing));
        GameTuning.PlayerSpeedMultiplier = Mathf.Pow(1.12f, upgrades.LevelOf(UpgradeKind.MoveSpeed));
        GameTuning.DeliverySpeedMultiplier = Mathf.Pow(0.85f, upgrades.LevelOf(UpgradeKind.ScooterSpeed));

        int extraFryers = upgrades.LevelOf(UpgradeKind.ExtraFryer);
        for (int index = 0; index < reserveFryers.Count; index++)
        {
            if (reserveFryers[index] != null)
            {
                reserveFryers[index].SetActive(index < extraFryers);
            }
        }
    }

    public void BuyUpgrade(int slot)
    {
        if (upgrades != null)
        {
            upgrades.TryPurchase(slot);
        }
    }

    public void ConnectEvents(RandomEventSystem system, Text eventText)
    {
        events = system;
        eventLabel = eventText;
    }

    public void OnEventStarted(GameEvent gameEvent)
    {
        switch (gameEvent.kind)
        {
            case GameEventKind.RushHour:
                orderIntervalMultiplier = 0.5f;
                break;
            case GameEventKind.AppPromotion:
                deliveryFeeMultiplier = 2f;
                break;
            case GameEventKind.PickyCustomers:
                patienceMultiplier = 0.5f;
                break;
            case GameEventKind.Blackout:
                powerOn = false;
                RenderSettings.ambientLight = new Color(0.14f, 0.12f, 0.12f);
                break;
        }

        ShowMessage($"[{gameEvent.displayName}] {gameEvent.description}");
    }

    public void OnEventEnded(GameEvent gameEvent)
    {
        orderIntervalMultiplier = 1f;
        deliveryFeeMultiplier = 1f;
        patienceMultiplier = 1f;
        if (!powerOn)
        {
            powerOn = true;
            RenderSettings.ambientLight = NormalAmbient;
        }

        ShowMessage($"{gameEvent.displayName} 종료");
    }

    /// <summary>단체 주문처럼 손님이 한꺼번에 몰릴 때 쓴다.</summary>
    public void SpawnOrderBurst(int count)
    {
        for (int index = 0; index < count; index++)
        {
            CreateOrder();
        }
    }

    public void PayFine(int amount)
    {
        revenue = Mathf.Max(0, revenue - amount);
        spending += amount;
    }

    private void HandOverDelivery(PlayerInteraction actor)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Packaged)
        {
            ShowMessage("포장된 치킨을 들고 오세요");
            return;
        }

        if (delivery == null)
        {
            return;
        }

        DeliveryOrder order = delivery.MatchPending(food.Recipe.kind);
        if (order == null)
        {
            ShowMessage($"{food.Recipe.displayName} 배달 주문이 없습니다");
            return;
        }

        delivery.Dispatch(order);
        actor.ClearHeldFood();
        Destroy(food.gameObject);
        ShowMessage($"배달 출발! {order.address} ({Mathf.CeilToInt(order.RideTime)}s)");
    }

    public void ReportDeliveryComplete(DeliveryOrder order)
    {
        int payout = order.recipe.price + Mathf.RoundToInt(order.DeliveryFee * deliveryFeeMultiplier);
        revenue += payout;
        successfulOrders++;
        ShowMessage($"배달 완료! {order.address} +₩{payout:N0}");
    }

    public void ReportDeliveryMissed(DeliveryOrder order)
    {
        failedOrders++;
        ShowMessage($"배달 주문 취소! {order.address}");
    }

    public void ShowMessage(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message;
        }

        messageTimer = 3f;
    }

    public void TickFood(FoodItem food, float deltaTime)
    {
        if (food.state != FoodState.Frying || !powerOn)
        {
            return;
        }

        food.cookProgress += deltaTime;
        if (food.cookProgress >= burnTime && !food.burnCounted)
        {
            food.burnCounted = true;
            burntChicken++;
            food.SetState(FoodState.Burnt);
            ShowMessage("치킨이 탔습니다!");
        }
        else if (food.cookProgress >= fryTime && food.state == FoodState.Frying)
        {
            food.SetState(FoodState.Cooked);
            ShowMessage("치킨이 익었습니다! 튀김기에서 꺼내세요");
        }
    }

    private void TakeRawChicken(PlayerInteraction actor)
    {
        if (!actor.HandsFree)
        {
            ShowMessage("손에 든 물건을 먼저 내려놓으세요");
            return;
        }

        GameObject chickenObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chickenObject.name = "Raw Chicken";
        chickenObject.transform.localScale = Vector3.one * 0.65f;
        FoodItem chicken = chickenObject.AddComponent<FoodItem>();
        chickenObject.AddComponent<FoodTickProxy>();
        chicken.SetState(FoodState.Raw);
        actor.SetHeldFood(chicken);
        ShowMessage("생닭을 들었습니다");
    }

    private void UseFryer(PlayerInteraction actor, Station station)
    {
        if (hazards != null && hazards.IsOnFire(station))
        {
            ShowMessage("불이 붙은 튀김기입니다! 소화기를 쓰세요");
            return;
        }

        if (actor.HeldFood != null && actor.HeldFood.state == FoodState.Raw)
        {
            if (station.StoredFood != null)
            {
                ShowMessage("튀김기가 사용 중입니다");
                return;
            }

            FoodItem chicken = actor.ReleaseHeldFood(station.transform);
            chicken.SetState(FoodState.Frying);
            chicken.cookProgress = 0f;
            ShowMessage($"튀김 시작! {fryTime:0.0}초 뒤 꺼내세요");
            return;
        }

        FoodItem cookedChicken = station.StoredFood;
        if (actor.HandsFree && cookedChicken != null && cookedChicken.state != FoodState.Frying)
        {
            actor.SetHeldFood(cookedChicken);
            ShowMessage(cookedChicken.state == FoodState.Burnt ? "탄 치킨입니다. 버리세요" : "튀긴 치킨을 들었습니다");
            return;
        }

        ShowMessage("생닭을 들고 튀김기에 넣으세요");
    }

    private void ApplySauce(PlayerInteraction actor, Station station)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Cooked)
        {
            ShowMessage("튀긴 치킨을 들고 오세요");
            return;
        }

        if (food.sauced)
        {
            ShowMessage("이미 양념을 발랐습니다");
            return;
        }

        MenuRecipe recipe = MenuDatabase.Get(station.sauceKind);
        food.sauced = true;
        food.SetRecipe(recipe);
        ShowMessage($"{recipe.displayName} 양념 완료!");
    }

    /// <summary>양념대에서 바를 소스를 바꾼다.</summary>
    public void CycleSauce(Station station)
    {
        station.sauceKind = station.sauceKind == MenuKind.Seasoned ? MenuKind.Soy : MenuKind.Seasoned;
        ShowMessage($"양념대: {MenuDatabase.Get(station.sauceKind).displayName}");
    }

    private void PackChicken(PlayerInteraction actor)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Cooked)
        {
            ShowMessage("익은 치킨을 들고 오세요");
            return;
        }

        if (food.dirty)
        {
            ShowMessage("바닥에 떨어진 치킨입니다. 쓰레기통에 버리세요");
            return;
        }

        if (!food.ReadyToPack)
        {
            ShowMessage($"{food.Recipe.displayName}은 양념대를 먼저 거쳐야 합니다");
            return;
        }

        food.SetState(FoodState.Packaged);
        ShowMessage($"{food.Recipe.displayName} 포장 완료! 계산대로 가져가세요");
    }

    private void CompleteOrder(PlayerInteraction actor)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Packaged)
        {
            ShowMessage("포장된 치킨을 들고 오세요");
            return;
        }

        if (activeOrders.Count == 0)
        {
            ShowMessage("현재 주문이 없습니다");
            return;
        }

        RestaurantOrder order = FindOrderFor(food.Recipe.kind);
        if (order == null)
        {
            ShowMessage($"{food.Recipe.displayName}을 기다리는 손님이 없습니다");
            return;
        }

        int bonus = Mathf.RoundToInt(Mathf.Clamp(order.remainingTime, 0f, OrderPatience) * 100f);
        int payout = order.recipe.price + bonus;
        revenue += payout;
        successfulOrders++;
        activeOrders.Remove(order);
        SendCustomerHome(order);
        ReflowQueue();
        actor.ClearHeldFood();
        Destroy(food.gameObject);
        ShowMessage($"주문 #{order.number} {order.recipe.displayName} 완료! +₩{payout:N0}");
    }

    /// <summary>같은 메뉴를 기다리는 손님 중 가장 급한 손님을 고른다.</summary>
    private RestaurantOrder FindOrderFor(MenuKind kind)
    {
        RestaurantOrder best = null;
        foreach (RestaurantOrder order in activeOrders)
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

    private void CreateOrder()
    {
        if (activeOrders.Count >= MaxWaitingOrders)
        {
            return;
        }

        totalOrders++;
        MenuRecipe recipe = MenuDatabase.RandomFor(day);
        GameObject customerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        customerObject.name = $"Customer {totalOrders}";
        customerObject.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Destroy(customerObject.GetComponent<Collider>());
        Customer customer = customerObject.AddComponent<Customer>();
        customer.Initialise(DoorPoint, QueueSlot(activeOrders.Count), ExitPoint, recipe.packagedColor);
        activeOrders.Add(new RestaurantOrder(totalOrders, recipe, customer, OrderPatience * patienceMultiplier));
        ShowMessage($"주문 #{totalOrders} {recipe.displayName} 들어왔습니다!");
    }

    private static Vector3 QueueSlot(int index)
    {
        return new Vector3(-4.5f + index * 2.2f, 0.9f, -4f);
    }

    private static void SendCustomerHome(RestaurantOrder order)
    {
        if (order.customer != null)
        {
            order.customer.Leave();
        }
    }

    /// <summary>앞 손님이 빠지면 뒷 손님들이 한 칸씩 당겨 선다.</summary>
    private void ReflowQueue()
    {
        for (int index = 0; index < activeOrders.Count; index++)
        {
            Customer customer = activeOrders[index].customer;
            if (customer != null)
            {
                customer.SetQueueSlot(QueueSlot(index));
            }
        }
    }

    private void UpdateHud()
    {
        if (revenueLabel != null)
        {
            revenueLabel.text = $"REVENUE  ₩{revenue:N0} / ₩{TargetRevenue:N0}";
        }

        if (dayLabel != null)
        {
            dayLabel.text = $"DAY {day}   {Mathf.CeilToInt(DayLength - dayTimer):00}s";
        }

        if (ordersLabel != null)
        {
            if (activeOrders.Count == 0)
            {
                ordersLabel.text = "주문을 기다리는 중...";
            }
            else
            {
                StringBuilder text = new StringBuilder("ORDERS\n");
                foreach (RestaurantOrder order in activeOrders)
                {
                    text.AppendLine($"#{order.number}  {order.recipe.displayName} x1  {Mathf.CeilToInt(order.remainingTime)}s");
                }

                ordersLabel.text = text.ToString();
            }
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
            statsLabel.text = $"주문 {totalOrders}  성공 {successfulOrders}  실패 {failedOrders}  탄 치킨 {burntChicken}" + (delivery != null ? $"  배달 {delivery.CompletedDeliveries}" : string.Empty);
        }
    }
}

public sealed class FoodTickProxy : MonoBehaviour
{
    private FoodItem food;

    private void Awake()
    {
        food = GetComponent<FoodItem>();
    }

    private void Update()
    {
        if (RestaurantGame.Instance != null && food != null)
        {
            RestaurantGame.Instance.TickFood(food, Time.deltaTime);
        }
    }
}
