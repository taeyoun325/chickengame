using UnityEngine;
using UnityEngine.UI;

public sealed class ChickenGameBootstrap : MonoBehaviour
{
    private void Start()
    {
        // 씬을 다시 불러올 때 이전 등록이 남아 있으면 안 된다.
        WorldRegistry.Clear();

        // 창이 포커스를 잃어도 계속 돌아야 한다. 특히 호스트가 멈추면 모두가 멈춘다.
        Application.runInBackground = true;
        BuildLighting();
        BuildShop();
        GameObject localPlayers = BuildLocalPlayers();
        BuildCamera();
        GameObject gameObject = new GameObject("Restaurant Game");
        RestaurantGame game = gameObject.AddComponent<RestaurantGame>();
        DeliverySystem delivery = gameObject.AddComponent<DeliverySystem>();
        delivery.Initialise(game, BuildScooter().transform);
        UpgradeSystem upgradeSystem = gameObject.AddComponent<UpgradeSystem>();
        upgradeSystem.Initialise(game);
        RegisterReserveFryers(game);
        RandomEventSystem eventSystem = gameObject.AddComponent<RandomEventSystem>();
        eventSystem.Initialise(game);
        gameObject.AddComponent<GameAudio>();
        gameObject.AddComponent<GameMusic>();
        gameObject.AddComponent<NetworkSession>();
        GameFlow flow = gameObject.AddComponent<GameFlow>();
        flow.Initialise(game, localPlayers);
        HazardSystem hazardSystem = gameObject.AddComponent<HazardSystem>();
        hazardSystem.Initialise(game);
        game.ConnectHazards(hazardSystem);
        BuildHud(game, delivery, upgradeSystem, eventSystem, flow);
        Debug.Log($"Chicken Game ready - DAY {game.Day}, 매출 {game.Revenue}");

        if (SelfTest.Requested)
        {
            gameObject.AddComponent<SelfTest>();
        }

        if (BalanceTest.Requested)
        {
            gameObject.AddComponent<BalanceTest>();
        }
    }

    /// <summary>증설로 열리는 2, 3번 튀김기를 미리 만들어 두고 꺼둔다.</summary>
    private void RegisterReserveFryers(RestaurantGame game)
    {
        // 예비 튀김기도 켜지면 바로 김이 오르도록 파티클을 미리 붙여둔다.
        game.RegisterReserveFryer(AddSteam(CreateStation("Fryer 3", StationType.Fryer, new Vector3(5f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f))).gameObject);
        game.RegisterReserveFryer(AddSteam(CreateStation("Fryer 4", StationType.Fryer, new Vector3(-4f, 0.8f, 1f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f))).gameObject);
    }

    private static Station AddSteam(Station station)
    {
        ParticleSystem steam = GameEffects.CreateSteam(station.transform, new Vector3(0f, 1.2f, 0f));
        station.gameObject.AddComponent<FryerSteam>().Bind(station, steam);
        return station;
    }

    private GameObject BuildScooter()
    {
        GameObject scooter = CreateBlock("Delivery Scooter", new Vector3(3.5f, 0.4f, -7.5f), new Vector3(0.9f, 0.8f, 1.8f), new Color(0.15f, 0.45f, 0.85f));
        Destroy(scooter.GetComponent<Collider>());
        PropVisual.Attach(scooter, "Scooter");
        return scooter;
    }

