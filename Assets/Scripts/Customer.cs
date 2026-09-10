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
    private CharacterVisual visual;
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
        if (mood.kind == CustomerMoodKind.Normal)
        {
            return;
        }

        baseColor = Color.Lerp(baseColor, mood.tint, 0.45f);
        Recolor(baseColor);
    }

    private void Recolor(Color color)
    {
        if (visual != null)
        {
            visual.Tint(color);
        }
        else if (bodyRenderer != null)
        {
            bodyRenderer.material.color = color;
        }
    }

    private void Awake()
    {
        // Initialise 는 호스트에서만 불린다. 접속한 플레이어도 손님을 사람으로 봐야 하므로
        // 모델은 여기서 붙이고, 색만 나중에 덧입힌다.
        bodyRenderer = GetComponent<Renderer>();
        baseColor = bodyRenderer != null ? bodyRenderer.material.color : Color.white;
        visual = CharacterVisual.Attach(gameObject, baseColor, HatChoices[Random.Range(0, HatChoices.Length)]);
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
        Recolor(color);
    }

    /// <summary>줄에 선 손님들이 서로 구분되도록 모자를 섞어 씌운다.</summary>
    private static readonly string[] HatChoices = { "party hat", "orange fedora", null };

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
        // 모자 쓴 손님이 있으므로 머리보다 넉넉히 위여야 주문이 가려지지 않는다.
        tagObject.transform.localPosition = new Vector3(0f, 1.85f, 0f);

        tagBaseScale = tagObject.transform.localScale;

        TextMesh mesh = tagObject.AddComponent<TextMesh>();
        mesh.characterSize = 0.06f;
        mesh.fontSize = 60;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = Color.white;

        UiFont.Apply(mesh);
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
        if (state == CustomerState.Leaving)
        {
            return;
        }

        Recolor(Color.Lerp(new Color(0.85f, 0.15f, 0.1f), baseColor, Mathf.Clamp01(normalized)));
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
            // 걷는 모습은 모델의 달리기 동작이 맡는다. 여기서 위아래로 튀기면 겹쳐서 어색하다.
            Vector3 step = toTarget.normalized * (WalkSpeed * mood.walkSpeedScale * Time.deltaTime);
            transform.position = flatPosition + Vector3.ClampMagnitude(step, toTarget.magnitude);
            transform.forward = Vector3.Slerp(transform.forward, toTarget.normalized, 10f * Time.deltaTime);
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
