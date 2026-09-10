using UnityEngine;

/// <summary>이 게임은 바닥부터 스테이션까지 전부 코드로 도형을 만든다.
/// CreatePrimitive 가 붙여주는 기본 머티리얼은 빌트인 파이프라인용이라 URP 에서는
/// 자홍색으로 나오므로, 만든 도형마다 여기서 URP 머티리얼을 새로 입힌다.</summary>
public static class GameMaterials
{
    private const string LitShader = "Universal Render Pipeline/Lit";
    private const string UnlitShader = "Universal Render Pipeline/Unlit";

    private static Shader lit;

    /// <summary>도형에 쓸 새 머티리얼. 색은 저마다 다르므로 공유하지 않고 하나씩 만든다.</summary>
    public static Material Lit(Color color)
    {
        if (lit == null)
        {
            lit = Shader.Find(LitShader) ?? Shader.Find(UnlitShader) ?? Shader.Find("Standard");
        }

        Material material = new Material(lit);
        material.color = color;
        return material;
    }

    /// <summary>도형을 만들고 색까지 입힌다.</summary>
    public static GameObject CreatePrimitive(PrimitiveType type, string name, Color color)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.GetComponent<Renderer>().sharedMaterial = Lit(color);
        return primitive;
    }
}
