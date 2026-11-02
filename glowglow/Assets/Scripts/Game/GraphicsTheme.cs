using System;
using UnityEngine;

public enum GraphicsThemeKind { Classic, Neon }

/// <summary>Local presentation preference. Never serialized into gameplay or network state.</summary>
public static class GraphicsTheme
{
    public const string SaveKey = "GraphicsTheme.v1";
    private static readonly int ClassicProperty = Shader.PropertyToID("_GlowGlowClassic");
    public static GraphicsThemeKind Current { get; private set; } = GraphicsThemeKind.Neon;
    public static bool IsClassic => Current == GraphicsThemeKind.Classic;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Changed = null; Load(); }

    public static void Load()
    {
        Apply(PlayerPrefs.GetInt(SaveKey, (int)GraphicsThemeKind.Neon) == 0
            ? GraphicsThemeKind.Classic : GraphicsThemeKind.Neon);
    }

    public static void Select(GraphicsThemeKind theme)
    {
        if (theme != GraphicsThemeKind.Classic && theme != GraphicsThemeKind.Neon) return;
        Apply(theme);
        PlayerPrefs.SetInt(SaveKey, (int)theme);
        PlayerPrefs.Save();
    }

    private static void Apply(GraphicsThemeKind theme)
    {
        Current = theme;
        Shader.SetGlobalFloat(ClassicProperty, IsClassic ? 1 : 0);
        NeonVisualStyle.RefreshTheme();
        Changed?.Invoke();
    }
}
