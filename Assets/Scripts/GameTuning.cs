using UnityEngine;

/// <summary>업그레이드가 곧바로 반영되어야 하는 값들을 한곳에 모아둔다.</summary>
public static class GameTuning
{
    public static float PlayerSpeedMultiplier = 1f;
    public static float DeliverySpeedMultiplier = 1f;

    public static void Reset()
    {
        PlayerSpeedMultiplier = 1f;
        DeliverySpeedMultiplier = 1f;
    }

    public static float MoveSpeed(float baseSpeed)
    {
        return baseSpeed * Mathf.Clamp(PlayerSpeedMultiplier, 0.4f, 2.5f);
    }
}
