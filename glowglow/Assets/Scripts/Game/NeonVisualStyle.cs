using UnityEngine;

/// <summary>Shared local artwork. The wire carries only a four-bit style, never decorative geometry.</summary>
public static class NeonVisualStyle
{
    public const int MaxPrismTrails = 48;
    public const byte BulletMode = 4, PrismMode = 5, BlastMode = 6, WarningMode = 7,
        PlanetMode = 8, SunMode = 9;
    private static readonly Material[] materials = new Material[10];
    private static Material ribbon, glow;
    public static Material Glow => glow != null ? glow : glow = GraphicsTheme.IsClassic
        ? new Material(BarrageVisibility.SpriteShader) : Make("NeonImpact");
    internal static void RefreshTheme()
    {
        // Preserve material identity: network style detection must not depend on the local theme.
        if (glow != null) glow.shader = GraphicsTheme.IsClassic
            ? BarrageVisibility.SpriteShader : Resources.Load<Shader>("NeonImpact");
    }
    public static Material Ribbon => ribbon != null ? ribbon : ribbon = Make("NeonRibbon");
    public static Material Get(byte mode)
    {
        if (materials[mode] != null) return materials[mode];
        var material = Make(mode >= PlanetMode ? "SolarBody" : "NeonCore");
        if (mode >= PlanetMode) material.SetFloat("_Sun", mode == SunMode ? 1 : 0);
        else material.SetFloat("_Style", mode == 2 ? 2 : mode == PrismMode ? 1 : mode == BlastMode ? 3 : mode == WarningMode ? 4 : 0);
        return materials[mode] = material;
    }
    private static Material Make(string shader) => new Material(Resources.Load<Shader>(shader))
        { name = "GlowGlow " + shader, hideFlags = HideFlags.HideAndDontSave };

    public static byte Identify(Material material)
    {
        for (byte i = 2; i < materials.Length; i++)
            if (materials[i] != null && material == materials[i]) return i;
        return 0;
    }

    // All prism paths use the same bounded trail, including reconstructed guest paths.
    public static void ConfigureTrail(TrailRenderer trail, Color color, float diameter, int layer, int order)
    {
        trail.sharedMaterial = Ribbon;
        trail.time = .2f;
        trail.minVertexDistance = .09f;
        trail.numCornerVertices = 0;
        trail.numCapVertices = 0;
        trail.textureMode = LineTextureMode.Stretch;
        trail.startWidth = Mathf.Min(.23f, diameter * .75f);
        trail.endWidth = 0;
        color.a *= .62f;
        trail.startColor = color;
        trail.endColor = new Color(.65f, .15f, 1, 0);
        trail.sortingLayerID = layer;
        trail.sortingOrder = order - 1;
        trail.autodestruct = false;
        trail.generateLightingData = false;
        trail.emitting = true;
    }
}
