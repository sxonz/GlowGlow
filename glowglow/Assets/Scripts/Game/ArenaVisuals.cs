using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One floor draw replaces the separate grid and border glow renderers.</summary>
public sealed class ArenaVisuals : MonoBehaviour
{
    private static readonly Dictionary<int, ArenaVisuals> arenas = new();
    private Material floorMaterial;
    private SpriteRenderer floor;
    private readonly List<LineRenderer> classicLines = new();
    public Rect Bounds { get; private set; }
    public CombatVfx Effects { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => arenas.Clear();
    public static bool TryGet(Scene scene, out ArenaVisuals arena) => arenas.TryGetValue(scene.handle, out arena) && arena != null;

    public static ArenaVisuals Install(MatchController match)
    {
        if (TryGet(match.gameObject.scene, out var existing)) return existing;
        var arena = match.gameObject.AddComponent<ArenaVisuals>();
        arena.Build();
        return arena;
    }
    private void Build()
    {
        Bounds = new Rect(-8.7f, -4.65f, 17.4f, 9.3f);
        RuntimeShapes.AddArenaGlow(gameObject.scene);
        var lines = new List<LineRenderer>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            root.GetComponentsInChildren(false, lines);
            foreach (var line in lines)
            {
                if (line.name == "Arena Border" && line.positionCount >= 4)
                {
                    Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        Vector2 point = line.useWorldSpace ? line.GetPosition(i) : line.transform.TransformPoint(line.GetPosition(i));
                        min = Vector2.Min(min, point); max = Vector2.Max(max, point);
                    }
                    Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                    if (line.enabled) classicLines.Add(line);
                }
                else if ((line.name == "Grid Line" || line.name.StartsWith("Border Glow ")) && line.enabled) classicLines.Add(line);
            }
        }
        floor = new GameObject("Neon Arena Floor", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        floor.transform.SetParent(transform, false);
        floor.transform.position = Bounds.center;
        floor.sprite = RuntimeShapes.Square;
        floor.transform.localScale = new Vector3(Bounds.width / floor.sprite.bounds.size.x, Bounds.height / floor.sprite.bounds.size.y, 1);
        floor.sortingOrder = -20;
        floorMaterial = new Material(Resources.Load<Shader>("NeonArena"));
        floor.sharedMaterial = floorMaterial;
        Effects = gameObject.AddComponent<CombatVfx>();
        Effects.Initialize(Bounds);
        arenas[gameObject.scene.handle] = this;
        GraphicsTheme.Changed += ApplyTheme;
        ApplyTheme();
    }
    private void ApplyTheme()
    {
        // Editor preview scenes can be closed without MonoBehaviour lifecycle callbacks.
        if (this == null || floor == null) { GraphicsTheme.Changed -= ApplyTheme; return; }
        floor.enabled = !GraphicsTheme.IsClassic;
        foreach (var line in classicLines) if (line != null) line.enabled = GraphicsTheme.IsClassic;
        if (GraphicsTheme.IsClassic) Effects.Clear();
    }
    private void OnDestroy()
    {
        GraphicsTheme.Changed -= ApplyTheme;
        arenas.Remove(gameObject.scene.handle);
        if (floorMaterial != null)
        {
            if (Application.isPlaying) Destroy(floorMaterial); else DestroyImmediate(floorMaterial);
        }
    }
}
