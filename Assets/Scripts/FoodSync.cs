using Unity.Netcode;
using UnityEngine;

/// <summary>치킨의 조리 상태를 클라이언트에도 그대로 보여준다.</summary>
[RequireComponent(typeof(FoodItem))]
public sealed class FoodSync : NetworkBehaviour
{
    private readonly NetworkVariable<byte> netState = new NetworkVariable<byte>();
    private readonly NetworkVariable<byte> netRecipe = new NetworkVariable<byte>();
    private readonly NetworkVariable<bool> netSauced = new NetworkVariable<bool>();
    private readonly NetworkVariable<bool> netDirty = new NetworkVariable<bool>();

    private FoodItem food;
    private byte appliedState = 255;
    private byte appliedRecipe = 255;
    private bool appliedSauced;
    private bool appliedDirty;

    private void Awake()
    {
        food = GetComponent<FoodItem>();
    }

    private void Update()
    {
        if (food == null)
        {
            return;
        }

        if (IsServer)
        {
            PushState();
            return;
        }

        ApplyState();
    }

    private void PushState()
    {
        byte state = (byte)food.state;
        byte recipe = (byte)food.Recipe.kind;
        if (netState.Value != state) netState.Value = state;
        if (netRecipe.Value != recipe) netRecipe.Value = recipe;
        if (netSauced.Value != food.sauced) netSauced.Value = food.sauced;
        if (netDirty.Value != food.dirty) netDirty.Value = food.dirty;
    }

    private void ApplyState()
    {
        if (appliedRecipe != netRecipe.Value || appliedSauced != netSauced.Value)
        {
            appliedRecipe = netRecipe.Value;
            appliedSauced = netSauced.Value;
            food.sauced = netSauced.Value;
            food.SetRecipe(MenuDatabase.Get((MenuKind)netRecipe.Value));
        }

        if (appliedDirty != netDirty.Value)
        {
            appliedDirty = netDirty.Value;
            if (netDirty.Value)
            {
                food.MarkDirty();
            }
        }

        if (appliedState != netState.Value)
        {
            appliedState = netState.Value;
            food.SetState((FoodState)netState.Value);
        }
    }
}
