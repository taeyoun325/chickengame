using System.Collections.Generic;
using UnityEngine;

/// <summary>캡슐 몸통 대신 파티 캐릭터 모델을 씌운다. 걸으면 달리는 동작이 나온다.
///
/// 두 가지를 맞춰줘야 한다. 하나는 크기 - 모델의 임포트 배율을 코드에서 알 수 없으므로
/// 렌더러 경계를 재서 몸통 높이에 맞춘다. 다른 하나는 머티리얼 - 에셋 팩은 URP 로
/// 만들어져 있고 이 프로젝트는 빌트인 파이프라인이라, 그대로 두면 전부 자홍색으로 나온다.</summary>
public sealed class CharacterVisual : MonoBehaviour
{
    private const string CharacterResource = "Prefabs/character_default";
    private const string HatFolder = "Prefabs/Hats/";
    private const string HatAnchorName = "customize_objects";
    private const float RunThreshold = 0.6f;

    private static readonly int RunTrigger = Animator.StringToHash("run");
    private static readonly int IdleTrigger = Animator.StringToHash("idle");
    private static readonly int JumpTrigger = Animator.StringToHash("jump");

    private Animator animator;
    private Renderer[] bodyRenderers;
    private Material[] tintable;
    private Vector3 lastPosition;
    private bool running;

    /// <summary>모델을 host 의 자식으로 붙이고 캡슐 렌더러를 숨긴다.
    /// 에셋 팩이 없으면 아무것도 하지 않고 캡슐을 그대로 둔다.</summary>
    public static CharacterVisual Attach(GameObject host, Color tint, string hatName = null)
    {
        GameObject prefab = Resources.Load<GameObject>(CharacterResource);
        if (prefab == null)
        {
            return null;
        }

        Renderer hostRenderer = host.GetComponent<Renderer>();
        float hostHalfHeight = hostRenderer != null ? hostRenderer.bounds.extents.y : 1f;
        if (hostRenderer != null)
        {
            hostRenderer.enabled = false;
        }

        GameObject model = Instantiate(prefab, host.transform);
        model.name = "Character";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        CharacterVisual visual = host.AddComponent<CharacterVisual>();
        visual.Initialise(model, hostHalfHeight * 2f, tint, hatName);
        return visual;
    }

    private void Initialise(GameObject model, float targetHeight, Color tint, string hatName)
    {
        bodyRenderers = model.GetComponentsInChildren<Renderer>(true);
        animator = model.GetComponent<Animator>();
        lastPosition = transform.position;

        // 키는 몸만 재서 맞춘다. 모자까지 넣으면 높은 모자를 쓴 사람이 난쟁이가 된다.
        FitToHeight(model.transform, targetHeight);
        AttachHat(model.transform, hatName);

        // 모자도 같은 URP 머티리얼을 쓰므로 붙인 뒤에 함께 옮겨 담아야 한다.
        bodyRenderers = model.GetComponentsInChildren<Renderer>(true);
        Retint(tint);
    }

    /// <summary>임포트 배율과 무관하게 몸통 높이에 맞추고, 발이 바닥에 닿게 내린다.</summary>
    private void FitToHeight(Transform model, float targetHeight)
    {
        if (!TryMeasure(model, out Bounds bounds))
        {
            return;
        }

        float fit = targetHeight / Mathf.Max(0.01f, bounds.size.y);
        Vector3 parentScale = transform.lossyScale;
        model.localScale = new Vector3(
            fit / Mathf.Max(0.01f, parentScale.x),
            fit / Mathf.Max(0.01f, parentScale.y),
            fit / Mathf.Max(0.01f, parentScale.z));

        // 몸통 바닥과 모델 발바닥을 맞춘다. bounds 는 모델을 배율 1 로 두고 잰 값이다.
        float halfHeight = parentScale.y;
        model.localPosition = new Vector3(0f, (-halfHeight - fit * bounds.min.y) / Mathf.Max(0.01f, parentScale.y), 0f);
    }

