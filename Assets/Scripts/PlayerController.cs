using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;

    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector2 input = ReadMoveInput();
        Vector3 movement = new Vector3(input.x, 0f, input.y);
        movement = Vector3.ClampMagnitude(movement, 1f);

        if (movement.sqrMagnitude > 0.001f)
        {
            transform.forward = Vector3.Slerp(transform.forward, movement, 12f * Time.deltaTime);
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = movement * GameTuning.MoveSpeed(moveSpeed);
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private static Vector2 ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        return input.normalized;
    }
}
