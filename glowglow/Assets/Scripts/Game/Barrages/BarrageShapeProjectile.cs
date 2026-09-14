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
    private float muzzleOffsetDegrees;
    private static Material arenaClipMaterial;
    private Material unclippedMaterial;
    private bool clipped;
    public bool IsTelegraph => step != null && !step.dealsDamage;
    public bool IsBeam => step != null && (step.shape == BarrageShape.Line || step.followMuzzle);

    // Signed clearance from the visible attack's dangerous area, including body radius.
    // Bomb warnings cover a rotating square; use its corner radius rather than only
    // the decorative warning circle so a bot does not stop inside the explosion.
    public float DangerClearance(Vector2 position, float bodyRadius)
    {
        if (step == null) return float.PositiveInfinity;
        if (IsBeam)
        {
            Vector2 axis = transform.right;
            float halfLength = transform.localScale.x * .5f;
            Vector2 center = transform.position;
            float along = Mathf.Clamp(Vector2.Dot(position - center, axis), -halfLength, halfLength);
            float halfWidth = Mathf.Max(.15f, transform.localScale.y * .5f);
            return Vector2.Distance(position, center + axis * along) - halfWidth - bodyRadius;
        }
        float radius = baseDimensions.magnitude * .5f * Mathf.Max(1.2f, step.peakSize);
        return Vector2.Distance(position, transform.position) - radius - bodyRadius;
    }
    protected override bool DespawnOnHit => false;
    protected override bool GlowEnabled => step != null && step.shape != BarrageShape.Line;

    protected override void OnSpawn()
    {
        visual = GetComponent<SpriteRenderer>();
        if(unclippedMaterial==null) unclippedMaterial=visual.sharedMaterial;
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
        if (square == null) square = RuntimeShapes.Square;
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

    public void Configure(BarrageStep value, float offsetDegrees = 0)
    {
        step = value;
        muzzleOffsetDegrees = offsetDegrees;
        if (step.followMuzzle)
        {
            Owner.BeginLaserAim(Stats.Lifetime, step.dealsDamage || !Effects.Has(WeaponEffects.LaserFreeWarning));
            if (step.dealsDamage) Owner.ApplyLaserRecoil();
        }
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
        bool laser = step.shape == BarrageShape.Box && step.position == BarragePosition.Gun;
        const float flashScale = 1.15f;
        flashVisual.transform.localScale = Vector3.one * flashScale;
        // Keep the enlarged flash's rear edge at the muzzle instead of extending behind it.
        flashVisual.transform.localPosition = laser
            ? Vector3.right * (visual.sprite.bounds.min.x * (1f - flashScale))
            : Vector3.zero;
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
        FollowMuzzle();
        visual.color = new Color(Stats.Color.r, Stats.Color.g, Stats.Color.b, step.opacity);
        bool laser = step.shape == BarrageShape.Box && step.position == BarragePosition.Gun;
        float flashDuration = laser ? .22f : .12f;
        flashVisual.enabled = step.flash && age < flashDuration;
        Color flashColor = Color.Lerp(Stats.Color, Color.white, laser ? .85f : .25f);
        flashColor.a = (laser ? .8f : .32f) * Mathf.Clamp01(1 - age / flashDuration);
        flashVisual.color = flashColor;
    }

    private void OnTriggerEnter2D(Collider2D other) => Hit(other);
    public void ClipToArena(Rect bounds)
    {
        if(arenaClipMaterial==null)
            arenaClipMaterial=new Material(Resources.Load<Shader>("ArenaClippedSprite"));
        var properties=new MaterialPropertyBlock();
        properties.SetVector("_ClipRect",new Vector4(bounds.xMin,bounds.yMin,bounds.xMax,bounds.yMax));
        foreach(var renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sharedMaterial=arenaClipMaterial;
            renderer.SetPropertyBlock(properties);
        }
        clipped=true;
    }
    protected override void LateUpdate()
    {
        // Run after player movement/dash coroutines; the collider and artwork share this transform.
        FollowMuzzle();
        base.LateUpdate();
    }

    private void FollowMuzzle()
    {
        if (step == null || !step.followMuzzle || Owner == null) return;
        float angle = Mathf.Atan2(Owner.AimDirection.y, Owner.AimDirection.x) * Mathf.Rad2Deg + muzzleOffsetDegrees;
        Quaternion rotation = Quaternion.Euler(0, 0, angle);
        Vector3 direction = rotation * Vector3.right;
        transform.SetPositionAndRotation((Vector3)Owner.MuzzlePosition + direction * transform.localScale.x * .5f, rotation);
    }
    private void OnTriggerStay2D(Collider2D other) => Hit(other);
    private void Hit(Collider2D other)
    {
        if (step?.dealsDamage == true) TryHit(other, step.knockbackDistance, step.slowMultiplier, step.slowDuration);
    }
    protected override void OnDespawn()
    {
        step = null; box.enabled = circle.enabled = false; flashVisual.enabled = false;
        if(!clipped) return;
        foreach(var renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sharedMaterial=unclippedMaterial;
            renderer.SetPropertyBlock(null);
        }
        clipped=false;
    }
}
