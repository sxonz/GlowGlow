using System.Collections.Generic;
using UnityEngine;

/// <summary>A comet traces one authored constellation, accelerating from star to star.</summary>
public sealed class ConstellationIgnition : ProjectileBase
{
    public const float WarningTime = 0f;
    public const float BurnTime = .1f;
    public const float AfterglowTime = .4f;
    public const float FirstTraceTime = .11f;
    public const float LastTraceTime = .05f;
    public const float RevelationTime = 1.25f;
    public const float FigureArmTime = .16f;
    public const float FigureDamageEnd = .85f;
    public const float Lifetime = WarningTime + (MaxStars - 1) * (FirstTraceTime + LastTraceTime) * .5f + RevelationTime;
    public const float BeamWidth = .18f;
    public const int MaxStars = 22;
    public static readonly string[] PatternNames = { "Hydra", "Eridanus", "Draco" };

    // Simplified, fixed silhouettes based on the IAU constellation charts (see Tests/CELESTIAL_BARRAGES.md).
    // A repeated point closes the head before the long body continues.
    private static readonly Vector2[][] Patterns =
    {
        new[] { new Vector2(-4.2f, .7f), new Vector2(-5, 1), new Vector2(-4.8f, 1.7f), new Vector2(-4.1f, 1.8f),
            new Vector2(-3.7f, 1.2f), new Vector2(-4.2f, .7f), new Vector2(-3.2f, -.1f), new Vector2(-2.6f, .3f),
            new Vector2(-1.5f, -.5f), new Vector2(-.4f, -.1f), new Vector2(.6f, -1),
            new Vector2(2, -.8f), new Vector2(3.3f, -1.6f), new Vector2(5, -1.9f) },
        new[] { new Vector2(-4.8f, 2), new Vector2(-3.8f, 1.7f), new Vector2(-2.6f, 2),
            new Vector2(-1.5f, 1.4f), new Vector2(-2.1f, .8f), new Vector2(-3.2f, .65f),
            new Vector2(-4, .2f), new Vector2(-3.6f, -.55f), new Vector2(-2.4f, -.8f),
            new Vector2(-1.2f, -.5f), new Vector2(-.2f, -.9f), new Vector2(.5f, -.3f),
            new Vector2(1.2f, .4f), new Vector2(2, .1f), new Vector2(2.4f, -.65f),
            new Vector2(1.8f, -1.25f), new Vector2(2.5f, -1.8f), new Vector2(3.4f, -1.4f),
            new Vector2(4.1f, -2), new Vector2(5, -1.7f) },
        new[] { new Vector2(-5, -1.5f), new Vector2(-3.9f, -.9f), new Vector2(-2.7f, -.6f),
            new Vector2(-1.5f, .4f), new Vector2(-.4f, 1.3f), new Vector2(1, 1.8f),
            new Vector2(2.4f, 1.5f), new Vector2(2.8f, .6f), new Vector2(1.6f, .1f),
            new Vector2(.7f, -.7f), new Vector2(2, -1.6f), new Vector2(3.6f, -1.2f),
            new Vector2(4.5f, -.6f), new Vector2(5, -1.35f), new Vector2(4.2f, -2),
            new Vector2(3.6f, -1.2f) }
    };
    // Editor baking uses the same authored coordinates as the attack.
    public static int AuthoredStarCount(int pattern) => Patterns[pattern].Length;
    public static Vector2 AuthoredStarPosition(int pattern, int star) => Patterns[pattern][star];
    private static readonly BarrageStep Warning = new()
    {
        shape = BarrageShape.Line, position = BarragePosition.Gun, dealsDamage = false,
        duration = Lifetime, opacity = .25f, startSize = BeamWidth / .025f
    };
    private static readonly BarrageStep Beam = new()
    {
        shape = BarrageShape.Box, position = BarragePosition.Gun, dealsDamage = true,
        duration = BurnTime, dimensions = new Vector2(1, BeamWidth), flash = true
    };

