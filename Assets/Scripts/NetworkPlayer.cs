using Unity.Netcode;
using UnityEngine;

/// <summary>네트워크로 접속한 플레이어 한 명. 소유자만 입력을 받고, 나머지는 위치만 따라온다.</summary>
[RequireComponent(typeof(PlayerMotor))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    private static readonly Color[] PlayerColors =
    {
        new Color(0.95f, 0.8f, 0.2f),
        new Color(0.25f, 0.65f, 1f),
        new Color(0.4f, 0.85f, 0.4f),
        new Color(0.9f, 0.45f, 0.75f)
    };

    public override void OnNetworkSpawn()
    {
        Renderer bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = PlayerColors[(int)(OwnerClientId % (ulong)PlayerColors.Length)];
        }

        name = IsOwner ? "Network Player (me)" : $"Network Player {OwnerClientId}";
        Debug.Log($"[Net] 플레이어 아바타 스폰 id={OwnerClientId} owner={IsOwner}");

        // 내 캐릭터만 입력을 읽는다. 게임패드가 꽂혀 있으면 그것을 먼저 쓴다.
        LocalPlayerInput input = GetComponent<LocalPlayerInput>();
        if (input != null)
        {
            input.enabled = IsOwner;
            if (IsOwner)
            {
                input.Configure(UnityEngine.InputSystem.Gamepad.current != null
                    ? InputScheme.Gamepad
                    : InputScheme.KeyboardLeft);
            }
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
            FollowPlayerCamera.SetTarget(transform);
        }
    }
}
