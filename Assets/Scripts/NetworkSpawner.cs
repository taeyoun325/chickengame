using Unity.Netcode;
using UnityEngine;

/// <summary>치킨과 손님을 만든다. 네트워크 세션이 열려 있으면 NetworkObject 로 스폰해
/// 모두가 같은 것을 보고, 싱글/로컬 협동에서는 예전처럼 기본 도형을 만든다.</summary>
public static class NetworkSpawner
{
    private const string FoodResource = "NetworkFood";
    private const string CustomerResource = "NetworkCustomer";

    public static FoodItem SpawnChicken()
    {
        GameObject prefab = Online ? Resources.Load<GameObject>(FoodResource) : null;
        if (prefab != null)
        {
            GameObject spawned = Object.Instantiate(prefab);
            spawned.GetComponent<NetworkObject>().Spawn();
            return spawned.GetComponent<FoodItem>();
        }

        GameObject chickenObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chickenObject.name = "Raw Chicken";
        chickenObject.transform.localScale = Vector3.one * 0.65f;
        FoodItem chicken = chickenObject.AddComponent<FoodItem>();
        chickenObject.AddComponent<FoodTickProxy>();
        return chicken;
    }

    public static Customer SpawnCustomer(string customerName)
    {
        GameObject prefab = Online ? Resources.Load<GameObject>(CustomerResource) : null;
        if (prefab != null)
        {
            GameObject spawned = Object.Instantiate(prefab);
            spawned.name = customerName;
            spawned.GetComponent<NetworkObject>().Spawn();
            return spawned.GetComponent<Customer>();
        }

        GameObject customerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        customerObject.name = customerName;
        customerObject.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.Destroy(customerObject.GetComponent<Collider>());
        return customerObject.AddComponent<Customer>();
    }

    /// <summary>스폰된 오브젝트는 서버에서 Despawn 으로 지워야 한다.</summary>
    public static void Remove(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        NetworkObject networkObject = target.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            if (networkObject.NetworkManager.IsServer)
            {
                networkObject.Despawn();
            }

            return;
        }

        Object.Destroy(target);
    }

    private static bool Online =>
        NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && KitchenNetwork.Online;
}
