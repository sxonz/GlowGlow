using UnityEngine;

/// <summary>Orbits a lagging player center, or another orb for the second axis.</summary>
public sealed class OrbitOrb : Bullet
{
    [SerializeField, Min(.1f)] private float orbitRadius = 1.2f;
    [SerializeField] private float degreesPerSecond = 180f;
    [SerializeField, Min(.01f)] private float followSmoothTime = .45f;
    private Vector2 orbitCenter;
    private Vector2 centerVelocity;
    private OrbitOrb orbitParent;
    private OrbitOrb satellite;
    private bool sharedCenter;
    private int axisDepth;
    private int updatedFrame = -1;
    private const int GhostCount = 12;
    private const float GhostInterval = .025f;
    private const float GhostLifetime = .3f;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[GhostCount];
    private readonly Vector3[] ghostPositions = new Vector3[GhostCount];
    private readonly float[] ghostTimes = new float[GhostCount];
    private int nextGhost;
    private float nextGhostAt;
    private float startedAt;
    private float startAngle;
    private bool fourAxis;
    protected override float ArmingDuration => fourAxis ? 1f : 0f;
    protected override bool DespawnOnHit => false;
    protected override bool AllowRepeatedHits => true;
    public override bool IsSmallBullet => false;
    protected override bool CanReflect => false;
    protected override bool FadeOnLifetimeEnd => true;
    protected override void OnBeginDisappear()
    {
        if (satellite != null) satellite.BeginDisappear();
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        fourAxis = false;
        orbitParent = satellite = null;
        sharedCenter = false;
        axisDepth = 0;
        updatedFrame = -1;
        startedAt = Time.time;
        orbitCenter = Owner.transform.position;
        centerVelocity = Vector2.zero;
        startAngle = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        nextGhost = 0;
        nextGhostAt = Time.time;
        for (int i = 0; i < GhostCount; i++)
        {
            if (ghosts[i] == null)
            {
                var ghost = new GameObject("Orb Afterimage " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                ghost.transform.SetParent(transform, false);
                ghost.transform.localScale = Vector3.one * .85f;
                ghost.sprite = RuntimeShapes.Circle;
                ghost.sortingOrder = 3;
                ghosts[i] = ghost;
            }
            ghosts[i].enabled = false;
            ghostTimes[i] = float.NegativeInfinity;
        }
        FollowOwner();
    }

    internal static Vector2 OrbitOffset(float angleDegrees, float radius)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    public void OrbitAround(OrbitOrb primary)
    {
        orbitParent = primary;
        primary.satellite = this;
        axisDepth = primary.axisDepth + 1;
        startedAt = primary.startedAt;
        FollowOwner();
    }

    public void ConfigureFourAxis()
    {
        if (!fourAxis) ExtendLifetime(1f);
        fourAxis = true;
        startAngle = axisDepth % 2 == 0 ? 180f : 0f;
        FollowOwner();
    }

    public void ShareOrbit(OrbitOrb primary)
    {
        orbitParent = primary;
        primary.satellite = this;
        sharedCenter = true;
        startedAt = primary.startedAt;
        startAngle = primary.startAngle + 180f;
        FollowOwner();
    }

    private void FollowOwner()
    {
        if (Owner == null || IsDisappearing) return;
        if (fourAxis) UpdateSpawnGrowth();
        Vector2 center = orbitParent != null
            ? sharedCenter ? orbitParent.orbitCenter : (Vector2)orbitParent.transform.position
            : orbitCenter;
        float elapsed = Mathf.Max(0, Time.time - startedAt - (fourAxis ? 1f : 0f));
        Vector2 position = center +
            OrbitOffset(startAngle + elapsed * degreesPerSecond *
                (Effects.Has(WeaponEffects.OrbSpeed) ? 1.5f : 1f) * (axisDepth + 1),
                orbitRadius * (Effects.Has(WeaponEffects.FourAxisRange) ? 1.5f : 1f));
        if (fourAxis && !AttackReady)
        {
            Vector2 cluster = (Vector2)Owner.transform.position + Vector2.left * .8f + Vector2.up * ((axisDepth - 1.5f) * .12f);
            position = Vector2.Lerp(cluster, position, Mathf.SmoothStep(0, 1, Mathf.Clamp01(Time.time - startedAt)));
        }
        transform.position = position;
    }

    protected override void Tick(float deltaTime) => FollowOwner();

    private void UpdateOrbit(float deltaTime)
    {
        if (updatedFrame == Time.frameCount || Owner == null || IsDisappearing) return;
        updatedFrame = Time.frameCount;
        if (orbitParent != null)
        {
            if (!orbitParent.IsSpawned) { Despawn(); return; }
            // Resolve the first axis first, regardless of Unity's LateUpdate ordering.
            orbitParent.UpdateOrbit(deltaTime);
        }
        else if (deltaTime > 0f)
        {
            // Let movement and dashes leave the center behind instead of snapping it
            // back within a short leash every frame.
            orbitCenter = Vector2.SmoothDamp(orbitCenter, Owner.transform.position, ref centerVelocity,
                Mathf.Max(.01f, followSmoothTime), Mathf.Infinity, deltaTime);
        }
        FollowOwner();
    }

    protected override void LateUpdate()
    {
        UpdateOrbit(Time.deltaTime);
        if (!IsSpawned) return;
        if (!IsDisappearing && Time.time >= nextGhostAt)
        {
            ghostPositions[nextGhost] = transform.position;
            ghostTimes[nextGhost] = Time.time;
            nextGhost = (nextGhost + 1) % GhostCount;
            nextGhostAt = Time.time + GhostInterval;
        }
        for (int i = 0; i < GhostCount; i++)
        {
            float fade = Mathf.Clamp01(1f - (Time.time - ghostTimes[i]) / GhostLifetime);
            ghosts[i].enabled = fade > 0;
            // World positions remain fixed while their parent orb continues to move.
            ghosts[i].transform.position = ghostPositions[i];
            Color color = Stats.Color;
            color.a *= .35f * fade * fade;
            ghosts[i].color = color;
        }
        base.LateUpdate();
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryReflect(other)) base.OnTriggerEnter2D(other);
    }
    private bool TryReflect(Collider2D other)
    {
        return IsSpawned && AttackReady && Effects.Has(WeaponEffects.FourAxisReflection) &&
            other.TryGetComponent<Bullet>(out var bullet) && bullet.Reflect(Owner);
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!TryReflect(other)) TryHit(other);
    }
    protected override void OnDespawn()
    {
        if (satellite != null) satellite.Despawn();
        if (orbitParent != null && orbitParent.satellite == this) orbitParent.satellite = null;
        orbitParent = satellite = null;
        updatedFrame = -1;
        centerVelocity = Vector2.zero;
        foreach (var ghost in ghosts) if (ghost != null) ghost.enabled = false;
    }
}
