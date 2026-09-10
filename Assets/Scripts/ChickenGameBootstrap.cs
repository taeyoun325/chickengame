using UnityEngine;
using UnityEngine.UI;

public sealed class ChickenGameBootstrap : MonoBehaviour
{
    private void Start()
    {
        BuildLighting();
        BuildShop();
        GameObject localPlayers = new GameObject("Local Players");
        BuildPlayer().transform.SetParent(localPlayers.transform, true);
        BuildLocalCoopPlayer().transform.SetParent(localPlayers.transform, true);
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
        gameObject.AddComponent<NetworkSession>();
        GameFlow flow = gameObject.AddComponent<GameFlow>();
        flow.Initialise(game, localPlayers);
        HazardSystem hazardSystem = gameObject.AddComponent<HazardSystem>();
        hazardSystem.Initialise(game);
        game.ConnectHazards(hazardSystem);
        BuildHud(game, delivery, upgradeSystem, eventSystem, flow);
        Debug.Log($"Chicken Game ready - DAY {game.Day}, 매출 {game.Revenue}");
    }

    /// <summary>증설로 열리는 2, 3번 튀김기를 미리 만들어 두고 꺼둔다.</summary>
    private void RegisterReserveFryers(RestaurantGame game)
    {
        // 예비 튀김기도 켜지면 바로 김이 오르도록 파티클을 미리 붙여둔다.
        game.RegisterReserveFryer(AddSteam(CreateStation("Fryer 2", StationType.Fryer, new Vector3(-4f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f))).gameObject);
        game.RegisterReserveFryer(AddSteam(CreateStation("Fryer 3", StationType.Fryer, new Vector3(-1f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f))).gameObject);
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
        AddSteam(CreateStation("Fryer", StationType.Fryer, new Vector3(-7f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f)));
        CreateStation("Sauce Table", StationType.Sauce, new Vector3(2f, 0.8f, 4.2f), new Vector3(2.4f, 1.6f, 1.6f), new Color(0.75f, 0.2f, 0.3f));
        CreateStation("Fridge", StationType.Fridge, new Vector3(5f, 1.5f, 4.2f), new Vector3(2f, 3f, 2f), new Color(0.35f, 0.7f, 0.8f));
        CreateStation("Upgrade Desk", StationType.Upgrade, new Vector3(8f, 0.8f, 4.2f), new Vector3(1.5f, 1.6f, 2.5f), new Color(0.55f, 0.35f, 0.8f));
        CreateStation("Packing Counter", StationType.Packing, new Vector3(0f, 0.8f, 0.8f), new Vector3(3f, 1.6f, 1.5f), new Color(0.95f, 0.75f, 0.25f));
        CreateStation("Trash Bin", StationType.Trash, new Vector3(-7.5f, 0.6f, 0.5f), new Vector3(1.4f, 1.2f, 1.4f), new Color(0.25f, 0.25f, 0.28f));
        CreateStation("Extinguisher Stand", StationType.Extinguisher, new Vector3(7.5f, 0.7f, 0.5f), new Vector3(1f, 1.4f, 1f), new Color(0.8f, 0.15f, 0.15f));
        CreateStation("Delivery Counter", StationType.Delivery, new Vector3(-4f, 0.8f, -2.8f), new Vector3(2.5f, 1.6f, 1.5f), new Color(0.15f, 0.45f, 0.85f));
        CreateStation("Checkout", StationType.Checkout, new Vector3(4f, 0.8f, -2.8f), new Vector3(2.5f, 1.6f, 1.5f), new Color(0.25f, 0.65f, 0.35f));
        CreateBlock("Customer Queue", new Vector3(0f, 0.2f, -4.5f), new Vector3(11f, 0.4f, 0.5f), new Color(0.9f, 0.25f, 0.25f));
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
        player.AddComponent<PlayerMotor>();
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
        cameraObject.AddComponent<AudioListener>();
        cameraObject.AddComponent<FollowPlayerCamera>();
    }

    private GameObject BuildLocalCoopPlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Local Player 2";
        player.transform.position = new Vector3(1.8f, 1.2f, -1f);
        player.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Destroy(player.GetComponent<Collider>());
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        player.AddComponent<PlayerMotor>();
        player.AddComponent<LocalCoopPlayerController>();
        player.AddComponent<PlayerInteraction>();
        player.GetComponent<Renderer>().material.color = new Color(0.25f, 0.65f, 1f);
        return player;
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
        Text instructions = CreateLabel(canvasObject.transform, "P1 WASD + E   P2 IJKL + E   Q 소스   F 내려놓기   1~5 업그레이드\n냉장고 → 튀김기 → (양념대) → 포장대 → 계산대 / 배달대", new Vector2(24f, 24f), 18);
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
        BuildPanels(canvasObject.transform, game, flow);
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
