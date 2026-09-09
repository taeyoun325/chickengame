using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class LocalCoopPlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    private CharacterController characterController;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.jKey.isPressed) input.x -= 1f;
        if (Keyboard.current.lKey.isPressed) input.x += 1f;
        if (Keyboard.current.kKey.isPressed) input.y -= 1f;
        if (Keyboard.current.iKey.isPressed) input.y += 1f;
        input = Vector2.ClampMagnitude(input, 1f);

        Vector3 movement = new Vector3(input.x, 0f, input.y);
        if (movement.sqrMagnitude > 0.001f)
        {
            transform.forward = Vector3.Slerp(transform.forward, movement, 12f * Time.deltaTime);
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += -20f * Time.deltaTime;
        Vector3 velocity = movement * moveSpeed;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }
}
