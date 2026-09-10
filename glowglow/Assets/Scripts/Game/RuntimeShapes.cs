using UnityEngine;

// All sprite artwork is imported from Assets/Resources/GameplaySprites.
// Runtime code only loads and reuses those assets; it never builds textures or sprites.
public static class RuntimeShapes
{
    private static Sprite circle, barrel, softGlow, square, spike;
    public static Sprite Circle => Load(ref circle, "Circle");
    public static Sprite Barrel => Load(ref barrel, "Barrel");
    public static Sprite SoftGlow => Load(ref softGlow, "SoftGlow");
    public static Sprite Square => Load(ref square, "Square");
    public static Sprite Spike => Load(ref spike, "Spike");

    private static Sprite Load(ref Sprite cached, string name)
    {
        if (cached != null) return cached;
        cached = Resources.Load<Sprite>("GameplaySprites/" + name);
        if (cached == null)
            throw new System.InvalidOperationException("Missing prepared gameplay sprite: " + name);
        return cached;
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

}
