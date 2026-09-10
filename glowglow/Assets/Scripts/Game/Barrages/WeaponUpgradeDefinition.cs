using UnityEngine;

/// <summary>Subclass to define an upgrade. Assets hold configuration, never player progress.</summary>
public abstract class WeaponUpgradeDefinition : ScriptableObject
{
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    [Tooltip("Visual shard position only; never a prerequisite or selection order.")]
    [Range(0, 2)] public int displaySlot;
    [Min(1)] public int maxLevel = 1;
    public virtual WeaponEffects Effects => WeaponEffects.None;

    public virtual bool CanApplyTo(WeaponDefinition weapon) => weapon != null;

    /// <summary>Transform stats for the total level without mutating assets or runtime state.</summary>
    public abstract WeaponStats ModifyStats(WeaponStats stats, int level);
}
