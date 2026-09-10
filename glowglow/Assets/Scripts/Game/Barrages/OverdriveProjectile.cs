using UnityEngine;

/// <summary>A five-second body attachment: three shield charges and six close rotating spikes.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(LineRenderer))]
public sealed class OverdriveProjectile : ProjectileBase
{
    public int ShieldRemaining { get; private set; }
    public bool IsRunning => ShieldRemaining > 0 && Time.time < expiresAt;
    public int MaxShield => Effects.Has(WeaponEffects.OverdriveShield) ? 6 : 3;
    public float TopSpeedMultiplier => Effects.Has(WeaponEffects.OverdriveAcceleration) ? 2.6f : 1.8f;
    public float AccelerationTime => Effects.Has(WeaponEffects.OverdriveAcceleration) ? .7f : 1.4f;
    public float InertiaMultiplier => Effects.Has(WeaponEffects.OverdriveHandling) ? .35f : 1f;
    private float expiresAt;
    private float startedAt;
    private float bodyRadius;
    private Transform[] spikes;
    private LineRenderer shield;
    private static Sprite spikeSprite;
    private static Material shieldMaterial;
    protected override bool DespawnOnHit => false;
    protected override bool AllowRepeatedHits => true;

    protected override void OnSpawn()
    {
        ShieldRemaining = MaxShield;
        startedAt = Time.time;
        expiresAt = Time.time + Mathf.Min(5, Stats.Lifetime);
        transform.SetParent(Owner.transform, true);
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;
        body.gravityScale = 0;
        var ownerCollider = Owner.GetComponent<CircleCollider2D>();
        bodyRadius = ownerCollider.radius * Mathf.Abs(Owner.transform.lossyScale.x);
        shield = GetComponent<LineRenderer>();
        if (shieldMaterial == null) shieldMaterial = new Material(Shader.Find("Sprites/Default"));
        shield.sharedMaterial = shieldMaterial;
        shield.useWorldSpace = false;
        shield.loop = false;
        shield.positionCount = 65;
        shield.startWidth = shield.endWidth = .045f;
        shield.startColor = shield.endColor = Stats.Color;
        shield.sortingOrder = 6;
        if (spikes == null) BuildSpikes();
        for (int i = 0; i < spikes.Length; i++)
        {
            float angle = i * 60 * Mathf.Deg2Rad;
            spikes[i].localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (bodyRadius + .11f);
            spikes[i].localRotation = Quaternion.Euler(0, 0, i * 60);
            spikes[i].localScale = new Vector3(.26f, .2f, 1);
            spikes[i].GetComponent<SpriteRenderer>().color = Stats.Color;
        }
        Owner.BeginOverdrive(this);
        Tick(0);
    }

    private void BuildSpikes()
    {
        if (spikeSprite == null) spikeSprite = RuntimeShapes.Spike;
        spikes = new Transform[6];
        for (int i = 0; i < spikes.Length; i++)
        {
            var go = new GameObject("Spike " + (i + 1), typeof(SpriteRenderer), typeof(PolygonCollider2D));
            spikes[i] = go.transform;
            spikes[i].SetParent(transform, false);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = spikeSprite;
            renderer.sortingOrder = 5;
            var collider = go.GetComponent<PolygonCollider2D>();
            collider.isTrigger = true;
            collider.points = new[] { new Vector2(-.5f, -.5f), new Vector2(.5f, 0), new Vector2(-.5f, .5f) };
        }
    }

    protected override void Tick(float deltaTime)
    {
        if (!IsRunning) { Despawn(); return; }
        transform.position = Owner.transform.position;
        transform.rotation = Quaternion.Euler(0, 0, (Time.time - startedAt) * 240);
        for (int i = 0; i < shield.positionCount; i++)
        {
            float angle = i / 64f * Mathf.PI * 2 * ShieldRemaining / MaxShield;
            shield.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * (bodyRadius + .01f));
        }
    }

    public bool AbsorbHit()
    {
        if (!IsRunning) return false;
        ShieldRemaining--;
        if (ShieldRemaining == 0) Despawn();
        return true;
    }

    public void Cancel() => Despawn();
    private void OnTriggerEnter2D(Collider2D other) { if (IsRunning) TryHit(other); }
    private void OnTriggerStay2D(Collider2D other) { if (IsRunning) TryHit(other); }
    protected override void OnDespawn()
    {
        ShieldRemaining = 0;
        transform.SetParent(null, true);
        if (Owner != null) Owner.EndOverdrive(this);
    }
}
