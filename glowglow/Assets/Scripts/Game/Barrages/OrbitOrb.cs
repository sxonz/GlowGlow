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
    protected override bool DespawnOnHit => false;
    protected override bool AllowRepeatedHits => true;
    public override bool IsSmallBullet => false;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        orbitParent = satellite = null;
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
        FollowOwner();
    }

    private void FollowOwner()
    {
        if (Owner == null) return;
        Vector2 center = orbitParent != null ? (Vector2)orbitParent.transform.position : orbitCenter;
        transform.position = center +
            OrbitOffset(startAngle + (Time.time - startedAt) * degreesPerSecond *
                (Effects.Has(WeaponEffects.OrbSpeed) ? 1.5f : 1f) * (orbitParent != null ? 2f : 1f), orbitRadius);
    }

    protected override void Tick(float deltaTime) => FollowOwner();

    private void UpdateOrbit(float deltaTime)
    {
        if (updatedFrame == Time.frameCount || Owner == null) return;
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
        if (Time.time >= nextGhostAt)
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

    private void OnTriggerStay2D(Collider2D other) => TryHit(other);
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
