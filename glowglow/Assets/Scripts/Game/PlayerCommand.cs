using UnityEngine;

public struct PlayerCommand
{
    public Vector2 Move;
    public Vector2 Aim;
    public Vector2 AimPosition;
    public bool Fire;
    public bool DashPressed;
    public int SelectedSlot;
}

public interface IPlayerInputSource
{
    PlayerCommand ReadCommand(Vector2 worldPosition);
}
