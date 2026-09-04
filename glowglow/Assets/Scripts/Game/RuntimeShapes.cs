using UnityEngine;

public static class RuntimeShapes
{
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
