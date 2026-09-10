using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>접속한 플레이어 화면에서만 확인할 수 있는 것들을 검사한다.
///
/// 셀프테스트는 한 프로세스 안에서 도는데, 거기에는 남의 캐릭터가 존재하지 않고
/// 클라이언트 분기도 실행되지 않는다. 그래서 "호스트에서는 되는데 접속하면 안 되는"
/// 종류의 결함이 그동안 전부 UNKNOWN 으로 남아 있었다.
///
/// `-mode client -address ... -nettest` 로 쓴다.</summary>
public sealed class NetworkTest : MonoBehaviour
{
    private const float ConnectTimeout = 25f;

    private readonly List<string> failures = new List<string>();
    private int checks;

    public static bool Requested =>
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nettest") >= 0;

    private IEnumerator Start()
    {
        float waited = 0f;
        while (waited < ConnectTimeout && !Connected())
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Check(Connected(), $"호스트에 접속했다 ({waited:0.0}초)");
        if (!Connected())
        {
            Report();
            yield break;
        }

        // 스폰과 첫 동기화가 도착할 시간을 준다.
        yield return new WaitForSecondsRealtime(4f);

        TestOwnAvatar();
        TestRemoteBodies();
        TestServerAuthority();

        Report();
    }

    private static bool Connected()
    {
        return NetworkManager.Singleton != null
               && NetworkManager.Singleton.IsClient
               && NetworkManager.Singleton.IsConnectedClient;
    }

    private void TestOwnAvatar()
    {
        Check(FirstPersonView.Subject != null, "내 아바타가 스폰되고 시점이 잡혔다");
        // 정확히 둘이어야 한다. 온라인으로 넘어가며 꺼둔 로컬 캐릭터가 등록부에 남으면
        // 아무도 조종하지 않는 몸이 사고 판정 같은 전체 순회에 끼어든다.
        Check(WorldRegistry.Players.Count == 2,
            $"등록된 플레이어가 나와 호스트 둘뿐이다 ({WorldRegistry.Players.Count}명)");
    }

    /// <summary>남의 캐릭터는 CharacterController 가 꺼져 있어 콜라이더가 사라진다.
    /// 좁은 주방에서 서로 막는 것이 이 게임의 재미이므로 몸통이 있어야 한다.</summary>
    private void TestRemoteBodies()
    {
        int remotes = 0;
        int withBody = 0;

        foreach (NetworkPlayer player in Object.FindObjectsByType<NetworkPlayer>())
        {
            if (player == null || player.IsOwner)
            {
                continue;
            }

            remotes++;
            if (player.GetComponent<CapsuleCollider>() != null)
            {
                withBody++;
            }
        }

        Check(remotes > 0, $"남의 캐릭터가 보인다 ({remotes}명)");
        Check(remotes > 0 && withBody == remotes, $"남의 캐릭터에 막아서는 몸통이 있다 ({withBody}/{remotes})");
    }

    /// <summary>매출·DAY·평판은 호스트가 정하고 내려보내야 한다.
    /// 클라이언트가 스스로 굴리면 두 화면의 숫자가 갈라진다.</summary>
    private void TestServerAuthority()
    {
        Check(KitchenNetwork.Online, "주방 네트워크가 붙었다");
        Check(KitchenNetwork.Instance != null && !KitchenNetwork.Instance.IsServer,
            "이 프로세스는 클라이언트다");
        Check(!KitchenNetwork.IsHostSide, "클라이언트는 호스트 권한이 없다");

        if (KitchenNetwork.Instance == null)
        {
            return;
        }

        Check(KitchenNetwork.Instance.Day >= 1, $"호스트의 DAY 가 내려왔다 (DAY {KitchenNetwork.Instance.Day})");
        Check(KitchenNetwork.Instance.Reputation > 0,
            $"호스트의 평판이 내려왔다 ({KitchenNetwork.Instance.Reputation})");
    }

    private void Check(bool passed, string label)
    {
        checks++;
        if (passed)
        {
            Debug.Log($"[NetTest] PASS  {label}");
            return;
        }

        failures.Add(label);
        Debug.LogError($"[NetTest] FAIL  {label}");
    }

    private void Report()
    {
        if (failures.Count == 0)
        {
            Debug.Log($"[NetTest] RESULT OK  {checks}개 항목 통과");
            Application.Quit(0);
            return;
        }

        Debug.LogError($"[NetTest] RESULT FAILED  {failures.Count}/{checks} 실패: {string.Join(", ", failures)}");
        Application.Quit(1);
    }
}
