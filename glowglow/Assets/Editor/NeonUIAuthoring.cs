using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI glyphs and nine-slice chrome are authored and baked offline, like the barrage icons.</summary>
public static class NeonUIAuthoring
{
    public const string AtlasPath = "Assets/Resources/UI/NeonUIAtlas.png";
    private const int Cell = 128, Width = 1024, Height = 512;
    public static readonly string[] Names = { "Panel Fill", "Panel Frame", "Button Fill", "Button Frame", "Card Frame", "Selected Frame", "Rule", "Orbit Seal",
        "Training", "Bot", "Network", "Deck", "Settings", "Return", "Confirm", "Lock", "Close", "Next", "Previous", "Health", "Shield", "Clock", "Reset", "Info", "Star", "Volume", "Opacity", "Campaign", "Chevron", "Diamond", "Status", "Aim" };

    [MenuItem("GlowGlow/Bake UI Theme")]
    public static void Bake()
    {
        Directory.CreateDirectory("Assets/Resources/UI");
        var pixels = new Color32[Width * Height];
        for (int kind = 0; kind < Names.Length; kind++)
        {
            var glyph = Draw(Names[kind]);
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                Vector2 p = new(x + .5f - 64, y + .5f - 64);
                float d;
                bool fill = kind == 0 || kind == 2;
                if (kind < 6)
                {
                    Vector2 a = new(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    float edge = Mathf.Max(Mathf.Max(a.x - 55, a.y - 55), (a.x + a.y - 99) * .7071068f);
                    d = fill ? edge : Mathf.Abs(edge) - (kind == 5 ? 1.45f : .65f);
                    if (kind == 4 || kind == 5)
                    {
                        // Corner markers and an inner track keep selected cards distinct at a glance.
                        float inner = Mathf.Abs(Mathf.Max(a.x - 49, a.y - 49)) - .45f;
                        if (a.x > 35 && a.y > 35) d = Mathf.Min(d, inner);
                    }
                }
                else d = glyph.Distance(p);
                float core = Mathf.Clamp01(.5f - d);
                float outside = Mathf.Max(0,d);
                float alpha = fill ? core : core + (1 - core) * (.34f * Mathf.Exp(-outside * outside / 7) + .06f * Mathf.Exp(-outside * outside / 36));
                if (alpha < 1f / 255) continue;
                int left = kind % 8 * Cell, bottom = (3 - kind / 8) * Cell;
                pixels[(bottom + y) * Width + left + x] = new Color(1,1,1,alpha);
            }
        }
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels); texture.Apply();
        File.WriteAllBytes(AtlasPath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false; importer.isReadable = false;
        importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024; importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var previous = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        var rects = new SpriteRect[Names.Length];
        for (int i = 0; i < rects.Length; i++) rects[i] = new SpriteRect {
            name = Names[i], spriteID = previous.TryGetValue(Names[i], out var id) ? id : GUID.Generate(),
            rect = new Rect(i % 8 * Cell, (3 - i / 8) * Cell + (i==6?56:0), Cell, i==6?16:Cell),
            border = i < 6 ? new Vector4(28,28,28,28) : Vector4.zero,
            alignment = SpriteAlignment.Center, pivot = new Vector2(.5f,.5f) };
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name,r.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
    }

