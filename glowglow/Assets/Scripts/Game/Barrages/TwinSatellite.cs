using UnityEngine;

/// <summary>Two opposing lights share a circular orbit and emit six rotating fans.</summary>
public sealed class TwinSatellite : CelestialBullet
{
    public const float Lifetime = 4.8f;
    public const int VolleyCount = 6;
    public const float WarningTime = .7f;
    public const float OrbitRadius = 1.6f;
    public const float OrbitPeriod = 2.4f;
    public const float AngularSpeed = Mathf.PI * 2 / OrbitPeriod;
    private Vector2 center;
    private float phase;
    private int volleys;
    protected override bool CanReflect => false;
    protected override bool DespawnOnHit => false;
    protected override float ArmingDuration => WarningTime;

    public static void Cast(PlayerCombatant owner, Vector2 aim, WeaponStats stats)
    {
        Vector2 center = CelestialBarrageGeometry.Clamp(aim, CelestialBarrageGeometry.ArenaBounds(owner), 2);
        float phase = Random.Range(0, Mathf.PI * 2);
        var orbitStats = new WeaponStats(stats.Cooldown, stats.Speed, Lifetime, stats.Radius, stats.Range, stats.Color, stats.Damage);
        for (int i = 0; i < 2; i++)
        {
            var satellite = SpawnBuiltin<TwinSatellite>(owner, center, Vector2.right, orbitStats);
            satellite.center = center;
            satellite.phase = phase + i * Mathf.PI;
            satellite.transform.position = satellite.PositionAt(0);
            satellite.Velocity = satellite.TangentAt(0);
        }
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        center = transform.position;
        phase = 0;
        volleys = 0;
        Velocity = Vector2.zero;
    }

    public Vector2 PositionAt(float age)
    {
        return center + CelestialBarrageGeometry.Unit(phase + age * AngularSpeed) * OrbitRadius;
    }

    private Vector2 TangentAt(float age)
    {
        Vector2 radial = CelestialBarrageGeometry.Unit(phase + age * AngularSpeed);
        return new Vector2(-radial.y, radial.x) * (OrbitRadius * AngularSpeed);
    }

    protected override void Tick(float deltaTime)
    {
        float age = Time.time - SpawnedAt;
        Vector2 radial = CelestialBarrageGeometry.Unit(phase + age * AngularSpeed);
        Vector2 next = center + radial * OrbitRadius;
        Velocity = new Vector2(-radial.y, radial.x) * (OrbitRadius * AngularSpeed);
        if (!MoveSwept(next)) return;
        UpdateSpawnGrowth();
        // Catch up only the bounded six volleys after a slow frame.
        while (volleys < VolleyCount && age >= WarningTime + volleys * .65f)
        {
            float emissionAge = WarningTime + volleys * .65f;
            Vector2 origin = PositionAt(emissionAge);
            float angle = phase + emissionAge * AngularSpeed;
            const float fan = .25f;
            var shotStats = new WeaponStats(Stats.Cooldown, Stats.Speed, 2.7f, .105f, Stats.Range, Stats.Color, BarrageDamage.SatelliteBullet);
            for (int i = -1; i <= 1; i++)
                SpawnBuiltin<CelestialBullet>(Owner, origin, CelestialBarrageGeometry.Unit(angle + i * fan), shotStats);
            volleys++;
        }
    }
}
