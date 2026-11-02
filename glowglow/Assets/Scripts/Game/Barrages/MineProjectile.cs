using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Pooled, host-authoritative floor trap. All artwork uses prepared sprite assets.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class MineProjectile : ProjectileBase
{
    public const float FuseSeconds = 20f;
    public const int MaxHealth = 3;
    public const float BlastRadius = 1.5f;
    public int Health { get; private set; }
    private CircleCollider2D sensor;
    private SpriteRenderer core;
    private Vector2 velocity;
    private Rect bounds;
    private bool detonating;
    // Warning material contains its own ring glow; guests use this same shared style.
    protected override bool GlowEnabled => false;
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[32];
    private static readonly BarrageStep Blast = new()
    {
        shape = BarrageShape.Circle, position = BarragePosition.Player,
        dimensions = Vector2.one, duration = .3f, startSize = 1, peakSize = 1,
        endSize = 0, shrinkTime = .18f, flash = true, dealsDamage = true
    };

    protected override void OnSpawn()
    {
        Health = MaxHealth;
        detonating = false;
        velocity = Vector2.zero;
        var visual = GetComponent<SpriteRenderer>();
        visual.sprite = RuntimeShapes.Circle;
        visual.sharedMaterial = NeonVisualStyle.Get(NeonVisualStyle.WarningMode);
        visual.color = Stats.Color;
        visual.sortingOrder = 3;
        transform.localScale = Vector3.one * (Stats.Radius * 2);
        sensor = GetComponent<CircleCollider2D>();
        sensor.isTrigger = true;
        sensor.radius = .5f;
        sensor.enabled = true;
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0;
        body.useFullKinematicContacts = true;
        if (core == null)
        {
            core = new GameObject("Mine Core").AddComponent<SpriteRenderer>();
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * .38f;
            core.transform.localRotation = Quaternion.Euler(0, 0, 45);
            core.sprite = RuntimeShapes.Square;
            core.sortingOrder = 4;
        }
        core.color = Color.Lerp(Stats.Color, Color.white, .55f);
        Vector2 half = Owner.ArenaHalfSize;
        bounds = ArenaVisuals.TryGet(gameObject.scene, out var arena) ? arena.Bounds : new Rect(-half, half * 2);
        transform.position = Clamp(transform.position);
    }

    protected override void Tick(float deltaTime) { }

    private void FixedUpdate()
    {
        if (!IsSpawned || detonating || Owner == null || !Owner.CanSelectWeapon || velocity.sqrMagnitude < .0001f) return;
        // Analytic drag keeps pulse travel independent of rendering frame rate.
        float decay = Mathf.Exp(-7f * Time.fixedDeltaTime);
        Vector2 from = transform.position;
        Vector2 to = Clamp(from + velocity * ((1 - decay) / 7f));
        velocity *= decay;
        Vector2 travel = to - from;
        if (travel.sqrMagnitude < .000001f) { velocity = Vector2.zero; return; }
        int count = gameObject.scene.GetPhysicsScene2D().CircleCast(from, Stats.Radius,
            travel.normalized, travel.magnitude, new ContactFilter2D { useTriggers = true }, sweepHits);
        for (int i = 0; i < count; i++)
        {
            if (!IsEnemy(sweepHits[i].collider)) continue;
            transform.position = from + travel.normalized * sweepHits[i].distance;
            Detonate();
            return;
        }
        transform.position = to;
    }

    private Vector2 Clamp(Vector2 p) => new(
        Mathf.Clamp(p.x, bounds.xMin + Stats.Radius, bounds.xMax - Stats.Radius),
        Mathf.Clamp(p.y, bounds.yMin + Stats.Radius, bounds.yMax - Stats.Radius));

    internal void ReceiveAttack()
    {
        if (!IsSpawned || detonating) return;
        if (--Health <= 0) Detonate();
    }

    internal void ApplyPulse(Vector2 origin, float range, bool pull, bool empowered)
    {
        if (!IsSpawned || detonating) return;
        Vector2 away = (Vector2)transform.position - origin;
        float distance = away.magnitude;
        if (distance > range) return;
        Vector2 direction = distance > .001f ? away / distance : Vector2.right;
        float travel = empowered ? 3.5f : 2.4f;
        if (pull) { direction = -direction; travel = Mathf.Min(travel, distance); }
        velocity = direction * (travel * 7f);
    }

    private bool IsEnemy(Collider2D other) => other != null &&
        other.TryGetComponent<PlayerCombatant>(out var player) && player != Owner &&
        player.CanSelectWeapon && !player.IsMeteorDiving;

    private void OnTriggerEnter2D(Collider2D other) => CheckContact(other);
    private void OnTriggerStay2D(Collider2D other) => CheckContact(other);
    private void CheckContact(Collider2D other)
    {
        if (IsSpawned && !detonating && IsEnemy(other)) Detonate();
    }

    protected override void OnLifetimeExpired() => Detonate();

    private void Detonate()
    {
        if (!IsSpawned || detonating) return;
        detonating = true;
        sensor.enabled = false;
        var blast = SpawnBuiltin<BarrageShapeProjectile>(Owner, transform.position, Vector2.right,
            new WeaponStats(Stats.Cooldown, 0, Blast.duration, BlastRadius, BlastRadius, Stats.Color, Stats.Damage));
        blast.Configure(Blast);
        blast.ClipToArena(bounds);
        Despawn();
    }

    protected override void OnDespawn()
    {
        sensor.enabled = false;
        velocity = Vector2.zero;
    }
}
