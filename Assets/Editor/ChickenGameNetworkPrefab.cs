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
        player.AddComponent<PlayerController>();
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
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Network player prefab written to {PrefabPath}");
    }
}
