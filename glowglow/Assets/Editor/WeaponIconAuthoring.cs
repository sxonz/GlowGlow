using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Authored vector glyphs baked into one shared, colored UI atlas. Editor only.</summary>
public static class WeaponIconAuthoring
{
    public const string AtlasPath = "Assets/UI/BarrageIcons/WeaponIcons.png";
    public const int CellSize = 192;
    public const int AtlasSize = 1024;
    public static readonly string[] Names = { "Basic Shot", "Laser Shot", "Spread Shot", "Bomb",
        "Electric Pulse", "Overdrive", "Bouncer", "Orbit Orb", "Meteor Dive", "Octo Shot",
        "Boomerang", "Prism Shot", "4-axis", "Solar System", "Mine", "Firefly Convergence",
        "Twin Satellites", "Blossom Burst", "Constellation Ignition" };
    private static readonly string[] Files = { "Pulse Shot", "Basic Barrage 02", "Basic Barrage 03",
        "Basic Barrage 04", "Basic Barrage 05", "Basic Barrage 06", "Basic Barrage 07", "Basic Barrage 08",
        "Meteor Dive", "Octo Shot", "Boomerang", "Prism Shot", "4-axis", "Solar System", "Mine",
        "Firefly Convergence", "Twin Satellites", "Blossom Burst", "Constellation Ignition" };

    public static Sprite Get(string name)
    {
        if (!File.Exists(AtlasPath)) Bake();
        var sprite = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().FirstOrDefault(s => s.name == name);
        return sprite != null ? sprite : throw new InvalidOperationException("Missing weapon icon: " + name);
    }

