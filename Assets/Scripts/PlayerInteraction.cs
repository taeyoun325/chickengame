using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInteraction : MonoBehaviour
{
    private Transform holdPoint;
    private FoodItem heldFood;

    public FoodItem HeldFood => heldFood;

    private void Awake()
    {
        GameObject holdObject = new GameObject("Hold Point");
        holdObject.transform.SetParent(transform, false);
        holdObject.transform.localPosition = new Vector3(0f, 1.35f, 0.75f);
        holdPoint = holdObject.transform;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Interact();
        }
    }

    public void SetHeldFood(FoodItem food)
    {
        heldFood = food;
        food.transform.SetParent(holdPoint, false);
        food.transform.localPosition = Vector3.zero;
        food.transform.localRotation = Quaternion.identity;
        food.SetHeld(true);
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

    private void Interact()
    {
        if (RestaurantGame.Instance == null)
        {
            return;
        }

        Station station = FindNearbyStation();
        if (station != null)
        {
            RestaurantGame.Instance.InteractWithStation(this, station);
            return;
        }

        if (heldFood == null)
        {
            FoodItem nearbyFood = FindNearbyFood();
            if (nearbyFood != null)
            {
                SetHeldFood(nearbyFood);
                RestaurantGame.Instance.ShowMessage("들었습니다");
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
