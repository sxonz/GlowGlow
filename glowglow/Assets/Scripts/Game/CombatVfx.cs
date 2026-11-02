using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Local cosmetic effects with a fixed, prewarmed budget. Never part of network capture or physics.</summary>
public sealed class CombatVfx : MonoBehaviour
{
    public const int Capacity = 128;
    private const int EventsPerSecond = 24, EventsPerFrame = 3;
    private struct Particle
    {
        public Vector2 Position, Velocity, Size;
        public Color Color;
        public float Born, Lifetime, Growth, Rotation, Spin;
    }
    private readonly SpriteRenderer[] renderers = new SpriteRenderer[Capacity];
    private readonly Particle[] particles = new Particle[Capacity];
    private readonly int[] active = new int[Capacity], free = new int[Capacity];
    private Material additive, ring, shard;
    private int count, freeCount, eventFrame = -1, frameEvents;
    private float tokens = EventsPerSecond, lastEmission;
    private bool initialized;
    private byte emittingOwner;
    public int ActiveCount => count;
    public int PeakCount { get; private set; }
    public int DroppedEvents { get; private set; }

    public void Initialize(Rect bounds)
    {
        if (initialized) return;
        initialized = true;
        additive = new Material(Resources.Load<Shader>("NeonImpact"));
        ring = new Material(additive); ring.SetFloat("_Ring", 1);
        shard = new Material(additive); shard.SetFloat("_Ring", 2);
        var clip = new Vector4(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
        additive.SetVector("_ClipRect", clip); ring.SetVector("_ClipRect", clip); shard.SetVector("_ClipRect", clip);
        for (int i = 0; i < Capacity; i++)
        {
            var renderer = new GameObject("Local Impact", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sharedMaterial = additive;
            renderer.sprite = RuntimeShapes.SoftGlow;
            renderer.enabled = false;
            renderers[i] = renderer;
            free[freeCount++] = i;
        }
    }

    public static void Burst(Scene scene, Vector2 position, Color color, float strength = 1, byte owner = 0)
    {
        if (ArenaVisuals.TryGet(scene, out var arena)) arena.Effects.Emit(position, color, strength, owner);
    }

    public void Emit(Vector2 position, Color color, float strength = 1, byte owner = 0)
    {
        if (!initialized || Time.timeScale <= 0 || GraphicsTheme.IsClassic) return;
        float now = Time.time;
        tokens = Mathf.Min(EventsPerSecond, tokens + Mathf.Max(0, now - lastEmission) * EventsPerSecond);
        lastEmission = now;
        if (eventFrame != Time.frameCount) { eventFrame = Time.frameCount; frameEvents = 0; }
        if (frameEvents >= EventsPerFrame || tokens < 1 || freeCount < 3) { DroppedEvents++; return; }
        frameEvents++; tokens--;
        emittingOwner = owner;
        strength = Mathf.Clamp(strength, .25f, 2);
        // Decorative load degrades independently of hitboxes, telegraphs, and network state.
        int shards = Time.smoothDeltaTime > 1f / 45f || freeCount < 12 ? 2 : 6;
        Add(position, Vector2.zero, Vector2.one * .3f * strength, Color.Lerp(color, Color.white, .8f), .11f, 2.5f, RuntimeShapes.SoftGlow, false, 9);
        Add(position, Vector2.zero, Vector2.one * .32f * strength, color, .32f, 6, RuntimeShapes.Circle, true, 3);
        var floorColor = color; floorColor.a = .24f;
        Add(position, Vector2.zero, Vector2.one * 1.8f * strength, floorColor, .38f, .35f, RuntimeShapes.SoftGlow, false, -10);
        // A local hash avoids changing Unity's gameplay random sequence.
        float phase = Mathf.Repeat(position.x * 17.13f + position.y * 31.7f, Mathf.PI * 2);
        for (int i = 0; i < shards; i++)
        {
            float angle = phase + i * Mathf.PI * 2 / shards;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var tint = Color.Lerp(color, i % 2 == 0 ? Color.cyan : new Color(1, .3f, .85f), .4f);
            Add(position, direction * (1.6f + i * .17f) * strength, new Vector2(.07f, .2f) * strength,
                tint, .24f + i * .018f, -.7f, RuntimeShapes.Square, false, 7, angle * Mathf.Rad2Deg - 90, (i % 2 == 0 ? 1 : -1) * 120);
        }
    }

    private void Add(Vector2 position, Vector2 velocity, Vector2 size, Color color, float lifetime, float growth,
        Sprite sprite, bool isRing, int order, float rotation = 0, float spin = 0)
    {
        if (freeCount == 0) return;
        int index = free[--freeCount];
        active[count++] = index;
        PeakCount = Mathf.Max(PeakCount, count);
        particles[index] = new Particle { Position = position, Velocity = velocity, Size = size, Color = color,
            Born = Time.time, Lifetime = lifetime, Growth = growth, Rotation = rotation, Spin = spin };
        var renderer = renderers[index];
        renderer.sprite = sprite; renderer.sharedMaterial = isRing ? ring : order == 7 ? shard : additive;
        renderer.sortingOrder = order; renderer.color = color;
        BarrageVisibility.ApplyOwner(renderer, emittingOwner);
        renderer.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, rotation));
        renderer.transform.localScale = new Vector3(size.x, size.y, 1);
        renderer.enabled = true;
    }
    private void LateUpdate()
    {
        if (count == 0 || Time.deltaTime <= 0) return;
        for (int n = count - 1; n >= 0; n--)
        {
            int index = active[n];
            ref var particle = ref particles[index];
            float age = Time.time - particle.Born;
            float t = age / particle.Lifetime;
            var renderer = renderers[index];
            if (t >= 1)
            {
                renderer.enabled = false;
                active[n] = active[--count]; free[freeCount++] = index;
                continue;
            }
            float ease = 1 - (1 - t) * (1 - t);
            renderer.transform.SetPositionAndRotation(particle.Position + particle.Velocity * age * (1 - t * .45f),
                Quaternion.Euler(0, 0, particle.Rotation + age * particle.Spin));
            Vector2 size = particle.Size * (1 + particle.Growth * ease);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1);
            var color = particle.Color; color.a *= (1 - t) * (1 - t); renderer.color = color;
        }
    }
    public void Clear()
    {
        while (count > 0) { int index = active[--count]; renderers[index].enabled = false; free[freeCount++] = index; }
    }
    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        if (Application.isPlaying) { Destroy(additive); Destroy(ring); Destroy(shard); }
        else { DestroyImmediate(additive); DestroyImmediate(ring); DestroyImmediate(shard); }
    }
}
