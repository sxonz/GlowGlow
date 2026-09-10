using System;

[Flags]
public enum WeaponEffects
{
    None = 0,
    BasicSpeed = 1 << 0, BasicSize = 1 << 1, BasicCooldown = 1 << 2,
    LaserQuickWarning = 1 << 3, LaserFreeWarning = 1 << 4, LaserCooldown = 1 << 5,
    SpreadExtra = 1 << 6, SpreadInterceptor = 1 << 7, SpreadSpeed = 1 << 8,
    BombChain = 1 << 9, BombFragments = 1 << 10, BombQuickWarning = 1 << 11,
    PulseDash = 1 << 12, PulseCooldown = 1 << 13, PulsePull = 1 << 14,
    OverdriveAcceleration = 1 << 15, OverdriveHandling = 1 << 16, OverdriveShield = 1 << 17,
    BouncerLifetime = 1 << 18, BouncerRandom = 1 << 19, BouncerCooldown = 1 << 20,
    OrbDouble = 1 << 21, OrbSpeed = 1 << 22, OrbLifetime = 1 << 23
}

public static class WeaponEffectExtensions
{
    public static bool Has(this WeaponEffects effects, WeaponEffects value) => (effects & value) != 0;
}
