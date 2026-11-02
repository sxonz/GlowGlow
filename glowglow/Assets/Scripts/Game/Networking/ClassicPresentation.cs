using System;
using System.Collections.Generic;
using FishNet.Broadcast;
using UnityEngine;

namespace GlowGlow.Online
{
    public struct ClassicVisualFrame : IBroadcast
    {
        public int Round;
        public uint Sequence;
        public ClassicVisual[] Views;
    }
    public struct ClassicVisualRemoval : IBroadcast
    {
        public int Round;
        public uint Sequence;
        public int[] Ids;
    }

    [DefaultExecutionOrder(1000)]
    public sealed class ClassicPresentation : MonoBehaviour
    {
        public const int BytesPerCapture = 3000;
        public const int PacketPayload = 800;
        private const uint HistoryTicks = 120;
        private const float RefreshSeconds = .25f;
        private MatchController match;
        private readonly Dictionary<int, Replica> replicas = new();
        private readonly Dictionary<int, Sent> sent = new();
        private readonly Dictionary<int, uint> removedAt = new();
        private readonly HashSet<int> active = new();
        private readonly List<Renderer> scan = new(), scratch = new();
        private readonly List<ClassicVisual> priority = new(), regular = new(), batch = new();
        private readonly List<ClassicVisualFrame> frames = new();
        private readonly List<ClassicVisualRemoval> removals = new();
        private readonly List<int> keys = new();
        private readonly Stack<Replica> spritePool = new(), linePool = new();
        public const int MaxLocalTrails = NeonVisualStyle.MaxPrismTrails;
        private readonly Stack<TrailRenderer> trailPool = new();
        private Material material, clippedMaterial;
        private MaterialPropertyBlock properties;
        private Sprite[] sprites;
        private bool host;
        private uint newest;
        private int cursor;
        private int performanceFrames, slowFrames, previousAppliedFrames;
        private float performanceTime, longestFrame;
        public int FramesPerSecond { get; private set; }
        public int AppliedFrames { get; private set; }
        public int ActiveReplicaCount => replicas.Count;
        public int CreatedReplicaCount { get; private set; }
        public int SourceVisualCount { get; private set; }
        public int LastCaptureBytes { get; private set; }
        public long TotalCaptureBytes { get; private set; }
        public int SpriteStyleUpdates { get; private set; }
        public IReadOnlyList<ClassicVisualRemoval> Removals => removals;
        public bool ContainsReplica(int id) => replicas.ContainsKey(id);

        private sealed class Sent { public ClassicVisual View; public float Time; }
        private sealed class Replica
        {
            public Renderer Renderer;
            public SpriteRenderer Sprite, Glow;
            public LineRenderer Line;
            public TrailRenderer Trail;
            public uint Sequence;
            public Vector3 Target;
            public byte GlowMode;
            public ClassicVisual Style;
            public bool HasStyle;
        }

        public void Initialize(MatchController controller, bool isHost)
        {
            match = controller; host = isHost;
            properties = new MaterialPropertyBlock();
            material = new Material(BarrageVisibility.SpriteShader);
            clippedMaterial = new Material(Resources.Load<Shader>("ArenaClippedSprite"));
            sprites = new[] { (Sprite)null, RuntimeShapes.Circle, RuntimeShapes.Barrel, RuntimeShapes.SoftGlow,
                RuntimeShapes.Square, RuntimeShapes.Spike, RuntimeShapes.ConstellationFigure(0),
                RuntimeShapes.ConstellationFigure(1), RuntimeShapes.ConstellationFigure(2) };
            if (!host)
                for (int i = 0; i < MaxLocalTrails; i++)
                {
                    var trail = new GameObject("Local Prism Ribbon", typeof(TrailRenderer)).GetComponent<TrailRenderer>();
                    trail.transform.SetParent(transform, false);
                    trail.enabled = trail.emitting = false;
                    trailPool.Push(trail);
                }
            if (!host && match != null)
            {
                scan.Clear();
                match.PlayerOne.AppendNetworkRenderers(scan, scratch);
                match.PlayerTwo.AppendNetworkRenderers(scan, scratch);
                foreach (var renderer in scan) renderer.forceRenderingOff = true;
            }
        }

