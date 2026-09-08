using UnityEngine;

/// <summary>A five-second body attachment: three shield charges and six close rotating spikes.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(LineRenderer))]
public sealed class OverdriveProjectile : ProjectileBase
{
    public int ShieldRemaining { get; private set; }
    public bool IsRunning => ShieldRemaining > 0 && Time.time < expiresAt;
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
        ShieldRemaining = 3;
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
        if (spikeSprite == null)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, Mathf.Abs((y + .5f) / size - .5f) <= .5f * (1 - (x + .5f) / size) ? Color.white : Color.clear);
            texture.Apply();
            spikeSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }
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
            float angle = i / 64f * Mathf.PI * 2 * ShieldRemaining / 3f;
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
