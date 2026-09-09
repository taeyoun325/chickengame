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

    private readonly List<RestaurantOrder> activeOrders = new List<RestaurantOrder>();
    private int revenue;
    private int day = 1;
    private int totalOrders;
    private int successfulOrders;
    private int failedOrders;
    private int burntChicken;
    private int spending;
    private float dayTimer;
    private float orderTimer = 3f;
    private float messageTimer;
    private GameObject player;
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

    public int Day => day;
    public int Revenue => revenue;

    private void Awake()
    {
        Instance = this;
        GameTuning.Reset();
    }

    public void SetPlayer(GameObject playerObject)
    {
        player = playerObject;
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
        dayTimer += Time.deltaTime;
        orderTimer -= Time.deltaTime;
        messageTimer -= Time.deltaTime;

        if (orderTimer <= 0f)
        {
            CreateOrder();
            orderTimer = orderInterval;
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
            dayTimer -= DayLength;
            day++;
            ShowMessage($"DAY {day} 시작!");
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
        }
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
        Destroy(food.gameObject);
        ShowMessage($"배달 출발! {order.address} ({Mathf.CeilToInt(order.RideTime)}s)");
    }

    public void ReportDeliveryComplete(DeliveryOrder order)
    {
        int payout = order.recipe.price + order.DeliveryFee;
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
        if (food.state != FoodState.Frying)
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
        if (actor.HeldFood != null)
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
        if (actor.HeldFood == null && cookedChicken != null && cookedChicken.state != FoodState.Frying)
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
        activeOrders.Add(new RestaurantOrder(totalOrders, recipe, customer, OrderPatience));
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
