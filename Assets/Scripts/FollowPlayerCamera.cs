using UnityEngine;

public sealed class FollowPlayerCamera : MonoBehaviour
{
    public static FollowPlayerCamera Instance { get; private set; }

    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -13f);

    private Transform target;
    private float shakeTimer;
    private float shakeDuration;
    private float shakeStrength;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    /// <summary>네트워크로 스폰된 내 캐릭터를 카메라가 따라가게 한다.</summary>
    public static void SetTarget(Transform newTarget)
    {
        if (Instance != null)
        {
            Instance.target = newTarget;
        }
    }

    /// <summary>사고가 났을 때 화면을 짧게 흔든다.</summary>
    public void Shake(float duration, float strength)
    {
        shakeTimer = duration;
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeStrength = strength;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 focus = target.position;
        transform.position = focus + offset;
        transform.LookAt(focus + Vector3.up * 0.5f);

        if (shakeTimer <= 0f)
        {
            return;
        }

        shakeTimer -= Time.unscaledDeltaTime;
        float falloff = Mathf.Clamp01(shakeTimer / shakeDuration);
        Vector3 jitter = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (shakeStrength * falloff);
        transform.position += jitter;
    }
}
