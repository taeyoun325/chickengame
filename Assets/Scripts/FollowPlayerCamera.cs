using UnityEngine;

/// <summary>로컬 협동에서는 플레이어 전원이 화면에 들어오도록 잡고,
/// 네트워크 플레이에서는 내 캐릭터만 따라간다.</summary>
public sealed class FollowPlayerCamera : MonoBehaviour
{
    public static FollowPlayerCamera Instance { get; private set; }

    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -13f);

    private Transform target;
    private Vector3 smoothedFocus;
    private float smoothedZoom;
    private bool hasFocus;
    private float shakeTimer;
    private float shakeDuration;
    private float shakeStrength;

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>네트워크로 스폰된 내 캐릭터만 따라가게 한다.</summary>
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

    /// <summary>네트워크에서 내가 조종하는 캐릭터. 1인칭 시점이 이 대상을 따라간다.</summary>
    public Transform NetworkTarget => target;

    private void LateUpdate()
    {
        // 1인칭일 때는 그쪽이 카메라를 잡는다.
        if (FirstPersonView.Active)
        {
            return;
        }

        if (!TryGetFocus(out Vector3 focus, out float spread))
        {
            return;
        }

        // 플레이어가 흩어질수록 조금 물러나서 전원을 담는다.
        float zoom = Mathf.Clamp(spread * 0.55f, 0f, 9f);
        if (!hasFocus)
        {
            smoothedFocus = focus;
            smoothedZoom = zoom;
            hasFocus = true;
        }
        else
        {
            float step = Time.unscaledDeltaTime * 4f;
            smoothedFocus = Vector3.Lerp(smoothedFocus, focus, step);
            smoothedZoom = Mathf.Lerp(smoothedZoom, zoom, step);
        }

        Vector3 pulledBack = offset + new Vector3(0f, smoothedZoom * 0.7f, -smoothedZoom * 0.6f);
        transform.position = smoothedFocus + pulledBack;
        transform.LookAt(smoothedFocus + Vector3.up * 0.5f);

        if (shakeTimer <= 0f)
        {
            return;
        }

        shakeTimer -= Time.unscaledDeltaTime;
        float falloff = Mathf.Clamp01(shakeTimer / shakeDuration);
        transform.position += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * (shakeStrength * falloff);
    }

    /// <summary>따라갈 지점과 플레이어들이 흩어진 정도를 구한다.</summary>
    private bool TryGetFocus(out Vector3 focus, out float spread)
    {
        focus = Vector3.zero;
        spread = 0f;

        if (target != null)
        {
            focus = target.position;
            return true;
        }

        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;
        int count = 0;

        foreach (PlayerInteraction player in WorldRegistry.Players)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 position = player.transform.position;
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
            focus += position;
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        focus /= count;
        Vector3 size = max - min;
        spread = Mathf.Max(size.x * 0.6f, size.z);
        return true;
    }
}
