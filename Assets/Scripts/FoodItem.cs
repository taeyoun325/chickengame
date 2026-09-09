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

    private Renderer itemRenderer;

    private void Awake()
    {
        itemRenderer = GetComponent<Renderer>();
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
            FoodState.Cooked => new Color(0.85f, 0.3f, 0.05f),
            FoodState.Burnt => new Color(0.08f, 0.04f, 0.02f),
            FoodState.Packaged => new Color(0.95f, 0.85f, 0.35f),
            _ => Color.white
        };
    }
}
