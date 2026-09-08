using UnityEngine;

/// <summary>Independent timeline: changing the equipped slot does not cancel a fired attack.</summary>
public sealed class BarrageSequence : ProjectileBase
{
    private BarrageStep[] steps;
    private bool[] emitted;
    private Vector2 cursor;
    private Vector2 playerPosition;
    private float startedAt;

    public static ProjectileBase Fire(PlayerCombatant owner, Vector2 gun, Vector2 direction,
        Vector2 cursor, WeaponStats stats, BarrageStep[] source)
    {
        var timelineStats = new WeaponStats(stats.Cooldown, stats.Speed, float.PositiveInfinity, stats.Radius, stats.Range, stats.Color);
        var sequence = SpawnBuiltin<BarrageSequence>(owner, gun, direction, timelineStats);
        sequence.cursor = cursor;
        sequence.playerPosition = owner.transform.position;
        sequence.steps = new BarrageStep[source.Length];
        sequence.emitted = new bool[source.Length];
        for (int i = 0; i < source.Length; i++) sequence.steps[i] = source[i]?.Snapshot();
        // Emit zero-delay steps immediately; the remaining events use scaled game time.
        sequence.Tick(0);
        return sequence;
    }

    protected override void OnSpawn() { startedAt = Time.time; steps = null; emitted = null; }

    protected override void Tick(float deltaTime)
    {
        if (steps == null) return;
        bool pending = false;
        for (int i = 0; i < steps.Length; i++)
        {
            if (emitted[i] || steps[i] == null) continue;
            if (Time.time - startedAt < steps[i].delay) { pending = true; continue; }
            emitted[i] = true;
            Emit(steps[i]);
        }
        if (!pending) Despawn();
    }

    private void Emit(BarrageStep step)
    {
        Vector2 origin = step.position == BarragePosition.Cursor ? cursor : (Vector2)transform.position;
        if (step.position == BarragePosition.Player) origin = playerPosition;
        float offset = Random.Range(step.offsetDegrees.x, step.offsetDegrees.y);
        Vector2 direction = Quaternion.Euler(0, 0, offset) * Direction;
        float speed = Stats.Speed * Random.Range(step.speedMultiplier.x, step.speedMultiplier.y);
        float lifetime = step.duration > 0 ? step.duration : float.PositiveInfinity;
        var stats = new WeaponStats(Stats.Cooldown, speed, lifetime,
            Stats.Radius * (step.shape == BarrageShape.Bullet ? step.startSize : 1), Stats.Range, Stats.Color);
        if (step.shape == BarrageShape.Overdrive)
            SpawnBuiltin<OverdriveProjectile>(Owner, origin, direction, stats);
        else if (step.shape == BarrageShape.ElectricPulse)
            SpawnBuiltin<ElectricPulseProjectile>(Owner, origin, direction, stats).Configure(step);
        else if (step.shape == BarrageShape.Bullet)
            SpawnBuiltin<BulletProjectile>(Owner, origin, direction, stats);
        else
        {
            var shape = SpawnBuiltin<BarrageShapeProjectile>(Owner, origin, direction, stats);
            shape.Configure(step);
        }
    }

    protected override void OnDespawn() { steps = null; emitted = null; }
}
