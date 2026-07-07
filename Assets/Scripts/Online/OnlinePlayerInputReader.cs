using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class OnlinePlayerInputReader
{
    private bool bombInputWasPressed;

    public void ResetBombState()
    {
        bombInputWasPressed = false;
    }

    public void SetInput(NetworkInput input)
    {
        OnlinePlayerInput playerInput = new OnlinePlayerInput
        {
            Move = ReadMoveInput(),
            PlaceBomb = ReadBombInput()
        };

        input.Set(playerInput);
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 rawInput = Vector2.zero;

        if (Gamepad.current != null)
        {
            rawInput = Gamepad.current.leftStick.ReadValue();
        }
        else if (Keyboard.current != null)
        {
            float h = 0f;
            float v = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            rawInput = new Vector2(h, v);
        }

        Vector3 direction = CameraFollow.GetCameraRelativeDirection(rawInput);
        return new Vector2(direction.x, direction.z);
    }

    private bool ReadBombInput()
    {
        bool bombNow = false;

        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        {
            bombNow = true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
        {
            bombNow = true;
        }

        bool placeBomb = bombNow && !bombInputWasPressed;
        bombInputWasPressed = bombNow;
        return placeBomb;
    }
}
