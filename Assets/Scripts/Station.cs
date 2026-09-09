using UnityEngine;

public enum StationType
{
    Fridge,
    Fryer,
    Packing,
    Checkout
}

public sealed class Station : MonoBehaviour
{
    public StationType stationType;

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
