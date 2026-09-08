using System;
using UnityEngine;

public sealed class WeaponRuntime
{
    public WeaponDefinition Definition { get; }
    public WeaponUpgradeState Upgrades { get; } = new();
    public WeaponStats Stats => Definition.ResolveStats(Upgrades);
    private float readyAt;
    public float CooldownDuration { get; private set; }
    public float CooldownRemaining => Mathf.Max(0, readyAt - Time.time);
    public float CooldownFraction => CooldownDuration > 0 ? Mathf.Clamp01(CooldownRemaining / CooldownDuration) : 0;

    public WeaponRuntime(WeaponDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        Definition = definition;
    }

    public bool TryUpgrade(WeaponUpgrade upgrade) => Upgrades.TryUpgrade(upgrade, Definition.maxUpgradeLevel);
    public ProjectileBase Fire(PlayerCombatant owner, Vector2 position, Vector2 direction)
        => Fire(owner, position, direction, position + direction.normalized * Stats.Range);

    public ProjectileBase Fire(PlayerCombatant owner, Vector2 position, Vector2 direction, Vector2 aimPosition)
    {
        if (CooldownRemaining > 0 || owner != null && owner.IsOverdriving) return null;
        var stats = Stats;
        var projectile = Definition.steps != null && Definition.steps.Length > 0
            ? BarrageSequence.Fire(owner, position, direction, aimPosition, stats, Definition.steps)
            : ProjectileBase.Spawn(Definition.projectilePrefab, owner, position, direction, stats);
        CooldownDuration = stats.Cooldown;
        readyAt = Time.time + CooldownDuration;
        return projectile;
    }
}
