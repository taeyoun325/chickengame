using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>플레이어 2: IJKL 로컬 협동용.</summary>
[RequireComponent(typeof(PlayerMotor))]
public sealed class LocalCoopPlayerController : MonoBehaviour
{
    private PlayerMotor motor;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
    }

    private void Update()
    {
        motor.SetInput(ReadMoveInput());
    }

    private static Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.jKey.isPressed) input.x -= 1f;
        if (Keyboard.current.lKey.isPressed) input.x += 1f;
        if (Keyboard.current.kKey.isPressed) input.y -= 1f;
        if (Keyboard.current.iKey.isPressed) input.y += 1f;
        return input.normalized;
    }
}
