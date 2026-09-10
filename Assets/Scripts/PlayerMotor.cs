using UnityEngine;

/// <summary>플레이어 1, 2가 공유하는 이동 로직. 미끄러짐도 여기서 처리한다.</summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMotor : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpSpeed = 6.2f;

    private CharacterController characterController;
    private Vector2 input;
    private Vector3 slideVelocity;
    private float slipTimer;
    private float verticalVelocity;
    private bool jumpQueued;

    public bool IsSlipping => slipTimer > 0f;

    /// <summary>수평 이동 속력. 머리 흔들림과 발소리가 이 값을 본다.</summary>
    public float PlanarSpeed { get; private set; }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public void SetInput(Vector2 moveInput)
    {
        input = Vector2.ClampMagnitude(moveInput, 1f);
    }

    /// <summary>미끄러지는 동안에는 점프로 빠져나갈 수 없다. 그게 사고의 값이다.</summary>
    public void Jump()
    {
        if (slipTimer > 0f || !characterController.isGrounded)
        {
            return;
        }

        jumpQueued = true;

        CharacterVisual visual = GetComponent<CharacterVisual>();
        if (visual != null)
        {
            visual.PlayJump();
        }
    }

    /// <summary>기름을 밟으면 잠시 조작을 잃고 미끄러진다.</summary>
    public void Slip(float duration, Vector3 direction)
    {
        slipTimer = Mathf.Max(slipTimer, duration);
        slideVelocity = direction.normalized * (moveSpeed * 1.6f);

        PlayerInteraction interaction = GetComponent<PlayerInteraction>();
        if (interaction != null)
        {
            interaction.DropEverything();
        }
    }

    private void Update()
    {
        Vector3 horizontal;
        if (slipTimer > 0f)
        {
            slipTimer -= Time.deltaTime;
            slideVelocity = Vector3.Lerp(slideVelocity, Vector3.zero, 1.6f * Time.deltaTime);
            horizontal = slideVelocity;
            transform.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
        }
        else
        {
            Vector3 movement = new Vector3(input.x, 0f, input.y);
            horizontal = movement * GameTuning.MoveSpeed(moveSpeed);
            // 1인칭에서는 카메라가 몸의 방향을 잡으므로 여기서 돌리지 않는다.
            if (movement.sqrMagnitude > 0.001f && !FirstPersonView.IsSubject(transform))
            {
                transform.forward = Vector3.Slerp(transform.forward, movement.normalized, 12f * Time.deltaTime);
            }
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (jumpQueued)
        {
            jumpQueued = false;
            verticalVelocity = jumpSpeed;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 velocity = horizontal;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);

        Vector3 travelled = characterController.velocity;
        travelled.y = 0f;
        PlanarSpeed = characterController.isGrounded ? travelled.magnitude : 0f;
    }
}
