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

    public static GameObject Attach(GameObject host, string propName, Placement placement = Placement.Replace, Bounds? space = null)
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
        Bounds hostBounds = space ?? (hostRenderer != null
            ? hostRenderer.bounds
            : new Bounds(host.transform.position, host.transform.lossyScale));

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
        //
        // 딸려온 스크립트도 함께 걷어낸다. 애니메이터만 지우면, 그것을 붙들고 있던
        // 스크립트가 다음 프레임에 null 을 참조해 예외를 던진다. 치킨 팩의
        // anim_clip_offset 이 실제로 그래서, 치킨이 생길 때마다 예외가 쌓였다.
        // 소품은 움직이지 않는 장식이므로 팩이 딸려 보낸 동작은 하나도 필요 없다.
        foreach (MonoBehaviour behaviour in prop.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.Destroy(behaviour);
        }

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
        // 앵커에는 배율을 주지 않는다. 배율을 준 채로 돌리면 회전과 비균등 배율이 서로
        // 얽혀 잰 크기가 엉키므로, 내줄 공간은 배율 대신 상자로 따로 넘긴다.
        GameObject anchor = new GameObject("Decor " + propName);
        anchor.transform.position = floor + Vector3.up * (box.y * 0.5f);
        anchor.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

        if (Attach(anchor, propName, Placement.Replace, new Bounds(anchor.transform.position, box)) != null)
        {
            return anchor;
        }

        // 에셋이 없으면 빈 껍데기를 남기지 않는다.
        Object.Destroy(anchor);
        return null;
    }

    /// <summary>다른 소품 위에 얹는다. 밑에 깔린 것의 실제 높이를 재서 올리므로
    /// 나중에 가구를 바꿔도 위에 놓인 물건이 공중에 뜨거나 파묻히지 않는다.</summary>
    public static GameObject PlaceOn(GameObject baseProp, string propName, Vector3 offset, Vector3 box)
    {
        if (baseProp == null)
        {
            return null;
        }

        float top = float.NegativeInfinity;
        foreach (Renderer renderer in baseProp.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.gameObject.activeInHierarchy)
            {
                top = Mathf.Max(top, renderer.bounds.max.y);
            }
        }

        if (float.IsNegativeInfinity(top))
        {
            return null;
        }

        Vector3 baseposition = baseProp.transform.position;
        return Place(propName, new Vector3(baseposition.x + offset.x, top, baseposition.z + offset.z), box);
    }

    /// <summary>도형 상자에 들어가도록 맞춘다. 에셋들은 실제 크기로 만들어져 있으므로
    /// 넘칠 때만 줄이고 키우지는 않는다 - 금전등록기를 상자에 꽉 채우면 괴물이 된다.</summary>
    private static void FitInside(Transform prop, Bounds hostBounds, Placement placement)
    {
        if (!TryMeasure(prop, out Bounds bounds))
        {
            return;
        }

        // 잰 값에는 부모 배율이 이미 곱해져 있다. 스테이션 블록은 (2.4, 1.6, 1.6) 처럼
        // 눌려 있으므로 나누어 모델 본래 크기로 되돌린 뒤에 계산해야 한다.
        // 이걸 빼먹으면 배율이 두 번 곱해져 소품이 부모 배율만큼 작아진다.
        Vector3 parentScale = prop.parent != null ? prop.parent.lossyScale : Vector3.one;
        Vector3 natural = Divide(bounds.size, parentScale);
        Vector3 naturalCentre = Divide(bounds.center, parentScale);
        float naturalBottom = bounds.min.y / Mathf.Max(0.001f, parentScale.y);

        // 위에 올리는 소품은 상자 높이를 다 쓰면 안 되고, 상판에 얹힐 만큼만 크면 된다.
        float allowedHeight = placement == Placement.OnTop ? hostBounds.size.y * 0.9f : hostBounds.size.y;
        float fit = Mathf.Min(
            hostBounds.size.x / Mathf.Max(0.001f, natural.x),
            allowedHeight / Mathf.Max(0.001f, natural.y),
            hostBounds.size.z / Mathf.Max(0.001f, natural.z));
        fit = Mathf.Min(1f, fit);

        // 부모 배율을 되돌려, 눌린 블록에 얹어도 소품은 제 비율로 선다.
        prop.localScale = new Vector3(
            fit / Mathf.Max(0.001f, parentScale.x),
            fit / Mathf.Max(0.001f, parentScale.y),
            fit / Mathf.Max(0.001f, parentScale.z));

        // 모델마다 원점이 발밑일 수도 한가운데일 수도 있어, 잰 경계로 직접 맞춰야 한다.
        float floor = placement == Placement.OnTop ? hostBounds.max.y : hostBounds.min.y;
        prop.position = new Vector3(
            hostBounds.center.x - fit * naturalCentre.x,
            floor - fit * naturalBottom,
            hostBounds.center.z - fit * naturalCentre.z);

        GameLog.Verbose($"[Prop] {prop.name} 모델 {natural} → 상자 {hostBounds.size} 배율 {fit:0.00} {placement}");
    }

    private static Vector3 Divide(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            value.x / Mathf.Max(0.001f, divisor.x),
            value.y / Mathf.Max(0.001f, divisor.y),
            value.z / Mathf.Max(0.001f, divisor.z));
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
