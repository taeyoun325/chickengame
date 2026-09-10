using UnityEngine;

public enum CustomerState
{
    Entering,
    Waiting,
    Leaving
}

/// <summary>손님은 문에서 걸어 들어와 줄을 서고, 만족하거나 화가 나면 문으로 돌아간다.</summary>
public sealed class Customer : MonoBehaviour
{
    public const float WalkSpeed = 2.6f;

    private CustomerState state = CustomerState.Entering;
    private Vector3 queueSlot;
    private Vector3 exitPoint;
    private Renderer bodyRenderer;
    private Color baseColor;
    private float bobTimer;
    private TextMesh tagMesh;
    private Transform cameraTransform;
    private Vector3 tagBaseScale = Vector3.one;

    public CustomerState State => state;

    private CustomerMood mood = CustomerMood.Normal;
    private float impatience;

    /// <summary>성격에 따라 색조와 걸음이 달라진다.</summary>
    public void ApplyMood(CustomerMood customerMood)
    {
        mood = customerMood ?? CustomerMood.Normal;
        if (bodyRenderer != null && mood.kind != CustomerMoodKind.Normal)
        {
            baseColor = Color.Lerp(baseColor, mood.tint, 0.45f);
            bodyRenderer.material.color = baseColor;
        }
    }

    private void Start()
    {
        GameLog.Verbose($"[Net] 손님 스폰 ({(KitchenNetwork.IsHostSide ? "host" : "client")})");
    }
    public bool HasLeft { get; private set; }

    public void Initialise(Vector3 spawnPoint, Vector3 slot, Vector3 exit, Color color)
    {
        transform.position = spawnPoint;
        queueSlot = slot;
        exitPoint = exit;
        baseColor = color;
        bodyRenderer = GetComponent<Renderer>();
        if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }
    }

    /// <summary>머리 위에 무엇을 기다리는지 띄운다.</summary>
    public string CurrentTag { get; private set; } = string.Empty;

    public void ShowTag(string text)
    {
        if (CurrentTag == text && tagMesh != null)
        {
            return;
        }

        CurrentTag = text;
        if (tagMesh == null)
        {
            tagMesh = CreateTag();
        }

        if (tagMesh != null)
        {
            tagMesh.text = text;
        }
    }

    private TextMesh CreateTag()
    {
        GameObject tagObject = new GameObject("Order Tag");
        tagObject.transform.SetParent(transform, false);

        Vector3 scale = transform.lossyScale;
        tagObject.transform.localScale = new Vector3(
            1f / Mathf.Max(0.01f, scale.x),
            1f / Mathf.Max(0.01f, scale.y),
            1f / Mathf.Max(0.01f, scale.z));
        tagObject.transform.localPosition = new Vector3(0f, 1.35f, 0f);

        tagBaseScale = tagObject.transform.localScale;

        TextMesh mesh = tagObject.AddComponent<TextMesh>();
        mesh.characterSize = 0.06f;
        mesh.fontSize = 60;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.white;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null)
        {
            mesh.font = font;
            MeshRenderer meshRenderer = tagObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = font.material;
            }
        }

        return mesh;
    }

    public void SetQueueSlot(Vector3 slot)
    {
        queueSlot = slot;
        if (state == CustomerState.Waiting)
        {
            state = CustomerState.Entering;
        }
    }

    /// <summary>주문이 끝났거나 인내심이 다한 손님을 퇴장시킨다.</summary>
    public void Leave()
    {
        state = CustomerState.Leaving;
    }

    /// <summary>남은 인내심에 따라 손님 색이 붉게 변한다.</summary>
    public void ShowPatience(float normalized)
    {
        impatience = 1f - Mathf.Clamp01(normalized);
        ApplyPatienceColor(normalized);
    }

    private void ApplyPatienceColor(float normalized)
    {
        if (bodyRenderer == null || state == CustomerState.Leaving)
        {
            return;
        }

        bodyRenderer.material.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), baseColor, Mathf.Clamp01(normalized));
    }

    private void LateUpdate()
    {
        if (tagMesh == null)
        {
            return;
        }

        // Camera.main 은 매 프레임 찾을 만큼 싼 호출이 아니다.
        if (cameraTransform == null)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            cameraTransform = main.transform;
        }

        tagMesh.transform.rotation = cameraTransform.rotation;

        // 스테이션 이름표와 같은 이유로, 가까이 가도 글자 크기가 유지되게 한다.
        float distance = Vector3.Distance(cameraTransform.position, tagMesh.transform.position);
        tagMesh.transform.localScale = tagBaseScale * Mathf.Clamp(distance / 15f, 0.2f, 1.3f);
    }

    private void Update()
    {
        // 네트워크에서는 호스트만 손님을 움직이고, 나머지는 NetworkTransform 이 위치를 받는다.
        if (!KitchenNetwork.IsHostSide)
        {
            return;
        }

        Vector3 target = state == CustomerState.Leaving ? exitPoint : queueSlot;
        Vector3 flatPosition = new Vector3(transform.position.x, target.y, transform.position.z);
        Vector3 toTarget = target - flatPosition;

        if (toTarget.sqrMagnitude > 0.04f)
        {
            Vector3 step = toTarget.normalized * (WalkSpeed * mood.walkSpeedScale * Time.deltaTime);
            transform.position = flatPosition + Vector3.ClampMagnitude(step, toTarget.magnitude);
            transform.forward = Vector3.Slerp(transform.forward, toTarget.normalized, 10f * Time.deltaTime);
            bobTimer += Time.deltaTime * 9f;
            Vector3 bobbed = transform.position;
            bobbed.y = target.y + Mathf.Abs(Mathf.Sin(bobTimer)) * 0.08f;
            transform.position = bobbed;
            return;
        }

        transform.position = target;

        // 기다리다 지치면 발을 구른다. 줄만 봐도 급한 사람이 누군지 보인다.
        if (state == CustomerState.Waiting && impatience > 0.55f)
        {
            bobTimer += Time.deltaTime * (6f + impatience * 10f);
            Vector3 tapping = transform.position;
            tapping.y = target.y + Mathf.Abs(Mathf.Sin(bobTimer)) * 0.12f * impatience;
            transform.position = tapping;
        }

        if (state == CustomerState.Leaving)
        {
            HasLeft = true;
            NetworkSpawner.Remove(gameObject);
            return;
        }

        state = CustomerState.Waiting;
    }
}
