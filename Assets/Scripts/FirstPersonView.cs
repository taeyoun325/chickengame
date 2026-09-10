using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>이 게임의 유일한 시점. 카메라는 내 캐릭터의 눈이고, 마우스로 둘러본다.
/// 조준선이 가리키는 것이 상호작용 대상이므로 시선과 몸의 방향이 늘 일치해야 한다.</summary>
public sealed class FirstPersonView : MonoBehaviour
{
    private const float EyeHeight = 0.72f;
    private const float MinPitch = -75f;
    private const float MaxPitch = 80f;
    private const float BobSpeed = 9.5f;
    private const float BobHeight = 0.055f;
    private const float BobSway = 0.03f;

    private const string SensitivityKey = "chickengame.sensitivity";

    public static FirstPersonView Instance { get; private set; }

    private Transform subject;
    private PlayerMotor subjectMotor;
    private float yaw;
    private float pitch;
    private float sensitivity = 0.12f;
    private float bobPhase;
    private float bobAmount;
    private float lastStepPhase;
    private float shakeTimer;
    private float shakeDuration;
    private float shakeStrength;

    private void Awake()
    {
        Instance = this;
        sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.12f), 0.03f, 0.5f);
    }

    /// <summary>네트워크에서 내 아바타가 스폰되면 그쪽으로 시점을 옮긴다.</summary>
    public static void SetSubject(Transform newSubject)
    {
        if (Instance == null)
        {
            return;
        }

        // 내 몸은 카메라 안에 있어서 화면을 가린다. 시점을 옮기면 이전 몸은 다시 보여야 한다.
        SetBodyVisible(Instance.subject, true);
        Instance.subject = newSubject;
        Instance.subjectMotor = newSubject != null ? newSubject.GetComponent<PlayerMotor>() : null;
        SetBodyVisible(newSubject, false);
        if (newSubject != null)
        {
            Instance.yaw = newSubject.eulerAngles.y;
        }
    }

    private static void SetBodyVisible(Transform body, bool visible)
    {
        if (body == null)
        {
            return;
        }

        CharacterVisual visual = body.GetComponent<CharacterVisual>();
        if (visual != null)
        {
            visual.SetVisible(visible);
        }

        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null && visual == null)
        {
            bodyRenderer.enabled = visible;
        }
    }

    /// <summary>이 캐릭터가 내가 보고 있는 몸인지. 남의 아바타는 시선을 따라가면 안 된다.</summary>
    public static bool IsSubject(Transform candidate)
    {
        return Instance != null && Instance.subject == candidate;
    }

    /// <summary>내가 조종하는 캐릭터. 조준 HUD 도 이 대상을 본다.</summary>
    public static Transform Subject => Instance != null ? Instance.subject : null;

    /// <summary>이동 입력은 화면이 아니라 보고 있는 방향 기준이다.</summary>
    public static Vector2 ToViewSpace(Vector2 input)
    {
        if (Instance == null || input.sqrMagnitude < 0.0001f)
        {
            return input;
        }

        float radians = Instance.yaw * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(input.x * cos + input.y * sin, input.y * cos - input.x * sin);
    }

    /// <summary>조준선이 나가는 광선. 흔들림이 섞인 카메라가 아니라 눈 위치에서 곧게 나간다.</summary>
    public static Ray AimRay
    {
        get
        {
            if (Instance == null || Instance.subject == null)
            {
                return new Ray(Vector3.zero, Vector3.forward);
            }

            return new Ray(
                Instance.subject.position + Vector3.up * EyeHeight,
                Quaternion.Euler(Instance.pitch, Instance.yaw, 0f) * Vector3.forward);
        }
    }

    /// <summary>사고가 났을 때 화면을 짧게 흔든다.</summary>
    public void Shake(float duration, float strength)
    {
        shakeTimer = duration;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeStrength = strength;
    }

    private void Start()
    {
        if (subject == null)
        {
            SetSubject(PickLocalSubject());
        }
    }

    /// <summary>로컬 플레이는 혼자 하므로 등록된 첫 플레이어가 곧 나다.</summary>
    private static Transform PickLocalSubject()
    {
        foreach (PlayerInteraction player in WorldRegistry.Players)
        {
            if (player != null && player.gameObject.activeInHierarchy)
            {
                return player.transform;
            }
        }

        return null;
    }

    private void Update()
    {
        if (subject == null)
        {
            SetSubject(PickLocalSubject());
            if (subject == null)
            {
                return;
            }
        }

        bool playing = GameFlow.Instance == null || GameFlow.Instance.IsPlaying;
        ApplyCursor(playing);
        if (!playing)
        {
            return;
        }

        ReadSensitivityKeys();

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, MinPitch, MaxPitch);
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            // 스틱은 프레임당 이동량이 아니라 속도라 시간을 곱해야 한다.
            Vector2 look = pad.rightStick.ReadValue() * (260f * sensitivity * Time.unscaledDeltaTime);
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, MinPitch, MaxPitch);
        }

        // 조준선이 곧 손이 닿는 곳이므로 몸도 늘 같은 곳을 본다.
        subject.rotation = Quaternion.Euler(0f, yaw, 0f);
        UpdateBob();
    }

    /// <summary>걷는 동안 시야가 위아래로 출렁인다. 멈추면 서서히 가라앉는다.</summary>
    private void UpdateBob()
    {
        float speed = subjectMotor != null ? subjectMotor.PlanarSpeed : 0f;
        float target = Mathf.Clamp01(speed / 5f);
        bobAmount = Mathf.MoveTowards(bobAmount, target, Time.deltaTime * 4f);
        if (bobAmount <= 0.001f)
        {
            return;
        }

        bobPhase += Time.deltaTime * BobSpeed * Mathf.Max(0.35f, target);

        // 발소리는 시야가 출렁이는 주기에 맞춘다. 반 바퀴가 한 걸음이다.
        if (bobAmount > 0.15f && bobPhase - lastStepPhase >= Mathf.PI)
        {
            lastStepPhase = bobPhase;
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.Play(GameSound.Footstep, 0.35f * bobAmount);
            }
        }
    }

    /// <summary>마우스가 너무 빠르거나 느리면 게임을 멈추지 않고 바로 고칠 수 있어야 한다.</summary>
    private void ReadSensitivityKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        int step = 0;
        if (keyboard.leftBracketKey.wasPressedThisFrame) step = -1;
        if (keyboard.rightBracketKey.wasPressedThisFrame) step = 1;
        if (step == 0)
        {
            return;
        }

        sensitivity = Mathf.Clamp(sensitivity + step * 0.02f, 0.03f, 0.5f);
        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        PlayerPrefs.Save();
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage($"마우스 감도 {sensitivity:0.00}  ( [ 낮추기   ] 올리기 )");
        }
    }

    private void LateUpdate()
    {
        if (subject == null)
        {
            return;
        }

        float bob = Mathf.Sin(bobPhase) * BobHeight * bobAmount;
        float sway = Mathf.Cos(bobPhase * 0.5f) * BobSway * bobAmount;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        transform.rotation = rotation;
        transform.position = subject.position
                             + Vector3.up * (EyeHeight + bob)
                             + rotation * Vector3.right * sway;

        if (shakeTimer <= 0f)
        {
            return;
        }

        shakeTimer -= Time.unscaledDeltaTime;
        float falloff = Mathf.Clamp01(shakeTimer / shakeDuration);
        transform.position += rotation * new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (shakeStrength * falloff);
    }

    private void ApplyCursor(bool playing)
    {
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
