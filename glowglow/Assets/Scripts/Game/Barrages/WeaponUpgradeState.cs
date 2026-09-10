using System;
using System.Collections.Generic;

/// <summary>Per-weapon session progress. Effects run in first-acquired order.</summary>
public sealed class WeaponUpgradeState
{
    private readonly Dictionary<WeaponUpgradeDefinition, int> levels = new();
    private readonly List<WeaponUpgradeDefinition> order = new();
    public event Action Changed;
    public int Count => order.Count;
    public WeaponEffects Effects
    {
        get
        {
            WeaponEffects effects = WeaponEffects.None;
            foreach (var upgrade in order) if (upgrade != null) effects |= upgrade.Effects;
            return effects;
        }
    }

    public int GetLevel(WeaponUpgradeDefinition upgrade)
        => upgrade != null && levels.TryGetValue(upgrade, out int level) ? level : 0;

    internal bool TryUpgrade(WeaponUpgradeDefinition upgrade, WeaponDefinition weapon)
    {
        if (upgrade == null || !upgrade.CanApplyTo(weapon)) return false;
        int level = GetLevel(upgrade);
        if (level >= upgrade.maxLevel) return false;
        if (level == 0) order.Add(upgrade);
        levels[upgrade] = level + 1;
        Changed?.Invoke();
        return true;
    }

    public bool Remove(WeaponUpgradeDefinition upgrade)
    {
        if (upgrade == null || !levels.Remove(upgrade)) return false;
        order.Remove(upgrade);
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        if (order.Count == 0) return;
        levels.Clear();
        order.Clear();
        Changed?.Invoke();
    }

    public WeaponStats Apply(WeaponStats baseStats)
    {
        var stats = baseStats;
        foreach (var upgrade in order)
            if (upgrade != null) stats = upgrade.ModifyStats(stats, levels[upgrade]);
        return stats;
    }
}