    /// <summary>배율 1 상태의 모델 경계. 스킨드 메시는 bounds 가 로컬 기준이라 그대로 쓸 수 있다.</summary>
    private bool TryMeasure(Transform model, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach (Renderer bodyRenderer in bodyRenderers)
        {
            // 꺼져 있는 모자들은 경계가 갱신되지 않아 재면 키가 엉뚱하게 나온다.
            if (bodyRenderer == null || !bodyRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            // world 경계를 모델 로컬로 환산한다. 모델 배율이 1 이라 위치 차만 빼면 된다.
            Bounds worldBounds = bodyRenderer.bounds;
            Bounds local = new Bounds(worldBounds.center - model.position, worldBounds.size);
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

    /// <summary>몸에만 플레이어 색을 입힌다. 에셋의 텍스처는 그대로 두고 색만 곱한다.</summary>
    private void Retint(Color tint)
    {
        List<Material> recolorable = new List<Material>();
        foreach (Renderer bodyRenderer in bodyRenderers)
        {
            if (bodyRenderer == null)
            {
                continue;
            }

            // 얼굴을 물들이면 눈코입이 묻히고, 모자를 물들이면 셰프 모자가 노래진다.
            bool keepOwnColor = IsUnder(bodyRenderer.transform, HatAnchorName);
            Material[] materials = bodyRenderer.materials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                bool plain = keepOwnColor
                             || (source != null && source.name.IndexOf("face", System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (PropVisual.IsBroken(source))
                {
                    source = GameMaterials.Lit(Color.white);
                    materials[index] = source;
                }

                source.color = plain ? Color.white : tint;
                if (!plain)
                {
                    recolorable.Add(source);
                }
            }

            bodyRenderer.materials = materials;
        }

        tintable = recolorable.ToArray();
    }

    /// <summary>모자는 에셋 팩이 쓰는 자리에 그대로 얹는다.</summary>
    private static void AttachHat(Transform model, string hatName)
    {
        if (string.IsNullOrEmpty(hatName))
        {
            return;
        }

        GameObject hatPrefab = Resources.Load<GameObject>(HatFolder + hatName);
        Transform anchor = FindDeep(model, HatAnchorName);
        if (hatPrefab == null || anchor == null)
        {
            return;
        }

        GameObject hat = Instantiate(hatPrefab, anchor);
        hat.transform.localPosition = Vector3.zero;
        hat.transform.localRotation = Quaternion.identity;
        hat.transform.localScale = Vector3.one;
        hat.SetActive(true);
    }

    private static bool IsUnder(Transform node, string ancestorName)
    {
        for (Transform step = node; step != null; step = step.parent)
        {
            if (step.name == ancestorName)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName)
        {
            return root;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDeep(root.GetChild(index), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>손님 색은 남은 인내심에 따라 매 프레임 바뀐다. Renderer.materials 는
    /// 부를 때마다 배열을 새로 만들므로 물들일 머티리얼은 한 번만 모아둔다.</summary>
    public void Tint(Color color)
    {
        if (tintable == null)
        {
            return;
        }

        for (int index = 0; index < tintable.Length; index++)
        {
            tintable[index].color = color;
        }
    }

    /// <summary>1인칭에서 내 몸은 카메라를 가리므로 숨긴다. 남의 캐릭터는 보여야 한다.</summary>
    public void SetVisible(bool visible)
    {
        foreach (Renderer bodyRenderer in bodyRenderers)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = visible;
            }
        }
    }

    public void PlayJump()
    {
        if (animator != null)
        {
            animator.SetTrigger(JumpTrigger);
        }
    }

    /// <summary>네트워크로 밀려오는 남의 캐릭터도 위치 변화만 보면 되므로 여기서 판단한다.</summary>
    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        Vector3 position = transform.position;
        Vector3 travelled = position - lastPosition;
        travelled.y = 0f;
        lastPosition = position;

        bool moving = travelled.magnitude / Mathf.Max(0.0001f, Time.deltaTime) > RunThreshold;
        if (moving == running)
        {
            return;
        }

        running = moving;
        animator.SetTrigger(moving ? RunTrigger : IdleTrigger);
    }
}
