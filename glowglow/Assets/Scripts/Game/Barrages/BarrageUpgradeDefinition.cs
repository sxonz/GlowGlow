using UnityEngine;

[CreateAssetMenu(menuName = "GlowGlow/Barrage Upgrade")]
public sealed class BarrageUpgradeDefinition : WeaponUpgradeDefinition
{
    public WeaponDefinition targetWeapon;
    public WeaponEffects effect;
    public override WeaponEffects Effects => effect;
    public override bool CanApplyTo(WeaponDefinition weapon) => weapon != null && weapon == targetWeapon;
    public override WeaponStats ModifyStats(WeaponStats stats, int level)
        => level > 0 ? ApplyStats(stats, effect) : stats;

    public static WeaponStats ApplyStats(WeaponStats stats, WeaponEffects effects)
    {
        float speed = stats.Speed, radius = stats.Radius, cooldown = stats.Cooldown;
        if (effects.Has(WeaponEffects.BasicSpeed)) speed *= 1.2f;
        if (effects.Has(WeaponEffects.SpreadSpeed)) speed *= 1.25f;
        if (effects.Has(WeaponEffects.BasicSize)) radius *= 1.2f;
        if (effects.Has(WeaponEffects.BasicCooldown | WeaponEffects.BouncerCooldown)) cooldown *= .9f;
        if (effects.Has(WeaponEffects.LaserCooldown | WeaponEffects.PulseCooldown)) cooldown *= .85f;
        return new WeaponStats(cooldown, speed, stats.Lifetime, radius, stats.Range, stats.Color);
    }
}
