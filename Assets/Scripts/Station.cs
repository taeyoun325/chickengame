using UnityEngine;

public enum StationType
{
    Fridge,
    Fryer,
    Sauce,
    Packing,
    Checkout,
    Delivery,
    Upgrade,
    Trash,
    Extinguisher
}

public sealed class Station : MonoBehaviour
{
    public StationType stationType;

    /// <summary>네트워크 RPC 가 스테이션을 가리킬 때 쓰는 고정 인덱스.</summary>
    public int Index { get; set; } = -1;

    /// <summary>튀김기 등에 올려둔 음식. 자식 트랜스폼 대신 참조로 들고 있다.
    /// 네트워크에서는 음식이 NetworkObject 라 스테이션의 자식이 될 수 없기 때문이다.</summary>
    public FoodItem StoredFood { get; private set; }

    private void Awake()
    {
        WorldRegistry.Register(this);
    }

    private void OnDestroy()
    {
        WorldRegistry.Unregister(this);
    }

    public void Place(FoodItem food)
    {
        StoredFood = food;
        if (food != null)
        {
            food.transform.position = transform.position + Vector3.up * 1.2f;
            food.transform.rotation = Quaternion.identity;
            food.RestingStation = this;
        }
    }

    public void Clear()
    {
        if (StoredFood != null)
        {
            StoredFood.RestingStation = null;
        }

        StoredFood = null;
    }

    private void Update()
    {
        // 음식이 사라졌거나 다른 곳으로 옮겨졌으면 자리를 비운다.
        if (StoredFood == null || StoredFood.RestingStation != this)
        {
            StoredFood = null;
        }
    }
}
