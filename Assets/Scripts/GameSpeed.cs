using UnityEngine;

/// <summary>게임이 다시 시작될 때 돌아갈 기본 배속. 밸런스 측정에서 배속을 올리면
/// 결산 화면을 지나도 그 속도가 유지되어야 한다.</summary>
public static class GameSpeed
{
    private static float normal = 1f;

    public static float Normal
    {
        get => normal;
        set => normal = Mathf.Clamp(value, 0.1f, 20f);
    }

    public static void Resume()
    {
        Time.timeScale = normal;
    }
}
