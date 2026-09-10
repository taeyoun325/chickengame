using System.Collections.Generic;
using UnityEngine;

public enum FoodState
{
    Raw,
    Frying,
    Cooked,
    Burnt,
    Packaged
}

public sealed class FoodItem : MonoBehaviour
{
    public FoodState state;
    public float cookProgress;
    public bool burnCounted;
    public bool sauced;
    public bool dirty;

    /// <summary>튀김기 같은 스테이션에 올려져 있으면 그 스테이션.</summary>
    public Station RestingStation { get; set; }

    private MenuRecipe recipe = MenuDatabase.Fried;
    private Renderer itemRenderer;
    private Material[] modelMaterials;
    private GameObject[] packagingProps;

    /// <summary>생닭은 아직 메뉴가 정해지지 않았고, 양념대에서 최종 메뉴가 결정된다.</summary>
    public MenuRecipe Recipe => recipe;

    public bool ReadyToPack => state == FoodState.Cooked && !dirty && (!recipe.needsSauce || sauced);

    private void Awake()
    {
        itemRenderer = GetComponent<Renderer>();
        BuildModel();
        RefreshVisual();
        WorldRegistry.Register(this);
    }

    /// <summary>1인칭에서 손에 들고 다니는 물건이라 가장 자주 보인다.
    /// 에셋 팩이 없으면 예전의 구 모양으로 남는다.</summary>
    private void BuildModel()
    {
        GameObject model = PropVisual.Attach(gameObject, "Chicken");
        if (model == null)
        {
            return;
        }

        List<Material> materials = new List<Material>();
        foreach (Renderer modelRenderer in model.GetComponentsInChildren<Renderer>(true))
        {
            materials.AddRange(modelRenderer.materials);
        }

        modelMaterials = materials.ToArray();
        packagingProps = FindPackaging(model.transform);
    }

    /// <summary>모델에 딸린 상자 프롭. 포장한 뒤에만 씌운다.</summary>
    private static GameObject[] FindPackaging(Transform root)
    {
        List<GameObject> boxes = new List<GameObject>();
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
        {
            if (node.name.StartsWith("Box", System.StringComparison.OrdinalIgnoreCase))
            {
                node.gameObject.SetActive(false);
                boxes.Add(node.gameObject);
            }
        }

        return boxes.ToArray();
    }

    private void OnDestroy()
    {
        WorldRegistry.Unregister(this);
    }

    public void SetRecipe(MenuRecipe nextRecipe)
    {
        recipe = nextRecipe;
        RefreshVisual();
    }

    /// <summary>바닥에 닿은 음식은 손님에게 낼 수 없다.</summary>
    public void MarkDirty()
    {
        dirty = true;
        RefreshVisual();
    }

    public void SetState(FoodState nextState)
    {
        state = nextState;
        RefreshVisual();
    }

    public void SetHeld(bool held)
    {
        Collider itemCollider = GetComponent<Collider>();
        if (itemCollider != null)
        {
            itemCollider.enabled = !held;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = held;
        }
    }

    public string Describe()
    {
        if (dirty)
        {
            return "바닥에 떨어진 치킨";
        }

        return state switch
        {
            FoodState.Raw => "생닭",
            FoodState.Frying => "튀기는 중",
            FoodState.Burnt => "탄 치킨",
            FoodState.Cooked => sauced ? $"{recipe.displayName} 치킨" : "튀긴 치킨",
            FoodState.Packaged => $"{recipe.displayName} 포장",
            _ => "치킨"
        };
    }

    private void RefreshVisual()
    {
        Paint(dirty ? new Color(0.35f, 0.3f, 0.22f) : StateColor());

        if (packagingProps != null)
        {
            foreach (GameObject box in packagingProps)
            {
                box.SetActive(state == FoodState.Packaged);
            }
        }
    }

    private void Paint(Color color)
    {
        if (modelMaterials != null)
        {
            foreach (Material material in modelMaterials)
            {
                material.color = color;
            }

            return;
        }

        if (itemRenderer != null)
        {
            itemRenderer.material.color = color;
        }
    }

    private Color StateColor()
    {
        return state switch
        {
            FoodState.Raw => new Color(0.9f, 0.65f, 0.45f),
            FoodState.Frying => new Color(1f, 0.55f, 0.05f),
            FoodState.Cooked => sauced ? recipe.cookedColor : new Color(0.85f, 0.55f, 0.15f),
            FoodState.Burnt => new Color(0.08f, 0.04f, 0.02f),
            FoodState.Packaged => recipe.packagedColor,
            _ => Color.white
        };
    }
}
