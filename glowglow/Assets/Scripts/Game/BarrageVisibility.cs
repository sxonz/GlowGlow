using UnityEngine;

/// <summary>Local rendering preference; gameplay colors and network samples stay unchanged.</summary>
public static class BarrageVisibility
{
    public const string SaveKey = "OwnBarrageOpacity.v1";
    public static readonly int OwnerProperty = Shader.PropertyToID("_BarrageOwner");
    private static readonly int LocalOwnerProperty = Shader.PropertyToID("_LocalBarrageOwner");
    private static readonly int OpacityProperty = Shader.PropertyToID("_OwnBarrageOpacity");
    private static MaterialPropertyBlock properties;
    private static Material spriteMaterial;
    private static Shader spriteShader, defaultSpriteShader;
    public static Shader SpriteShader => spriteShader != null ? spriteShader : spriteShader = Resources.Load<Shader>("ArenaClippedSprite");
    public static Material SpriteMaterial => spriteMaterial != null ? spriteMaterial :
        spriteMaterial = new Material(SpriteShader) { name = "Barrage Sprite", hideFlags = HideFlags.HideAndDontSave };
    public static float Opacity { get; private set; } = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { SetLocalOwner(1); Load(); }

    public static void Load() => Apply(PlayerPrefs.GetFloat(SaveKey, 1));
    public static void SetLocalOwner(int playerIndex) => Shader.SetGlobalFloat(LocalOwnerProperty, Mathf.Clamp(playerIndex, 1, 2));
    public static void Select(float opacity)
    {
        Apply(opacity);
        PlayerPrefs.SetFloat(SaveKey, Opacity);
        // The settings window saves once on close, rather than writing to disk during every drag.
    }
    private static void Apply(float opacity)
    {
        Opacity = float.IsNaN(opacity) ? 1 : Mathf.Clamp01(opacity);
        Shader.SetGlobalFloat(OpacityProperty, Opacity);
    }
    public static byte OwnerTag(PlayerCombatant owner) => owner == null ? (byte)0 : (byte)Mathf.Clamp(owner.PlayerIndex, 1, 2);
    public static Material SupportedMaterial(Material material)
    {
        if (defaultSpriteShader == null) defaultSpriteShader = Shader.Find("Sprites/Default");
        return material == null || material.shader == defaultSpriteShader ? SpriteMaterial : material;
    }

    public static void ApplyOwner(Renderer renderer, byte owner)
    {
        var current = renderer.sharedMaterial;
        var supported = SupportedMaterial(current);
        if (supported != current) renderer.sharedMaterial = supported;
        properties ??= new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        if (properties.GetFloat(OwnerProperty) == owner) return;
        properties.SetFloat(OwnerProperty, owner);
        renderer.SetPropertyBlock(properties);
    }
}
