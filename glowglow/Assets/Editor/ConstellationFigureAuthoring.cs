using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Bakes approved hollow artwork and its matching distance field before play.</summary>
public static class ConstellationFigureAuthoring
{
    private const int Width = 768, Height = 448, PixelsPerUnit = 64;

    [MenuItem("GlowGlow/Bake Constellation Figures")]
    public static void Bake()
    {
        for (int kind = 0; kind < 3; kind++)
        {
            string name = "Constellation" + ConstellationIgnition.PatternNames[kind];
            string sourcePath = "Assets/Editor/ConstellationArt/" + ConstellationIgnition.PatternNames[kind] + ".png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(sourcePath)))
                    throw new InvalidDataException("Cannot read prepared artwork: " + sourcePath);
                var pixels = new Color[Width * Height];
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    pixels[y * Width + x] = source.GetPixelBilinear((x + .5f) / Width, (y + .5f) / Height);

                BakeCollision(pixels, "Assets/Resources/GameplaySprites/" + name + "Collision.bytes");
                // No star map is painted into the empty body: actual traced light supplies it.
                for (int i = 0; i < pixels.Length; i++) pixels[i].a *= .75f;
                texture.SetPixels(pixels);
                texture.Apply();
                string path = "Assets/Resources/GameplaySprites/" + name + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(.5f, .5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        AssetDatabase.SaveAssets();
    }

    private static void BakeCollision(Color[] pixels, string path)
    {
        int width = ConstellationFigureMask.Width, height = ConstellationFigureMask.Height;
        var field = new float[width * height];
        int opaque = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            // Two artwork pixels per distance cell, with 16 cells of empty padding.
            int px = (x - 16) * 2, py = (y - 16) * 2;
            bool solid = false;
            if (px >= 0 && px < Width && py >= 0 && py < Height)
                for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    Color color = pixels[(py + dy) * Width + px + dx];
                    // Faint halo/noise is decoration. Only the luminous strands are dangerous.
                    solid |= color.a * Mathf.Max(color.r, Mathf.Max(color.g, color.b)) >= .6f;
                }
            field[y * width + x] = solid ? 0 : 1e8f;
            if (solid) opaque++;
        }
        if (opaque == 0 || opaque > field.Length / 3)
            throw new InvalidDataException("Constellation collision must describe hollow luminous strands: " + path);

        // Exact squared Euclidean distance transform, linear time, editor only.
        var input = new float[Mathf.Max(width, height)];
        var output = new float[input.Length];
        var sites = new int[input.Length];
        var borders = new float[input.Length + 1];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++) input[y] = field[y * width + x];
            TransformDistance(input, output, sites, borders, height);
            for (int y = 0; y < height; y++) field[y * width + x] = output[y];
        }
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) input[x] = field[y * width + x];
            TransformDistance(input, output, sites, borders, width);
            for (int x = 0; x < width; x++) field[y * width + x] = output[x];
        }
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(new byte[] { (byte)'C', (byte)'F', (byte)'D', 1 });
            foreach (float squared in field)
            {
                float distance = Mathf.Max(0, Mathf.Sqrt(squared) - .5f) / ConstellationFigureMask.PixelsPerUnit;
                writer.Write((ushort)Mathf.Clamp(Mathf.RoundToInt(distance / ConstellationFigureMask.Precision), 0, ushort.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"CONSTELLATION_COLLISION_BAKED {path}: {opaque} occupied cells, {field.Length * 2 + 4} bytes");
    }

    private static void TransformDistance(float[] input, float[] output, int[] sites, float[] borders, int length)
    {
        int k = 0;
        sites[0] = 0;
        borders[0] = float.NegativeInfinity;
        borders[1] = float.PositiveInfinity;
        for (int q = 1; q < length; q++)
        {
            float crossing;
            do
            {
                int previous = sites[k];
                crossing = ((input[q] + q * q) - (input[previous] + previous * previous)) / (2f * (q - previous));
                if (crossing > borders[k]) break;
                k--;
            } while (k >= 0);
            k++;
            sites[k] = q;
            borders[k] = crossing;
            borders[k + 1] = float.PositiveInfinity;
        }
        k = 0;
        for (int q = 0; q < length; q++)
        {
            while (borders[k + 1] < q) k++;
            float delta = q - sites[k];
            output[q] = delta * delta + input[sites[k]];
        }
    }

    public static void BakeBatch()
    {
        try { Bake(); Debug.Log("CONSTELLATION_FIGURES_BAKED"); EditorApplication.Exit(0); }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
