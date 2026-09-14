using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "GlowGlow/Barrage Upgrade")]
public sealed class BarrageUpgradeDefinition : WeaponUpgradeDefinition
{
    public WeaponDefinition targetWeapon;
    // Unity serializes enums as 32-bit values. Keep old assets readable while extending runtime flags.
    [FormerlySerializedAs("effect"), SerializeField] private int effectLow;
    [SerializeField] private int effectHigh;
    public WeaponEffects effect
    {
        get => (WeaponEffects)((long)(uint)effectLow | ((long)(uint)effectHigh << 32));
        set { effectLow=unchecked((int)(long)value); effectHigh=unchecked((int)((long)value >> 32)); }
    }
    public override WeaponEffects Effects => effect;
    public override bool CanApplyTo(WeaponDefinition weapon) => weapon != null && weapon == targetWeapon;
    public override WeaponStats ModifyStats(WeaponStats stats, int level)
        => level > 0 ? ApplyStats(stats, effect) : stats;

    public static WeaponStats ApplyStats(WeaponStats stats, WeaponEffects effects)
    {
        float speed = stats.Speed, radius = stats.Radius, cooldown = stats.Cooldown;
        if (effects.Has(WeaponEffects.BasicSpeed)) speed *= 1.2f;
        if (effects.Has(WeaponEffects.SpreadSpeed)) speed *= 1.25f;
        if (effects.Has(WeaponEffects.OctoRapid)) { speed *= 1.25f; cooldown *= .85f; }
        if (effects.Has(WeaponEffects.BasicSize)) radius *= 1.2f;
        if (effects.Has(WeaponEffects.BoomerangSize)) radius *= 1.15f;
        if (effects.Has(WeaponEffects.BasicCooldown | WeaponEffects.BouncerCooldown)) cooldown *= .9f;
        if (effects.Has(WeaponEffects.LaserCooldown | WeaponEffects.PulseCooldown | WeaponEffects.PrismCooldown)) cooldown *= .85f;
        return new WeaponStats(cooldown, speed, stats.Lifetime, radius, stats.Range, stats.Color);
    }
}