    private readonly Vector2[] points = new Vector2[MaxStars];
    private readonly SpriteRenderer[] stars = new SpriteRenderer[MaxStars];
    private readonly BarrageShapeProjectile[] segments = new BarrageShapeProjectile[MaxStars - 1];
    private readonly ulong[] segmentIds = new ulong[MaxStars - 1];
    private readonly float[] ignitionTimes = new float[MaxStars - 1];
    private readonly float[] traceTimes = new float[MaxStars - 1];
    private readonly float[] arrivalTimes = new float[MaxStars];
    private readonly float[] pulseTimes = new float[MaxStars];
    private readonly int[] starIndices = new int[MaxStars];
    private readonly SpriteRenderer[] comet = new SpriteRenderer[3];
    private SpriteRenderer cometGlow;
    private SpriteRenderer revelation;
    private readonly LineRenderer[] afterglow = new LineRenderer[2];
    private static Material afterglowMaterial;
    private ConstellationFigureMask figureMask;
    private readonly List<PlayerCombatant> figurePlayers = new(4);
    private readonly List<FigureTarget> figureTargets = new(4);
    private struct FigureTarget
    {
        public PlayerCombatant Player;
        public CircleCollider2D Collider;
        public Vector2 Previous;
        public bool WasVulnerable;
    }
    private float previousHitAge, figureScale;
    private int afterglowCount;
    private int count;
    private int warningIndex, ignited, arrived;
    public int PatternIndex { get; private set; }
    public int StarCount => count;
    public float EndTime { get; private set; }
    public float CompletionTime => arrivalTimes[count - 1];
    public int IgnitedSegments => ignited;
    public Vector2 StarPosition(int index) => points[index];
    public float IgnitionTime(int index) => ignitionTimes[index];
    public float TraceTime(int index) => traceTimes[index];
    public bool FigureDealsDamage => IsSpawned && !IsDisappearing &&
        Time.time - SpawnedAt >= CompletionTime + FigureArmTime &&
        Time.time - SpawnedAt < CompletionTime + FigureDamageEnd;
    protected override bool DespawnOnHit => false;

