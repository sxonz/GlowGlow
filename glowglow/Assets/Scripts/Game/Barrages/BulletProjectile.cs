using UnityEngine;

// Retains the existing Unity script identity and legacy spawn entry point.
public sealed class BulletProjectile : Bullet
{
    public static BulletProjectile Spawn(PlayerCombatant source, Vector2 position, Vector2 direction, WeaponDefinition weapon)
        => (BulletProjectile)ProjectileBase.Spawn(null, source, position, direction, weapon.ResolveStats(null));
}
