using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>주방 상호작용을 호스트에서만 처리하고, 결과 상태를 손님들에게 뿌린다.
/// 싱글 플레이나 로컬 협동에서는 이 컴포넌트가 스폰되지 않으므로 기존 흐름 그대로 돌아간다.</summary>
public sealed class KitchenNetwork : NetworkBehaviour
{
    public static KitchenNetwork Instance { get; private set; }

    private readonly NetworkVariable<int> netRevenue = new NetworkVariable<int>();
    private readonly NetworkVariable<int> netDay = new NetworkVariable<int>(1);
    private readonly NetworkVariable<int> netReputation = new NetworkVariable<int>(100);
    private readonly NetworkVariable<FixedString512Bytes> netOrders = new NetworkVariable<FixedString512Bytes>();

    /// <summary>네트워크 세션이 살아 있고 이 오브젝트가 스폰됐을 때만 참이다.</summary>
    public static bool Online => Instance != null && Instance.IsSpawned;

    public static bool IsHostSide => !Online || Instance.IsServer;

    public int Revenue => netRevenue.Value;
    public int Day => netDay.Value;
    public int Reputation => netReputation.Value;
    public string Orders => netOrders.Value.ToString();

    public override void OnNetworkSpawn()
    {
        Instance = this;
        Debug.Log($"[Net] 주방 동기화 시작 ({(IsServer ? "host" : "client")})");
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>호스트가 매 프레임 최신 상태를 밀어 넣는다.</summary>
    public void PublishState(int revenue, int day, int reputation, string orders)
    {
        if (!IsServer)
        {
            return;
        }

        netRevenue.Value = revenue;
        netDay.Value = day;
        netReputation.Value = reputation;
        if (orders.Length > 480)
        {
            orders = orders.Substring(0, 480);
        }

        netOrders.Value = new FixedString512Bytes(orders);
    }

    public void RequestInteract(int stationIndex)
    {
        InteractServerRpc(stationIndex, NetworkManager.Singleton.LocalClientId);
    }

    public void RequestPickup()
    {
        PickupServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestDrop()
    {
        DropServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestSauceCycle(int stationIndex)
    {
        SauceServerRpc(stationIndex);
    }

    public void RequestUpgrade(int slot)
    {
        UpgradeServerRpc(slot);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InteractServerRpc(int stationIndex, ulong clientId)
    {
        Station station = WorldRegistry.StationAt(stationIndex);
        PlayerInteraction actor = FindActor(clientId);
        if (station != null && actor != null && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.InteractWithStation(actor, station);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PickupServerRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null)
        {
            actor.TryPickUpNearbyFood();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DropServerRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null)
        {
            actor.DropEverything();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SauceServerRpc(int stationIndex)
    {
        Station station = WorldRegistry.StationAt(stationIndex);
        if (station != null && station.stationType == StationType.Sauce && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.CycleSauce(station);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void UpgradeServerRpc(int slot)
    {
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.BuyUpgrade(slot);
        }
    }

    /// <summary>중요한 알림은 모두에게 보여준다.</summary>
    public void BroadcastMessage(string message)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (message.Length > 120)
        {
            message = message.Substring(0, 120);
        }

        MessageClientRpc(new FixedString128Bytes(message));
    }

    [ClientRpc]
    private void MessageClientRpc(FixedString128Bytes message)
    {
        if (!IsServer && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowLocalMessage(message.ToString());
        }
    }

    /// <summary>호스트 쪽에 있는 그 클라이언트의 아바타를 찾는다.</summary>
    private static PlayerInteraction FindActor(ulong clientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            return null;
        }

        NetworkObject playerObject = client.PlayerObject;
        return playerObject != null ? playerObject.GetComponent<PlayerInteraction>() : null;
    }
}