    protected override void OnSpawn()
    {
        warningIndex = -1;
        ignited = arrived = 0;
        PatternIndex = Random.Range(0, Patterns.Length);
        var pattern = Patterns[PatternIndex];
        count = pattern.Length;
        float ignition = WarningTime;
        arrivalTimes[0] = WarningTime;
        for (int i = 0; i < count - 1; i++)
        {
            ignitionTimes[i] = ignition;
            traceTimes[i] = Mathf.Lerp(FirstTraceTime, LastTraceTime, (float)i / (count - 2));
            ignition += traceTimes[i];
            arrivalTimes[i + 1] = ignition;
        }
        EndTime = ignition + RevelationTime;
        Rect bounds = CelestialBarrageGeometry.ArenaBounds(Owner);
        Vector2 axis = Direction;
        Vector2 lateral = new(-axis.y, axis.x);
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        for (int i = 0; i < count; i++)
        {
            points[i] = axis * pattern[i].x + lateral * pattern[i].y;
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }
        Vector2 size = max - min;
        float scale = Mathf.Min(Stats.Range / Mathf.Max(size.x, size.y),
            (bounds.width - 1.2f) / size.x, (bounds.height - 1.2f) / size.y);
        // Anchor the authored first star to the firing muzzle. Re-centering to fit the
        // arena would move the launch point; every visual and hit mask shares this offset.
        Vector2 origin = transform.position;
        Vector2 offset = points[0];
        if (revelation == null)
        {
            revelation = new GameObject("Constellation Revelation", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            revelation.transform.SetParent(transform, false);
            revelation.sortingOrder = 0;
        }
        revelation.sprite = RuntimeShapes.ConstellationFigure(PatternIndex);
        revelation.transform.SetPositionAndRotation(origin - offset * scale,
            Quaternion.Euler(0, 0, Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg));
        revelation.transform.localScale = new Vector3(scale, scale, 1);
        revelation.enabled = false;
        figureScale = scale;
        figureMask = ConstellationFigureMask.Load(PatternIndex);
        CelestialBarrageGeometry.CollectPlayers(Owner, figurePlayers);
        figureTargets.Clear();
        foreach (var player in figurePlayers)
        {
            var collider = player.GetComponent<CircleCollider2D>();
            figureTargets.Add(new FigureTarget { Player = player, Collider = collider,
                Previous = collider.transform.TransformPoint(collider.offset), WasVulnerable = false });
        }
        figurePlayers.Clear();
        previousHitAge = 0;
        afterglowCount = 0;
        if (afterglowMaterial == null) afterglowMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < afterglow.Length; i++)
        {
            if (afterglow[i] == null)
            {
                afterglow[i] = new GameObject(i == 0 ? "Constellation Trail Glow" : "Constellation Trail",
                    typeof(LineRenderer)).GetComponent<LineRenderer>();
                afterglow[i].transform.SetParent(transform, false);
                afterglow[i].sharedMaterial = afterglowMaterial;
                afterglow[i].useWorldSpace = true;
                afterglow[i].numCornerVertices = 3;
                afterglow[i].numCapVertices = 3;
                afterglow[i].sortingOrder = i + 1;
                afterglow[i].startWidth = afterglow[i].endWidth = i == 0 ? .12f : .03f;
            }
            afterglow[i].positionCount = 0;
            afterglow[i].enabled = false;
        }
        for (int i = 0; i < stars.Length; i++)
        {
            if (i >= count) { if (stars[i] != null) stars[i].enabled = false; continue; }
            points[i] = origin + (points[i] - offset) * scale;
            if (stars[i] == null)
            {
                stars[i] = new GameObject("Constellation Star", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                stars[i].transform.SetParent(transform, false);
                stars[i].sprite = RuntimeShapes.Circle;
                stars[i].sharedMaterial = NeonVisualStyle.Get(NeonVisualStyle.BulletMode);
                stars[i].sortingOrder = 5;
            }
            starIndices[i] = i;
            for (int j = 0; j < i; j++)
                if (pattern[j] == pattern[i]) { starIndices[i] = j; break; }
            pulseTimes[i] = float.NegativeInfinity;
            stars[i].enabled = false;
            stars[i].transform.position = points[i];
            stars[i].transform.localScale = Vector3.one * .15f;
            stars[i].color = Stats.Color;
        }
        for (int i = 0; i < comet.Length; i++)
        {
            if (comet[i] == null)
            {
                comet[i] = new GameObject(i == 0 ? "Constellation Comet" : "Comet Echo", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                comet[i].transform.SetParent(transform, false);
                comet[i].sprite = RuntimeShapes.Circle;
                comet[i].sharedMaterial = NeonVisualStyle.Get(NeonVisualStyle.BulletMode);
                comet[i].sortingOrder = 8 - i;
            }
            comet[i].enabled = false;
            comet[i].transform.localScale = Vector3.one * (.3f - i * .085f);
        }
        if (cometGlow == null) cometGlow = RuntimeShapes.CreateGlow(comet[0]);
        cometGlow.enabled = false;
        Tick(0);
    }

    private void EmitSegment(int index, bool damage, float age)
    {
        RemoveSegment(index);
        float remaining = ignitionTimes[index] - age + (damage ? traceTimes[index] + BurnTime : 0);
        // Never replay an expired damaging segment after a hitch.
        if (remaining <= 0) return;
        Vector2 delta = points[index + 1] - points[index];
        var stats = new WeaponStats(Stats.Cooldown, 0, remaining, .5f, delta.magnitude, Stats.Color, BarrageDamage.ConstellationTrace);
        segments[index] = SpawnBuiltin<BarrageShapeProjectile>(Owner, points[index], delta, stats);
        segmentIds[index] = segments[index].SpawnId;
        if (damage) segments[index].ConfigureTrace(Beam, traceTimes[index], age - ignitionTimes[index]);
        else segments[index].Configure(Warning);
    }

    protected override void Tick(float deltaTime)
    {
        float age = Time.time - SpawnedAt;
        if (age >= EndTime) { Despawn(); return; }
        while (ignited < count - 1 && age >= ignitionTimes[ignited])
        {
            EmitSegment(ignited, true, age);
            ignited++;
        }
        // Only the very next segment is readable. Its warning is replaced by its beam.
        if (ignited < count - 1 && warningIndex != ignited)
        {
            EmitSegment(ignited, false, age);
            warningIndex = ignited;
        }
        while (arrived < count && age >= arrivalTimes[arrived])
        {
            pulseTimes[starIndices[arrived]] = arrivalTimes[arrived];
            if (age - arrivalTimes[arrived] < .1f)
                CombatVfx.Burst(gameObject.scene, points[arrived], Stats.Color, arrived == count - 1 ? .8f : .35f, BarrageVisibility.OwnerTag(Owner));
            arrived++;
        }
        for (int i = 0; i < ignited; i++)
        {
            var segment = segments[i];
            if (segment != null && segment.IsSpawned && segment.SpawnId == segmentIds[i] &&
                age >= ignitionTimes[i] + traceTimes[i] + BurnTime) segment.BeginDisappear();
        }
        UpdateStars(age);
        UpdateComet(age);
        UpdateRevelation(age);
        UpdateAfterglow(age);
    }

    private void UpdateStars(float age)
    {
        for (int i = 0; i < count; i++)
        {
            if (starIndices[i] != i) continue;
            bool upcoming = ignited < count - 1 &&
                (starIndices[ignited] == i || starIndices[ignited + 1] == i);
            float sincePulse = age - pulseTimes[i];
            float pulse = Mathf.Clamp01(1 - sincePulse / .3f);
            float charge = age < arrivalTimes[i] ? Mathf.Clamp01(1 - (arrivalTimes[i] - age) / .25f) : 0;
            var color = Color.Lerp(Stats.Color, Color.white, pulse * .95f + charge * .2f);
            color.a = Mathf.Max(upcoming ? .4f + charge * .5f : 0,
                sincePulse >= 0 && !float.IsInfinity(sincePulse) ? Mathf.Max(.3f, 1 - sincePulse / .48f) : 0);
            color.a *= FigureFade(age);
            stars[i].enabled = color.a > .005f;
            if (!stars[i].enabled) continue;
            if (stars[i].color != color) stars[i].color = color;
            float size = .12f + pulse * .24f + charge * .08f;
            if (stars[i].transform.localScale.x != size) stars[i].transform.localScale = Vector3.one * size;
        }
    }

    private void UpdateRevelation(float age)
    {
        float elapsed = age - CompletionTime;
        revelation.enabled = elapsed > 0 && elapsed < RevelationTime;
        if (!revelation.enabled) return;
        float reveal = Mathf.SmoothStep(0, 1, elapsed / FigureArmTime);
        var color = new Color(1, 1, 1, .62f * reveal * FigureFade(age));
        if (revelation.color != color) revelation.color = color;
    }

    private float FigureFade(float age) => 1 - Mathf.SmoothStep(0, 1,
        (age - CompletionTime - FigureDamageEnd) / (RevelationTime - FigureDamageEnd));

    private void UpdateAfterglow(float age)
    {
        int length = Mathf.Min(count, ignited + 1);
        for (int i = 0; i < afterglow.Length; i++)
        {
            var line = afterglow[i];
            line.enabled = length >= 2;
            if (length != afterglowCount)
            {
                line.positionCount = length;
                for (int j = 0; j < length - 1; j++) line.SetPosition(j, points[j]);
            }
            if (length > 0) line.SetPosition(length - 1, TracePosition(age));
            Color color = Color.Lerp(Stats.Color, Color.white, i == 0 ? .15f : .6f);
            color.a = (i == 0 ? .14f : .38f) * FigureFade(age);
            if (line.startColor != color) line.startColor = line.endColor = color;
        }
        afterglowCount = length;
    }

    // Constant-time distance queries use a prepared mask, never a filled bounding polygon.
    public float FigureClearance(Vector2 position, float bodyRadius) =>
        figureMask.Distance(revelation.transform.InverseTransformPoint(position)) * figureScale - bodyRadius;

    public float DangerClearance(Vector2 position, float bodyRadius)
    {
        float elapsed = Time.time - SpawnedAt - CompletionTime;
        return !IsSpawned || IsDisappearing || elapsed <= 0 || elapsed >= FigureDamageEnd
            ? float.PositiveInfinity : FigureClearance(position, bodyRadius);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (IsSpawned && !IsDisappearing && Time.deltaTime > 0) CheckFigureHits();
    }

    private void CheckFigureHits()
    {
        float age = Time.time - SpawnedAt;
        bool damaging = FigureDealsDamage;
        float activeFraction = age > previousHitAge
            ? Mathf.Clamp01((CompletionTime + FigureArmTime - previousHitAge) / (age - previousHitAge)) : 1;
        for (int i = 0; i < figureTargets.Count; i++)
        {
            var tracked = figureTargets[i];
            if (tracked.Player == null || tracked.Collider == null) continue;
            var collider = tracked.Collider;
            Vector2 current = collider.transform.TransformPoint(collider.offset);
            bool vulnerable = collider.enabled && collider.gameObject.activeInHierarchy &&
                tracked.Player != Owner && tracked.Player.CanBeHit && tracked.Player.gameObject.scene == gameObject.scene;
            if (damaging && vulnerable)
            {
                Vector2 from = tracked.WasVulnerable ? Vector2.Lerp(tracked.Previous, current, activeFraction) : current;
                float radius = collider.radius * Mathf.Max(Mathf.Abs(collider.transform.lossyScale.x),
                    Mathf.Abs(collider.transform.lossyScale.y)) / figureScale;
                if (figureMask.IntersectsSweep(revelation.transform.InverseTransformPoint(from),
                    revelation.transform.InverseTransformPoint(current), radius)) TryHit(collider);
            }
            tracked.Previous = current;
            tracked.WasVulnerable = vulnerable;
            figureTargets[i] = tracked;
        }
        previousHitAge = age;
    }

    public Vector2 TracePosition(float age)
    {
        for (int i = count - 2; i >= 0; i--)
            if (age >= ignitionTimes[i])
                return Vector2.Lerp(points[i], points[i + 1], (age - ignitionTimes[i]) / traceTimes[i]);
        return points[0];
    }

    private void UpdateComet(float age)
    {
        for (int i = 0; i < comet.Length; i++)
        {
            float sample = age - i * .0275f;
            comet[i].enabled = sample >= WarningTime && sample <= arrivalTimes[count - 1];
            if (!comet[i].enabled) continue;
            comet[i].transform.position = TracePosition(sample);
            var color = Color.Lerp(Stats.Color, Color.white, i == 0 ? .9f : .4f);
            color.a = 1 - i * .32f;
            if (comet[i].color != color) comet[i].color = color;
        }
        RuntimeShapes.SyncGlow(comet[0], cometGlow, .65f, .55f);
    }

    private void RemoveSegment(int index)
    {
        var segment = segments[index];
        if (segment != null && segment.IsSpawned && segment.SpawnId == segmentIds[index]) segment.RemoveProjectile();
        segments[index] = null;
    }

    protected override void OnDespawn()
    {
        for (int i = 0; i < segments.Length; i++) RemoveSegment(i);
        figureTargets.Clear();
        if (revelation != null) revelation.enabled = false;
        foreach (var line in afterglow) if (line != null) line.enabled = false;
    }
}