    [MenuItem("GlowGlow/Bake Weapon Icons")]
    public static void Bake()
    {
        var pixels = new Color32[AtlasSize * AtlasSize];
        var weapons = Files.Select(n => AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Settings/" + n + ".asset")).ToArray();
        for (int i = 0; i < Names.Length; i++)
        {
            var glyph = Draw(i);
            Color tint = weapons[i] != null ? weapons[i].color : new Color(.5f, .8f, 1);
            int left = i % 5 * CellSize, bottom = (3 - i / 5) * CellSize;
            for (int y = 0; y < CellSize; y++)
            for (int x = 0; x < CellSize; x++)
            {
                Vector2 p = new((x + .5f) * 128 / CellSize - 64, (y + .5f) * 128 / CellSize - 64);
                float distance = glyph.Distance(p);
                float core = Mathf.Clamp01(.5f - distance * CellSize / 128);
                float outside = Mathf.Max(0, distance);
                float halo = .32f * Mathf.Exp(-outside * outside / 7) + .075f * Mathf.Exp(-outside * outside / 48);
                float alpha = core + halo * (1 - core);
                // Transparent gutter prevents neighboring glyphs bleeding into bilinear samples.
                if (alpha < 1f / 255) continue;
                Color color = Color.Lerp(tint, Color.white, .88f * Mathf.Clamp01(-distance / 1.3f));
                color.a = alpha;
                pixels[(bottom + y) * AtlasSize + left + x] = color;
            }
        }
        var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(AtlasPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellSize;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = AtlasSize;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        var rects = new SpriteRect[Names.Length];
        for (int i = 0; i < rects.Length; i++) rects[i] = new SpriteRect
        {
            name = Names[i], spriteID = previous.TryGetValue(Names[i], out var id) ? id : GUID.Generate(),
            rect = new Rect(i % 5 * CellSize, (3 - i / 5) * CellSize, CellSize, CellSize),
            alignment = SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
        };
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(s => s.name);
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] == null) continue;
            weapons[i].icon = sprites[Names[i]];
            EditorUtility.SetDirty(weapons[i]);
        }
        AssetDatabase.SaveAssets();
    }

    public static void BakeBatch()
    {
        try { Bake(); Debug.Log("WEAPON_ICONS_BAKED"); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    // Coordinates are authored in a 128-unit square. Primary contours stay within
    // +/-44, leaving room for the baked halo and a fully transparent atlas gutter.
    private static Glyph Draw(int kind)
    {
        var g = new Glyph();
        switch (kind)
        {
            case 0: // A luminous projectile with separated speed marks.
                g.Ring(17, 0, 15, 4); g.Disc(17, 0, 8);
                g.Line(-40, 0, -6, 0, 4); g.Line(-28, 15, -7, 15, 3); g.Line(-28, -15, -7, -15, 3);
                break;
            case 1: // Focusing aperture and a long beam.
                g.Path(4, -29, -25, -36, -25, -36, 25, -29, 25);
                g.Line(-24, -15, -24, 15, 5); g.Line(-17, 0, 42, 0, 5);
                g.Line(-10, 9, 33, 9, 2); g.Line(-10, -9, 33, -9, 2); g.Star(36, 0, 11, 3);
                break;
            case 2: // Five visibly separated shots in a fan.
                g.Disc(-32, 0, 4);
                for (int j = -2; j <= 2; j++)
                {
                    Vector2 v = Unit(j * 23);
                    Vector2 start = new(-29, 0);
                    g.Line(start + v * 13, start + v * 32, 2.5f);
                    g.Line(start + v * 44, start + v * 60, 5);
                }
                break;
            case 3: // Angular detonation and flying fragments.
                g.Polygon(4, -17, 4, -7, 9, -6, 21, 5, 12, 18, 15, 14, 2, 24, -7,
                    10, -10, 5, -24, -5, -13, -19, -17, -15, -4, -25, 3);
                g.Disc(0, 0, 5);
                for (int j = 0; j < 4; j++) { Vector2 v = Unit(45 + j * 90); g.Line(v * 33, v * 45, 4); }
                break;
            case 4: // A bolt inside expanding pulse arcs.
                g.Polygon(3.8f, 4, 32, -17, 2, -1, 2, -8, -30, 18, 7, 2, 7);
                g.Arc(0, 0, 36, -65, 65, 3); g.Arc(0, 0, 36, 115, 245, 3);
                break;
            case 5: // Armored boost: shield and forward chevrons.
                g.Polygon(4, 0, 39, 28, 27, 25, -12, 0, -37, -25, -12, -28, 27);
                g.Path(4, -12, 10, 0, 22, 12, 10); g.Path(4, -12, -8, 0, 4, 12, -8);
                g.Line(-39, -10, -35, 17, 2.5f); g.Line(39, -10, 35, 17, 2.5f);
                break;
            case 6: // A sharp bounce against a vertical wall.
                g.Line(36, -36, 36, 36, 5);
                g.Path(3.5f, -35, -27, 27, -3, -24, 30);
                g.Path(4, -8, 31, -24, 30, -18, 15); g.Disc(-35, -27, 6); g.Star(27, -3, 7, 2.5f);
                break;
            case 7: // One large satellite around a small central body.
                g.Arc(0, 0, 33, 70, 365, 3); g.Ring(0, 0, 10, 3); g.Disc(0, 0, 4);
                g.Ring(24, 23, 10, 4); g.Disc(24, 23, 4);
                break;
            case 8: // Diagonal meteor with three long, parallel trails.
                g.Ring(-17, -18, 16, 4); g.Disc(-20, -20, 6);
                g.Line(-7, 3, 25, 39, 4); g.Line(7, -7, 39, 29, 4); g.Line(10, 12, 31, 36, 2.5f);
                g.Arc(-17, -18, 24, 190, 285, 2.5f);
                break;
            case 9: // Eight independent radial projectiles.
                g.Polygon(3, 0, 9, 9, 0, 0, -9, -9, 0);
                for (int j = 0; j < 8; j++) { Vector2 v = Unit(j * 45); g.Line(v * 24, v * 42, 5); }
                break;
            case 10: // Swept returning blade, with a separate return arc.
                g.Polygon(4, -28, -13, 0, 30, 29, -13, 11, -6, 0, 9, -11, -6);
                g.Arc(0, 0, 40, 190, 340, 2.5f); g.Path(3, 25, -34, 38, -14, 24, -19);
                break;
            case 11: // A prism refracting a single ray into three.
                g.Polygon(4, -20, -27, 10, 28, 31, -27);
                g.Line(-44, 8, -3, 8, 3.5f); g.Line(13, 8, 43, 27, 3);
                g.Line(16, 0, 44, 0, 3); g.Line(21, -10, 43, -25, 3);
                g.Line(-6, -20, 7, 3, 2);
                break;
            case 12: // Four linked orbital stages, distinct from a single orbit.
                g.Path(2.5f, -32, -25, -12, 10, 14, -6, 34, 29);
                g.Ring(-32, -25, 10, 3); g.Ring(-12, 10, 9, 3); g.Ring(14, -6, 8, 3); g.Ring(34, 29, 7, 3);
                g.Disc(-32, -25, 3); g.Disc(-12, 10, 3); g.Disc(14, -6, 2.5f); g.Disc(34, 29, 2);
                g.Arc(-32, -25, 17, 95, 220, 1.8f); g.Arc(14, -6, 15, -95, 25, 1.8f);
                break;
            case 13: // Solar disc, tilted orbit and three planets.
                g.Ring(0, 0, 12, 3); g.Disc(0, 0, 6);
                for (int j = 0; j < 8; j++) { Vector2 v = Unit(j * 45); g.Line(v * 18, v * 22, 2); }
                g.Ellipse(0, 0, 44, 29, 20, 2); g.Disc(39, 18, 5.5f); g.Disc(-34, -25, 4.5f); g.Disc(-16, 24, 4);
                break;
            case 14: // A circular pressure plate with a diamond detonator.
                for (int j = 0; j < 4; j++) g.Arc(0, 0, 36, j * 90 + 12, j * 90 + 78, 4);
                g.Ring(0, 0, 25, 2.5f); g.Polygon(4, 0, 13, 13, 0, 0, -13, -13, 0); g.Disc(0, 0, 3);
                for (int j = 0; j < 4; j++) { Vector2 v = Unit(j * 90); g.Line(v * 33, v * 42, 4); }
                break;
            case 15: // Individual lights flowing inward, not a radial explosion.
                g.Polygon(3, 0, 7, 7, 0, 0, -7, -7, 0);
                for (int j = 0; j < 5; j++)
                {
                    float angle = 90 + j * 72;
                    Vector2 v = Unit(angle), n = new(-v.y, v.x), light = v * 36;
                    g.Disc(light.x, light.y, 4.5f);
                    g.Curve(light - v * 9, v * 22 + n * 10, v * 13 + n * 7, v * 13, 2.5f);
                    g.Line(light + n * 7, light + n * 10 + v * 5, 1.8f);
                }
                break;
            case 16: // Two equal opposing satellites with a circular orbit.
                g.Arc(0, 0, 33, 65, 195, 3); g.Arc(0, 0, 33, 245, 375, 3);
                g.Ring(-23.33f, -23.33f, 10, 4); g.Disc(-23.33f, -23.33f, 4);
                g.Ring(23.33f, 23.33f, 10, 4); g.Disc(23.33f, 23.33f, 4);
                g.Star(0, 0, 8, 2.5f);
                break;
            case 17: // Eight open petals around a luminous heart.
                for (int j = 0; j < 8; j++)
                {
                    Vector2 v = Unit(22.5f + j * 45), n = new(-v.y, v.x);
                    g.Curve(v * 11, v * 22 + n * 11, v * 34 + n * 6, v * 42, 3);
                    g.Curve(v * 42, v * 34 - n * 6, v * 22 - n * 11, v * 11, 3);
                }
                g.Disc(0, 0, 5);
                break;
            case 18: // A rising star route; bright nodes dominate fine connections.
                g.Path(2.5f, -39, -27, -25, -3, -10, -12, 3, 14, 22, 7, 37, 32);
                g.Star(-39, -27, 7, 2.5f); g.Disc(-25, -3, 3.5f); g.Disc(-10, -12, 3.5f);
                g.Star(3, 14, 8, 2.5f); g.Disc(22, 7, 3.5f); g.Star(37, 32, 9, 3);
                g.Curve(new(-36, -38), new(-6, -30), new(28, -17), new(40, 4), 1.8f);
                break;
        }
        return g;
    }

    private static Vector2 Unit(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    private readonly struct Stroke
    {
        private readonly Vector2 a, delta;
        private readonly float inverseLength, radius;
        public Stroke(Vector2 from, Vector2 to, float width)
        { a = from; delta = to - from; inverseLength = delta.sqrMagnitude > 0 ? 1 / delta.sqrMagnitude : 0; radius = width * .5f; }
        public float Distance(Vector2 p) => (p - a - delta * Mathf.Clamp01(Vector2.Dot(p - a, delta) * inverseLength)).magnitude - radius;
    }
    private sealed class Glyph
    {
        private readonly List<Stroke> strokes = new();
        public float Distance(Vector2 p)
        {
            float distance = float.PositiveInfinity;
            foreach (var stroke in strokes) distance = Mathf.Min(distance, stroke.Distance(p));
            return distance;
        }
        public void Line(Vector2 a, Vector2 b, float width) => strokes.Add(new Stroke(a, b, width));
        public void Line(float x0, float y0, float x1, float y1, float width) => Line(new(x0, y0), new(x1, y1), width);
        public void Disc(float x, float y, float radius) => Line(x, y, x, y, radius * 2);
        public void Ring(float x, float y, float radius, float width) => Arc(x, y, radius, 0, 360, width);
        public void Arc(float x, float y, float radius, float start, float end, float width)
        {
            int steps = Mathf.CeilToInt((end - start) / 6);
            Vector2 center = new(x, y), previous = center + Unit(start) * radius;
            for (int i = 1; i <= steps; i++) { Vector2 next = center + Unit(Mathf.Lerp(start, end, (float)i / steps)) * radius; Line(previous, next, width); previous = next; }
        }
        public void Ellipse(float x, float y, float rx, float ry, float angle, float width)
        {
            Vector2 axis = Unit(angle), normal = new(-axis.y, axis.x), center = new(x, y), previous = center + axis * rx;
            for (int i = 1; i <= 60; i++) { Vector2 v = Unit(i * 6), next = center + axis * (rx * v.x) + normal * (ry * v.y); Line(previous, next, width); previous = next; }
        }
        public void Path(float width, params float[] coordinates)
        { for (int i = 2; i < coordinates.Length; i += 2) Line(coordinates[i - 2], coordinates[i - 1], coordinates[i], coordinates[i + 1], width); }
        public void Polygon(float width, params float[] coordinates)
        { Path(width, coordinates); Line(coordinates[^2], coordinates[^1], coordinates[0], coordinates[1], width); }
        public void Star(float x, float y, float radius, float width)
        { Line(x - radius, y, x + radius, y, width); Line(x, y - radius, x, y + radius, width); Disc(x, y, width * .8f); }
        public void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float width)
        {
            Vector2 previous = a;
            for (int i = 1; i <= 16; i++) { float t = i / 16f, u = 1 - t; Vector2 next = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d; Line(previous, next, width); previous = next; }
        }
    }
}
