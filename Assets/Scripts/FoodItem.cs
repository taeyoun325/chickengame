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

    private MenuRecipe recipe = MenuDatabase.Fried;
    private Renderer itemRenderer;

    /// <summary>생닭은 아직 메뉴가 정해지지 않았고, 양념대에서 최종 메뉴가 결정된다.</summary>
    public MenuRecipe Recipe => recipe;

    public bool ReadyToPack => state == FoodState.Cooked && (!recipe.needsSauce || sauced);

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
