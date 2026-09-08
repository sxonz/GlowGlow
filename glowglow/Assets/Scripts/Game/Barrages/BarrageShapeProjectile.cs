using UnityEngine;

/// <summary>Reusable non-moving telegraph or damaging shape, with independent visual flash.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(CircleCollider2D))]
public sealed class BarrageShapeProjectile : ProjectileBase
{
    private static Sprite square;
    private SpriteRenderer visual;
    private SpriteRenderer flashVisual;
    private BoxCollider2D box;
    private CircleCollider2D circle;
    private BarrageStep step;
    private Vector2 baseDimensions;
    private float startedAt;
    private Quaternion initialRotation;
    protected override bool DespawnOnHit => false;
    protected override bool GlowEnabled => step != null && step.shape != BarrageShape.Line;

    protected override void OnSpawn()
    {
        visual = GetComponent<SpriteRenderer>();
        box = GetComponent<BoxCollider2D>();
        circle = GetComponent<CircleCollider2D>();
        box.enabled = circle.enabled = false;
        box.isTrigger = circle.isTrigger = true;
        box.size = Vector2.one;
        circle.radius = .5f;
        var body = GetComponent<Rigidbody2D>();
        if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0;
        body.useFullKinematicContacts = true;
        if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
        if (flashVisual == null)
        {
            flashVisual = new GameObject("Flash outside hitbox").AddComponent<SpriteRenderer>();
            flashVisual.transform.SetParent(transform, false);
            flashVisual.sortingOrder = 6;
        }
        flashVisual.enabled = false;
        step = null;
        startedAt = Time.time;
    }

    public void Configure(BarrageStep value)
    {
        step = value;
        bool round = step.shape == BarrageShape.Circle;
        visual.sprite = round ? RuntimeShapes.Circle : square;
        visual.sortingOrder = step.dealsDamage ? 4 : 2;
        baseDimensions = step.dimensions * (Stats.Radius * 2);
        if (step.shape == BarrageShape.Line)
        {
            baseDimensions = new Vector2(Stats.Range, .025f);
            transform.position += (Vector3)(Direction * Stats.Range * .5f);
        }
        if (step.shape == BarrageShape.Line || step.position == BarragePosition.Gun)
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        if (step.shape == BarrageShape.Box && step.position == BarragePosition.Gun)
        {
            // Gun-mounted rectangles begin at the muzzle; dimensions are world units for beams.
            baseDimensions = new Vector2(Stats.Range, step.dimensions.y);
            transform.position += (Vector3)(Direction * baseDimensions.x * .5f);
        }
        circle.enabled = step.dealsDamage && round;
        box.enabled = step.dealsDamage && !round;
        flashVisual.sprite = visual.sprite;
        flashVisual.transform.localScale = Vector3.one * 1.15f;
        initialRotation = transform.rotation;
        Tick(0);
    }

    protected override void Tick(float deltaTime)
    {
        if (step == null) return;
        float age = Time.time - startedAt;
        float size = Mathf.Max(0, step.SizeAt(age));
        // Absolute angle avoids frame-rate drift; the collider rotates with the visible shape.
        transform.rotation = initialRotation * Quaternion.Euler(0, 0, step.RotationAt(age));
        // Telegraph growth adjusts thickness without moving the indicated beam endpoint.
        transform.localScale = step.shape == BarrageShape.Line
            ? new Vector3(baseDimensions.x, baseDimensions.y * size, 1)
            : new Vector3(baseDimensions.x * size, baseDimensions.y * size, 1);
        visual.color = new Color(Stats.Color.r, Stats.Color.g, Stats.Color.b, step.opacity);
        flashVisual.enabled = step.flash && age < .12f;
        // Keep the impact readable without an opaque white flash over the playfield.
        Color flashColor = Color.Lerp(Stats.Color, Color.white, .25f);
        flashColor.a = .32f * Mathf.Clamp01(1 - age / .12f);
        flashVisual.color = flashColor;
    }

    private void OnTriggerEnter2D(Collider2D other) => Hit(other);
    private void OnTriggerStay2D(Collider2D other) => Hit(other);
    private void Hit(Collider2D other)
    {
        if (step?.dealsDamage == true) TryHit(other, step.knockbackDistance, step.slowMultiplier, step.slowDuration);
    }
    protected override void OnDespawn() { step = null; box.enabled = circle.enabled = false; flashVisual.enabled = false; }
}
