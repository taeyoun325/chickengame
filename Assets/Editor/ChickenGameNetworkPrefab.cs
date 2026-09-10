using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

/// <summary>NGO 는 스폰할 프리팹 에셋을 요구하므로, 런타임 생성 대신 프리팹을 한 번 구워둔다.</summary>
public static class ChickenGameNetworkPrefab
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string PrefabPath = ResourcesFolder + "/NetworkPlayer.prefab";

    [MenuItem("Chicken Game/Build Network Prefab")]
    public static void BuildPrefab()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "NetworkPlayer";
        player.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
        Object.DestroyImmediate(player.GetComponent<Collider>());

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;

        player.AddComponent<PlayerMotor>();
        player.AddComponent<LocalPlayerInput>();
        player.AddComponent<PlayerInteraction>();
        player.AddComponent<NetworkObject>();

        NetworkTransform networkTransform = player.AddComponent<NetworkTransform>();
        networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        networkTransform.SyncScaleX = false;
        networkTransform.SyncScaleY = false;
        networkTransform.SyncScaleZ = false;

        player.AddComponent<NetworkPlayer>();

        PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
        Object.DestroyImmediate(player);

        BuildFoodPrefab();
        BuildCustomerPrefab();
        BuildKitchenPrefab();
        BuildHazardPrefab("NetworkOil", PrimitiveType.Cylinder, addPuddle: true);
        BuildHazardPrefab("NetworkFire", PrimitiveType.Capsule, addPuddle: false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Network prefabs written to {ResourcesFolder}");
    }

    /// <summary>네트워크에서 공유되는 치킨.</summary>
    private static void BuildFoodPrefab()
    {
        GameObject food = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        food.name = "NetworkFood";
        food.transform.localScale = Vector3.one * 0.65f;
        food.AddComponent<FoodItem>();
        food.AddComponent<NetworkObject>();
        NetworkTransform transform = food.AddComponent<NetworkTransform>();
        transform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
        food.AddComponent<FoodSync>();
        PrefabUtility.SaveAsPrefabAsset(food, ResourcesFolder + "/NetworkFood.prefab");
        Object.DestroyImmediate(food);
    }

    /// <summary>네트워크에서 공유되는 손님.</summary>
    private static void BuildCustomerPrefab()
    {
        GameObject customer = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        customer.name = "NetworkCustomer";
        customer.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.DestroyImmediate(customer.GetComponent<Collider>());
        customer.AddComponent<Customer>();
        customer.AddComponent<NetworkObject>();
        customer.AddComponent<CustomerSync>();
        NetworkTransform transform = customer.AddComponent<NetworkTransform>();
        transform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
        PrefabUtility.SaveAsPrefabAsset(customer, ResourcesFolder + "/NetworkCustomer.prefab");
        Object.DestroyImmediate(customer);
    }

    /// <summary>기름 웅덩이와 불. 위치만 공유하면 되므로 NetworkTransform 은 붙이지 않는다.</summary>
    private static void BuildHazardPrefab(string prefabName, PrimitiveType shape, bool addPuddle)
    {
        GameObject hazard = GameObject.CreatePrimitive(shape);
        hazard.name = prefabName;
        Object.DestroyImmediate(hazard.GetComponent<Collider>());
        hazard.AddComponent<NetworkObject>();
        if (addPuddle)
        {
            hazard.AddComponent<OilPuddle>();
        }
        else
        {
            hazard.AddComponent<FryerFire>();
        }

        PrefabUtility.SaveAsPrefabAsset(hazard, ResourcesFolder + "/" + prefabName + ".prefab");
        Object.DestroyImmediate(hazard);
    }

    /// <summary>가게 상태를 뿌리고 상호작용 RPC 를 받는 오브젝트.</summary>
    private static void BuildKitchenPrefab()
    {
        GameObject kitchen = new GameObject("KitchenNetwork");
        kitchen.AddComponent<NetworkObject>();
        kitchen.AddComponent<KitchenNetwork>();
        PrefabUtility.SaveAsPrefabAsset(kitchen, ResourcesFolder + "/KitchenNetwork.prefab");
        Object.DestroyImmediate(kitchen);
    }
}
