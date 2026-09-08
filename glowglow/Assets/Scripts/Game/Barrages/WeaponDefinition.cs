using UnityEngine;

[CreateAssetMenu(menuName = "GlowGlow/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    public string displayName = "Basic Shot";
    [Tooltip("Empty keeps the legacy projectile. Steps share the firing time and snapshot aim position.")]
    public BarrageStep[] steps = System.Array.Empty<BarrageStep>();
    public Sprite icon;
    [Tooltip("Prefab with a ProjectileBase subclass. Empty uses the default Bullet.")]
    public ProjectileBase projectilePrefab;
    [Min(.05f)] public float cooldown = .22f;
    [Min(1f)] public float projectileSpeed = 12f;
    [Min(.1f)] public float projectileLifetime = 3f;
    [Min(.02f)] public float projectileRadius = .13f;
    public Color color = new Color(1f, .15f, .8f, 1f);
    [Min(.1f)] public float range = 12f;

    [Header("Upgrades")]
    [Min(0)] public int maxUpgradeLevel = 5;
    [Min(0f)] public float bonusPerLevel = .15f;

    public WeaponStats ResolveStats(WeaponUpgradeState upgrades)
    {
        float Multiplier(WeaponUpgrade upgrade) => 1f +
            Mathf.Clamp(upgrades?.GetLevel(upgrade) ?? 0, 0, Mathf.Max(0, maxUpgradeLevel)) * Mathf.Max(0f, bonusPerLevel);
        return new WeaponStats(cooldown / Multiplier(WeaponUpgrade.FireRate),
            projectileSpeed * Multiplier(WeaponUpgrade.Speed),
            projectileLifetime * Multiplier(WeaponUpgrade.Lifetime),
            projectileRadius * Multiplier(WeaponUpgrade.Size),
            range * Multiplier(WeaponUpgrade.Range), color);
    }
}
