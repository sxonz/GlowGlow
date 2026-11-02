using System;
using FishNet.Serializing;
using GlowGlow.Online;
using UnityEditor;
using UnityEngine;

public static class ClassicVisualChecks
{
    public static void Run()
    {
        var root = new GameObject("Visual loss regression");
        var receiver = root.AddComponent<ClassicPresentation>();
        receiver.Initialize(null, false);
        try
        {
            var one = Sprite(1);
            receiver.Apply(Frame(10, one)); // Other chunks at tick 10 are deliberately dropped.
            Check(receiver.ContainsReplica(1), "A missing chunk must not block another projectile");
            receiver.Apply(Frame(12, Sprite(3)));
            receiver.Apply(Frame(11, Sprite(2)));
            Check(receiver.ContainsReplica(2), "Reordered samples for different objects must apply");
            receiver.ApplyRemoval(Remove(15, 1));
            receiver.Apply(Frame(14, one));
            Check(!receiver.ContainsReplica(1), "Late motion resurrected a removed projectile");
            receiver.Apply(Frame(16, one));
            receiver.ApplyRemoval(Remove(15, 1));
            Check(receiver.ContainsReplica(1), "Old removal deleted a newer pooled incarnation");
            receiver.ApplyRemoval(Remove(17, 1));
            receiver.Apply(Frame(1000, Sprite(4)));
            receiver.Apply(Frame(14, one));
            Check(!receiver.ContainsReplica(1), "Pruned tombstone allowed ancient data to resurrect");
            receiver.ClearReplicas();

            var moving = Sprite(20);
            receiver.Apply(Frame(1, moving));
            int styleUpdates = receiver.SpriteStyleUpdates;
            for (uint seq = 2; seq < 102; seq++)
            {
                moving.Position.x += .01f;
                moving.Rotation += 1;
                receiver.Apply(Frame(seq, moving));
            }
            Check(receiver.SpriteStyleUpdates == styleUpdates, "Motion rewrote unchanged sprite style");
            moving.Color = Color.cyan;
            receiver.Apply(Frame(102, moving));
            Check(receiver.SpriteStyleUpdates == styleUpdates + 1, "Changed color was not applied");
            receiver.ApplyRemoval(Remove(103, 20));
            var reused = Clipped();
            receiver.Apply(Frame(104, reused));
            var rendered = root.GetComponentInChildren<SpriteRenderer>();
            Check(rendered.HasPropertyBlock(), "Pooled sprite lost clipping");
            reused.Clipped = false;
            receiver.Apply(Frame(105, reused));
            Check(!rendered.HasPropertyBlock(), "Unclipped sprite retained per-renderer override");
            receiver.ClearReplicas();

            int warmCount = 0;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                var views = new ClassicVisual[256];
                var ids = new int[256];
                for (int i = 0; i < views.Length; i++)
                { ids[i] = 10000 + cycle * 256 + i; views[i] = Sprite(ids[i]); }
                uint seq = (uint)(cycle * 2 + 1);
                receiver.Apply(new ClassicVisualFrame { Sequence = seq, Views = views });
                Check(receiver.ActiveReplicaCount == 256, "Burst was not fully displayed");
                if (cycle == 0) warmCount = receiver.CreatedReplicaCount;
                receiver.ApplyRemoval(new ClassicVisualRemoval { Sequence = seq + 1, Ids = ids });
                Check(receiver.ActiveReplicaCount == 0, "Burst left ghost projectiles");
                Check(receiver.CreatedReplicaCount == warmCount, "Pool allocated again after warm-up");
            }

            foreach (var view in new[] { Sprite(8), Clipped(), Line() })
            {
                var writer = WriterPool.Retrieve();
                try
                {
                    writer.WriteClassicVisual(view);
                    Check(writer.Length == ClassicVisualCodec.SizeOf(view), "Wire size differs from send budget");
                    var reader = ReaderPool.Retrieve(writer.GetArraySegment(), null);
                    try
                    {
                        var copy = reader.ReadClassicVisual();
                        Check(copy.Id == view.Id && copy.Sprite == view.Sprite && copy.Order == view.Order
                            && copy.Layer == view.Layer && copy.Glow == view.Glow, "Style roundtrip");
                        Check(((Color32)copy.Color).Equals((Color32)view.Color), "Color roundtrip");
                        if (view.Sprite > 0)
                        {
                            Check((copy.Position - view.Position).sqrMagnitude < .000001f && (copy.Scale - view.Scale).sqrMagnitude < .000001f, "Transform roundtrip");
                            Check(Mathf.Abs(Mathf.DeltaAngle(copy.Rotation, view.Rotation)) < .01f, "Rotation quantization");
                            Check(copy.Clipped == view.Clipped && copy.ClipRect == view.ClipRect, "Arena clipping roundtrip");
                        }
                        else Check(copy.Points.Length == 64 && copy.Points[63] == view.Points[63], "Line roundtrip");
                    }
                    finally { reader.Store(); }
                }
                finally { writer.Store(); }
            }
            Debug.Log("CLASSIC_VISUAL_CHECKS_PASSED: dropped/reordered chunks, reliable removal, late resurrection, pooled reuse; 20 x 256-object bursts; sprite payload 34 bytes");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static ClassicVisual Sprite(int id) => new ClassicVisual { Id = id, Sprite = 1, Scale = new Vector3(.2f, .2f, 1),
        Position = new Vector3(1, 2, 0), Color = Color.magenta, Rotation = 32.12f, Glow = 1, Order = 4 };
    private static ClassicVisual Clipped()
    {
        var result = Sprite(9); result.Clipped = true; result.ClipRect = new Vector4(-8, -4, 8, 4); return result;
    }
    private static ClassicVisual Line()
    {
        var points = new Vector2[64];
        for (int i = 0; i < points.Length; i++) points[i] = new Vector2(i * .1f, .5f);
        return new ClassicVisual { Id = 10, Points = points, Color = Color.cyan, EndColor = Color.clear,
            StartWidth = .2f, EndWidth = .1f, Loop = true };
    }
    private static ClassicVisualFrame Frame(uint seq, ClassicVisual view) => new ClassicVisualFrame { Sequence = seq, Views = new[] { view } };
    private static ClassicVisualRemoval Remove(uint seq, int id) => new ClassicVisualRemoval { Sequence = seq, Ids = new[] { id } };
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}

