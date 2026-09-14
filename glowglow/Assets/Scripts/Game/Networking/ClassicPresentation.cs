using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Broadcast;
using UnityEngine;

namespace GlowGlow.Online
{
    public struct ClassicVisual
    {
        public int Id, Sprite, Order, Layer;
        public Vector3 Position, Scale;
        public float Rotation, StartWidth, EndWidth;
        public Color Color, EndColor;
        public bool FlipX, FlipY, Loop, Clipped;
        public Vector4 ClipRect;
        public Vector2[] Points;
    }
    public struct ClassicVisualFrame : IBroadcast
    {
        public int Round, Index, Count;
        public uint Sequence;
        public ClassicVisual[] Views;
    }

    // Replicates the existing combat renderers, without instantiating another set of weapon rules.
    // This is the authoritative baseline; client movement prediction is deliberately not implied here.
    [DefaultExecutionOrder(1000)]
    public sealed class ClassicPresentation : MonoBehaviour
    {
        private MatchController match;
        private readonly HashSet<int> staticRenderers = new();
        private readonly Dictionary<int, Renderer> replicas = new();
        private readonly Dictionary<uint, Assembly> pending = new();
        private readonly List<Renderer> scan = new();
        private Material material, clippedMaterial;
        private MaterialPropertyBlock properties;
        public int AppliedFrames { get; private set; }
        private Sprite[] sprites;
        private uint applied;
        private bool host;

