using UnityEngine;

/// <summary>업그레이드가 곧바로 반영되어야 하는 값들을 한곳에 모아둔다.</summary>
public static class GameTuning
{
    public static float PlayerSpeedMultiplier = 1f;
    public static float DeliverySpeedMultiplier = 1f;

    /// <summary>기름을 밟았을 때 실제로 미끄러질 확률의 배율. 1 이면 밟으면 무조건 미끄러진다.
    /// 미끄럼방지 신발이 이 값을 내린다 - 사고를 줄일 방법이 하나도 없으면
    /// 사고는 실력이 아니라 그냥 당하는 일이 된다.</summary>
    public static float SlipResistance = 1f;

    public static void Reset()
    {
        PlayerSpeedMultiplier = 1f;
        DeliverySpeedMultiplier = 1f;
        SlipResistance = 1f;
    }

    public static float MoveSpeed(float baseSpeed)
    {
        return baseSpeed * Mathf.Clamp(PlayerSpeedMultiplier, 0.4f, 2.5f);
    }
}
