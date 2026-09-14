using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public sealed class LocalPlayerInput : MonoBehaviour, IPlayerInputSource
{
    public enum ControlScheme { MouseAndKeyboard, KeyboardTwo }

    [SerializeField] private ControlScheme scheme;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform aimTarget;
    private PointerEventData pointerEvent;
    private EventSystem pointerEventSystem;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    public void Configure(ControlScheme value, Camera camera, Transform target)
    {
        scheme = value;
        worldCamera = camera;
        aimTarget = target;
    }

    public PlayerCommand ReadCommand(Vector2 worldPosition)
    {
        var keyboard = Keyboard.current;
        PlayerCommand command = new PlayerCommand { SelectedSlot = -1 };
        if (keyboard == null) return command;

        if (scheme == ControlScheme.MouseAndKeyboard)
        {
            command.Move = ReadAxis(keyboard.aKey, keyboard.dKey, keyboard.sKey, keyboard.wKey);
            if (Mouse.current != null && worldCamera != null)
            {
                Vector3 mouse = Mouse.current.position.ReadValue();
                mouse.z = -worldCamera.transform.position.z;
                command.AimPosition = worldCamera.ScreenToWorldPoint(mouse);
                command.Aim = (command.AimPosition - worldPosition).normalized;
                command.Fire = Mouse.current.leftButton.isPressed && !PointerOverUI();
                command.FirePressed = command.Fire && Mouse.current.leftButton.wasPressedThisFrame;
            }
            command.DashPressed = keyboard.spaceKey.wasPressedThisFrame;
            command.SelectedSlot = ReadSlot(keyboard);
            if (Mouse.current != null && !PointerOverUI())
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                command.WeaponCycle = scroll > 0f ? -1 : scroll < 0f ? 1 : 0;
            }
        }
        else
        {
            command.Move = ReadAxis(keyboard.leftArrowKey, keyboard.rightArrowKey, keyboard.downArrowKey, keyboard.upArrowKey);
            command.Aim = aimTarget == null ? Vector2.left : ((Vector2)aimTarget.position - worldPosition).normalized;
            command.AimPosition = aimTarget == null ? worldPosition + Vector2.left * 5f : (Vector2)aimTarget.position;
            command.Fire = keyboard.rightCtrlKey.isPressed || keyboard.numpad0Key.isPressed;
            command.FirePressed = keyboard.rightCtrlKey.wasPressedThisFrame || keyboard.numpad0Key.wasPressedThisFrame;
            command.DashPressed = keyboard.rightShiftKey.wasPressedThisFrame;
        }

        if (command.Move.sqrMagnitude > 1f) command.Move.Normalize();
        if (command.Aim.sqrMagnitude < .01f) command.Aim = Vector2.right;
        return command;
    }

    private bool PointerOverUI()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        if (pointerEvent == null || pointerEventSystem != EventSystem.current)
        {
            pointerEventSystem = EventSystem.current;
            pointerEvent = new PointerEventData(EventSystem.current);
        }
        pointerEvent.position = Mouse.current.position.ReadValue();
        uiHits.Clear();
        EventSystem.current.RaycastAll(pointerEvent, uiHits);
        return uiHits.Count > 0;
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
        return -1;
    }
}
