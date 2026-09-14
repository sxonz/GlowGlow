using UnityEngine;

[CreateAssetMenu(menuName = "GlowGlow/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    public string displayName = "Basic Shot";
    public WeaponRarity rarity = WeaponRarity.Common;
    public string RarityLabel => rarity.Label();
    public Color RarityColor => rarity.Tint();
    public string RarityTag => $"<color=#{ColorUtility.ToHtmlStringRGB(RarityColor)}>{RarityLabel}</color>";
    [Tooltip("Empty keeps the legacy projectile. Steps share the firing time and snapshot aim position.")]
    public BarrageStep[] steps = System.Array.Empty<BarrageStep>();
    public Sprite icon;
    [Tooltip("Prefab with a ProjectileBase subclass. Empty uses the default Bullet.")]
    public ProjectileBase projectilePrefab;
    [Min(.05f)] public float cooldown = .22f;
    [Min(1f)] public float projectileSpeed = 12f;
    [Min(.1f)] public float projectileLifetime = 3f;
    public bool unlimitedLifetime;
    [Min(.02f)] public float projectileRadius = .13f;
    public Color color = new Color(1f, .15f, .8f, 1f);
    [Min(.1f)] public float range = 12f;

    public WeaponStats ResolveStats(WeaponUpgradeState upgrades)
    {
        var stats = new WeaponStats(cooldown, projectileSpeed, unlimitedLifetime ? float.PositiveInfinity : projectileLifetime, projectileRadius, range, color);
        return upgrades != null ? upgrades.Apply(stats) : stats;
    }
}
