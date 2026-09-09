using UnityEngine;

public enum StationType
{
    Fridge,
    Fryer,
    Sauce,
    Packing,
    Checkout,
    Delivery
}

public sealed class Station : MonoBehaviour
{
    public StationType stationType;

    /// <summary>양념대는 어떤 소스를 바르는지에 따라 최종 메뉴가 결정된다.</summary>
    public MenuKind sauceKind = MenuKind.Seasoned;

    public FoodItem StoredFood
    {
        get
        {
            for (int index = 0; index < transform.childCount; index++)
            {
                FoodItem food = transform.GetChild(index).GetComponent<FoodItem>();
                if (food != null)
                {
                    return food;
                }
            }

            return null;
        }
    }
}
