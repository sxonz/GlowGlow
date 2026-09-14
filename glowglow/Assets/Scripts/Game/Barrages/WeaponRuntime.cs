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

    public bool TryUpgrade(WeaponUpgradeDefinition upgrade) => Upgrades.TryUpgrade(upgrade, Definition);
    public void ResetCooldown() { readyAt = 0; CooldownDuration = 0; }
    public void ApplyNetworkCooldown(float remaining, float duration)
    {
        CooldownDuration = Mathf.Max(0, duration);
        readyAt = Time.time + Mathf.Clamp(remaining, 0, CooldownDuration);
    }
    public void ReduceCooldown(float seconds) => readyAt = Mathf.Max(Time.time,readyAt-Mathf.Max(0,seconds));
    public ProjectileBase Fire(PlayerCombatant owner, Vector2 position, Vector2 direction)
        => Fire(owner, position, direction, position + direction.normalized * Stats.Range);

    public ProjectileBase Fire(PlayerCombatant owner, Vector2 position, Vector2 direction, Vector2 aimPosition)
    {
        if (CooldownRemaining > 0 || owner != null && (owner.IsOverdriving || owner.IsMeteorDiving)) return null;
        var stats = Stats;
        var effects = Upgrades.Effects;
        var projectile = Definition.steps != null && Definition.steps.Length > 0
            ? BarrageSequence.Fire(owner, position, direction, aimPosition, stats, Definition.steps, effects, this)
            : ProjectileBase.Spawn(Definition.projectilePrefab, owner, position, direction, stats, effects);
        CooldownDuration = stats.Cooldown;
        readyAt = Time.time + CooldownDuration;
        return projectile;
    }
}
