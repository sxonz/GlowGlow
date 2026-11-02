using UnityEngine;

/// <summary>Three travelling buds stop, open, and release eight petals each.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BlossomBud : ProjectileBase
{
    public const float TravelTime = .85f;
    public const float BloomTime = 1.55f;
    public const int PetalCount = 8;
    private readonly SpriteRenderer[] leaves = new SpriteRenderer[4];
    private SpriteRenderer core;
    private Vector2 launch, destination;
    private float angle;
    private bool bloomed;

    public static void Cast(PlayerCombatant owner, Vector2 origin, Vector2 direction, Vector2 aim, WeaponStats stats)
    {
        Rect bounds = CelestialBarrageGeometry.ArenaBounds(owner);
        float distance = Mathf.Clamp(Vector2.Distance(origin, aim), 2.2f, 5);
        for (int i = -1; i <= 1; i++)
        {
            Vector2 axis = Quaternion.Euler(0, 0, i * 23) * direction;
            var bud = SpawnBuiltin<BlossomBud>(owner, origin, axis,
                new WeaponStats(stats.Cooldown, stats.Speed, BloomTime + .22f, stats.Radius, stats.Range, stats.Color, stats.Damage));
            bud.destination = CelestialBarrageGeometry.Clamp(origin + axis * distance, bounds, .65f);
            bud.angle = Mathf.Atan2(axis.y, axis.x);
        }
    }

    protected override void OnSpawn()
    {
        core = GetComponent<SpriteRenderer>();
        core.sprite = RuntimeShapes.Circle;
        core.sharedMaterial = NeonVisualStyle.Get(NeonVisualStyle.BulletMode);
        core.sortingOrder = 4;
        core.color = Stats.Color;
        launch = destination = transform.position;
        bloomed = false;
        angle = 0;
        for (int i = 0; i < leaves.Length; i++)
        {
            if (leaves[i] == null)
            {
                leaves[i] = new GameObject("Bud Petal", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                leaves[i].transform.SetParent(transform, false);
                leaves[i].sprite = RuntimeShapes.Circle;
                leaves[i].sharedMaterial = core.sharedMaterial;
                leaves[i].sortingOrder = 5;
            }
            leaves[i].enabled = true;
            leaves[i].color = Stats.Color;
        }
        UpdateVisual(0);
    }

    protected override void Tick(float deltaTime)
    {
        float age = Time.time - SpawnedAt;
        float t = Mathf.Clamp01(age / TravelTime);
        transform.position = Vector2.Lerp(launch, destination, 1 - (1 - t) * (1 - t));
        UpdateVisual(age);
        if (age >= BloomTime) Bloom();
    }

    // A long frame may cross both the opening and cleanup time. Still release only once.
    protected override void OnLifetimeExpired() => Bloom();

    private void Bloom()
    {
        if (bloomed) return;
        bloomed = true;
        var petalStats = new WeaponStats(Stats.Cooldown, Stats.Speed, 2.8f, .12f, Stats.Range, Stats.Color, BarrageDamage.BlossomPetal);
        for (int i = 0; i < PetalCount; i++)
            SpawnBuiltin<CelestialBullet>(Owner, destination,
                CelestialBarrageGeometry.Unit(angle + i * Mathf.PI * 2 / PetalCount), petalStats);
        CombatVfx.Burst(gameObject.scene, destination, Stats.Color, .6f, BarrageVisibility.OwnerTag(Owner));
    }

    private void UpdateVisual(float age)
    {
        float opening = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(TravelTime, BloomTime, age));
        float fade = 1 - Mathf.Clamp01((age - BloomTime) / .2f);
        transform.localScale = Vector3.one * (Stats.Radius * 2 * fade);
        core.color = Color.Lerp(Stats.Color, Color.white, opening * .65f);
        for (int i = 0; i < leaves.Length; i++)
        {
            float rotation = i * 90 + age * 35;
            leaves[i].transform.localPosition = CelestialBarrageGeometry.Unit(rotation * Mathf.Deg2Rad) * (.2f + opening * .65f);
            leaves[i].transform.localRotation = Quaternion.Euler(0, 0, rotation);
            leaves[i].transform.localScale = new Vector3(.65f + opening * .25f, .3f, 1);
        }
    }
}
