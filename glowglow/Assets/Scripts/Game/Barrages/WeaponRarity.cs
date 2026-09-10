using UnityEngine;

public enum WeaponRarity { Common = 0, Rare = 1, Unique = 2, Legendary = 3 }

public static class WeaponRarityDisplay
{
    public static string Label(this WeaponRarity rarity) => rarity switch
    {
        WeaponRarity.Rare => "레어",
        WeaponRarity.Unique => "유니크",
        WeaponRarity.Legendary => "레전더리",
        _ => "커먼"
    };

    public static Color Tint(this WeaponRarity rarity) => rarity switch
    {
        WeaponRarity.Rare => new Color(0.2f, .5f, 1f),
        WeaponRarity.Unique => new Color(.7f, .3f, 1f),
        WeaponRarity.Legendary => new Color(1f, .5f, .1f),
        _ => Color.white
    };
}
