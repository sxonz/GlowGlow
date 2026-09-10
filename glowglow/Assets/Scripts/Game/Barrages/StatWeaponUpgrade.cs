using UnityEngine;

public enum WeaponUpgradeStat { Cooldown, Size }

[CreateAssetMenu(menuName = "GlowGlow/Stat Weapon Upgrade")]
public sealed class StatWeaponUpgrade : WeaponUpgradeDefinition
{
    public WeaponUpgradeStat stat;
    [Range(0f, .9f)] public float amountPerLevel = .15f;

    public override bool CanApplyTo(WeaponDefinition weapon)
    {
        if (weapon == null) return false;
        if (stat == WeaponUpgradeStat.Cooldown) return true;
        if (weapon.steps == null || weapon.steps.Length == 0)
            return weapon.projectilePrefab == null || weapon.projectilePrefab is Bullet;
        foreach (var step in weapon.steps)
            if (step != null && (step.shape == BarrageShape.Bullet || step.shape == BarrageShape.Bouncer ||
                step.shape == BarrageShape.OrbitOrb || step.shape == BarrageShape.Circle)) return true;
        return false;
    }

    public override WeaponStats ModifyStats(WeaponStats stats, int level)
    {
        float amount = Mathf.Clamp(amountPerLevel, 0f, .9f);
        level = Mathf.Max(0, level);
        return new WeaponStats(stat == WeaponUpgradeStat.Cooldown ? stats.Cooldown * Mathf.Pow(1f - amount, level) : stats.Cooldown,
            stats.Speed, stats.Lifetime,
            stat == WeaponUpgradeStat.Size ? stats.Radius * (1f + amount * level) : stats.Radius,
            stats.Range, stats.Color);
    }
}
