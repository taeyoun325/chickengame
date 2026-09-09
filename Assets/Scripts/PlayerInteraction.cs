using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInteraction : MonoBehaviour
{
    private static readonly Key[] ShopKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

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
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            Interact();
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            DropEverything();
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            SwitchSauce();
        }

        CheckUpgradeShopKeys();
    }

    public void SetHeldFood(FoodItem food)
    {
        heldFood = food;
        food.transform.SetParent(holdPoint, false);
        food.transform.localPosition = Vector3.zero;
        food.transform.localRotation = Quaternion.identity;
        food.SetHeld(true);
    }

    /// <summary>쓰레기통에 버렸을 때처럼 들고 있던 음식이 사라진 경우.</summary>
    public void ClearHeldFood()
    {
        heldFood = null;
    }

    public FoodItem ReleaseHeldFood(Transform target)
    {
        if (heldFood == null)
        {
            return null;
        }

        FoodItem released = heldFood;
        heldFood = null;
        released.transform.SetParent(target, false);
        released.transform.localPosition = Vector3.up * 1.2f;
        released.transform.localRotation = Quaternion.identity;
        released.SetHeld(false);
        return released;
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
        dropped.transform.SetParent(null);
        Vector3 spot = transform.position + transform.forward * 0.8f;
        spot.y = 0.35f;
        dropped.transform.position = spot;
        dropped.SetHeld(false);
        dropped.MarkDirty();

        if (RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.ShowMessage("떨어뜨렸습니다! 바닥에 닿은 치킨은 못 씁니다");
        }
    }

    private void SwitchSauce()
    {
        Station station = FindNearbyStation();
        if (station != null && station.stationType == StationType.Sauce && RestaurantGame.Instance != null)
        {
            RestaurantGame.Instance.CycleSauce(station);
        }
    }

    /// <summary>업그레이드 스테이션 앞에서만 숫자 키가 구매로 이어진다.</summary>
    private void CheckUpgradeShopKeys()
    {
        Station station = null;
        for (int slot = 0; slot < ShopKeys.Length; slot++)
        {
            if (!Keyboard.current[ShopKeys[slot]].wasPressedThisFrame)
            {
                continue;
            }

            station ??= FindNearbyStation();
            if (station == null || station.stationType != StationType.Upgrade || RestaurantGame.Instance == null)
            {
                return;
            }

            RestaurantGame.Instance.BuyUpgrade(slot);
            return;
        }
    }

    private void Interact()
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
        if (station != null)
        {
            RestaurantGame.Instance.InteractWithStation(this, station);
            return;
        }

        if (HandsFree)
        {
            FoodItem nearbyFood = FindNearbyFood();
            if (nearbyFood != null)
            {
                SetHeldFood(nearbyFood);
                RestaurantGame.Instance.ShowMessage($"{nearbyFood.Describe()}을 들었습니다");
            }
        }
    }

    private Station FindNearbyStation()
    {
        Station closest = null;
        float closestDistance = 2.4f;
        foreach (Station station in FindObjectsByType<Station>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(transform.position, station.transform.position);
            if (distance < closestDistance)
            {
                closest = station;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private FoodItem FindNearbyFood()
    {
        FoodItem closest = null;
        float closestDistance = 2f;
        foreach (FoodItem food in FindObjectsByType<FoodItem>(FindObjectsSortMode.None))
        {
            if (food.state == FoodState.Frying)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, food.transform.position);
            if (distance < closestDistance)
            {
                closest = food;
                closestDistance = distance;
            }
        }

        return closest;
    }
}
