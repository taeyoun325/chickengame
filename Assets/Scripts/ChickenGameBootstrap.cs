using UnityEngine;
using UnityEngine.UI;

public sealed class ChickenGameBootstrap : MonoBehaviour
{
    private void Start()
    {
        BuildLighting();
        BuildShop();
        GameObject player = BuildPlayer();
        BuildLocalCoopPlayer();
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
        CreateBlock("Entrance Path", new Vector3(0f, -0.25f, -8.5f), new Vector3(6f, 0.5f, 5f), new Color(0.3f, 0.28f, 0.26f));
        CreateBlock("Front Wall Left", new Vector3(-5.75f, 2f, -6f), new Vector3(6.5f, 4f, 0.5f), new Color(0.38f, 0.18f, 0.12f));
        CreateBlock("Front Wall Right", new Vector3(5.75f, 2f, -6f), new Vector3(6.5f, 4f, 0.5f), new Color(0.38f, 0.18f, 0.12f));
        CreateStation("Fryer", StationType.Fryer, new Vector3(-4f, 0.8f, 2.5f), new Vector3(3f, 1.6f, 1.6f), new Color(0.9f, 0.38f, 0.08f));
        CreateStation("Fridge", StationType.Fridge, new Vector3(5f, 1.5f, 3.5f), new Vector3(2f, 3f, 2f), new Color(0.35f, 0.7f, 0.8f));
        CreateStation("Sauce Table", StationType.Sauce, new Vector3(-7f, 0.8f, 0f), new Vector3(1.6f, 1.6f, 3f), new Color(0.75f, 0.2f, 0.3f));
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

    private void BuildLocalCoopPlayer()
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Local Player 2";
        player.transform.position = new Vector3(1.8f, 1.2f, -1f);
        player.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Destroy(player.GetComponent<Collider>());
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        player.AddComponent<LocalCoopPlayerController>();
        player.AddComponent<PlayerInteraction>();
        player.GetComponent<Renderer>().material.color = new Color(0.25f, 0.65f, 1f);
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
        Text instructions = CreateLabel(canvasObject.transform, "P1 WASD + E   P2 IJKL + E   Q 소스 변경\n냉장고 → 튀김기 → (양념대) → 포장대 → 계산대", new Vector2(24f, 24f), 18);
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
