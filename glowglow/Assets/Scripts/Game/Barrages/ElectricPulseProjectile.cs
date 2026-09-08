using UnityEngine;

[RequireComponent(typeof(LineRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class ElectricPulseProjectile : ProjectileBase
{
    private LineRenderer ring;
    private CircleCollider2D hitArea;
    private BarrageStep step;
    private float startedAt;
    private static Material material;
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
        // Control effects still need collision detection when damage is disabled.
        hitArea.enabled = value.dealsDamage || value.knockbackDistance > 0 || value.slowDuration > 0;
        Tick(0);
    }

    protected override void Tick(float deltaTime)
    {
        if (step == null) return;
        float age = Time.time - startedAt;
        float radius = Mathf.Lerp(.1f, Stats.Range, Mathf.Clamp01(age / .25f));
        hitArea.radius = radius;
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
        if (step != null) TryHit(other, step.knockbackDistance, step.slowMultiplier, step.slowDuration, step.dealsDamage);
    }
    protected override void OnDespawn() { step = null; hitArea.enabled = false; }
}
