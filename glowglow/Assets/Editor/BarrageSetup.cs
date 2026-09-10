using System;
using System.IO;
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
        }, 5);
        overdrive.color = new Color(.7f, 1f, .25f, 1);
        catalog.cards[5].description = "최대 5초간 직선 이동을 유지할수록 가속하고 관성이 커집니다. 보호막 3과 회전 가시를 얻으며 보호막 소진 또는 시간 종료 시 해제됩니다. 쿨타임 10초.";
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
        }, 4);
        pulse.range = 3;
        pulse.color = new Color(.3f, .9f, 1f, 1f);
        catalog.cards[4].description = "주변 적을 펄스 범위 끝까지 밀어내며 밀려난 거리에 따른 관성을 적용합니다. 기본 피해는 없으며 0.3초 동안 이동 속도를 75%로 낮춥니다. 범위 3, 쿨타임 2초.";
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
        Configure(basic, "Basic Shot", .26f, new[] { Bullet(0, 0, 1) }, 0);
        basic.projectileSpeed = 11;
        Configure(catalog.cards[1].weapon, "Laser Shot", 1.5f, new[]
        {
            new BarrageStep { shape = BarrageShape.Line, followMuzzle = true, dealsDamage = false, duration = 1, startSize = .1f, peakSize = 2, growTime = 1 },
            new BarrageStep { shape = BarrageShape.Box, followMuzzle = true, delay = 1, dimensions = new Vector2(20, .3f), duration = .5f, flash = true }
        }, 1);
        catalog.cards[1].weapon.range = 20;
        Configure(catalog.cards[2].weapon, "Spread Shot", 2f, new[]
        {
            Bullet(0, 0, 1), Bullet(.1f, 10, .8f), Bullet(.1f, -10, .8f), Bullet(.2f, 20, .5f), Bullet(.2f, -20, .5f)
        }, 2);
        foreach (var step in catalog.cards[2].weapon.steps) step.fireFromCurrentMuzzle = true;
        var bomb = new BarrageStep[6];
        bomb[0] = new BarrageStep { shape = BarrageShape.Circle, position = BarragePosition.Cursor,
            dimensions = Vector2.one * 3, dealsDamage = false, opacity = .25f, duration = 1 };
        bomb[1] = new BarrageStep { shape = BarrageShape.Box, position = BarragePosition.Cursor, dimensions = Vector2.one * 3,
            delay = 1, duration = .8f, startSize = 1, peakSize = 1.2f, endSize = 0, growTime = .3f, shrinkTime = .5f,
            rotationDegrees = 360 };
        for (int i = 0; i < 4; i++)
        {
            bomb[i + 2] = Bullet(1, 0, 1);
            bomb[i + 2].position = BarragePosition.Cursor;
            bomb[i + 2].offsetDegrees = new Vector2(i * 90, (i + 1) * 90);
            bomb[i + 2].speedMultiplier = new Vector2(.8f, 1.2f);
        }
        Configure(catalog.cards[3].weapon, "Bomb", 3f, bomb, 3);
        catalog.cards[0].description = "작은 구체를 총구에서 발사합니다. 기본 탄속 11, 발사 간격 0.26초.";
        catalog.cards[1].description = "총구를 따라가는 예고선 1초 후 레이저를 0.5초간 발사하며 뒤로 반동을 받습니다. 예고와 발사 중 감속하고 초당 최대 30도로 조준합니다. 종료 후 커서로 빠르게 회전합니다. 쿨타임 1.5초.";
        catalog.cards[2].description = "정면 1발과 ±10도·±20도의 작은 탄환을 0.1초 간격으로 발사합니다. 총 5발, 쿨타임 2초.";
        catalog.cards[3].description = "지정 위치를 1초간 예고한 뒤 사각 폭발과 4방향 무작위 파편을 생성합니다. 쿨타임 3초.";
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static BarrageStep Bullet(float delay, float angle, float size) => new BarrageStep
    {
        delay = delay, offsetDegrees = new Vector2(angle, angle), startSize = size
    };

    private static void Configure(WeaponDefinition weapon, string name, float cooldown, BarrageStep[] steps, int icon)
    {
        weapon.displayName = name;
        weapon.cooldown = cooldown;
        weapon.projectileSpeed = 12;
        weapon.projectileRadius = .13f;
        weapon.steps = steps;
        weapon.icon = MakeIcon(name.Replace(" ", ""), icon);
        EditorUtility.SetDirty(weapon);
    }

    private static Sprite MakeIcon(string name, int kind)
    {
        const int size = 64;
        const string folder = "Assets/UI/BarrageIcons";
        Directory.CreateDirectory(folder);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x - 31.5f, y - 31.5f);
            bool ink = kind switch
            {
                0 => p.magnitude < 12 || (Mathf.Abs(p.y) < 2 && p.x < -16 && p.x > -27),
                1 => Mathf.Abs(p.y) < 4 && Mathf.Abs(p.x) < 27 || Mathf.Abs(p.y) < 12 && Mathf.Abs(p.x + 17) < 2,
                2 => (p - new Vector2(13, 0)).magnitude < 7 || (p - new Vector2(0, 15)).magnitude < 5 ||
                     (p - new Vector2(0, -15)).magnitude < 5 || (p - new Vector2(-15, 25)).magnitude < 3 || (p - new Vector2(-15, -25)).magnitude < 3,
                4 => (p.y > 0 && p.y < 26 && p.x > -10 + p.y * .45f && p.x < 5 + p.y * .45f) ||
                     (p.y <= 0 && p.y > -26 && p.x > -5 + p.y * .45f && p.x < 10 + p.y * .45f),
                5 => p.magnitude < 10 || p.magnitude > 15 &&
                     p.magnitude < 22 + 6 * Mathf.Cos(Mathf.Atan2(p.y, p.x) * 6),
                _ => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) < 10 ||
                     Mathf.Abs(Mathf.Abs(p.x) - Mathf.Abs(p.y)) < 3 && p.magnitude > 19 && p.magnitude < 32
            };
            texture.SetPixel(x, y, ink ? Color.white : Color.clear);
        }
        texture.Apply();
        string path = folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = size;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
