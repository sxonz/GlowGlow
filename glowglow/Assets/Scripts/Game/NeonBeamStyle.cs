using UnityEngine;

/// <summary>Shared beam artwork; reuses the imported square and one shared material.</summary>
public static class NeonBeamStyle
{
    private static Material material;
    public static Material Material => material != null ? material :
        material = new Material(Resources.Load<Shader>("NeonBeam"));

    public static Color BodyColor(Color tint, float age, float remaining)
    {
        float ignition = Mathf.Clamp01(1 - age / .075f);
        var color = Color.Lerp(tint, Color.white, ignition * ignition * .48f);
        // The beam remains readable until its collider is removed.
        color.a = tint.a * Mathf.Lerp(.72f, 1f, Mathf.Clamp01(remaining / .1f));
        return color;
    }
}
