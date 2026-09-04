using UnityEngine;
using UnityEngine.InputSystem;

public sealed class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
{
    public enum ControlScheme { MouseAndKeyboard, KeyboardTwo }

    [SerializeField] private ControlScheme scheme;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform aimTarget;

    public void Configure(ControlScheme value, Camera camera, Transform target)
    {
        scheme = value;
        worldCamera = camera;
        aimTarget = target;
    }

    public PlayerCommand ReadCommand(Vector2 worldPosition)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return default;

        PlayerCommand command = default;
        if (scheme == ControlScheme.MouseAndKeyboard)
        {
            command.Move = ReadAxis(keyboard.aKey, keyboard.dKey, keyboard.sKey, keyboard.wKey);
            if (Mouse.current != null && worldCamera != null)
            {
                Vector3 mouse = Mouse.current.position.ReadValue();
                mouse.z = -worldCamera.transform.position.z;
                command.Aim = ((Vector2)worldCamera.ScreenToWorldPoint(mouse) - worldPosition).normalized;
                command.Fire = Mouse.current.leftButton.isPressed;
            }
            command.DashPressed = keyboard.spaceKey.wasPressedThisFrame;
            command.SelectedSlot = ReadSlot(keyboard);
        }
        else
        {
            command.Move = ReadAxis(keyboard.leftArrowKey, keyboard.rightArrowKey, keyboard.downArrowKey, keyboard.upArrowKey);
            command.Aim = aimTarget == null ? Vector2.left : ((Vector2)aimTarget.position - worldPosition).normalized;
            command.Fire = keyboard.rightCtrlKey.isPressed || keyboard.numpad0Key.isPressed;
            command.DashPressed = keyboard.rightShiftKey.wasPressedThisFrame;
        }

        if (command.Move.sqrMagnitude > 1f) command.Move.Normalize();
        if (command.Aim.sqrMagnitude < .01f) command.Aim = Vector2.right;
        return command;
    }

    private static Vector2 ReadAxis(KeyControl left, KeyControl right, KeyControl down, KeyControl up)
    {
        return new Vector2((right.isPressed ? 1 : 0) - (left.isPressed ? 1 : 0),
            (up.isPressed ? 1 : 0) - (down.isPressed ? 1 : 0));
    }

    private static int ReadSlot(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame) return 0;
        if (keyboard.digit2Key.wasPressedThisFrame) return 1;
        if (keyboard.digit3Key.wasPressedThisFrame) return 2;
        if (keyboard.digit4Key.wasPressedThisFrame) return 3;
        if (keyboard.digit5Key.wasPressedThisFrame) return 4;
        return -1;
    }
}