        private sealed class Assembly
        {
            public ClassicVisualFrame[] Parts;
            public int Received;
        }
        public void Initialize(MatchController controller, bool isHost)
        {
            match = controller; host = isHost;
            properties = new MaterialPropertyBlock();
            material = new Material(Shader.Find("Sprites/Default"));
            clippedMaterial = new Material(Resources.Load<Shader>("ArenaClippedSprite"));
            sprites = new[] { (Sprite)null, RuntimeShapes.Circle, RuntimeShapes.Barrel, RuntimeShapes.SoftGlow,
                RuntimeShapes.Square, RuntimeShapes.Spike };
            Collect();
            foreach (var renderer in scan)
            {
                bool combatant = renderer.GetComponentInParent<PlayerCombatant>() != null
                    || renderer.transform.root.name.Contains("Body Afterimages");
                if (!combatant) staticRenderers.Add(renderer.GetInstanceID());
                else if (!host) renderer.forceRenderingOff = true;
            }
        }
        private void Collect()
        {
            scan.Clear();
            foreach (var root in match.gameObject.scene.GetRootGameObjects())
                scan.AddRange(root.GetComponentsInChildren<Renderer>(false));
        }
        public IEnumerable<ClassicVisualFrame> Capture(int round, uint sequence)
        {
            Collect();
            var chunks = new List<ClassicVisual[]>();
            var batch = new List<ClassicVisual>();
            int bytes = 0;
            foreach (var renderer in scan)
            {
                if (!renderer.enabled || staticRenderers.Contains(renderer.GetInstanceID())
                    || renderer.GetComponentInParent<Canvas>() != null) continue;
                ClassicVisual view = new ClassicVisual { Id = renderer.GetInstanceID(), Order = renderer.sortingOrder,
                    Layer = renderer.sortingLayerID, Position = renderer.transform.position,
                    Scale = renderer.transform.lossyScale, Rotation = renderer.transform.eulerAngles.z };
                if (renderer is SpriteRenderer sprite)
                {
                    view.Sprite = Array.IndexOf(sprites, sprite.sprite);
                    if (view.Sprite <= 0) continue;
                    view.Color = sprite.color; view.FlipX = sprite.flipX; view.FlipY = sprite.flipY;
                    view.Clipped = sprite.sharedMaterial != null && sprite.sharedMaterial.shader == clippedMaterial.shader;
                    if (view.Clipped)
                    {
                        renderer.GetPropertyBlock(properties);
                        view.ClipRect = properties.GetVector("_ClipRect");
                    }
                }
                else if (renderer is LineRenderer line)
                {
                    view.Points = Points(line.positionCount, i => line.useWorldSpace ? line.GetPosition(i) : line.transform.TransformPoint(line.GetPosition(i)));
                    view.Color = line.startColor; view.EndColor = line.endColor;
                    view.StartWidth = line.startWidth; view.EndWidth = line.endWidth; view.Loop = line.loop;
                }
                else if (renderer is TrailRenderer trail)
                {
                    if (trail.positionCount < 2) continue;
                    view.Points = Points(trail.positionCount, trail.GetPosition);
                    view.Color = trail.startColor; view.EndColor = trail.endColor;
                    view.StartWidth = trail.startWidth; view.EndWidth = trail.endWidth;
                }
                else continue;
                int estimated = 150 + (view.Points?.Length ?? 0) * 8;
                if (bytes + estimated > 850 && batch.Count > 0)
                { chunks.Add(batch.ToArray()); batch.Clear(); bytes = 0; }
                batch.Add(view); bytes += estimated;
            }
            if (batch.Count > 0 || chunks.Count == 0) chunks.Add(batch.ToArray());
            for (int i = 0; i < chunks.Count; i++)
                yield return new ClassicVisualFrame { Round = round, Sequence = sequence, Index = i, Count = chunks.Count, Views = chunks[i] };
        }
        private static Vector2[] Points(int count, Func<int, Vector3> get)
        {
            // Current circles contain 64 vertices. Longer cosmetic trails are resampled.
            int length = Mathf.Min(count, 64);
            var result = new Vector2[length];
            for (int i = 0; i < length; i++) result[i] = get(length > 1 ? Mathf.RoundToInt((float)i * (count - 1) / (length - 1)) : 0);
            return result;
        }
        public void Apply(ClassicVisualFrame frame)
        {
            if (host || (int)(frame.Sequence - applied) <= 0 || frame.Count < 1 || frame.Count > 1024
                || frame.Index < 0 || frame.Index >= frame.Count || frame.Views == null) return;
            if (!pending.TryGetValue(frame.Sequence, out var assembly))
            {
                // Incomplete unreliable frames never alter the current display.
                foreach (uint old in pending.Keys.Where(key => (int)(frame.Sequence - key) > 4).ToArray()) pending.Remove(old);
                assembly = new Assembly { Parts = new ClassicVisualFrame[frame.Count] };
                pending.Add(frame.Sequence, assembly);
            }
            if (assembly.Parts.Length != frame.Count || assembly.Parts[frame.Index].Views != null) return;
            assembly.Parts[frame.Index] = frame; assembly.Received++;
            if (assembly.Received != frame.Count) return;
            var visible = new HashSet<int>();
            foreach (var part in assembly.Parts)
            foreach (var view in part.Views)
            {
                visible.Add(view.Id);
                bool isSprite = view.Sprite > 0;
                if (!replicas.TryGetValue(view.Id, out var renderer))
                {
                    var root = new GameObject("Classic Remote Visual");
                    root.transform.SetParent(transform, false);
                    renderer = isSprite ? root.AddComponent<SpriteRenderer>() : root.AddComponent<LineRenderer>();
                    replicas.Add(view.Id, renderer);
                }
                renderer.sortingOrder = view.Order; renderer.sortingLayerID = view.Layer;
                if (renderer is SpriteRenderer sprite)
                {
                    if (view.Sprite >= sprites.Length) continue;
                    sprite.sprite = sprites[view.Sprite]; sprite.color = view.Color;
                    sprite.flipX = view.FlipX; sprite.flipY = view.FlipY;
                    sprite.transform.SetPositionAndRotation(view.Position, Quaternion.Euler(0, 0, view.Rotation));
                    sprite.transform.localScale = view.Scale;
                    sprite.sharedMaterial = view.Clipped ? clippedMaterial : material;
                    properties.Clear();
                    if (view.Clipped) properties.SetVector("_ClipRect", view.ClipRect);
                    sprite.SetPropertyBlock(properties);
                }
                else if (renderer is LineRenderer line)
                {
                    line.sharedMaterial = material; line.useWorldSpace = true;
                    line.startColor = view.Color; line.endColor = view.EndColor;
                    line.startWidth = view.StartWidth; line.endWidth = view.EndWidth;
                    line.loop = view.Loop;
                    line.positionCount = view.Points?.Length ?? 0;
                    for (int i = 0; i < line.positionCount; i++) line.SetPosition(i, view.Points[i]);
                }
            }
            foreach (int id in replicas.Keys.Where(id => !visible.Contains(id)).ToArray())
            { Destroy(replicas[id].gameObject); replicas.Remove(id); }
            applied = frame.Sequence;
            AppliedFrames++;
            foreach (uint old in pending.Keys.Where(key => (int)(applied - key) >= 0).ToArray()) pending.Remove(old);
        }
        public void ClearReplicas()
        {
            foreach (var renderer in replicas.Values) if (renderer != null) Destroy(renderer.gameObject);
            replicas.Clear(); pending.Clear(); applied = 0;
        }
        private void LateUpdate()
        {
            if (host) OnlineSession.Current?.PublishState();
        }
        private void OnDestroy()
        {
            ClearReplicas();
            if (material != null) Destroy(material);
            if (clippedMaterial != null) Destroy(clippedMaterial);
        }
    }
}

