using UnityEngine;

[RequireComponent(typeof(LineRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class ElectricPulseProjectile : ProjectileBase
{
    private LineRenderer ring;
    private CircleCollider2D hitArea;
    private BarrageStep step;
    private float startedAt;
    private static Material material;
    private float pulseRange;
    private bool pull;
    private bool empowered;
    protected override bool DespawnOnHit => false;

    protected override void OnSpawn()
    {
        ring = GetComponent<LineRenderer>();
        hitArea = GetComponent<CircleCollider2D>();
        hitArea.enabled = false;
        hitArea.isTrigger = true;
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        if (material == null) material = new Material(Shader.Find("Sprites/Default"));
        ring.sharedMaterial = material;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 96;
        ring.sortingOrder = 5;
        step = null;
        startedAt = Time.time;
    }

    public void Configure(BarrageStep value)
    {
        step = value;
        pull = Effects.Has(WeaponEffects.PulsePull);
        empowered = Effects.Has(WeaponEffects.PulseDash) && Owner.IsPostDashWindow;
        pulseRange = Stats.Range * (empowered ? .5f : 1f);
        hitArea.enabled = true;
        hitArea.radius = pulseRange;
        Tick(0);
    }

    protected override void Tick(float deltaTime)
    {
        if (step == null) return;
        float age = Time.time - startedAt;
        float progress = Mathf.Clamp01(age / .25f);
        float radius = pull ? Mathf.Lerp(pulseRange, .1f, progress) : Mathf.Lerp(.1f, pulseRange, progress);
        Color color = Stats.Color;
        color.a = Mathf.Clamp01((step.duration - age) / .15f);
        ring.startColor = ring.endColor = color;
        ring.startWidth = ring.endWidth = .055f;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2 / ring.positionCount;
            float jag = .06f * Mathf.Sin(i * 2.3f + age * 55);
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (radius + jag));
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => Hit(other);
    private void OnTriggerStay2D(Collider2D other) => Hit(other);
    private void Hit(Collider2D other)
    {
        if (step == null) return;
        var candidate = other.GetComponent<PlayerCombatant>();
        if (candidate == null || Vector2.Distance(candidate.transform.position, transform.position) > pulseRange) return;
        if (!TryRegisterHit(other, out var target)) return;
        // Range is measured from the target center, not from its collider's nearest edge.
        target.ApplyPulseImpulse(Owner, transform.position, pulseRange, pull, empowered);
        target.ApplyImpact(Vector2.zero, 0, step.slowMultiplier, step.slowDuration);
        if (step.dealsDamage) target.ReceiveHit(Owner);
    }
    protected override void OnDespawn() { step = null; hitArea.enabled = false; }
}
