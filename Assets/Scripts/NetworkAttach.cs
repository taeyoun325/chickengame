using Unity.Netcode;
using UnityEngine;

/// <summary>네트워크 오브젝트는 일반 SetParent 대신 NGO 의 부모 변경을 써야 클라이언트에도 반영된다.</summary>
public static class NetworkAttach
{
    public static void Parent(FoodItem food, Transform holdPoint)
    {
        NetworkObject networkObject = food.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            // 부모 변경은 서버만 할 수 있다.
            if (networkObject.NetworkManager.IsServer)
            {
                networkObject.TrySetParent(holdPoint, false);
                food.transform.localPosition = Vector3.zero;
            }

            return;
        }

        food.transform.SetParent(holdPoint, false);
    }

    public static void Unparent(FoodItem food)
    {
        NetworkObject networkObject = food.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            if (networkObject.NetworkManager.IsServer)
            {
                networkObject.TryRemoveParent(false);
            }

            return;
        }

        food.transform.SetParent(null);
    }
}
