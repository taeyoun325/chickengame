using UnityEngine;

/// <summary>스테이션 위에 떠 있는 이름표. 조준선으로 겨냥하면 할 일도 함께 알려준다.</summary>
public sealed class StationLabel : MonoBehaviour
{
    private const float ReferenceDistance = 15f;

    private TextMesh label;
    private Vector3 baseScale = Vector3.one;
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
        stationLabel.baseScale = labelObject.transform.localScale;
        stationLabel.Initialise(station, title);
        return stationLabel;
    }

    private void Initialise(Station owner, string labelTitle)
    {
        station = owner;
        title = labelTitle;

        label = gameObject.AddComponent<TextMesh>();
        label.text = labelTitle;
        UiFont.Apply(label);

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

        // 라벨은 늘 카메라를 향하고, 거리와 무관하게 같은 크기로 보인다.
        transform.rotation = camaraTransform.rotation;
        KeepApparentSize();

        // 1인칭에서는 겨냥한 것이 곧 다음에 할 일이므로, 조준한 스테이션만 안내를 편다.
        PlayerInteraction actor = PlayerInteraction.Local;
        if (actor == null || PlayerInteraction.AimedStation != station)
        {
            label.text = title;
            label.color = Color.white;
            return;
        }

        label.text = $"{title}\n{StationHints.For(station, actor)}";
        label.color = new Color(1f, 0.92f, 0.55f);
    }

    /// <summary>1인칭으로 가까이 가면 글자가 화면을 덮는다. 거리에 비례해 키워
    /// 어느 시점에서나 같은 크기로 보이게 한다.</summary>
    private void KeepApparentSize()
    {
        float distance = Vector3.Distance(camaraTransform.position, transform.position);
        float factor = Mathf.Clamp(distance / ReferenceDistance, 0.18f, 1.3f);
        transform.localScale = baseScale * factor;
    }

}
