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

    /// <summary>하루가 끝나면 게임을 멈추고 결산을 보여준 뒤 저장한다.</summary>
    private void EndDay()
    {
        dayTimer = 0f;

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

        text.AppendLine($"최고 콤보 {bestStreak}연속   평판 {reputation}/{MaxReputation}");
        text.AppendLine($"업그레이드 지출  ₩{spending:N0}");
        text.AppendLine($"내일 단가 배율   x{Difficulty.PriceMultiplier(day + 1):0.0}");
        text.AppendLine();
        text.AppendLine($"누적 매출        ₩{revenue:N0} / ₩{TargetRevenue:N0}  ({progress:0.00}%)");
        text.AppendLine();
        text.AppendLine("SPACE 를 눌러 다음 DAY 시작");

        Debug.Log($"[Day] DAY {day} 결산 - 매출 {revenue}");
        SaveProgress();

        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.EnterSettlement(text.ToString());
        }

        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastSettlement(text.ToString());
        }
    }

    public void StartNextDay()
    {
        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.ResumeFromSettlement();
        }

        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastResume();
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
            reputation = reputation,
            bestStreak = bestStreak,
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
        reputation = data.reputation > 0 ? data.reputation : MaxReputation;
        bestStreak = data.bestStreak;
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
        NetworkSpawner.Remove(actor.HeldFood.gameObject);
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
        NetworkSpawner.Remove(food.gameObject);
        ShowMessage($"배달 출발! {order.address} ({Mathf.CeilToInt(order.RideTime)}s)");
    }

    public void ReportDeliveryComplete(DeliveryOrder order)
    {
        streak++;
        bestStreak = Mathf.Max(bestStreak, streak);
        int payout = Mathf.RoundToInt((order.recipe.price + order.DeliveryFee * deliveryFeeMultiplier)
                                      * Difficulty.PriceMultiplier(day) * Difficulty.ComboMultiplier(streak));
        revenue += payout;
        successfulOrders++;
        ChangeReputation(+2);
        PlaySound(GameSound.Delivery);
        ShowMessage($"배달 완료! {order.address} +₩{payout:N0}");
    }

    public void ReportDeliveryMissed(DeliveryOrder order)
    {
        failedOrders++;
        streak = 0;
        ChangeReputation(-5);
        ShowMessage($"배달 주문 취소! {order.address}");
    }

    /// <summary>평판이 0이 되면 폐업, 목표 매출을 넘기면 승리.</summary>
    public void ChangeReputation(int amount)
    {
        reputation = Mathf.Clamp(reputation + amount, 0, MaxReputation);
        if (reputation <= 0)
        {
            FinishGame(false);
        }
    }

    private void CheckVictory()
    {
        if (revenue >= TargetRevenue)
        {
            FinishGame(true);
        }
    }

    private void FinishGame(bool won)
    {
        if (finished || GameFlow.Instance == null)
        {
            return;
        }

        finished = true;
        StringBuilder text = new StringBuilder();
        text.AppendLine(won ? "목표 달성! 치킨 재벌" : "평판 0 - 폐업했습니다");
        text.AppendLine();
        text.AppendLine($"DAY {day} 까지 영업");
        text.AppendLine($"누적 매출  ₩{revenue:N0} / ₩{TargetRevenue:N0}");
        text.AppendLine($"주문 {totalOrders}건   성공 {successfulOrders}   실패 {failedOrders}");
        text.AppendLine($"탄 치킨 {burntChicken}   버린 음식 {wastedFood}");
        if (hazards != null)
        {
            text.AppendLine($"미끄러짐 {hazards.SlipCount}회   화재 {hazards.FireCount}회");
        }

        text.AppendLine();
        text.AppendLine("SPACE 를 눌러 처음부터");

        if (won)
        {
            GameFlow.Instance.EnterVictory(text.ToString());
        }
        else
        {
            GameFlow.Instance.EnterDefeat(text.ToString());
        }
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

    /// <summary>조리는 호스트가 등록된 음식들을 직접 돌린다.
    /// 프리팹에 붙는 별도 컴포넌트를 두면 직렬화가 깨질 여지가 있어 한곳에서 처리한다.</summary>
    private void TickAllFood(float deltaTime)
    {
        for (int index = 0; index < WorldRegistry.Foods.Count; index++)
        {
            FoodItem food = WorldRegistry.Foods[index];
            if (food != null)
            {
                TickFood(food, deltaTime);
            }
        }
    }

    public void TickFood(FoodItem food, float deltaTime)
    {
        if (!powerOn || food.burnCounted)
        {
            return;
        }

        // 익은 뒤에도 튀김기에 그대로 두면 계속 익어서 결국 탄다.
        // (익는 순간 진행이 멈춰 버려서 한동안 치킨이 아예 타지 않았다.)
        bool restingInFryer = food.RestingStation != null && food.RestingStation.stationType == StationType.Fryer;
        bool stillCooking = food.state == FoodState.Frying || (food.state == FoodState.Cooked && restingInFryer);
        if (!stillCooking)
        {
            return;
        }

        food.cookProgress += deltaTime;
        if (food.cookProgress >= burnTime && !food.burnCounted)
        {
            food.burnCounted = true;
            burntChicken++;
            ChangeReputation(-2);
            food.SetState(FoodState.Burnt);
            PlaySound(GameSound.Burnt);
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

        FoodItem chicken = NetworkSpawner.SpawnChicken();
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

            FoodItem chicken = actor.ReleaseHeldFoodTo(station);
            chicken.SetState(FoodState.Frying);
            chicken.cookProgress = 0f;
            PlaySound(GameSound.FryStart);
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
        streak++;
        bestStreak = Mathf.Max(bestStreak, streak);
        float multiplier = Difficulty.PriceMultiplier(day) * Difficulty.ComboMultiplier(streak);
        int payout = Mathf.RoundToInt((order.recipe.price + bonus) * multiplier);
        revenue += payout;
        successfulOrders++;
        ChangeReputation(+3);
        activeOrders.Remove(order);
        SendCustomerHome(order);
        ReflowQueue();
        actor.ClearHeldFood();
        NetworkSpawner.Remove(food.gameObject);
        PlaySound(GameSound.Cash);
        GameEffects.Burst(actor.transform.position + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.25f));
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
        Customer customer = NetworkSpawner.SpawnCustomer($"Customer {totalOrders}");
        customer.Initialise(DoorPoint, QueueSlot(activeOrders.Count), ExitPoint, recipe.packagedColor);
        activeOrders.Add(new RestaurantOrder(totalOrders, recipe, customer, Difficulty.Patience(OrderPatience, day) * patienceMultiplier));
        PlaySound(GameSound.OrderIn);
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
