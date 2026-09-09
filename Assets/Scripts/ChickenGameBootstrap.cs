using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChickenGameBootstrap : MonoBehaviour
{
    private void Start()
    {
        BuildLighting();
        BuildShop();
        GameObject player = BuildPlayer();
        BuildCamera();
        GameObject gameObject = new GameObject("Restaurant Game");
        RestaurantGame game = gameObject.AddComponent<RestaurantGame>();
        game.SetPlayer(player);
        BuildHud(game);
    }

    private void BuildLighting()
    {
        RenderSettings.ambientLight = new Color(0.55f, 0.48f, 0.38f);
        GameObject lightObject = new GameObject("Shop Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private void BuildShop()
    {
        CreateBlock("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(18f, 0.5f, 12f), new Color(0.22f, 0.18f, 0.14f));
        CreateBlock("Back Wall", new Vector3(0f, 2f, 6f), new Vector3(18f, 4f, 0.5f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Left Wall", new Vector3(-9f, 2f, 0f), new Vector3(0.5f, 4f, 12f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Right Wall", new Vector3(9f, 2f, 0f), new Vector3(0.5f, 4f, 12f), new Color(0.38f, 0.18f, 0.12f));
        CreateStation("Fryer", StationType.Fryer, new Vector3(-4f, 0.8f, 2.5f), new Vector3(3f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f));
        CreateStation("Fridge", StationType.Fridge, new Vector3(5f, 1.5f, 3.5f), new Vector3(2f, 3f, 2f), new Color(0.35f, 0.7f, 0.8f));
        CreateStation("Packing Counter", StationType.Packing, new Vector3(0f, 0.8f, 3.5f), new Vector3(3f, 1.6f, 1.5f), new Color(0.95f, 0.75f, 0.25f));
        CreateStation("Checkout", StationType.Checkout, new Vector3(4f, 0.8f, -2.5f), new Vector3(2.5f, 1.6f, 1.5f), new Color(0.25f, 0.65f, 0.35f));
        CreateBlock("Customer Queue", new Vector3(0f, 0.2f, -4f), new Vector3(5f, 0.4f, 0.5f), new Color(0.9f, 0.25f, 0.25f));
    }

    private GameObject BuildPlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1.2f, -1f);
        player.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Destroy(player.GetComponent<Collider>());
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerInteraction>();
        player.GetComponent<Renderer>().material.color = new Color(0.95f, 0.8f, 0.2f);
        return player;
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 14f, -13f);
        camera.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
        camera.fieldOfView = 55f;
        cameraObject.AddComponent<FollowPlayerCamera>();
    }

    private void BuildHud(RestaurantGame game)
    {
        GameObject canvasObject = new GameObject("HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        Text revenue = CreateLabel(canvasObject.transform, "REVENUE  ₩0 / ₩10,000,000", new Vector2(24f, -24f), 26);
        Text day = CreateLabel(canvasObject.transform, "DAY 1   600s", new Vector2(-24f, -24f), 26);
        Text orders = CreateLabel(canvasObject.transform, "주문을 기다리는 중...", new Vector2(24f, -70f), 20);
        Text instructions = CreateLabel(canvasObject.transform, "WASD 이동   E 상호작용\n냉장고 → 튀김기 → 포장대 → 계산대", new Vector2(24f, 24f), 18);
        Text message = CreateLabel(canvasObject.transform, string.Empty, new Vector2(0f, 90f), 24);
        Text stats = CreateLabel(canvasObject.transform, "주문 0  성공 0  실패 0  탄 치킨 0", new Vector2(-24f, -100f), 16);
        revenue.rectTransform.anchorMin = new Vector2(0f, 1f);
        revenue.rectTransform.anchorMax = new Vector2(0f, 1f);
        day.rectTransform.anchorMin = new Vector2(1f, 1f);
        day.rectTransform.anchorMax = new Vector2(1f, 1f);
        orders.rectTransform.anchorMin = new Vector2(0f, 1f);
        orders.rectTransform.anchorMax = new Vector2(0f, 1f);
        instructions.rectTransform.anchorMin = new Vector2(0f, 0f);
        instructions.rectTransform.anchorMax = new Vector2(0f, 0f);
        message.alignment = TextAnchor.MiddleCenter;
        message.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        message.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        stats.rectTransform.anchorMin = new Vector2(1f, 1f);
        stats.rectTransform.anchorMax = new Vector2(1f, 1f);
        game.ConnectHud(revenue, day, orders, message, stats);
    }

    private static Text CreateLabel(Transform parent, string text, Vector2 position, int fontSize)
    {
        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(parent, false);
        Text label = labelObject.AddComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.rectTransform.sizeDelta = new Vector2(760f, 90f);
        label.rectTransform.anchoredPosition = position;
        return label;
    }

    private static Station CreateStation(string name, StationType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject stationObject = CreateBlock(name, position, scale, color);
        Station station = stationObject.AddComponent<Station>();
        station.stationType = type;
        return station;
    }

    private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.position = position;
        block.transform.localScale = scale;
        block.GetComponent<Renderer>().material.color = color;
        return block;
    }
}

public sealed class RestaurantGame : MonoBehaviour
{
    public static RestaurantGame Instance { get; private set; }

    private const int TargetRevenue = 10_000_000;
    private const float DayLength = 600f;
    private const float OrderInterval = 12f;
    private readonly List<RestaurantOrder> activeOrders = new List<RestaurantOrder>();
    private int revenue;
    private int day = 1;
    private int totalOrders;
    private int successfulOrders;
    private int failedOrders;
    private int burntChicken;
    private float dayTimer;
    private float orderTimer = 3f;
    private float messageTimer;
    private GameObject player;
    private Text revenueLabel;
    private Text dayLabel;
    private Text ordersLabel;
    private Text messageLabel;
    private Text statsLabel;

    private void Awake()
    {
        Instance = this;
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
            orderTimer = OrderInterval;
        }

        for (int index = activeOrders.Count - 1; index >= 0; index--)
        {
            RestaurantOrder order = activeOrders[index];
            order.remainingTime -= Time.deltaTime;
            if (order.remainingTime <= 0f)
            {
                failedOrders++;
                Destroy(order.customer);
                activeOrders.RemoveAt(index);
                ShowMessage("주문 취소! 손님이 떠났습니다");
            }
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
            case StationType.Packing:
                PackChicken(actor);
                break;
            case StationType.Checkout:
                CompleteOrder(actor);
                break;
        }
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
        if (food.cookProgress >= 8f && !food.burnCounted)
        {
            food.burnCounted = true;
            burntChicken++;
            food.SetState(FoodState.Burnt);
            ShowMessage("치킨이 탔습니다!");
        }
        else if (food.cookProgress >= 5f && food.state == FoodState.Frying)
        {
            food.SetState(FoodState.Cooked);
            ShowMessage("치킨 완성! 튀김기에서 꺼내세요");
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
            FoodItem chicken = actor.ReleaseHeldFood(station.transform);
            chicken.SetState(FoodState.Frying);
            chicken.cookProgress = 0f;
            ShowMessage("튀김 시작! 5초 뒤 꺼내세요");
            return;
        }

        FoodItem cookedChicken = station.StoredFood;
        if (actor.HeldFood == null && cookedChicken != null && cookedChicken.state != FoodState.Frying)
        {
            actor.SetHeldFood(cookedChicken);
            ShowMessage(cookedChicken.state == FoodState.Burnt ? "탄 치킨입니다" : "완성된 치킨을 들었습니다");
            return;
        }

        ShowMessage("생닭을 들고 튀김기에 넣으세요");
    }

    private void PackChicken(PlayerInteraction actor)
    {
        if (actor.HeldFood == null || actor.HeldFood.state != FoodState.Cooked)
        {
            ShowMessage("완성된 치킨을 들고 오세요");
            return;
        }

        actor.HeldFood.SetState(FoodState.Packaged);
        ShowMessage("포장 완료! 계산대로 가져가세요");
    }

    private void CompleteOrder(PlayerInteraction actor)
    {
        if (actor.HeldFood == null || actor.HeldFood.state != FoodState.Packaged)
        {
            ShowMessage("포장된 치킨을 들고 오세요");
            return;
        }

        if (activeOrders.Count == 0)
        {
            ShowMessage("현재 주문이 없습니다");
            return;
        }

        RestaurantOrder order = activeOrders[0];
        int bonus = Mathf.RoundToInt(Mathf.Clamp(order.remainingTime, 0f, 45f) * 100f);
        revenue += 18_000 + bonus;
        successfulOrders++;
        activeOrders.RemoveAt(0);
        Destroy(order.customer);
        Destroy(actor.HeldFood.gameObject);
        ShowMessage($"주문 완료! +₩{18_000 + bonus:N0}");
    }

    private void CreateOrder()
    {
        if (activeOrders.Count >= 5)
        {
            return;
        }

        totalOrders++;
        GameObject customer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        customer.name = $"Customer {totalOrders}";
        customer.transform.position = new Vector3(-4.5f + activeOrders.Count * 2.2f, 0.9f, -4f);
        customer.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        customer.GetComponent<Renderer>().material.color = new Color(0.7f, 0.35f, 0.25f);
        RestaurantOrder order = new RestaurantOrder(totalOrders, customer);
        activeOrders.Add(order);
        ShowMessage($"주문 #{order.number} 들어왔습니다!");
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
                System.Text.StringBuilder text = new System.Text.StringBuilder("ORDERS\n");
                foreach (RestaurantOrder order in activeOrders)
                {
                    text.AppendLine($"#{order.number}  후라이드 x1  {Mathf.CeilToInt(order.remainingTime)}s");
                }

                ordersLabel.text = text.ToString();
            }
        }

        if (statsLabel != null)
        {
            statsLabel.text = $"주문 {totalOrders}  성공 {successfulOrders}  실패 {failedOrders}  탄 치킨 {burntChicken}";
        }
    }

    private sealed class RestaurantOrder
    {
        public readonly int number;
        public readonly GameObject customer;
        public float remainingTime = 45f;

        public RestaurantOrder(int orderNumber, GameObject customerObject)
        {
            number = orderNumber;
            customer = customerObject;
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
