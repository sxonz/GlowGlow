using UnityEngine;

/// <summary>One damaging orb orbiting its owner; ghosts reuse the imported circle PNG.</summary>
public sealed class OrbitOrb : Bullet
{
    [SerializeField, Min(.1f)] private float orbitRadius = 1.2f;
    [SerializeField] private float degreesPerSecond = 180f;
    [SerializeField, Min(.01f)] private float followSmoothTime = .1f;
    private Vector2 orbitCenter;
    private Vector2 centerVelocity;
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

    private void FollowOwner()
    {
        if (Owner == null) return;
        transform.position = orbitCenter +
            OrbitOffset(startAngle + (Time.time - startedAt) * degreesPerSecond *
                (Effects.Has(WeaponEffects.OrbSpeed) ? 1.5f : 1f), orbitRadius);
    }

    protected override void Tick(float deltaTime) => FollowOwner();
    protected override void LateUpdate()
    {
        // Integrate once, after owner movement; orbit phase remains independent of follow momentum.
        if (Owner != null && Time.deltaTime > 0f)
        {
            orbitCenter = Vector2.SmoothDamp(orbitCenter, Owner.transform.position, ref centerVelocity,
                Mathf.Max(.01f, followSmoothTime), Mathf.Infinity, Time.deltaTime);
            orbitCenter = (Vector2)Owner.transform.position + Vector2.ClampMagnitude(orbitCenter - (Vector2)Owner.transform.position, .8f);
        }
        FollowOwner();
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
        centerVelocity = Vector2.zero;
        foreach (var ghost in ghosts) if (ghost != null) ghost.enabled = false;
    }
}
