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

    public CustomerState State => state;

    private void Start()
    {
        // 클라이언트에서도 손님이 실제로 스폰됐는지 확인할 수 있게 남긴다.
        if (!KitchenNetwork.IsHostSide)
        {
            Debug.Log("[Net] 손님 스폰 수신");
        }
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
        if (bodyRenderer == null || state == CustomerState.Leaving)
        {
            return;
        }

        bodyRenderer.material.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), baseColor, Mathf.Clamp01(normalized));
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
            Vector3 step = toTarget.normalized * (WalkSpeed * Time.deltaTime);
            transform.position = flatPosition + Vector3.ClampMagnitude(step, toTarget.magnitude);
            transform.forward = Vector3.Slerp(transform.forward, toTarget.normalized, 10f * Time.deltaTime);
            bobTimer += Time.deltaTime * 9f;
            Vector3 bobbed = transform.position;
            bobbed.y = target.y + Mathf.Abs(Mathf.Sin(bobTimer)) * 0.08f;
            transform.position = bobbed;
            return;
        }

        transform.position = target;

        if (state == CustomerState.Leaving)
        {
            HasLeft = true;
            NetworkSpawner.Remove(gameObject);
            return;
        }

        state = CustomerState.Waiting;
    }
}