    private void BuildLighting()
    {
        RenderSettings.ambientLight = new Color(0.55f, 0.48f, 0.38f);
        GameObject lightObject = new GameObject("Shop Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        lightObject.AddComponent<ShopLighting>();
    }

    private void BuildShop()
    {
        CreateBlock("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(18f, 0.5f, 12f), new Color(0.22f, 0.18f, 0.14f));
        CreateBlock("Back Wall", new Vector3(0f, 2f, 6f), new Vector3(18f, 4f, 0.5f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Left Wall", new Vector3(-9f, 2f, 0f), new Vector3(0.5f, 4f, 12f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Right Wall", new Vector3(9f, 2f, 0f), new Vector3(0.5f, 4f, 12f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Entrance Path", new Vector3(0f, -0.25f, -8.5f), new Vector3(6f, 0.5f, 5f), new Color(0.3f, 0.28f, 0.26f));
        CreateBlock("Front Wall Left", new Vector3(-5.75f, 2f, -6f), new Vector3(6.5f, 4f, 0.5f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Front Wall Right", new Vector3(5.75f, 2f, -6f), new Vector3(6.5f, 4f, 0.5f), new Color(0.38f, 0.18f, 0.12f));
        // 조리 라인은 뒷줄, 손님을 상대하는 카운터는 앞줄. 스테이션끼리는 최소 3m 떨어뜨려
        // 상호작용 반경(2.4m)이 겹치지 않게 한다.
        // 2~4인 협동이므로 튀김기는 처음부터 두 대. 한 대뿐이면 두 번째 플레이어가 할 일이 없다.
        AddSteam(CreateStation("Fryer", StationType.Fryer, new Vector3(-7f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f)));
        AddSteam(CreateStation("Fryer 2", StationType.Fryer, new Vector3(-4f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f)));
        CreateStation("Sauce Table", StationType.Sauce, new Vector3(2f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.75f, 0.2f, 0.3f));
        CreateStation("Fridge", StationType.Fridge, new Vector3(-1f, 1.5f, 4.2f), new Vector3(2f, 3f, 2f), new Color(0.35f, 0.7f, 0.8f));
        CreateStation("Upgrade Desk", StationType.Upgrade, new Vector3(8f, 0.8f, 4.2f), new Vector3(1.5f, 1.6f, 2.5f), new Color(0.55f, 0.35f, 0.8f));
        CreateStation("Packing Counter", StationType.Packing, new Vector3(0f, 0.8f, 0.8f), new Vector3(3f, 1.6f, 1.5f), new Color(0.95f, 0.75f, 0.25f));
        CreateStation("Trash Bin", StationType.Trash, new Vector3(-7.5f, 0.6f, 0.5f), new Vector3(1.4f, 1.2f, 1.4f), new Color(0.25f, 0.25f, 0.28f));
        CreateStation("Extinguisher Stand", StationType.Extinguisher, new Vector3(7.5f, 0.7f, 0.5f), new Vector3(1f, 1.4f, 1f), new Color(0.8f, 0.15f, 0.15f));
        CreateStation("Delivery Counter", StationType.Delivery, new Vector3(-4f, 0.8f, -2.8f), new Vector3(2.5f, 1.6f, 1.5f), new Color(0.15f, 0.45f, 0.85f));
        CreateStation("Checkout", StationType.Checkout, new Vector3(4f, 0.8f, -2.8f), new Vector3(2.5f, 1.6f, 1.5f), new Color(0.25f, 0.65f, 0.35f));
        CreateBlock("Customer Queue", new Vector3(0f, 0.2f, -4.5f), new Vector3(11f, 0.4f, 0.5f), new Color(0.9f, 0.25f, 0.25f));
    }

    /// <summary>화면이 하나이고 시점이 1인칭이므로 로컬 캐릭터는 나 하나다.
    /// 친구와 같이 하려면 타이틀에서 호스트로 열거나 접속한다.</summary>
    private GameObject BuildLocalPlayers()
    {
        GameObject root = new GameObject("Local Players");
        GameObject player = BuildPlayerBody(0, root.transform);
        player.AddComponent<LocalPlayerInput>();
        return root;
    }

    public static readonly Color[] PlayerColors =
    {
        new Color(0.95f, 0.8f, 0.2f),
        new Color(0.25f, 0.65f, 1f),
        new Color(0.4f, 0.85f, 0.4f),
        new Color(0.9f, 0.45f, 0.75f)
    };

    private GameObject BuildPlayerBody(int index, Transform parent)
    {
        Color color = PlayerColors[index % PlayerColors.Length];
        GameObject player = GameMaterials.CreatePrimitive(
            PrimitiveType.Capsule,
            index == 0 ? "Player" : $"Local Player {index + 1}",
            color);
        player.transform.SetParent(parent, true);
        player.transform.position = new Vector3(-2.4f + index * 1.6f, 1.2f, -1f);
        player.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Destroy(player.GetComponent<Collider>());
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        player.AddComponent<PlayerMotor>();
        player.AddComponent<PlayerInteraction>();
        CharacterVisual.Attach(player, color, "chef hat");
        return player;
    }

    private void BuildCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        // 좁은 주방을 1인칭으로 도는 게임이라 시야각이 넓어야 답답하지 않고,
        // 근거리 평면이 짧아야 카운터에 붙어도 뚫려 보이지 않는다.
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.05f;
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<FirstPersonView>();
    }

    private void BuildHud(RestaurantGame game, DeliverySystem delivery, UpgradeSystem upgradeSystem, RandomEventSystem eventSystem, GameFlow flow)
    {
        GameObject canvasObject = new GameObject("HUD");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        Text revenue = CreateLabel(canvasObject.transform, "REVENUE  ₩0 / ₩10,000,000", new Vector2(24f, -24f), 26);
        Text day = CreateLabel(canvasObject.transform, "DAY 1   600s", new Vector2(-24f, -24f), 26);
        Text orders = CreateLabel(canvasObject.transform, "주문을 기다리는 중...", new Vector2(24f, -70f), 20);
        Text instructions = CreateLabel(canvasObject.transform, "WASD 이동   마우스 시점   SPACE 점프   E 상호작용   F 내려놓기   Q 소스   1~5 업그레이드\n냉장고 → 튀김기 → (양념대) → 포장대 → 계산대 / 배달대", new Vector2(24f, 24f), 18);
        Text message = CreateLabel(canvasObject.transform, string.Empty, new Vector2(0f, 90f), 24);
        Text stats = CreateLabel(canvasObject.transform, "주문 0  성공 0  실패 0  탄 치킨 0", new Vector2(-24f, -100f), 16);
        revenue.rectTransform.anchorMin = new Vector2(0f, 1f);
        revenue.rectTransform.anchorMax = new Vector2(0f, 1f);
        revenue.rectTransform.pivot = new Vector2(0f, 1f);
        day.rectTransform.anchorMin = new Vector2(1f, 1f);
        day.rectTransform.anchorMax = new Vector2(1f, 1f);
        day.rectTransform.pivot = new Vector2(1f, 1f);
        day.alignment = TextAnchor.UpperRight;
        orders.rectTransform.anchorMin = new Vector2(0f, 1f);
        orders.rectTransform.anchorMax = new Vector2(0f, 1f);
        orders.rectTransform.pivot = new Vector2(0f, 1f);
        orders.rectTransform.sizeDelta = new Vector2(420f, 160f);
        instructions.rectTransform.anchorMin = new Vector2(0f, 0f);
        instructions.rectTransform.anchorMax = new Vector2(0f, 0f);
        instructions.rectTransform.pivot = new Vector2(0f, 0f);
        instructions.alignment = TextAnchor.LowerLeft;
        message.alignment = TextAnchor.MiddleCenter;
        message.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        message.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        message.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        stats.rectTransform.anchorMin = new Vector2(1f, 1f);
        stats.rectTransform.anchorMax = new Vector2(1f, 1f);
        stats.rectTransform.pivot = new Vector2(1f, 1f);
        stats.alignment = TextAnchor.UpperRight;
        Text deliveryStatus = CreateLabel(canvasObject.transform, "배달 대기 없음", new Vector2(-24f, -140f), 16);
        deliveryStatus.rectTransform.anchorMin = new Vector2(1f, 1f);
        deliveryStatus.rectTransform.anchorMax = new Vector2(1f, 1f);
        deliveryStatus.rectTransform.pivot = new Vector2(1f, 1f);
        deliveryStatus.alignment = TextAnchor.UpperRight;

        Text upgradeShop = CreateLabel(canvasObject.transform, string.Empty, new Vector2(-24f, 24f), 15);
        upgradeShop.rectTransform.anchorMin = new Vector2(1f, 0f);
        upgradeShop.rectTransform.anchorMax = new Vector2(1f, 0f);
        upgradeShop.rectTransform.pivot = new Vector2(1f, 0f);
        upgradeShop.rectTransform.sizeDelta = new Vector2(520f, 130f);
        upgradeShop.alignment = TextAnchor.LowerRight;

        game.ConnectHud(revenue, day, orders, message, stats);
        game.ConnectDelivery(delivery, deliveryStatus);
        Text eventBanner = CreateLabel(canvasObject.transform, string.Empty, new Vector2(0f, -24f), 20);
        eventBanner.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        eventBanner.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        eventBanner.rectTransform.pivot = new Vector2(0.5f, 1f);
        eventBanner.rectTransform.sizeDelta = new Vector2(600f, 40f);
        eventBanner.alignment = TextAnchor.UpperCenter;
        eventBanner.color = new Color(1f, 0.85f, 0.3f);

        game.ConnectUpgrades(upgradeSystem, upgradeShop);
        Text networkStatus = CreateLabel(canvasObject.transform, string.Empty, new Vector2(0f, 24f), 16);
        networkStatus.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        networkStatus.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        networkStatus.rectTransform.pivot = new Vector2(0.5f, 0f);
        networkStatus.rectTransform.sizeDelta = new Vector2(520f, 30f);
        networkStatus.alignment = TextAnchor.LowerCenter;
        networkStatus.color = new Color(0.6f, 0.85f, 1f);

        game.ConnectEvents(eventSystem, eventBanner);
        game.ConnectNetworkLabel(networkStatus);
        BuildAimHud(canvasObject.transform);
        BuildPanels(canvasObject.transform, game, flow);
    }

    /// <summary>조준점과 그 아래 안내. 1인칭에서 다음 동작을 읽는 자리다.</summary>
    private static void BuildAimHud(Transform parent)
    {
        GameObject crosshairObject = new GameObject("Crosshair");
        crosshairObject.transform.SetParent(parent, false);
        Image crosshair = crosshairObject.AddComponent<Image>();
        crosshair.color = new Color(1f, 1f, 1f, 0.35f);
        crosshair.raycastTarget = false;
        RectTransform crosshairRect = crosshair.rectTransform;
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.sizeDelta = new Vector2(7f, 7f);
        crosshairRect.anchoredPosition = Vector2.zero;

        Text prompt = CreateLabel(parent, string.Empty, new Vector2(0f, -70f), 22);
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = new Color(1f, 0.9f, 0.5f);
        prompt.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        prompt.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        prompt.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        Text hands = CreateLabel(parent, string.Empty, new Vector2(0f, 130f), 18);
        hands.alignment = TextAnchor.LowerCenter;
        hands.color = new Color(0.85f, 0.92f, 1f);
        hands.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        hands.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hands.rectTransform.pivot = new Vector2(0.5f, 0f);

        parent.gameObject.AddComponent<AimHud>().Connect(crosshair, prompt, hands);
    }

    /// <summary>타이틀, 일시정지, 결산, 결과 화면을 만들고 GameFlow 에 넘긴다.</summary>
    private static void BuildPanels(Transform parent, RestaurantGame game, GameFlow flow)
    {
        Text titleLabel;
        GameObject title = CreatePanel(parent, "Title Panel", 760f, 520f, 26, out titleLabel);
        Text pauseLabel;
        GameObject pause = CreatePanel(parent, "Pause Panel", 620f, 300f, 26, out pauseLabel);
        pauseLabel.text = "일시정지\n\nESC  계속하기\nR  처음부터";
        Text settlementLabel;
        GameObject settlement = CreatePanel(parent, "Settlement Panel", 760f, 500f, 22, out settlementLabel);
        Text resultLabel;
        GameObject result = CreatePanel(parent, "Result Panel", 760f, 500f, 24, out resultLabel);

        flow.ConnectPanels(title, titleLabel, pause, settlement, settlementLabel, result, resultLabel);
    }

    private static GameObject CreatePanel(Transform parent, string name, float width, float height, int fontSize, out Text label)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.05f, 0.04f, 0.03f, 0.93f);
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;

        label = CreateLabel(panel.transform, string.Empty, Vector2.zero, fontSize);
        label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.offsetMin = new Vector2(32f, 32f);
        label.rectTransform.offsetMax = new Vector2(-32f, -32f);

        panel.SetActive(false);
        return panel;
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

        // 소품은 겉모습만 바꾼다. 충돌과 조준 판정은 블록이 그대로 맡는다.
        AttachProp(stationObject, type);
        StationLabel.Attach(station, KoreanName(type), scale.y * 0.5f + 0.35f);
        return station;
    }

    /// <summary>스테이션마다 어울리는 가구를 얹는다. 덩치가 맞는 가구는 블록을 대신하고,
    /// 금전등록기처럼 작은 것은 블록을 카운터로 남긴 채 그 위에 올린다.
    ///
    /// 튀김기와 쓰레기통, 소화기는 에셋에 맞는 모델이 없어 색이 분명한 블록으로 남긴다.
    /// 주방에서 제일 급하게 찾는 것들이라 오히려 그 편이 눈에 잘 띈다.</summary>
    private static void AttachProp(GameObject stationObject, StationType type)
    {
        switch (type)
        {
            case StationType.Fridge:
                PropVisual.Attach(stationObject, "Freezer");
                break;
            case StationType.Sauce:
                PropVisual.Attach(stationObject, "Cafe_Cabinet_1");
                break;
            case StationType.Packing:
                PropVisual.Attach(stationObject, "Dinner_Table");
                break;
            case StationType.Checkout:
                PropVisual.Attach(stationObject, "Cafe_Cafe_Cash_Register_1", PropVisual.Placement.OnTop);
                break;
            case StationType.Delivery:
                PropVisual.Attach(stationObject, "Dinner_Stand", PropVisual.Placement.OnTop);
                break;
            case StationType.Upgrade:
                PropVisual.Attach(stationObject, "Cafe_Shelf_1", PropVisual.Placement.OnTop);
                break;
        }
    }

    /// <summary>어느 블록이 무엇인지 한눈에 보이도록 붙이는 이름.</summary>
    private static string KoreanName(StationType type)
    {
        return type switch
        {
            StationType.Fridge => "냉장고",
            StationType.Fryer => "튀김기",
            StationType.Sauce => "양념대",
            StationType.Packing => "포장대",
            StationType.Checkout => "계산대",
            StationType.Delivery => "배달대",
            StationType.Upgrade => "업그레이드",
            StationType.Trash => "쓰레기통",
            StationType.Extinguisher => "소화기",
            _ => type.ToString()
        };
    }

    private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject block = GameMaterials.CreatePrimitive(PrimitiveType.Cube, name, color);
        block.transform.position = position;
        block.transform.localScale = scale;
        return block;
    }
}