        public IReadOnlyList<ClassicVisualFrame> Capture(int round, uint sequence)
        {
            scan.Clear();
            match.PlayerOne.AppendNetworkRenderers(scan, scratch);
            match.PlayerTwo.AppendNetworkRenderers(scan, scratch);
            int playerCount = scan.Count;
            // Never traverse the arena UI or the scene's inactive projectile pool.
            foreach (var projectile in ProjectileBase.ActiveProjectiles)
            {
                if (projectile == null || projectile.gameObject.scene != match.gameObject.scene) continue;
                projectile.GetComponentsInChildren(false, scratch);
                scan.AddRange(scratch);
            }
            active.Clear(); priority.Clear(); regular.Clear(); frames.Clear(); removals.Clear();
            for (int i = 0; i < scan.Count; i++)
            {
                var renderer = scan[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                // Ribbon geometry is reconstructed from the authoritative sprite path on each peer.
                if (renderer is TrailRenderer ribbon && ribbon.sharedMaterial == NeonVisualStyle.Ribbon) continue;
                if (IsDerivedGlow(renderer)) continue;
                int id = renderer.GetInstanceID();
                if (!active.Add(id)) continue; // Attached overdrive effects also occur in the projectile list.
                if (!TryCapture(renderer, i < playerCount, out var view)) { active.Remove(id); continue; }
                if (sent.TryGetValue(id, out var previous)
                    && Time.unscaledTime - previous.Time < RefreshSeconds && Same(previous.View, view)) continue;
                (i < playerCount ? priority : regular).Add(view);
            }
            SourceVisualCount = active.Count;
            keys.Clear();
            foreach (var entry in sent) if (!active.Contains(entry.Key)) keys.Add(entry.Key);
            for (int offset = 0; offset < keys.Count; offset += 128)
            {
                int count = Mathf.Min(128, keys.Count - offset);
                var ids = new int[count];
                keys.CopyTo(offset, ids, 0, count);
                removals.Add(new ClassicVisualRemoval { Round = round, Sequence = sequence, Ids = ids });
            }
            foreach (int id in keys) sent.Remove(id);

            int budget = BytesPerCapture, packetBytes = 0;
            batch.Clear();
            foreach (var view in priority) Queue(view, round, sequence, ref budget, ref packetBytes);
            if (regular.Count > 0)
            {
                int start = cursor % regular.Count;
                int lastSent = start;
                for (int i = 0; i < regular.Count; i++)
                {
                    int index = (start + i) % regular.Count;
                    if (Queue(regular[index], round, sequence, ref budget, ref packetBytes)) lastSent = index + 1;
                }
                cursor = lastSent;
            }
            Flush(round, sequence);
            LastCaptureBytes = BytesPerCapture - budget + frames.Count * 32;
            foreach (var removal in removals) LastCaptureBytes += 24 + removal.Ids.Length * 4;
            TotalCaptureBytes += LastCaptureBytes;
            return frames;
        }
        private bool Queue(ClassicVisual view, int round, uint sequence, ref int budget, ref int packetBytes)
        {
            int size = ClassicVisualCodec.SizeOf(view);
            if (size > budget) return false;
            if (packetBytes + size > PacketPayload)
            { Flush(round, sequence); packetBytes = 0; }
            batch.Add(view); budget -= size; packetBytes += size;
            if (!sent.TryGetValue(view.Id, out var entry)) sent.Add(view.Id, entry = new Sent());
            entry.View = view; entry.Time = Time.unscaledTime;
            return true;
        }
        private void Flush(int round, uint sequence)
        {
            if (batch.Count == 0) return;
            frames.Add(new ClassicVisualFrame { Round = round, Sequence = sequence, Views = batch.ToArray() });
            batch.Clear();
        }
        private bool IsDerivedGlow(Renderer renderer)
        {
            return renderer.name == "Soft Glow" && renderer is SpriteRenderer
                && renderer.transform.parent != null
                && renderer.transform.parent.TryGetComponent<SpriteRenderer>(out var parent)
                && Array.IndexOf(sprites, parent.sprite) > 0;
        }
        private bool TryCapture(Renderer renderer, bool player, out ClassicVisual view)
        {
            renderer.GetPropertyBlock(properties);
            view = new ClassicVisual { Id = renderer.GetInstanceID(), Order = renderer.sortingOrder,
                Layer = renderer.sortingLayerID, Position = renderer.transform.position,
                Scale = renderer.transform.lossyScale, Rotation = renderer.transform.eulerAngles.z,
                Owner = (byte)properties.GetFloat(BarrageVisibility.OwnerProperty) };
            if (renderer is SpriteRenderer sprite)
            {
                view.Sprite = Array.IndexOf(sprites, sprite.sprite);
                if (view.Sprite <= 0) return false;
                view.Color = sprite.color; view.FlipX = sprite.flipX; view.FlipY = sprite.flipY;
                var glow = sprite.transform.Find("Soft Glow");
                if (glow != null && glow.TryGetComponent<SpriteRenderer>(out var glowRenderer) && glowRenderer.enabled)
                    view.Glow = (byte)(player && sprite.GetComponent<PlayerCombatant>() != null ? 2 : 1);
                bool beam = sprite.sharedMaterial == NeonBeamStyle.Material;
                if (beam) view.Glow = 3; // Derived shader style, no additional visual records.
                byte style = NeonVisualStyle.Identify(sprite.sharedMaterial);
                if (style != 0) view.Glow = style;
                bool mayBeClipped = ((beam || style != 0) && renderer.HasPropertyBlock())
                    || (sprite.sharedMaterial != null && sprite.sharedMaterial.shader == clippedMaterial.shader);
                if (mayBeClipped)
                {
                    var clip = properties.GetVector("_ClipRect");
                    // SpriteRenderer also installs texture/color properties after rendering.
                    // Only an explicit, nonempty arena rectangle represents clipping.
                    view.Clipped = clip.z > clip.x && clip.w > clip.y;
                    if (view.Clipped) view.ClipRect = clip;
                }
                return true;
            }
            if (renderer is LineRenderer line)
            {
                int length = Mathf.Min(line.positionCount, 64);
                view.Points = new Vector2[length];
                for (int i = 0; i < length; i++)
                {
                    int index = length > 1 ? Mathf.RoundToInt((float)i * (line.positionCount - 1) / (length - 1)) : 0;
                    view.Points[i] = line.useWorldSpace ? line.GetPosition(index) : line.transform.TransformPoint(line.GetPosition(index));
                }
                view.Color = line.startColor; view.EndColor = line.endColor;
                view.StartWidth = line.startWidth; view.EndWidth = line.endWidth; view.Loop = line.loop;
                return true;
            }
            if (renderer is TrailRenderer trail && trail.positionCount >= 2)
            {
                int length = Mathf.Min(trail.positionCount, 32);
                view.Points = new Vector2[length];
                for (int i = 0; i < length; i++)
                    view.Points[i] = trail.GetPosition(Mathf.RoundToInt((float)i * (trail.positionCount - 1) / (length - 1)));
                view.Color = trail.startColor; view.EndColor = trail.endColor;
                view.StartWidth = trail.startWidth; view.EndWidth = trail.endWidth;
                return true;
            }
            return false;
        }
        private static bool Same(ClassicVisual a, ClassicVisual b)
        {
            if (a.Sprite != b.Sprite || a.Order != b.Order || a.Layer != b.Layer || a.Glow != b.Glow || a.Owner != b.Owner
                || a.FlipX != b.FlipX || a.FlipY != b.FlipY || a.Loop != b.Loop || a.Clipped != b.Clipped
                || a.ClipRect != b.ClipRect || !((Color32)a.Color).Equals((Color32)b.Color)) return false;
            if (a.Sprite > 0) return (a.Position - b.Position).sqrMagnitude < .000001f
                && (a.Scale - b.Scale).sqrMagnitude < .000001f && Mathf.Abs(Mathf.DeltaAngle(a.Rotation, b.Rotation)) < .05f;
            if (a.StartWidth != b.StartWidth || a.EndWidth != b.EndWidth || !((Color32)a.EndColor).Equals((Color32)b.EndColor)
                || a.Points.Length != b.Points.Length) return false;
            for (int i = 0; i < a.Points.Length; i++) if ((a.Points[i] - b.Points[i]).sqrMagnitude > .000001f) return false;
            return true;
        }

        private bool AcceptSequence(uint sequence)
        {
            if ((int)(sequence - newest) > 0)
            {
                newest = sequence;
                keys.Clear();
                foreach (var entry in removedAt) if ((int)(newest - entry.Value) > HistoryTicks) keys.Add(entry.Key);
                foreach (int id in keys) removedAt.Remove(id);
            }
            return (int)(newest - sequence) <= HistoryTicks;
        }
        public void Apply(ClassicVisualFrame frame)
        {
            if (host || frame.Views == null || !AcceptSequence(frame.Sequence)) return;
            bool changed = false;
            foreach (var view in frame.Views)
            {
                if (view.Sprite < 0 || view.Sprite >= sprites.Length || view.Glow > NeonVisualStyle.SunMode
                    || view.Sprite == 0 && (view.Points == null || view.Points.Length > 64)) continue;
                if (removedAt.TryGetValue(view.Id, out uint removed) && (int)(frame.Sequence - removed) <= 0) continue;
                bool exists = replicas.TryGetValue(view.Id, out var replica);
                if (exists && (int)(frame.Sequence - replica.Sequence) <= 0) continue;
                if (exists && (replica.Sprite != null) != (view.Sprite > 0))
                { Release(view.Id, replica); exists = false; }
                if (!exists)
                {
                    replica = Rent(view.Sprite > 0);
                    replicas.Add(view.Id, replica);
                    replica.Renderer.transform.position = view.Position;
                }
                replica.Sequence = frame.Sequence;
                if (replica.Sprite != null)
                {
                    var sprite = replica.Sprite;
                    replica.Target = view.Position;
                    if ((sprite.transform.position - view.Position).sqrMagnitude > 4)
                    {
                        sprite.transform.position = view.Position;
                        replica.Trail?.Clear();
                    }
                    if (!replica.HasStyle || replica.Style.Rotation != view.Rotation)
                        sprite.transform.rotation = Quaternion.Euler(0, 0, view.Rotation);
                    ApplySpriteStyle(replica, view);
                    if (!exists && view.Order == 4 && (view.Glow == NeonVisualStyle.BlastMode
                        || view.Glow == 1 && view.Sprite == 4 && view.Scale.x > .5f))
                        CombatVfx.Burst(gameObject.scene, view.Position, view.Color, Mathf.Clamp(view.Scale.x, .5f, 1.5f), view.Owner);
                }
                else
                {
                    var line = replica.Line;
                    line.sortingOrder = view.Order; line.sortingLayerID = view.Layer;
                    line.startColor = view.Color; line.endColor = view.EndColor;
                    line.startWidth = view.StartWidth; line.endWidth = view.EndWidth; line.loop = view.Loop;
                    line.positionCount = view.Points.Length;
                    for (int i = 0; i < view.Points.Length; i++) line.SetPosition(i, view.Points[i]);
                    if (!replica.HasStyle || replica.Style.Owner != view.Owner) BarrageVisibility.ApplyOwner(line, view.Owner);
                    replica.Style = view;
                    replica.HasStyle = true;
                }
                changed = true;
            }
            if (changed) AppliedFrames++;
        }
        private void ApplySpriteStyle(Replica replica, ClassicVisual view)
        {
            var old = replica.Style;
            bool first = !replica.HasStyle;
            bool changed = first || old.Sprite != view.Sprite || old.Scale != view.Scale
                || old.Color != view.Color || old.Order != view.Order || old.Layer != view.Layer
                || old.FlipX != view.FlipX || old.FlipY != view.FlipY || old.Glow != view.Glow
                || old.Clipped != view.Clipped || old.ClipRect != view.ClipRect || old.Owner != view.Owner;
            if (changed)
            {
                var sprite = replica.Sprite;
                sprite.sortingOrder = view.Order; sprite.sortingLayerID = view.Layer;
                sprite.transform.localScale = view.Scale;
                sprite.sprite = sprites[view.Sprite]; sprite.color = view.Color;
                sprite.flipX = view.FlipX; sprite.flipY = view.FlipY;
                if (first || old.Glow != view.Glow || old.Clipped != view.Clipped)
                {
                    sprite.sharedMaterial = view.Glow == 3 ? NeonBeamStyle.Material
                        : view.Glow == 2 || view.Glow >= NeonVisualStyle.BulletMode ? NeonVisualStyle.Get(view.Glow)
                        : view.Clipped ? clippedMaterial : material;
                    // A halo uses its prepared soft texture, never the parent's body shader.
                    replica.Glow.sharedMaterial = view.Clipped ? clippedMaterial : NeonVisualStyle.Glow;
                }
                if (first || old.Clipped != view.Clipped || old.ClipRect != view.ClipRect || old.Owner != view.Owner)
                {
                    properties.Clear();
                    if (view.Clipped) properties.SetVector("_ClipRect", view.ClipRect);
                    if (view.Owner != 0) properties.SetFloat(BarrageVisibility.OwnerProperty, view.Owner);
                    sprite.SetPropertyBlock(view.Clipped || view.Owner != 0 ? properties : null);
                    replica.Glow.SetPropertyBlock(view.Clipped || view.Owner != 0 ? properties : null);
                }
                replica.GlowMode = view.Glow;
                UpdateTrail(replica, view);
                UpdateGlow(replica);
                SpriteStyleUpdates++;
            }
            replica.Style = view;
            replica.HasStyle = true;
        }
        private void UpdateTrail(Replica replica, ClassicVisual view)
        {
            if (view.Glow != NeonVisualStyle.PrismMode) { ReturnTrail(replica); return; }
            if (replica.Trail == null && trailPool.Count > 0)
            {
                replica.Trail = trailPool.Pop();
                replica.Trail.transform.SetParent(replica.Sprite.transform, false);
                replica.Trail.transform.localPosition = Vector3.zero;
                replica.Trail.Clear();
            }
            if (replica.Trail == null) return;
            NeonVisualStyle.ConfigureTrail(replica.Trail, view.Color, Mathf.Abs(view.Scale.x), view.Layer, view.Order);
            BarrageVisibility.ApplyOwner(replica.Trail, view.Owner);
            replica.Trail.enabled = true;
        }
        private void ReturnTrail(Replica replica)
        {
            if (replica.Trail == null) return;
            replica.Trail.Clear();
            replica.Trail.enabled = replica.Trail.emitting = false;
            replica.Trail.transform.SetParent(transform, false);
            trailPool.Push(replica.Trail);
            replica.Trail = null;
        }
        public void ApplyRemoval(ClassicVisualRemoval message)
        {
            if (host || message.Ids == null) return;
            AcceptSequence(message.Sequence);
            foreach (int id in message.Ids)
            {
                if (removedAt.TryGetValue(id, out uint previous) && (int)(message.Sequence - previous) <= 0) continue;
                removedAt[id] = message.Sequence;
                // A pooled host projectile may already have been reused with a newer sample.
                if (replicas.TryGetValue(id, out var replica) && (int)(message.Sequence - replica.Sequence) >= 0)
                {
                    if (replica.GlowMode == NeonVisualStyle.PrismMode)
                        CombatVfx.Burst(gameObject.scene, replica.Target, replica.Style.Color, .55f, replica.Style.Owner);
                    Release(id, replica);
                }
            }
        }
        private Replica Rent(bool sprite)
        {
            var pool = sprite ? spritePool : linePool;
            if (pool.Count > 0)
            {
                var reused = pool.Pop();
                reused.Renderer.gameObject.SetActive(true);
                return reused;
            }
            var root = new GameObject("Classic Remote Visual");
            root.transform.SetParent(transform, false);
            var replica = new Replica();
            if (sprite)
            {
                replica.Sprite = root.AddComponent<SpriteRenderer>();
                replica.Renderer = replica.Sprite;
                replica.Glow = RuntimeShapes.CreateGlow(replica.Sprite);
            }
            else
            {
                replica.Line = root.AddComponent<LineRenderer>();
                replica.Renderer = replica.Line;
                replica.Line.sharedMaterial = material; replica.Line.useWorldSpace = true;
            }
            CreatedReplicaCount++;
            return replica;
        }
        private void Release(int id, Replica replica)
        {
            replicas.Remove(id);
            ReturnTrail(replica);
            replica.HasStyle = false;
            replica.Renderer.gameObject.SetActive(false); // Disappearance is immediate, including its glow.
            var pool = replica.Sprite != null ? spritePool : linePool;
            if (pool.Count < (replica.Sprite != null ? 512 : 128)) pool.Push(replica);
            else Destroy(replica.Renderer.gameObject);
        }
        private static void UpdateGlow(Replica replica)
        {
            if (replica.GlowMode == 0 || replica.GlowMode == 3 || replica.GlowMode == NeonVisualStyle.WarningMode)
            { replica.Glow.enabled = false; return; }
            bool body = replica.GlowMode == 2;
            RuntimeShapes.SyncGlow(replica.Sprite, replica.Glow, body ? .55f : .42f, body ? 1.1f : .42f);
        }
        public void ClearReplicas()
        {
            keys.Clear(); keys.AddRange(replicas.Keys);
            foreach (int id in keys) Release(id, replicas[id]);
            removedAt.Clear(); sent.Clear(); newest = 0; cursor = 0;
            if (ArenaVisuals.TryGet(gameObject.scene, out var arena)) arena.Effects.Clear();
        }
        private void LateUpdate()
        {
            if (host) OnlineSession.Current?.PublishState();
            else
            {
                float amount = 1 - Mathf.Exp(-40 * Time.unscaledDeltaTime);
                foreach (var replica in replicas.Values)
                {
                    if (replica.Sprite == null) continue;
                    var current = replica.Sprite.transform.position;
                    if ((current - replica.Target).sqrMagnitude > .00000001f)
                        replica.Sprite.transform.position = Vector3.Lerp(current, replica.Target, amount);
                }
            }
            RecordPerformance();
        }
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD"), System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RecordPerformance()
        {
            if (match == null || !match.IsPlaying)
            {
                performanceFrames = slowFrames = 0;
                performanceTime = longestFrame = 0;
                previousAppliedFrames = AppliedFrames;
                return;
            }
            float delta = Time.unscaledDeltaTime;
            performanceFrames++; performanceTime += delta;
            longestFrame = Mathf.Max(longestFrame, delta);
            if (delta > .05f) slowFrames++;
            if (performanceTime < 5) return;
            FramesPerSecond = Mathf.RoundToInt(performanceFrames / performanceTime);
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, this,
                "CLASSIC_PERF role={0} fps={1} meanMs={2:F2} maxMs={3:F2} framesOver50Ms={4} visuals={5} created={6} receivedPackets={7} pingMs={8} resolution={9}x{10}",
                host ? "host" : "guest", FramesPerSecond, performanceTime * 1000 / performanceFrames,
                longestFrame * 1000, slowFrames, host ? SourceVisualCount : ActiveReplicaCount,
                CreatedReplicaCount, AppliedFrames - previousAppliedFrames, OnlineSession.Current?.Ping ?? 0,
                Screen.width, Screen.height);
            performanceFrames = slowFrames = 0; performanceTime = longestFrame = 0;
            previousAppliedFrames = AppliedFrames;
        }
        private void OnDestroy()
        {
            // Child objects and the bounded pools are destroyed with the scene.
            replicas.Clear(); sent.Clear(); removedAt.Clear(); spritePool.Clear(); linePool.Clear(); trailPool.Clear();
            if (Application.isPlaying)
            {
                if (material != null) Destroy(material);
                if (clippedMaterial != null) Destroy(clippedMaterial);
            }
            else
            {
                if (material != null) DestroyImmediate(material);
                if (clippedMaterial != null) DestroyImmediate(clippedMaterial);
            }
        }
    }
}

