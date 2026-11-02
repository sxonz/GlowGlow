using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local queries performed once per cast, including the isolated deck preview.</summary>
public static class CelestialBarrageGeometry
{
    private static readonly List<GameObject> Roots = new(32);
    private static readonly List<PlayerCombatant> Players = new(4);

    public static Rect ArenaBounds(PlayerCombatant owner)
    {
        if (ArenaVisuals.TryGet(owner.gameObject.scene, out var arena)) return arena.Bounds;
        Vector2 half = owner.ArenaHalfSize;
        return new Rect(-half, half * 2);
    }

    public static Rect ViewBounds(PlayerCombatant owner)
    {
        owner.gameObject.scene.GetRootGameObjects(Roots);
        Rect view = ArenaBounds(owner);
        foreach (var root in Roots)
        {
            var camera = root.GetComponentInChildren<Camera>();
            if (camera == null || !camera.enabled || !camera.orthographic ||
                (camera.cullingMask & (1 << owner.gameObject.layer)) == 0) continue;
            float aspect = camera.targetTexture != null
                ? (float)camera.targetTexture.width / camera.targetTexture.height : camera.aspect;
            var half = new Vector2(camera.orthographicSize * aspect, camera.orthographicSize);
            view = new Rect((Vector2)camera.transform.position - half, half * 2);
            break;
        }
        Roots.Clear();
        return view;
    }

    public static PlayerCombatant FindTarget(PlayerCombatant owner, Vector2 aim)
    {
        owner.gameObject.scene.GetRootGameObjects(Roots);
        PlayerCombatant nearest = null;
        float distance = float.PositiveInfinity;
        foreach (var root in Roots)
        {
            root.GetComponentsInChildren(false, Players);
            foreach (var player in Players)
            {
                if (player == owner || !player.CanSelectWeapon) continue;
                float candidate = ((Vector2)player.transform.position - aim).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = player;
                distance = candidate;
            }
        }
        Players.Clear();
        Roots.Clear();
        return nearest;
    }

    public static void CollectPlayers(PlayerCombatant owner, List<PlayerCombatant> result)
    {
        result.Clear();
        owner.gameObject.scene.GetRootGameObjects(Roots);
        foreach (var root in Roots)
        {
            root.GetComponentsInChildren(false, Players);
            result.AddRange(Players);
        }
        Players.Clear();
        Roots.Clear();
    }

    public static Vector2 Clamp(Vector2 point, Rect bounds, float inset) => new(
        Mathf.Clamp(point.x, bounds.xMin + inset, bounds.xMax - inset),
        Mathf.Clamp(point.y, bounds.yMin + inset, bounds.yMax - inset));

    public static Vector2 Unit(float radians) => new(Mathf.Cos(radians), Mathf.Sin(radians));

    public static Vector2 OutsideEdge(Rect view, float fraction, float margin)
    {
        // Distance around the perimeter avoids clustering on wide screens.
        float distance = Mathf.Repeat(fraction, 1) * (view.width + view.height) * 2;
        if (distance < view.width) return new Vector2(view.xMin + distance, view.yMax + margin);
        distance -= view.width;
        if (distance < view.height) return new Vector2(view.xMax + margin, view.yMax - distance);
        distance -= view.height;
        if (distance < view.width) return new Vector2(view.xMax - distance, view.yMin - margin);
        return new Vector2(view.xMin - margin, view.yMin + distance - view.width);
    }
}
