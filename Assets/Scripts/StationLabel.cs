using UnityEngine;

/// <summary>스테이션 위에 떠 있는 이름표. 가까이 가면 무엇을 하면 되는지도 알려준다.</summary>
public sealed class StationLabel : MonoBehaviour
{
    private const float HintRange = 3.2f;

    private TextMesh label;
    private Transform camaraTransform;
    private Station station;
    private string title;

    public static StationLabel Attach(Station station, string title, float height)
    {
        GameObject labelObject = new GameObject($"{station.name} Label");
        labelObject.transform.SetParent(station.transform, false);

        // 스테이션이 눌려 있어도 글자는 정상 비율로 보이도록 부모 스케일을 되돌린다.
        Vector3 parentScale = station.transform.lossyScale;
        labelObject.transform.localScale = new Vector3(
            1f / Mathf.Max(0.01f, parentScale.x),
            1f / Mathf.Max(0.01f, parentScale.y),
            1f / Mathf.Max(0.01f, parentScale.z));
        labelObject.transform.position = station.transform.position + Vector3.up * height;

        StationLabel stationLabel = labelObject.AddComponent<StationLabel>();
        stationLabel.Initialise(station, title);
        return stationLabel;
    }

    private void Initialise(Station owner, string labelTitle)
    {
        station = owner;
        title = labelTitle;

        label = gameObject.AddComponent<TextMesh>();
        label.text = labelTitle;

        // 폰트를 지정하지 않으면 TextMesh 는 아무것도 그리지 않는다.
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (font != null)
        {
            label.font = font;
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sharedMaterial = font.material;
            }
        }
        // characterSize 는 월드 단위라 조금만 키워도 글자가 거대해진다.
        label.characterSize = 0.06f;
        label.fontSize = 60;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
    }

    private void LateUpdate()
    {
        if (label == null)
        {
            return;
        }

        if (camaraTransform == null)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            camaraTransform = main.transform;
        }

        // 라벨은 늘 카메라를 향한다.
        transform.rotation = camaraTransform.rotation;

        PlayerInteraction nearest = NearestPlayer(out float distance);
        if (nearest == null || distance > HintRange)
        {
            label.text = title;
            label.color = Color.white;
            return;
        }

        label.text = $"{title}\n{Hint(nearest)}";
        label.color = new Color(1f, 0.92f, 0.55f);
    }

    /// <summary>손에 든 것과 스테이션 종류를 보고 다음에 할 일을 한 줄로 알려준다.</summary>
    private string Hint(PlayerInteraction actor)
    {
        FoodItem held = actor.HeldFood;
        switch (station.stationType)
        {
            case StationType.Fridge:
                return actor.HandsFree ? "생닭 꺼내기" : "손 비우기";
            case StationType.Fryer:
                if (held != null && held.state == FoodState.Raw) return "넣기";
                if (station.StoredFood != null && station.StoredFood.state != FoodState.Frying) return "꺼내기";
                return "생닭 필요";
            case StationType.Sauce:
                return held != null && held.state == FoodState.Cooked
                    ? $"{MenuDatabase.Get(actor.SauceChoice).displayName} 바르기 (소스 전환 키로 변경)"
                    : "튀긴 치킨 필요";
            case StationType.Packing:
                return held != null && held.ReadyToPack ? "포장하기" : "익은 치킨 필요";
            case StationType.Checkout:
                return held != null && held.state == FoodState.Packaged ? "손님에게 주기" : "포장 필요";
            case StationType.Delivery:
                return held != null && held.state == FoodState.Packaged ? "배달 보내기" : "포장 필요";
            case StationType.Upgrade:
                return "숫자 키로 구매";
            case StationType.Trash:
                return held != null ? "버리기" : "";
            case StationType.Extinguisher:
                return actor.CarryingExtinguisher ? "제자리에 두기" : "소화기 들기";
            default:
                return string.Empty;
        }
    }

    private PlayerInteraction NearestPlayer(out float distance)
    {
        PlayerInteraction closest = null;
        distance = float.MaxValue;
        foreach (PlayerInteraction player in WorldRegistry.Players)
        {
            if (player == null)
            {
                continue;
            }

            float candidate = Vector3.Distance(player.transform.position, transform.position);
            if (candidate < distance)
            {
                distance = candidate;
                closest = player;
            }
        }

        return closest;
    }
}
