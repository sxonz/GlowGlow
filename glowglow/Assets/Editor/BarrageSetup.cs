using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BarrageSetup
{
    [MenuItem("GlowGlow/Install Overdrive")]
    public static void InstallOverdrive()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        var overdrive = catalog.cards[5].weapon;
        Configure(overdrive, "Overdrive", 10f, new[]
        {
            new BarrageStep { shape = BarrageShape.Overdrive, position = BarragePosition.Player, duration = 5 }
        });
        overdrive.color = new Color(.7f, 1f, .25f, 1);
        catalog.cards[5].description = "이동 속도가 빨라지고 보호막 3과 회전 가시를 얻으며 적에게 부딪히면 피해를 입힙니다. 보호막 소진 또는 시간 종료 시 해제됩니다. 쿨타임 10초.";
        EditorUtility.SetDirty(overdrive);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    public static void InstallOverdriveBatch()
    {
        InstallOverdrive();
        Debug.Log("OVERDRIVE_INSTALLED");
        EditorApplication.Exit(0);
    }

    [MenuItem("GlowGlow/Install Electric Pulse")]
    public static void InstallElectricPulse()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        var pulse = catalog.cards[4].weapon;
        Configure(pulse, "Electric Pulse", 2f, new[]
        {
            new BarrageStep { shape = BarrageShape.ElectricPulse, position = BarragePosition.Player,
                duration = .4f, dealsDamage = false, knockbackDistance = .55f, slowMultiplier = .75f, slowDuration = .3f }
        });
        pulse.range = 3;
        pulse.color = new Color(.3f, .9f, 1f, 1f);
        catalog.cards[4].description = "펄스를 발사해 주변 적을 밀어내고 느리게 합니다. 범위 3, 쿨타임 2초.";
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(pulse);
        AssetDatabase.SaveAssets();
    }

    public static void BakePreviewBatch()
    {
        InstallElectricPulse();
        BakePreviewLayoutBatch();
    }

    public static void BakePreviewLayoutBatch()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity", OpenSceneMode.Single);
        var deck = UnityEngine.Object.FindFirstObjectByType<DeckScreen>(FindObjectsInactive.Include);
        deck.RebuildForEditor(AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset"));
        EditorUtility.SetDirty(deck);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PULSE_AND_PREVIEW_BAKED");
        EditorApplication.Exit(0);
    }

    [MenuItem("GlowGlow/Install First Four Barrages")]
    public static void Install()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
        if (catalog == null || catalog.cards.Length < 8) throw new InvalidOperationException("Eight-card catalog required.");
        var basic = catalog.cards[0].weapon;
        Configure(basic, "Basic Shot", .26f, new[] { Bullet(0, 0, 1) });
        basic.projectileSpeed = 11;
        Configure(catalog.cards[1].weapon, "Laser Shot", 2f, new[]
        {
            new BarrageStep { shape = BarrageShape.Line, followMuzzle = true, dealsDamage = false, duration = 1, startSize = .1f, peakSize = 2, growTime = 1 },
            new BarrageStep { shape = BarrageShape.Box, followMuzzle = true, delay = 1, dimensions = new Vector2(20, .3f), duration = .5f, flash = true }
        });
        catalog.cards[1].weapon.range = 20;
        Configure(catalog.cards[2].weapon, "Spread Shot", 2f, new[]
        {
            Bullet(0, 0, 1), Bullet(.1f, 10, .8f), Bullet(.1f, -10, .8f), Bullet(.2f, 20, .5f), Bullet(.2f, -20, .5f)
        });
        foreach (var step in catalog.cards[2].weapon.steps)
        {
            step.fireFromCurrentMuzzle = true;
            step.damage = step.delay == 0 ? 10 : step.delay < .2f ? BarrageDamage.SpreadInner : BarrageDamage.SpreadOuter;
        }
        var bomb = new BarrageStep[6];
        bomb[0] = new BarrageStep { shape = BarrageShape.Circle, position = BarragePosition.Cursor,
            dimensions = Vector2.one * 3, dealsDamage = false, opacity = .25f, duration = 1 };
        bomb[1] = new BarrageStep { shape = BarrageShape.Box, position = BarragePosition.Cursor, dimensions = Vector2.one * 3,
            delay = 1, duration = .8f, startSize = 1, peakSize = 1.2f, endSize = 0, growTime = .3f, shrinkTime = .5f,
            rotationDegrees = 360 };
        for (int i = 0; i < 4; i++)
        {
            bomb[i + 2] = Bullet(1, 0, 1);
            bomb[i + 2].damage = BarrageDamage.BombFragment;
            bomb[i + 2].position = BarragePosition.Cursor;
            bomb[i + 2].offsetDegrees = new Vector2(i * 90, (i + 1) * 90);
            bomb[i + 2].speedMultiplier = new Vector2(.8f, 1.2f);
        }
        Configure(catalog.cards[3].weapon, "Bomb", 3f, bomb);
        catalog.cards[0].description = "작은 구체를 총구에서 발사합니다. 기본 탄속 11, 발사 간격 0.26초.";
        catalog.cards[1].description = "짧은 예고 이후 레이저를 발사합니다. 쿨타임 2초.";
        catalog.cards[2].description = "총 5발의 탄환을 흩뿌립니다., 쿨타임 2초.";
        catalog.cards[3].description = "지정 위치가 1초 뒤 폭발하고 파편이 튑니다. 쿨타임 3초.";
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static BarrageStep Bullet(float delay, float angle, float size) => new BarrageStep
    {
        delay = delay, offsetDegrees = new Vector2(angle, angle), startSize = size
    };

    private static void Configure(WeaponDefinition weapon, string name, float cooldown, BarrageStep[] steps)
    {
        weapon.displayName = name;
        weapon.damage = BarrageDamage.DefaultFor(name);
        weapon.cooldown = cooldown;
        weapon.projectileSpeed = 12;
        weapon.projectileRadius = .13f;
        weapon.steps = steps;
        weapon.icon = WeaponIconAuthoring.Get(name);
        EditorUtility.SetDirty(weapon);
    }

    // Run only in an isolated validation project; leaves the user's open scene untouched.
    public static void ValidateBatch()
    {
        Install();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/BarrageSmoke.unity");
        EditorApplication.EnterPlaymode();
    }
}
