using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>호스트/참가 연결을 관리한다. 싱글 플레이일 때는 아무것도 켜지 않는다.</summary>
public sealed class NetworkSession : MonoBehaviour
{
    public const ushort DefaultPort = 7777;
    private const string PlayerPrefabResource = "NetworkPlayer";

    public static NetworkSession Instance { get; private set; }

    private NetworkManager manager;
    private UnityTransport transport;
    private string joinAddress = "127.0.0.1";

    public bool Active => manager != null && (manager.IsServer || manager.IsClient);
    public bool IsHost => manager != null && manager.IsHost;
    public string JoinAddress => joinAddress;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>NetworkManager 는 필요할 때 한 번만 만든다.</summary>
    private bool EnsureManager()
    {
        if (manager != null)
        {
            return true;
        }

        GameObject playerPrefab = Resources.Load<GameObject>(PlayerPrefabResource);
        if (playerPrefab == null)
        {
            Debug.LogError($"Resources/{PlayerPrefabResource}.prefab 이 없습니다. " +
                           "Chicken Game > Build Network Prefab 메뉴를 먼저 실행하세요.");
            return false;
        }

        GameObject managerObject = new GameObject("Network Manager");
        manager = managerObject.AddComponent<NetworkManager>();
        transport = managerObject.AddComponent<UnityTransport>();

        // 인스펙터가 아니라 코드로 붙였기 때문에 NetworkConfig 는 비어 있다.
        manager.NetworkConfig ??= new NetworkConfig();
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.PlayerPrefab = playerPrefab;
        manager.NetworkConfig.ConnectionApproval = false;
        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;
        return true;
    }

    public bool StartHost()
    {
        if (!EnsureManager())
        {
            return false;
        }

        transport.SetConnectionData("0.0.0.0", DefaultPort, "0.0.0.0");
        bool started = manager.StartHost();
        Report(started ? $"호스트 시작 (포트 {DefaultPort})" : "호스트 시작 실패");
        return started;
    }

    public bool StartClient(string address)
    {
        if (!EnsureManager())
        {
            return false;
        }

        joinAddress = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
        transport.SetConnectionData(joinAddress, DefaultPort);
        bool started = manager.StartClient();
        Report(started ? $"{joinAddress} 에 접속 중..." : "접속 실패");
        return started;
    }

    public void Shutdown()
    {
        if (manager != null && (manager.IsServer || manager.IsClient))
        {
            manager.Shutdown();
            Report("연결을 종료했습니다");
        }
    }

    public string BuildStatusText()
    {
        if (manager == null || !Active)
        {
            return string.Empty;
        }

        string role = manager.IsHost ? "HOST" : "CLIENT";
        int players = manager.IsServer ? manager.ConnectedClientsIds.Count : 1;
        return manager.IsServer ? $"NET {role}  접속 {players}명  포트 {DefaultPort}" : $"NET {role}  {joinAddress}";
    }

    private void OnClientConnected(ulong clientId)
    {
        Report($"플레이어 {clientId} 입장");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Report($"플레이어 {clientId} 퇴장");
    }

    private static void Report(string message)
    {
        Debug.Log($"[Net] {message}");
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage(message);
        }
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }
}
