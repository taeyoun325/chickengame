using UnityEngine;

/// <summary>Unity 6 에서 내장 폰트 이름이 LegacyRuntime.ttf 로 바뀌었다.
/// 옛 이름 Arial.ttf 로 부르면 null 이 아니라 ArgumentException 이 나므로
/// 폰트를 찾는 곳은 전부 여기를 거친다.</summary>
public static class UiFont
{
    private static Font cached;

    public static Font Default
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return cached;
        }
    }

    /// <summary>TextMesh 는 폰트와 머티리얼을 함께 지정해야 글자가 그려진다.</summary>
    public static void Apply(TextMesh mesh)
    {
        Font font = Default;
        if (font == null)
        {
            return;
        }

        mesh.font = font;
        MeshRenderer meshRenderer = mesh.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = font.material;
        }
    }
}
