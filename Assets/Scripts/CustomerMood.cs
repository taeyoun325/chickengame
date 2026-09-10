using UnityEngine;

public enum CustomerMoodKind
{
    Normal,
    Hurry,
    Patient,
    Regular
}

/// <summary>손님 성격. 기다리는 시간과 값이 달라져서, 줄에 선 사람을 보고
/// 누구부터 처리할지 정하는 판단이 생긴다.</summary>
public sealed class CustomerMood
{
    public readonly CustomerMoodKind kind;
    public readonly string displayName;
    public readonly float patienceScale;
    public readonly float priceScale;
    public readonly float walkSpeedScale;
    public readonly Color tint;

    private CustomerMood(CustomerMoodKind kind, string displayName, float patienceScale, float priceScale, float walkSpeedScale, Color tint)
    {
        this.kind = kind;
        this.displayName = displayName;
        this.patienceScale = patienceScale;
        this.priceScale = priceScale;
        this.walkSpeedScale = walkSpeedScale;
        this.tint = tint;
    }

    public static readonly CustomerMood Normal =
        new CustomerMood(CustomerMoodKind.Normal, string.Empty, 1f, 1f, 1f, Color.white);

    /// <summary>급한 손님: 오래 못 기다리지만 값을 더 쳐준다.</summary>
    public static readonly CustomerMood Hurry =
        new CustomerMood(CustomerMoodKind.Hurry, "급함", 0.6f, 1.45f, 1.35f, new Color(1f, 0.75f, 0.35f));

    /// <summary>느긋한 손님: 한참 기다려 주는 대신 값을 깎는다.</summary>
    public static readonly CustomerMood Patient =
        new CustomerMood(CustomerMoodKind.Patient, "느긋", 1.6f, 0.85f, 0.8f, new Color(0.7f, 0.9f, 1f));

    /// <summary>단골: 값을 두 배로 쳐주지만 실망시키면 평판 타격이 크다.</summary>
    public static readonly CustomerMood Regular =
        new CustomerMood(CustomerMoodKind.Regular, "단골", 0.9f, 2f, 1f, new Color(1f, 0.6f, 0.9f));

    /// <summary>단골을 놓치면 평판이 두 배로 깎인다.</summary>
    public int FailurePenalty(int basePenalty)
    {
        return kind == CustomerMoodKind.Regular ? basePenalty * 2 : basePenalty;
    }

    /// <summary>DAY 가 오를수록 급한 손님과 단골이 자주 온다.</summary>
    public static CustomerMood Roll(int day)
    {
        float roll = Random.value;
        float hurryChance = Mathf.Min(0.35f, 0.1f + day * 0.04f);
        float regularChance = day >= 2 ? Mathf.Min(0.2f, 0.05f + day * 0.03f) : 0f;

        if (roll < hurryChance)
        {
            return Hurry;
        }

        if (roll < hurryChance + regularChance)
        {
            return Regular;
        }

        if (roll < hurryChance + regularChance + 0.25f)
        {
            return Patient;
        }

        return Normal;
    }
}