    public static void BakeBatch()
    {
        try { Bake(); Debug.Log("NEON_UI_ATLAS_BAKED"); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private readonly struct Stroke
    {
        private readonly Vector2 a,b;
        private readonly float width,radius;
        public Stroke(Vector2 a, Vector2 b, float width, float radius = 0) { this.a=a; this.b=b; this.width=width; this.radius=radius; }
        public float Distance(Vector2 p)
        {
            if (radius > 0) return Mathf.Abs((p-a).magnitude-radius)-width;
            Vector2 v=b-a;
            return (p-a-v*Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(.001f,v.sqrMagnitude))).magnitude-width;
        }
    }
    private sealed class Glyph
    {
        private readonly List<Stroke> strokes = new();
        public void Line(float x0,float y0,float x1,float y1,float width=1.8f) => strokes.Add(new(new Vector2(x0,y0),new Vector2(x1,y1),width));
        public void Ring(float x,float y,float r,float width=1.6f) => strokes.Add(new(new Vector2(x,y),Vector2.zero,width,r));
        public void Dot(float x,float y,float r=3) => Line(x,y,x,y,r);
        public void Path(params float[] points) { for(int i=0;i+3<points.Length;i+=2) Line(points[i],points[i+1],points[i+2],points[i+3]); }
        public float Distance(Vector2 p) { float d=1000; foreach(var s in strokes) d=Mathf.Min(d,s.Distance(p)); return d; }
    }
    private static Glyph Draw(string name)
    {
        var g=new Glyph();
        switch(name)
        {
            case "Rule": g.Line(-54,0,54,0,3); g.Dot(-54,0,5); g.Dot(54,0,5); break;
            case "Orbit Seal":
                g.Ring(0,0,42,.7f); g.Ring(0,0,30,.9f); g.Ring(0,0,10,1.2f);
                g.Path(-51,0,-16,0); g.Path(16,0,51,0); g.Path(0,-51,0,-16); g.Path(0,16,0,51);
                g.Dot(30,30,4); g.Dot(-30,-30,3); g.Dot(0,0,3); break;
            case "Training": case "Aim":
                g.Ring(0,0,31); g.Ring(0,0,15); g.Dot(0,0,4);
                g.Line(-47,0,-23,0); g.Line(23,0,47,0); g.Line(0,-47,0,-23); g.Line(0,23,0,47); break;
            case "Bot":
                g.Path(-29,-22,29,-22,35,-14,35,20,27,28,-27,28,-35,20,-35,-14,-29,-22);
                g.Dot(-14,8,4); g.Dot(14,8,4); g.Line(-12,-9,12,-9); g.Line(0,28,0,42); g.Dot(0,45,3);
                g.Line(-44,10,-44,-7); g.Line(44,10,44,-7); break;
            case "Network":
                g.Ring(0,0,10); g.Ring(-32,27,8); g.Ring(32,27,8); g.Ring(0,-36,8);
                g.Line(-24,20,-8,7); g.Line(24,20,8,7); g.Line(0,-10,0,-27); break;
            case "Deck":
                g.Path(-32,-23,-32,33,8,33,8,22); g.Path(-20,-34,-20,22,32,22,32,-34,-20,-34);
                g.Path(6,11,19,-5,6,-21,-7,-5,6,11); break;
            case "Settings":
                g.Ring(0,0,24); g.Ring(0,0,8);
                for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; g.Line(Mathf.Cos(a)*29,Mathf.Sin(a)*29,Mathf.Cos(a)*41,Mathf.Sin(a)*41,3); } break;
            case "Return": g.Path(-12,23,-36,0,-12,-23); g.Line(-36,0,38,0); break;
            case "Confirm": g.Path(-33,0,-10,-24,35,29); break;
            case "Lock": g.Path(-29,-34,29,-34,29,8,-29,8,-29,-34); g.Path(-18,8,-18,29,-10,38,10,38,18,29,18,8); g.Dot(0,-12,3); break;
            case "Close": g.Line(-26,-26,26,26); g.Line(-26,26,26,-26); break;
            case "Next": case "Chevron": g.Path(-14,-29,15,0,-14,29); break;
            case "Previous": g.Path(14,-29,-15,0,14,29); break;
            case "Health": g.Path(0,-38,-34,-3,-34,18,-24,29,-10,29,0,17,10,29,24,29,34,18,34,-3,0,-38); g.Line(-15,0,15,0); g.Line(0,-15,0,15); break;
            case "Shield": g.Path(0,39,33,24,28,-15,0,-39,-28,-15,-33,24,0,39); g.Path(-15,0,-3,-13,18,15); break;
            case "Clock": g.Ring(0,0,34); g.Path(0,22,0,0,20,-11); g.Line(0,44,0,48); break;
            case "Reset":
                for(int i=0;i<20;i++) { float a=(i*14+40)*Mathf.Deg2Rad,b=(i*14+54)*Mathf.Deg2Rad; g.Line(Mathf.Cos(a)*32,Mathf.Sin(a)*32,Mathf.Cos(b)*32,Mathf.Sin(b)*32); }
                g.Path(12,38,35,28,34,49); break;
            case "Info": g.Ring(0,0,35); g.Dot(0,18,3); g.Line(0,3,0,-19,2.7f); break;
            case "Star": g.Path(0,40,10,11,40,0,10,-11,0,-40,-10,-11,-40,0,-10,11,0,40); g.Dot(0,0,4); break;
            case "Volume": g.Path(-36,-11,-22,-11,-4,-30,-4,30,-22,11,-36,11,-36,-11); g.Path(9,-16,18,-7,18,7,9,16); g.Path(22,-29,35,-13,35,13,22,29); break;
            case "Opacity": g.Ring(0,0,33); g.Ring(0,0,13); g.Line(-22,-22,22,22); break;
            case "Campaign": g.Ring(11,15,24); g.Path(-42,-30,-13,4,2,-14,17,0,43,-30,-42,-30); break;
            case "Diamond": g.Path(0,29,21,0,0,-29,-21,0,0,29); g.Dot(0,0,4); break;
            case "Status": g.Ring(0,0,20); g.Dot(0,0,7); break;
        }
        return g;
    }
}
