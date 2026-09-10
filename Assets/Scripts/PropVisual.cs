using UnityEngine;

/// <summary>도형 위에 에셋 프리팹을 얹는다. 도형은 그대로 남아 충돌과 조준 판정을 맡고
/// 보이는 것만 바뀌므로, 겉모습을 바꿔도 게임 감각은 검증된 그대로다.</summary>
public static class PropVisual
{
    private const string Folder = "Props/";

    /// <summary>소품을 도형 자리에 놓는 방식.</summary>
    public enum Placement
    {
        /// <summary>도형을 감추고 그 자리를 소품이 대신한다. 냉장고처럼 덩치가 맞는 가구용.</summary>
        Replace,

        /// <summary>도형은 카운터로 남기고 그 위에 올린다. 금전등록기처럼 작은 물건용.</summary>
        OnTop
    }

    public static GameObject Attach(GameObject host, string propName, Placement placement = Placement.Replace)
    {
        if (string.IsNullOrEmpty(propName))
        {
            return null;
        }

        bool hideHost = placement == Placement.Replace;

        GameObject prefab = Resources.Load<GameObject>(Folder + propName);
        if (prefab == null)
        {
            return null;
        }

        Renderer hostRenderer = host.GetComponent<Renderer>();
        Bounds hostBounds = hostRenderer != null
            ? hostRenderer.bounds
            : new Bounds(host.transform.position, host.transform.lossyScale);

        if (hideHost && hostRenderer != null)
        {
            hostRenderer.enabled = false;
        }

        GameObject prop = Object.Instantiate(prefab, host.transform);
        prop.name = propName;
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.identity;
        prop.transform.localScale = Vector3.one;

        // 소품이 들고 온 콜라이더는 조준 광선을 가로채고 사람의 길도 막는다. 판정은 도형이 한다.
        foreach (Collider collider in prop.GetComponentsInChildren<Collider>(true))
        {
            Object.Destroy(collider);
        }

        // 캐릭터용으로 만들어진 모델은 애니메이터가 붙어 있어 가만히 두면 혼자 움직인다.
        foreach (Animator animator in prop.GetComponentsInChildren<Animator>(true))
        {
            Object.Destroy(animator);
        }

        FitInside(prop.transform, hostBounds, placement);
        Repaint(prop.GetComponentsInChildren<Renderer>(true));
        return prop;
    }

    /// <summary>스테이션이 아닌 자리에 장식용으로 놓는다. floor 는 바닥에 닿는 지점이고
    /// box 는 이 자리에 내줄 공간이다. 소품은 그 안에 들어가도록 맞춰진다.
    ///
    /// box 는 회전을 적용한 뒤의 월드 축 기준이다. 90도 돌려 세우는 소품은 x 와 z 를
    /// 바꿔서 넘겨야 제 크기로 선다. 회전은 90도 단위로 쓴다 - 비스듬하면 경계 상자가
    /// 부풀어 소품이 실제보다 작게 줄어든다.</summary>
    public static GameObject Place(string propName, Vector3 floor, Vector3 box, float rotationY = 0f)
    {
        GameObject anchor = new GameObject("Decor " + propName);
        anchor.transform.position = floor + Vector3.up * (box.y * 0.5f);
        anchor.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        anchor.transform.localScale = box;

        if (Attach(anchor, propName) != null)
        {
            return anchor;
        }

        // 에셋이 없으면 빈 껍데기를 남기지 않는다.
        Object.Destroy(anchor);
        return null;
    }

    /// <summary>도형 상자에 들어가도록 맞춘다. 에셋들은 실제 크기로 만들어져 있으므로
    /// 넘칠 때만 줄이고 키우지는 않는다 - 금전등록기를 상자에 꽉 채우면 괴물이 된다.</summary>
    private static void FitInside(Transform prop, Bounds hostBounds, Placement placement)
    {
        if (!TryMeasure(prop, out Bounds bounds))
        {
            return;
        }

        Vector3 size = bounds.size;

        // 위에 올리는 소품은 상자 높이를 다 쓰면 안 되고, 상판에 얹힐 만큼만 크면 된다.
        float allowedHeight = placement == Placement.OnTop ? hostBounds.size.y * 0.9f : hostBounds.size.y;
        float fit = Mathf.Min(
            hostBounds.size.x / Mathf.Max(0.001f, size.x),
            allowedHeight / Mathf.Max(0.001f, size.y),
            hostBounds.size.z / Mathf.Max(0.001f, size.z));
        fit = Mathf.Min(1f, fit);

        Vector3 parentScale = prop.parent != null ? prop.parent.lossyScale : Vector3.one;
        prop.localScale = new Vector3(
            fit / Mathf.Max(0.001f, parentScale.x),
            fit / Mathf.Max(0.001f, parentScale.y),
            fit / Mathf.Max(0.001f, parentScale.z));

        // 모델마다 원점이 발밑일 수도 한가운데일 수도 있어, 잰 경계로 직접 맞춰야 한다.
        float floor = placement == Placement.OnTop ? hostBounds.max.y : hostBounds.min.y;
        prop.position = new Vector3(
            hostBounds.center.x - fit * bounds.center.x,
            floor - fit * bounds.min.y,
            hostBounds.center.z - fit * bounds.center.z);

        GameLog.Verbose($"[Prop] {prop.name} 모델 {size} → 상자 {hostBounds.size} 배율 {fit:0.00} {placement}");
    }

    /// <summary>배율 1 인 상태에서 잰 모델 경계. 원점 기준 상대값이다.</summary>
    private static bool TryMeasure(Transform prop, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds world = renderer.bounds;
            Bounds local = new Bounds(world.center - prop.position, world.size);
            if (!found)
            {
                bounds = local;
                found = true;
            }
            else
            {
                bounds.Encapsulate(local);
            }
        }

        return found;
    }

    /// <summary>에셋이 이미 URP 머티리얼을 쓰므로 프로젝트가 URP 이면 손댈 것이 없다.
    /// 다만 셰이더를 못 찾은 머티리얼은 자홍색으로 나오므로 그것만 갈아 끼운다.</summary>
    private static void Repaint(Renderer[] renderers)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            bool changed = false;
            for (int index = 0; index < materials.Length; index++)
            {
                if (!IsBroken(materials[index]))
                {
                    continue;
                }

                materials[index] = GameMaterials.Lit(Color.white);
                changed = true;
            }

            if (changed)
            {
                renderer.materials = materials;
            }
        }
    }

    /// <summary>셰이더가 사라진 머티리얼. 화면에는 자홍색으로 나온다.</summary>
    public static bool IsBroken(Material material)
    {
        return material == null
               || material.shader == null
               || material.shader.name.Contains("InternalErrorShader");
    }
}
