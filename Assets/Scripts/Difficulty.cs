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

    /// <summary>평판이 좋으면 손님이 더 자주 오고, 나쁘면 발길이 뜸해진다.
    /// 평판이 매출에 직접 영향을 줘야 관리할 이유가 생긴다.</summary>
    public static float ReputationOrderScale(int reputation)
    {
        float normalized = Mathf.Clamp01(reputation / 100f);
        return Mathf.Lerp(1.45f, 0.85f, normalized);
    }

    /// <summary>평판이 좋으면 팁이 붙는다. 최대 +10%.</summary>
    public static float ReputationTip(int reputation)
    {
        return 0.9f + Mathf.Clamp01(reputation / 100f) * 0.2f;
    }

    /// <summary>가게 등급. HUD 에 한 글자로 보여준다.</summary>
    public static string ReputationGrade(int reputation)
    {
        if (reputation >= 90) return "S";
        if (reputation >= 75) return "A";
        if (reputation >= 55) return "B";
        if (reputation >= 30) return "C";
        return "D";
    }

    /// <summary>세트 주문이 나오면 다음 손님까지의 간격을 늘린다.
    /// 수량과 간격이 함께 커져야 수요 곡선이 완만하게 오른다.</summary>
    public static float QuantitySpacing(int quantity)
    {
        return 0.55f + 0.45f * Mathf.Max(1, quantity);
    }

    /// <summary>연속 성공 보너스. 10연속이면 2배.</summary>
    public static float ComboMultiplier(int streak)
    {
        return 1f + 0.1f * Mathf.Clamp(streak, 0, MaxComboBonusSteps);
    }
}
