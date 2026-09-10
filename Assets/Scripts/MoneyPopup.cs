using UnityEngine;

/// <summary>돈이 들어온 자리에 금액이 떠올랐다 사라진다.
///
/// 지금까지 금액은 화면 가운데 공용 메시지 줄에만 나왔다. 그 줄은 경고와 안내도
/// 함께 쓰는 자리라, 돈을 벌었다는 신호가 다른 문구에 묻히고 어디서 벌었는지도 알 수 없었다.</summary>
public sealed class MoneyPopup : MonoBehaviour
{
    private const float Lifetime = 1.4f;
    private const float RiseSpeed = 1.1f;

    private TextMesh label;
    private Transform cameraTransform;
    private float remaining = Lifetime;
    private Color baseColor;

    public static void Show(Vector3 position, string text, Color color)
    {
        GameObject popupObject = new GameObject("Money Popup");
        popupObject.transform.position = position;

        TextMesh mesh = popupObject.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.characterSize = 0.08f;
        mesh.fontSize = 64;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        UiFont.Apply(mesh);

        MoneyPopup popup = popupObject.AddComponent<MoneyPopup>();
        popup.label = mesh;
        popup.baseColor = color;
    }

    private void LateUpdate()
    {
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

        if (cameraTransform == null)
        {
            Camera main = Camera.main;
            if (main == null)
            {
                return;
            }

            cameraTransform = main.transform;
        }

        transform.rotation = cameraTransform.rotation;

        // 끝에 가서 흐려진다. 갑자기 사라지면 눈이 놓친다.
        float fade = Mathf.Clamp01(remaining / (Lifetime * 0.5f));
        if (label != null)
        {
            label.color = new Color(baseColor.r, baseColor.g, baseColor.b, fade);
        }
    }
}
