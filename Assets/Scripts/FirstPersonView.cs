using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>1인칭 시점. 카메라가 플레이어 눈높이로 내려가고 마우스로 둘러본다.
/// 로컬 협동에서는 화면이 하나라 1번 플레이어 기준이고, 네트워크에서는 내 캐릭터 기준이다.</summary>
public sealed class FirstPersonView : MonoBehaviour
{
    private const float EyeHeight = 0.72f;
    private const float Sensitivity = 0.12f;
    private const float MinPitch = -70f;
    private const float MaxPitch = 75f;

    public static FirstPersonView Instance { get; private set; }

    private Transform subject;
    private float yaw;
    private float pitch;

    /// <summary>지금 1인칭인지. 카메라와 이동, 회전이 모두 이 값을 본다.</summary>
    public static bool Active { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>이 캐릭터가 1인칭 시점의 주인공인지.</summary>
    public static bool IsSubject(Transform candidate)
    {
        return Active && Instance != null && Instance.subject == candidate;
    }

    /// <summary>1인칭일 때는 이동 입력이 보는 방향 기준이 되어야 한다.</summary>
    public static Vector2 ToViewSpace(Vector2 input)
    {
        if (!Active || Instance == null || input.sqrMagnitude < 0.0001f)
        {
            return input;
        }

        float radians = Instance.yaw * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(input.x * cos + input.y * sin, input.y * cos - input.x * sin);
    }

    public void Toggle()
    {
        SetActive(!Active);
    }

    public void SetActive(bool active)
    {
        Active = active;
        if (active)
        {
            subject = PickSubject();
            if (subject != null)
            {
                yaw = subject.eulerAngles.y;
                pitch = 8f;
            }
        }

        ApplyCursor();
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage(active ? "1인칭 시점 (V 로 되돌리기)" : "위에서 보는 시점");
        }
    }

    /// <summary>네트워크에서는 내가 조종하는 캐릭터, 로컬에서는 1번 플레이어.</summary>
    private static Transform PickSubject()
    {
        if (FollowPlayerCamera.Instance != null && FollowPlayerCamera.Instance.NetworkTarget != null)
        {
            return FollowPlayerCamera.Instance.NetworkTarget;
        }

        foreach (PlayerInteraction player in WorldRegistry.Players)
        {
            if (player != null && player.gameObject.activeInHierarchy)
            {
                return player.transform;
            }
        }

        return null;
    }

    private void Start()
    {
        // -firstperson 으로 켜고 시작할 수 있다. 바로가기를 그렇게 만들어 두면 편하다.
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-firstperson") >= 0)
        {
            SetActive(true);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
        {
            Toggle();
        }

        if (!Active)
        {
            return;
        }

        if (subject == null)
        {
            subject = PickSubject();
            if (subject == null)
            {
                return;
            }
        }

        bool playing = GameFlow.Instance == null || GameFlow.Instance.IsPlaying;
        ApplyCursor();
        if (!playing)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * Sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * Sensitivity, MinPitch, MaxPitch);
        }

        // 몸도 보는 방향을 따라간다. 상호작용은 정면을 보므로 이게 맞아야 한다.
        subject.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void LateUpdate()
    {
        if (!Active || subject == null)
        {
            return;
        }

        transform.position = subject.position + Vector3.up * EyeHeight;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ApplyCursor()
    {
        bool playing = GameFlow.Instance == null || GameFlow.Instance.IsPlaying;
        bool locked = Active && playing;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
