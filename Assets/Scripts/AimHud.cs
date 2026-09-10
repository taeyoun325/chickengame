using UnityEngine;
using UnityEngine.UI;

/// <summary>화면 중앙의 조준점과 그 아래 안내. 1인칭에서는 무엇을 겨냥했는지가
/// 곧 무엇을 할 수 있는지이므로, 손에 든 것과 대상 스테이션을 여기서 읽어준다.</summary>
public sealed class AimHud : MonoBehaviour
{
    private static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.35f);
    private static readonly Color TargetColor = new Color(1f, 0.85f, 0.3f, 0.95f);

    private Image crosshair;
    private Text prompt;
    private Text hands;

    public void Connect(Image crosshairImage, Text promptLabel, Text handsLabel)
    {
        crosshair = crosshairImage;
        prompt = promptLabel;
        hands = handsLabel;
    }

    private void LateUpdate()
    {
        bool playing = GameFlow.Instance == null || GameFlow.Instance.IsPlaying;
        PlayerInteraction actor = playing ? PlayerInteraction.Local : null;

        if (crosshair != null)
        {
            crosshair.enabled = actor != null;
        }

        if (actor == null)
        {
            SetText(prompt, string.Empty);
            SetText(hands, string.Empty);
            return;
        }

        SetText(hands, HandsText(actor));

        Station target = PlayerInteraction.AimedStation;
        if (target == null)
        {
            SetText(prompt, string.Empty);
            if (crosshair != null)
            {
                crosshair.color = IdleColor;
            }

            return;
        }

        if (crosshair != null)
        {
            crosshair.color = TargetColor;
        }

        string hint = StationHints.For(target, actor);
        SetText(prompt, string.IsNullOrEmpty(hint) ? target.name : $"[E]  {hint}");
    }

    private static string HandsText(PlayerInteraction actor)
    {
        if (actor.CarryingExtinguisher)
        {
            return "손: 소화기   [E] 불 끄기   [F] 내려놓기";
        }

        return actor.HeldFood != null
            ? $"손: {actor.HeldFood.Describe()}   [F] 내려놓기"
            : "손: 비어 있음";
    }

    private static void SetText(Text label, string value)
    {
        if (label != null && label.text != value)
        {
            label.text = value;
        }
    }
}
