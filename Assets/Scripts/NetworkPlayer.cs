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

        if (IsOwner)
        {
            transform.position = new Vector3(-2f + OwnerClientId * 1.6f, 1.2f, -1f);
            FirstPersonView.SetSubject(transform);
        }
    }
}
