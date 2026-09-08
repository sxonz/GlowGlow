using System;
using UnityEngine;

public enum WeaponUpgrade { FireRate, Speed, Lifetime, Size, Range }

/// <summary>Progress owned by one player, never by a shared asset.</summary>
[Serializable]
public sealed class WeaponUpgradeState
{
    [SerializeField, Min(0)] private int fireRateLevel;
    [SerializeField, Min(0)] private int speedLevel;
    [SerializeField, Min(0)] private int lifetimeLevel;
    [SerializeField, Min(0)] private int sizeLevel;
    [SerializeField, Min(0)] private int rangeLevel;

    public int GetLevel(WeaponUpgrade upgrade) => upgrade switch
    {
        WeaponUpgrade.FireRate => fireRateLevel,
        WeaponUpgrade.Speed => speedLevel,
        WeaponUpgrade.Lifetime => lifetimeLevel,
        WeaponUpgrade.Size => sizeLevel,
        WeaponUpgrade.Range => rangeLevel,
        _ => throw new ArgumentOutOfRangeException(nameof(upgrade))
    };

    public bool TryUpgrade(WeaponUpgrade upgrade, int maxLevel)
    {
        if (GetLevel(upgrade) >= Mathf.Max(0, maxLevel)) return false;
        switch (upgrade)
        {
            case WeaponUpgrade.FireRate: fireRateLevel++; break;
            case WeaponUpgrade.Speed: speedLevel++; break;
            case WeaponUpgrade.Lifetime: lifetimeLevel++; break;
            case WeaponUpgrade.Size: sizeLevel++; break;
            case WeaponUpgrade.Range: rangeLevel++; break;
        }
        return true;
    }
}
