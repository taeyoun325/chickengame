using UnityEngine;

public enum GameEventKind
{
    RushHour,
    AppPromotion,
    PickyCustomers,
    Blackout,
    GroupOrder,
    HealthInspection
}

/// <summary>진행 중인 랜덤 이벤트 하나.</summary>
public sealed class GameEvent
{
    public readonly GameEventKind kind;
    public readonly string displayName;
    public readonly string description;
    public float remainingTime;

    public GameEvent(GameEventKind kind, string displayName, string description, float duration)
    {
        this.kind = kind;
        this.displayName = displayName;
        this.description = description;
        remainingTime = duration;
    }
}

/// <summary>일정 간격으로 가게에 사건을 하나씩 던진다.</summary>
public sealed class RandomEventSystem : MonoBehaviour
{
    private const float FirstEventDelay = 45f;
    private const float EventInterval = 70f;
    private const int InspectionFine = 60_000;

    private RestaurantGame game;
    private GameEvent current;
    private float nextEventTimer = FirstEventDelay;

    public GameEvent Current => current;

    public void Initialise(RestaurantGame restaurantGame)
    {
        game = restaurantGame;
    }

    private void Update()
    {
        if (game == null)
        {
            return;
        }

        if (current != null)
        {
            current.remainingTime -= Time.deltaTime;
            if (current.remainingTime <= 0f)
            {
                GameEvent finished = current;
                current = null;
                game.OnEventEnded(finished);
            }

            return;
        }

        nextEventTimer -= Time.deltaTime;
        if (nextEventTimer <= 0f)
        {
            StartRandomEvent();
            nextEventTimer = EventInterval;
        }
    }

    private void StartRandomEvent()
    {
        GameEventKind kind = PickKind();
        switch (kind)
        {
            case GameEventKind.RushHour:
                current = new GameEvent(kind, "러시아워", "주문이 2배로 몰립니다", 45f);
                break;
            case GameEventKind.AppPromotion:
                current = new GameEvent(kind, "배달앱 프로모션", "배달비 2배", 60f);
                break;
            case GameEventKind.PickyCustomers:
                current = new GameEvent(kind, "진상 손님", "손님 인내심이 절반", 50f);
                break;
            case GameEventKind.Blackout:
                current = new GameEvent(kind, "정전", "튀김기가 멈춥니다", 18f);
                break;
            case GameEventKind.GroupOrder:
                current = new GameEvent(kind, "단체 주문", "회식 주문이 한 번에 들어왔습니다", 3f);
                game.SpawnOrderBurst(3);
                break;
            case GameEventKind.HealthInspection:
                current = new GameEvent(kind, "위생 점검", $"벌금 ₩{InspectionFine:N0}", 4f);
                game.PayFine(InspectionFine);
                break;
        }

        if (current != null)
        {
            game.OnEventStarted(current);
        }
    }

    /// <summary>정전과 위생 점검은 초반 DAY 에는 나오지 않는다.</summary>
    private GameEventKind PickKind()
    {
        int roll = Random.Range(0, game.Day >= 2 ? 6 : 4);
        return (GameEventKind)roll;
    }

    public string BuildStatusText()
    {
        if (current == null)
        {
            return string.Empty;
        }

        return $"[{current.displayName}] {current.description}  {Mathf.CeilToInt(current.remainingTime)}s";
    }
}
