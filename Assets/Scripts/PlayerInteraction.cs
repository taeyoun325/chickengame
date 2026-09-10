using UnityEngine;

public sealed class PlayerInteraction : MonoBehaviour
{
    public const float InteractRange = 2.4f;
    private const float PickupRange = 2f;

    private Transform holdPoint;
    private FoodItem heldFood;
    private GameObject extinguisher;

    public FoodItem HeldFood => heldFood;
    public bool CarryingExtinguisher => extinguisher != null;
    public bool HandsFree => heldFood == null && extinguisher == null;

    private void Awake()
    {
        GameObject holdObject = new GameObject("Hold Point");
        holdObject.transform.SetParent(transform, false);
        holdObject.transform.localPosition = new Vector3(0f, 1.35f, 0.75f);
        holdPoint = holdObject.transform;
        WorldRegistry.Register(this);
    }

    private void OnDestroy()
    {
        WorldRegistry.Unregister(this);
    }

    /// <summary>F 키/게임패드 X. 네트워크에서는 호스트에 요청한다.</summary>
    public void RequestDrop()
    {
        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.RequestDrop();
        }
        else
        {
            DropEverything();
        }
    }

    public void RequestSauceSwitch()
    {
        Station station = FindNearbyStation();
        if (station == null || station.stationType != StationType.Sauce)
        {
            return;
        }

        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.RequestSauceCycle(station.Index);
        }
        else if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.CycleSauce(station);
        }
    }

    /// <summary>업그레이드 데스크 앞에 있을 때만 구매가 된다.</summary>
    public void RequestUpgrade(int slot)
    {
        Station station = FindNearbyStation();
        if (station == null || station.stationType != StationType.Upgrade || RestaurantGame.Instance == null)
        {
            return;
        }

        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.RequestUpgrade(slot);
        }
        else
        {
            RestaurantGame.Instance.BuyUpgrade(slot);
        }
    }

    public void SetHeldFood(FoodItem food)
    {
        heldFood = food;
        if (food.RestingStation != null)
        {
            food.RestingStation.Clear();
        }

        NetworkAttach.Parent(food, holdPoint);
        food.transform.localPosition = Vector3.zero;
        food.transform.localRotation = Quaternion.identity;
        food.SetHeld(true);
    }

    /// <summary>쓰레기통에 버렸을 때처럼 들고 있던 음식이 사라진 경우.</summary>
    public void ClearHeldFood()
    {
        heldFood = null;
    }

    /// <summary>들고 있던 음식을 스테이션 위에 올려둔다.</summary>
    public FoodItem ReleaseHeldFoodTo(Station station)
    {
        if (heldFood == null)
        {
            return null;
        }

        FoodItem released = heldFood;
        heldFood = null;
        NetworkAttach.Unparent(released);
        station.Place(released);
        released.SetHeld(false);
        return released;
    }

    /// <summary>손이 비어 있으면 근처에 떨어진 음식을 줍는다.</summary>
    public void TryPickUpNearbyFood()
    {
        if (!HandsFree)
        {
            return;
        }

        FoodItem nearbyFood = FindNearbyFood();
        if (nearbyFood == null)
        {
            return;
        }

        SetHeldFood(nearbyFood);
        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage($"{nearbyFood.Describe()}을 들었습니다");
        }
    }

    public void TakeExtinguisher()
    {
        if (extinguisher != null)
        {
            return;
        }

        extinguisher = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        extinguisher.name = "Extinguisher";
        extinguisher.transform.SetParent(holdPoint, false);
        extinguisher.transform.localPosition = Vector3.zero;
        extinguisher.transform.localScale = new Vector3(0.3f, 0.45f, 0.3f);
        Destroy(extinguisher.GetComponent<Collider>());
        extinguisher.GetComponent<Renderer>().material.color = new Color(0.85f, 0.1f, 0.1f);
    }

    public void ReturnExtinguisher()
    {
        if (extinguisher != null)
        {
            Destroy(extinguisher);
            extinguisher = null;
        }
    }

    /// <summary>미끄러지거나 F 키를 누르면 들고 있던 것을 바닥에 떨어뜨린다.</summary>
    public void DropEverything()
    {
        ReturnExtinguisher();

        if (heldFood == null)
        {
            return;
        }

        FoodItem dropped = heldFood;
        heldFood = null;
        NetworkAttach.Unparent(dropped);
        Vector3 spot = transform.position + transform.forward * 0.8f;
        spot.y = 0.35f;
        dropped.transform.position = spot;

        // 떨어진 치킨은 실제로 굴러가도록 물리를 붙인다.
        Rigidbody body = dropped.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = dropped.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.6f;
        }

        dropped.SetHeld(false);
        body.linearVelocity = transform.forward * 2f;
        dropped.MarkDirty();

        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage("떨어뜨렸습니다! 바닥에 닿은 치킨은 못 씁니다");
        }
    }

    public void Interact()
    {
        if (RestaurantGame.Instance == null)
        {
            return;
        }

        if (extinguisher != null && RestaurantGame.Instance.TryExtinguishNearby(transform.position))
        {
            return;
        }

        Station station = FindNearbyStation();

        // 접속한 손님이면 호스트에 요청하고, 호스트/싱글이면 바로 처리한다.
        if (KitchenNetwork.Online && !KitchenNetwork.Instance.IsServer)
        {
            if (station != null)
            {
                KitchenNetwork.Instance.RequestInteract(station.Index);
            }
            else
            {
                KitchenNetwork.Instance.RequestPickup();
            }

            return;
        }

        if (station != null)
        {
            RestaurantGame.Instance.InteractWithStation(this, station);
            return;
        }

        TryPickUpNearbyFood();
    }

    public Station FindNearbyStation()
    {
        return WorldRegistry.NearestStation(transform.position, InteractRange);
    }

    private FoodItem FindNearbyFood()
    {
        return WorldRegistry.NearestLooseFood(transform.position, PickupRange);
    }
}
