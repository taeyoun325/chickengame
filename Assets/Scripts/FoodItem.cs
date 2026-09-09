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

    private MenuRecipe recipe = MenuDatabase.Fried;
    private Renderer itemRenderer;

    /// <summary>생닭은 아직 메뉴가 정해지지 않았고, 양념대에서 최종 메뉴가 결정된다.</summary>
    public MenuRecipe Recipe => recipe;

    public bool ReadyToPack => state == FoodState.Cooked && !dirty && (!recipe.needsSauce || sauced);

    private void Awake()
    {
        itemRenderer = GetComponent<Renderer>();
        RefreshVisual();
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
        if (itemRenderer == null)
        {
            return;
        }

        if (dirty)
        {
            itemRenderer.material.color = new Color(0.35f, 0.3f, 0.22f);
            return;
        }

        itemRenderer.material.color = state switch
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
