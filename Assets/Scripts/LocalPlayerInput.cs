using UnityEngine;
using UnityEngine.InputSystem;

public enum InputScheme
{
    KeyboardLeft,
    KeyboardRight,
    Gamepad
}

/// <summary>플레이어 한 명의 조작을 담당한다. 키보드 두 벌과 게임패드를 지원해
/// 한 대의 PC 에서 최대 4명이 함께 할 수 있다.</summary>
[RequireComponent(typeof(PlayerMotor))]
public sealed class LocalPlayerInput : MonoBehaviour
{
    private static readonly Key[] LeftShopKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
    private static readonly Key[] RightShopKeys = { Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };

    [SerializeField] private InputScheme scheme = InputScheme.KeyboardLeft;
    [SerializeField] private int gamepadIndex;

    private PlayerMotor motor;
    private PlayerInteraction interaction;
    private int shopSlot;

    public InputScheme Scheme => scheme;

    public void Configure(InputScheme inputScheme, int padIndex = 0)
    {
        scheme = inputScheme;
        gamepadIndex = padIndex;
    }

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

        Vector2 move = ReadMove();
        if (FirstPersonView.IsSubject(transform))
        {
            move = FirstPersonView.ToViewSpace(move);
        }

        motor.SetInput(move);

        if (interaction == null)
        {
            return;
        }

        if (Pressed(PlayerAction.Interact))
        {
            interaction.Interact();
        }

        if (Pressed(PlayerAction.Drop))
        {
            interaction.RequestDrop();
        }

        if (Pressed(PlayerAction.Sauce))
        {
            interaction.RequestSauceSwitch();
        }

        ReadShopInput();
    }

    private enum PlayerAction
    {
        Interact,
        Drop,
        Sauce,
        ShopNext,
        ShopBuy
    }

    private Vector2 ReadMove()
    {
        switch (scheme)
        {
            case InputScheme.KeyboardLeft:
                return ReadKeys(Key.A, Key.D, Key.S, Key.W, arrows: true);
            case InputScheme.KeyboardRight:
                return ReadKeys(Key.J, Key.L, Key.K, Key.I, arrows: false);
            case InputScheme.Gamepad:
                Gamepad pad = Pad();
                return pad != null ? Vector2.ClampMagnitude(pad.leftStick.ReadValue(), 1f) : Vector2.zero;
            default:
                return Vector2.zero;
        }
    }

    private static Vector2 ReadKeys(Key left, Key right, Key down, Key up, bool arrows)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        Vector2 input = Vector2.zero;
        if (keyboard[left].isPressed || (arrows && keyboard.leftArrowKey.isPressed)) input.x -= 1f;
        if (keyboard[right].isPressed || (arrows && keyboard.rightArrowKey.isPressed)) input.x += 1f;
        if (keyboard[down].isPressed || (arrows && keyboard.downArrowKey.isPressed)) input.y -= 1f;
        if (keyboard[up].isPressed || (arrows && keyboard.upArrowKey.isPressed)) input.y += 1f;
        return input.normalized;
    }

    private bool Pressed(PlayerAction action)
    {
        Keyboard keyboard = Keyboard.current;
        switch (scheme)
        {
            case InputScheme.KeyboardLeft:
                if (keyboard == null) return false;
                return action switch
                {
                    PlayerAction.Interact => keyboard.eKey.wasPressedThisFrame,
                    PlayerAction.Drop => keyboard.fKey.wasPressedThisFrame,
                    PlayerAction.Sauce => keyboard.qKey.wasPressedThisFrame,
                    _ => false
                };
            case InputScheme.KeyboardRight:
                if (keyboard == null) return false;
                return action switch
                {
                    PlayerAction.Interact => keyboard.oKey.wasPressedThisFrame,
                    PlayerAction.Drop => keyboard.pKey.wasPressedThisFrame,
                    PlayerAction.Sauce => keyboard.uKey.wasPressedThisFrame,
                    _ => false
                };
            case InputScheme.Gamepad:
                Gamepad pad = Pad();
                if (pad == null) return false;
                return action switch
                {
                    PlayerAction.Interact => pad.buttonSouth.wasPressedThisFrame,
                    PlayerAction.Drop => pad.buttonWest.wasPressedThisFrame,
                    PlayerAction.Sauce => pad.buttonNorth.wasPressedThisFrame,
                    PlayerAction.ShopNext => pad.dpad.right.wasPressedThisFrame,
                    PlayerAction.ShopBuy => pad.buttonEast.wasPressedThisFrame,
                    _ => false
                };
            default:
                return false;
        }
    }

    /// <summary>키보드는 숫자키로 바로 사고, 게임패드는 슬롯을 넘기며 고른다.</summary>
    private void ReadShopInput()
    {
        if (scheme == InputScheme.Gamepad)
        {
            if (Pressed(PlayerAction.ShopNext))
            {
                shopSlot = (shopSlot + 1) % LeftShopKeys.Length;
                if (RestaurantGame.Instance != null)
                {
                    RestaurantGame.Instance.ShowMessage($"업그레이드 슬롯 {shopSlot + 1} 선택 (B 로 구매)");
                }
            }

            if (Pressed(PlayerAction.ShopBuy))
            {
                interaction.RequestUpgrade(shopSlot);
            }

            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Key[] keys = scheme == InputScheme.KeyboardLeft ? LeftShopKeys : RightShopKeys;
        for (int slot = 0; slot < keys.Length; slot++)
        {
            if (keyboard[keys[slot]].wasPressedThisFrame)
            {
                interaction.RequestUpgrade(slot);
                return;
            }
        }
    }

    private Gamepad Pad()
    {
        var pads = Gamepad.all;
        return gamepadIndex >= 0 && gamepadIndex < pads.Count ? pads[gamepadIndex] : null;
    }
}
