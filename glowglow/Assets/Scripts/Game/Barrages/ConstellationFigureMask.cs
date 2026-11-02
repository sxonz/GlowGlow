using System;
using UnityEngine;

/// <summary>Editor-baked distance to the visible outline, including its empty interior.</summary>
public sealed class ConstellationFigureMask
{
    public const int Width = 416, Height = 256, PixelsPerUnit = 32, HeaderSize = 4;
    public const float Precision = .001f;
    public static readonly Vector2 HalfSize = new(6.5f, 4);
    private static readonly ConstellationFigureMask[] Cache = new ConstellationFigureMask[3];
    private readonly byte[] distances;

    private ConstellationFigureMask(byte[] data)
    {
        if (data.Length != HeaderSize + Width * Height * 2 ||
            data[0] != 'C' || data[1] != 'F' || data[2] != 'D' || data[3] != 1)
            throw new InvalidOperationException("Rebake the constellation outline collision assets.");
        distances = data;
    }

    public static ConstellationFigureMask Load(int pattern)
    {
        if (Cache[pattern] != null) return Cache[pattern];
        var asset = Resources.Load<TextAsset>("GameplaySprites/Constellation" +
            ConstellationIgnition.PatternNames[pattern] + "Collision");
        if (asset == null) throw new InvalidOperationException("Missing prepared constellation collision mask.");
        return Cache[pattern] = new ConstellationFigureMask(asset.bytes);
    }

    public float Distance(Vector2 point)
    {
        Vector2 grid = (point + HalfSize) * PixelsPerUnit - Vector2.one * .5f;
        float x = Mathf.Clamp(grid.x, 0, Width - 1), y = Mathf.Clamp(grid.y, 0, Height - 1);
        int ix = Mathf.Min((int)x, Width - 2), iy = Mathf.Min((int)y, Height - 2);
        float a = Mathf.Lerp(Read(ix, iy), Read(ix + 1, iy), x - ix);
        float b = Mathf.Lerp(Read(ix, iy + 1), Read(ix + 1, iy + 1), x - ix);
        // A half-unit empty border surrounds the image; players outside it cannot touch the art.
        return Mathf.Lerp(a, b, y - iy) + Vector2.Distance(grid, new Vector2(x, y)) / PixelsPerUnit;
    }

    public bool IntersectsSweep(Vector2 from, Vector2 to, float radius)
    {
        float length = Vector2.Distance(from, to);
        int samples = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(.025f, radius * .5f)));
        for (int i = 0; i <= samples; i++)
            if (Distance(Vector2.Lerp(from, to, (float)i / samples)) <= radius) return true;
        return false;
    }

    private float Read(int x, int y)
    {
        int index = HeaderSize + (y * Width + x) * 2;
        return (distances[index] | distances[index + 1] << 8) * Precision;
    }
}
