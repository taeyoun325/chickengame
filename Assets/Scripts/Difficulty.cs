using UnityEngine;

/// <summary>DAY 가 오를수록 손님은 빨리 몰리고 단가는 올라간다.
/// 기본 단가만으로는 목표 매출 1,000만 원에 17일 넘게 걸려서, 가게가 커지는 만큼
/// 단가도 함께 올라가도록 곡선을 잡았다.</summary>
public static class Difficulty
{
    public const int MaxComboBonusSteps = 10;

    /// <summary>DAY 1 은 1.0배, 이후 하루마다 40%씩 단가가 오른다.</summary>
    public static float PriceMultiplier(int day)
    {
        return 1f + 0.4f * Mathf.Max(0, day - 1);
    }

    /// <summary>주문 간격은 하루마다 8%씩 짧아지고 5초 아래로는 내려가지 않는다.</summary>
    public static float OrderInterval(float baseInterval, int day)
    {
        return Mathf.Max(5f, baseInterval * Mathf.Pow(0.92f, Mathf.Max(0, day - 1)));
    }

    /// <summary>손님 인내심은 하루마다 5%씩 짧아지고 25초가 하한이다.</summary>
    public static float Patience(float basePatience, int day)
    {
        return Mathf.Max(25f, basePatience * Mathf.Pow(0.95f, Mathf.Max(0, day - 1)));
    }

    /// <summary>배달 요청도 조금씩 잦아진다.</summary>
    public static float DeliveryInterval(float baseInterval, int day)
    {
        return Mathf.Max(9f, baseInterval * Mathf.Pow(0.93f, Mathf.Max(0, day - 1)));
    }

    /// <summary>DAY 3 부터는 두 마리, DAY 5 부터는 세 마리까지 시킨다.</summary>
    public static int RollQuantity(int day)
    {
        int max = day >= 5 ? 3 : day >= 3 ? 2 : 1;
        return Random.Range(1, max + 1);
    }

    /// <summary>연속 성공 보너스. 10연속이면 2배.</summary>
    public static float ComboMultiplier(int streak)
    {
        return 1f + 0.1f * Mathf.Clamp(streak, 0, MaxComboBonusSteps);
    }
}
