using System;
using System.IO;
using FishNet.Serializing;
using GlowGlow.Online;
using UnityEditor;
using UnityEngine;

public static class NeonBeamChecks
{
    [MenuItem("GlowGlow/Validate Neon Beam")]
    public static void Run()
    {
        var material = NeonBeamStyle.Material;
        if (ShaderUtil.ShaderHasError(material.shader)) throw new Exception("Neon beam shader failed compilation.");
        foreach (bool clipped in new[] { false, true })
        {
            var view = new ClassicVisual { Id = 991, Sprite = 4, Glow = 3, Clipped = clipped,
                Color = Color.cyan, Scale = Vector3.one, ClipRect = new Vector4(-4, -2, 4, 2) };
            var writer = WriterPool.Retrieve();
            try
            {
                writer.WriteClassicVisual(view);
                if (writer.Length != ClassicVisualCodec.SizeOf(view)) throw new Exception("Beam wire size changed.");
                var reader = ReaderPool.Retrieve(writer.GetArraySegment(), null);
                try
                {
                    var copy = reader.ReadClassicVisual();
                    if (copy.Glow != 3 || copy.Clipped != clipped || (clipped && copy.ClipRect != view.ClipRect))
                        throw new Exception("Beam style or clipping lost on wire.");
                }
                finally { reader.Store(); }
            }
            finally { writer.Store(); }
        }
        ClassicVisualChecks.Run();
        RenderPreview();
        Debug.Log("NEON_BEAM_CHECKS_PASSED: shader, style serialization, wire budget, existing replica regressions and preview.");
    }

    private static void RenderPreview()
    {
        var root = new GameObject("Neon beam preview");
        var cameraObject = new GameObject("Preview camera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(1200, 600, 24);
        var previous = RenderTexture.active;
        Texture2D image = null;
        try
        {
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.018f, .025f, .055f);
            camera.cullingMask = 1 << 30;
            camera.targetTexture = target;
            var colors = new[] { new Color(.12f, .8f, 1), new Color(1, .2f, .55f), new Color(.55f, .3f, 1) };
            for (int i = 0; i < colors.Length; i++)
            {
                var beam = new GameObject("Beam", typeof(SpriteRenderer));
                beam.layer = 30;
                beam.transform.SetParent(root.transform);
                beam.transform.position = new Vector3(0, 1.6f - i * 1.6f, 0);
                beam.transform.localScale = new Vector3(9, .55f, 1);
                var sprite = beam.GetComponent<SpriteRenderer>();
                sprite.sprite = RuntimeShapes.Square;
                sprite.sharedMaterial = NeonBeamStyle.Material;
                sprite.color = colors[i];
            }
            camera.Render();
            RenderTexture.active = target;
            image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            var center = image.GetPixel(600, 300);
            if (center.r < .5f || center.g < .5f || center.b < .5f)
                throw new Exception("Beam core did not render bright.");
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/neon-beam-preview.png", image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(target);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
