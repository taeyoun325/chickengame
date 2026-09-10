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
    private readonly NetworkVariable<FixedString4096Bytes> netOrders = new NetworkVariable<FixedString4096Bytes>();

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
        GameLog.Verbose($"[Net] 주방 동기화 시작 ({(IsServer ? "host" : "client")})");
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>호스트가 매 프레임 최신 상태를 밀어 넣는다.</summary>
    private float publishTimer;

    public void PublishState(int revenue, int day, int reputation, string orders)
    {
        if (!IsServer)
        {
            return;
        }

        publishTimer -= Time.deltaTime;
        if (publishTimer > 0f)
        {
            return;
        }

        publishTimer = 0.2f;

        netRevenue.Value = revenue;
        netDay.Value = day;
        netReputation.Value = reputation;
        if (orders.Length > 900)
        {
            orders = orders.Substring(0, 900);
        }

        netOrders.Value = new FixedString4096Bytes(orders);
    }

    public void RequestInteract(int stationIndex)
    {
        InteractRpc(stationIndex, NetworkManager.Singleton.LocalClientId);
    }

    public void RequestPickup()
    {
        PickupRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestDrop()
    {
        DropRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestSauceCycle()
    {
        SauceRpc(NetworkManager.Singleton.LocalClientId);
    }

    /// <summary>불을 끄고 기름을 닦는 일도 호스트가 처리해야 한다. 접속한 플레이어가
    /// 자기 화면에서 지워봐야 호스트의 사고는 그대로 남는다.</summary>
    public void RequestExtinguish()
    {
        ExtinguishRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestClean()
    {
        CleanRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void RequestUpgrade(int slot)
    {
        UpgradeRpc(slot);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void InteractRpc(int stationIndex, ulong clientId)
    {
        Station station = WorldRegistry.StationAt(stationIndex);
        PlayerInteraction actor = FindActor(clientId);
        if (station != null && actor != null && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.InteractWithStation(actor, station);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PickupRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null)
        {
            actor.TryPickUpNearbyFood();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void DropRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null)
        {
            actor.DropEverything();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ExtinguishRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null && actor.CarryingExtinguisher && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.TryExtinguishNearby(actor.transform.position);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CleanRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null && actor.HandsFree && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.TryCleanNearby(actor.transform.position);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SauceRpc(ulong clientId)
    {
        PlayerInteraction actor = FindActor(clientId);
        if (actor != null && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.CycleSauce(actor);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void UpgradeRpc(int slot)
    {
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.BuyUpgrade(slot);
        }
    }

    /// <summary>하루 결산을 접속한 플레이어들에게도 띄운다.</summary>
    public void BroadcastSettlement(string report)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (report.Length > 900)
        {
            report = report.Substring(0, 900);
        }

        SettlementRpc(new FixedString4096Bytes(report));
    }

    [Rpc(SendTo.NotServer)]
    private void SettlementRpc(FixedString4096Bytes report)
    {
        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.EnterSettlement(report.ToString());
        }
    }

    /// <summary>승패도 결산처럼 모두에게 보여야 한다. 호스트만 결과 화면을 보면
    /// 접속한 사람은 아무 설명 없이 멈춘 가게를 보게 된다.</summary>
    public void BroadcastFinish(string report, bool won)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (report.Length > 900)
        {
            report = report.Substring(0, 900);
        }

        FinishRpc(new FixedString4096Bytes(report), won);
    }

    [Rpc(SendTo.NotServer)]
    private void FinishRpc(FixedString4096Bytes report, bool won)
    {
        if (GameFlow.Instance == null)
        {
            return;
        }

        if (won)
        {
            GameFlow.Instance.EnterVictory(report.ToString());
        }
        else
        {
            GameFlow.Instance.EnterDefeat(report.ToString());
        }
    }

    public void BroadcastResume()
    {
        if (IsServer && IsSpawned)
        {
            ResumeRpc();
        }
    }

    [Rpc(SendTo.NotServer)]
    private void ResumeRpc()
    {
        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.ResumeFromSettlement();
        }
    }

    /// <summary>중요한 알림은 모두에게 보여준다.</summary>
    public void BroadcastNotice(string message)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (message.Length > 120)
        {
            message = message.Substring(0, 120);
        }

        NoticeRpc(new FixedString128Bytes(message));
    }

    [Rpc(SendTo.NotServer)]
    private void NoticeRpc(FixedString128Bytes message)
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
