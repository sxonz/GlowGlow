using UnityEngine;

public static class RuntimeShapes
{
    private static Sprite softGlow;
    public static Sprite SoftGlow
    {
        get
        {
            if (softGlow != null) return softGlow;
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Soft Glow", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1).magnitude;
                float fade = Mathf.Clamp01(1 - radius);
                pixels[y * size + x] = new Color(1, 1, 1, fade * fade);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            softGlow = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            return softGlow;
        }
    }

    public static SpriteRenderer CreateGlow(SpriteRenderer source)
    {
        var glow = new GameObject("Soft Glow", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        glow.transform.SetParent(source.transform, false);
        glow.sprite = SoftGlow;
        return glow;
    }

    public static void SyncGlow(SpriteRenderer source, SpriteRenderer glow, float strength, float spread)
    {
        glow.enabled = source.enabled && source.sprite != null;
        if (!glow.enabled) return;
        var size = source.sprite.bounds.size;
        var scale = source.transform.lossyScale;
        // World-space padding keeps long beams from producing a screen-wide haze.
        glow.transform.localPosition = source.sprite.bounds.center;
        glow.transform.localScale = new Vector3(size.x + spread / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            size.y + spread / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
        glow.sortingLayerID = source.sortingLayerID;
        glow.sortingOrder = source.sortingOrder - 1;
        var color = source.color;
        color.a *= strength;
        glow.color = color;
    }

    public static void AddArenaGlow(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var line in root.GetComponentsInChildren<LineRenderer>())
        {
            if (line.gameObject.name != "Arena Border") continue;
            if (line.transform.Find("Border Glow 1") != null) continue;
            var positions = new Vector3[line.positionCount];
            line.GetPositions(positions);
            for (int i = 1; i <= 3; i++)
            {
                var glow = new GameObject("Border Glow " + i, typeof(LineRenderer)).GetComponent<LineRenderer>();
                glow.transform.SetParent(line.transform, false);
                glow.sharedMaterial = line.sharedMaterial;
                glow.useWorldSpace = line.useWorldSpace;
                glow.loop = line.loop;
                glow.positionCount = positions.Length;
                glow.SetPositions(positions);
                glow.startWidth = glow.endWidth = .06f + i * .055f;
                var color = line.startColor;
                color.a *= .07f;
                glow.startColor = glow.endColor = color;
                glow.numCornerVertices = 6;
                glow.sortingLayerID = line.sortingLayerID;
                glow.sortingOrder = line.sortingOrder - 1;
            }
        }
    }

    private static Sprite barrel;
    public static Sprite Barrel
    {
        get
        {
            if (barrel != null) return barrel;
            const int width = 80, height = 38, border = 4;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Tank Barrel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            var outline = new Color32(65, 70, 78, 255);
            var fill = new Color32(157, 164, 174, 255);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = x < border || x >= width - border || y < border || y >= height - border
                    ? outline : fill;
            texture.SetPixels32(pixels);
            texture.Apply();
            barrel = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, 100);
            return barrel;
        }
    }

    private static Sprite circle;
    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Runtime Circle" };
            var pixels = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * .5f;
            float radius = size * .48f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = radius - Vector2.Distance(new Vector2(x, y), center);
                byte alpha = (byte)(Mathf.Clamp01(edge + .5f) * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            circle = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            return circle;
        }
    }
}
