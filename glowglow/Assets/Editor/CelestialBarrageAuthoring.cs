using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Bakes icons and installs base cards. No upgrade assets are created.</summary>
public static class CelestialBarrageAuthoring
{
    public static readonly string[] Names =
        { "Firefly Convergence", "Twin Satellites", "Blossom Burst", "Constellation Ignition" };
    private static readonly string[] Ids =
        { "firefly-convergence", "twin-satellites", "blossom-burst", "constellation-ignition" };
    private static readonly WeaponRarity[] Rarities =
        { WeaponRarity.Common, WeaponRarity.Epic, WeaponRarity.Rare, WeaponRarity.Legendary };
    private static readonly string[] Descriptions =
    {
        "화면 밖에서 반딧불이 9마리가 일렁이며 자신을 향해 날아와 닿으면 흡수됩니다. 이동 경로의 적에게 피해를 입힙니다. 쿨타임 8.5초.",
        "지정 위치에 서로 마주 보는 두 위성을 소환합니다. 일정한 속도로 원을 그리며 각각 부채꼴 탄막을 6번 발사합니다. 쿨타임 8초.",
        "꽃봉오리 3개를 발사합니다. 봉오리는 서서히 멈춘 뒤 피어나며 각각 8방향으로 꽃잎 탄환을 퍼뜨립니다. 쿨타임 5초.",
        "총구에서 빛이 출발해 바다뱀자리, 에리다누스자리, 용자리 중 하나를 순서대로 그립니다. 바로 다음 구간만 예고하며, 완성된 형상의 외곽선도 피해를 입힌 뒤 별빛과 함께 사라집니다. 쿨타임 6초."
    };

    [MenuItem("GlowGlow/Install Celestial Barrages")]
    public static void Install()
    {
        ConstellationFigureAuthoring.Bake();
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        if (catalog == null) throw new InvalidOperationException("Deck Catalog is missing.");
        var cards = new List<DeckCatalog.Card>(catalog.cards);
        float[] cooldowns = { 8.5f, 8, 5, 6 };
        float[] speeds = { 4.2f, 3.6f, 4.6f, 1 };
        float[] radii = { .14f, .23f, .24f, .09f };
        float[] durations = { FireflyLight.Lifetime, 7, 4.5f, ConstellationIgnition.Lifetime };
        Color[] colors = { new(.7f, 1, .3f), new(.35f, .8f, 1), new(1, .38f, .65f), new(.65f, .55f, 1) };
        for (int i = 0; i < Names.Length; i++)
        {
            string path = "Assets/Settings/" + Names[i] + ".asset";
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
            {
                weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(weapon, path);
            }
            weapon.displayName = Names[i];
            weapon.damage = BarrageDamage.DefaultFor(Names[i]);
            weapon.rarity = Rarities[i];
            weapon.cooldown = cooldowns[i];
            weapon.projectileSpeed = speeds[i];
            weapon.projectileRadius = radii[i];
            weapon.projectileLifetime = durations[i];
            weapon.unlimitedLifetime = false;
            weapon.projectilePrefab = null;
            weapon.range = 11;
            weapon.color = colors[i];
            weapon.steps = new[] { new BarrageStep
            {
                shape = (BarrageShape)((int)BarrageShape.FireflyConvergence + i),
                position = i >= 2 ? BarragePosition.Gun : BarragePosition.Cursor,
                duration = durations[i]
            }};
            weapon.icon = BakeIcon(i);
            var card = cards.Find(c => c != null && c.id == Ids[i]);
            if (card == null) { card = new DeckCatalog.Card { id = Ids[i] }; cards.Add(card); }
            card.weapon = weapon;
            card.description = Descriptions[i];
            EditorUtility.SetDirty(weapon);
        }
        catalog.cards = cards.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    public static void InstallBatch()
    {
        try { Install(); Debug.Log("CELESTIAL_BARRAGES_INSTALLED"); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static Sprite BakeIcon(int kind) => WeaponIconAuthoring.Get(Names[kind]);
}
