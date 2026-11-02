using UnityEngine;

/// <summary>Lights sway in from outside the view and converge on their caster to be absorbed.</summary>
public sealed class FireflyLight : CelestialBullet
{
    public const int Count = 9;
    public const float Lifetime = 8;
    private Vector2 center, heading, previousOwnerPosition;
    private float phase;
    private bool reflected;
    protected override float ArmingDuration => .35f;

    public static void Cast(PlayerCombatant owner, Vector2 aim, WeaponStats stats)
    {
        if (owner == null) return;
        Rect view = CelestialBarrageGeometry.ViewBounds(owner);
        Vector2 destination = owner.transform.position;
        float start = Random.value;
        var lightStats = new WeaponStats(stats.Cooldown, stats.Speed, Lifetime, stats.Radius, stats.Range, stats.Color, stats.Damage);
        for (int i = 0; i < Count; i++)
        {
            Vector2 origin = CelestialBarrageGeometry.OutsideEdge(view, start + (float)i / Count, .65f);
            var light = SpawnBuiltin<FireflyLight>(owner, origin, destination - origin, lightStats);
            light.phase = i * 2.399963f;
        }
    }

    protected override void OnSpawn()
    {
        base.OnSpawn();
        center = transform.position;
        heading = Direction;
        previousOwnerPosition = Owner.transform.position;
        phase = 0;
        reflected = false;
    }

    protected override void Tick(float deltaTime)
    {
        if (reflected) { base.Tick(deltaTime); return; }
        float age = Time.time - SpawnedAt;
        Vector2 destination = Owner.transform.position;
        Vector2 toward = destination - center;
        float distance = toward.magnitude;
        if (distance > .001f)
            heading = distance < 1.5f ? toward / distance :
                (Vector2)Vector3.RotateTowards(heading, toward / distance, 2.8f * deltaTime, 0);
        float speed = Stats.Speed * Mathf.Lerp(.48f, 1.5f, Mathf.Clamp01(age / 2.4f));
        center += heading * Mathf.Min(distance, speed * deltaTime);
        Vector2 lateral = new(-heading.y, heading.x);
        float wave = .3f * Mathf.Sin(age * 5 + phase) * Mathf.Min(1, age * 3) * Mathf.Clamp01(distance - .5f);
        Vector2 next = center + lateral * wave;
        Vector2 from = transform.position;
        bool absorbed = TryAbsorption(from - previousOwnerPosition, next - destination,
            Owner.BodyRadius + Stats.Radius, out float arrival);
        previousOwnerPosition = destination;
        if (absorbed) next = Vector2.Lerp(from, next, arrival);
        Velocity = deltaTime > 0 ? (next - (Vector2)transform.position) / deltaTime : heading * speed;
        if (!MoveSwept(next)) return;
        if (absorbed) { Despawn(); return; }
        UpdateSpawnGrowth();
        transform.localScale *= Mathf.Lerp(.35f, 1, Mathf.Clamp01(distance / 1.2f));
        var color = Stats.Color;
        color.a = .7f + .3f * (.5f + .5f * Mathf.Sin(age * 7 + phase));
        Visual.color = color;
        // Continue following even when the caster moves near the edge of the view.
    }

    protected override void OnReflected(Vector2 velocity)
    {
        base.OnReflected(velocity);
        reflected = true;
    }

    private static bool TryAbsorption(Vector2 from, Vector2 to, float radius, out float time)
    {
        time = 0;
        float c = from.sqrMagnitude - radius * radius;
        if (c <= 0) return true;
        Vector2 travel = to - from;
        float a = travel.sqrMagnitude;
        if (a < .000001f) return false;
        float b = Vector2.Dot(from, travel);
        float discriminant = b * b - a * c;
        if (discriminant < 0) return false;
        time = (-b - Mathf.Sqrt(discriminant)) / a;
        return time >= 0 && time <= 1;
    }
}
