using System;

[Flags]
public enum WeaponEffects : long
{
    None = 0,
    BasicSpeed = 1L << 0, BasicSize = 1L << 1, BasicCooldown = 1L << 2,
    LaserQuickWarning = 1L << 3, LaserFreeWarning = 1L << 4, LaserCooldown = 1L << 5,
    SpreadExtra = 1L << 6, SpreadInterceptor = 1L << 7, SpreadSpeed = 1L << 8,
    BombChain = 1L << 9, BombFragments = 1L << 10, BombQuickWarning = 1L << 11,
    PulseDash = 1L << 12, PulseCooldown = 1L << 13, PulsePull = 1L << 14,
    OverdriveAcceleration = 1L << 15, OverdriveHandling = 1L << 16, OverdriveShield = 1L << 17,
    BouncerLifetime = 1L << 18, BouncerRandom = 1L << 19, BouncerCooldown = 1L << 20,
    OrbDouble = 1L << 21, OrbSpeed = 1L << 22, OrbLifetime = 1L << 23,
    MeteorMiniTanks = 1L << 24, MeteorTracking = 1L << 25, MeteorShield = 1L << 26,
    OctoRapid = 1L << 27, OctoCurve = 1L << 28, OctoSplit = 1L << 29,
    BoomerangRefund = 1L << 30, BoomerangSize = 1L << 31, BoomerangBounce = 1L << 32,
    PrismCooldown = 1L << 33, PrismTail = 1L << 34, PrismExplosion = 1L << 35
}

public static class WeaponEffectExtensions
{
    public static bool Has(this WeaponEffects effects, WeaponEffects value) => (effects & value) != 0;
}
