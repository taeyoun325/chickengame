using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>내가 조종하는 캐릭터의 조작. 1인칭 한 명분이라 키보드와 게임패드를
/// 나눌 필요가 없고, 잡히는 대로 둘 다 읽는다.</summary>
[RequireComponent(typeof(PlayerMotor))]
public sealed class LocalPlayerInput : MonoBehaviour
{
    private static readonly Key[] ShopKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

    private PlayerMotor motor;
    private PlayerInteraction interaction;
    private int shopSlot;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        interaction = GetComponent<PlayerInteraction>();
    }

    private void Update()
    {
        if (GameFlow.Instance != null && !GameFlow.Instance.IsPlaying)
        {
            motor.SetInput(Vector2.zero);
            return;
        }

        motor.SetInput(FirstPersonView.ToViewSpace(ReadMove()));

        if (Jumped())
        {
            motor.Jump();
        }

        if (interaction == null)
        {
            return;
        }

        if (Interacted())
        {
            interaction.Interact();
        }

        if (Dropped())
        {
            interaction.RequestDrop();
        }

        if (SauceSwitched())
        {
            interaction.RequestSauceSwitch();
        }

        ReadShopInput();
    }

    private static Vector2 ReadMove()
    {
        Vector2 move = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            move = move.normalized;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null && move.sqrMagnitude < 0.01f)
        {
            move = pad.leftStick.ReadValue();
        }

        return Vector2.ClampMagnitude(move, 1f);
    }

    private static bool Interacted()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.eKey.wasPressedThisFrame)
               || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private static bool Dropped()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.fKey.wasPressedThisFrame)
               || (pad != null && pad.buttonWest.wasPressedThisFrame);
    }

    private static bool SauceSwitched()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.qKey.wasPressedThisFrame)
               || (pad != null && pad.buttonNorth.wasPressedThisFrame);
    }

    private static bool Jumped()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
               || (pad != null && pad.leftShoulder.wasPressedThisFrame);
    }

    /// <summary>키보드는 숫자키로 바로 사고, 게임패드는 슬롯을 넘기며 고른다.</summary>
    private void ReadShopInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            for (int slot = 0; slot < ShopKeys.Length; slot++)
            {
                if (keyboard[ShopKeys[slot]].wasPressedThisFrame)
                {
                    interaction.RequestUpgrade(slot);
                    return;
                }
            }
        }

        Gamepad pad = Gamepad.current;
        if (pad == null)
        {
            return;
        }

        if (pad.dpad.right.wasPressedThisFrame)
        {
            shopSlot = (shopSlot + 1) % ShopKeys.Length;
            if (RestaurantGame.Instance != null)
            {
                RestaurantGame.Instance.ShowMessage($"업그레이드 슬롯 {shopSlot + 1} 선택 (B 로 구매)");
            }
        }

        if (pad.buttonEast.wasPressedThisFrame)
        {
            interaction.RequestUpgrade(shopSlot);
        }
    }
}
