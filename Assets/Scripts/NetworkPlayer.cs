using Unity.Netcode;
using UnityEngine;

/// <summary>네트워크로 접속한 플레이어 한 명. 소유자만 입력을 받고, 나머지는 위치만 따라온다.</summary>
[RequireComponent(typeof(PlayerMotor))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        Color[] colors = ChickenGameBootstrap.PlayerColors;
        Color color = colors[(int)(OwnerClientId % (ulong)colors.Length)];
        Renderer bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }

        CharacterVisual.Attach(gameObject, color, "chef hat");

        name = IsOwner ? "Network Player (me)" : $"Network Player {OwnerClientId}";
        GameLog.Verbose($"[Net] 플레이어 아바타 스폰 id={OwnerClientId} owner={IsOwner}");

        // 내 캐릭터만 입력을 읽는다. 키보드와 게임패드 중 손에 잡히는 쪽을 쓴다.
        LocalPlayerInput input = GetComponent<LocalPlayerInput>();
        if (input != null)
        {
            input.enabled = IsOwner;
        }

        // 남의 캐릭터는 NetworkTransform 이 위치를 밀어주므로 직접 움직이면 안 된다.
        PlayerMotor motor = GetComponent<PlayerMotor>();
        if (motor != null)
        {
            motor.enabled = IsOwner;
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = IsOwner;
        }

        if (!IsOwner)
        {
            AddBlockingBody(characterController);
        }

        if (IsOwner)
        {
            transform.position = new Vector3(-2f + OwnerClientId * 1.6f, 1.2f, -1f);
            FirstPersonView.SetSubject(transform);
        }
    }

    /// <summary>남의 캐릭터는 CharacterController 를 꺼두므로 콜라이더가 사라져
    /// 서로 몸을 그대로 통과했다. 좁은 주방에서 서로 부딪히고 길을 막는 것이
    /// 이 게임의 재미이므로, 움직이지 않는 몸통 콜라이더를 따로 붙여준다.
    ///
    /// CharacterController 를 켜두는 대신 캡슐을 쓰는 이유는, 켜두면
    /// NetworkTransform 이 매 프레임 밀어 넣는 위치와 서로 간섭하기 때문이다.</summary>
    private void AddBlockingBody(CharacterController shape)
    {
        if (GetComponent<CapsuleCollider>() != null)
        {
            return;
        }

        CapsuleCollider body = gameObject.AddComponent<CapsuleCollider>();
        if (shape == null)
        {
            return;
        }

        body.height = shape.height;
        body.radius = shape.radius;
        body.center = shape.center;
    }
}
