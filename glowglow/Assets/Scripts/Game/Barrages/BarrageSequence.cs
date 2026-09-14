using UnityEngine;

/// <summary>Independent timeline: changing the equipped slot does not cancel a fired attack.</summary>
public sealed class BarrageSequence : ProjectileBase
{
    private BarrageStep[] steps;
    private bool[] emitted;
    private Vector2 cursor;
    private Vector2 playerPosition;
    private float startedAt;
    private WeaponRuntime firingRuntime;

    public static ProjectileBase Fire(PlayerCombatant owner, Vector2 gun, Vector2 direction,
        Vector2 cursor, WeaponStats stats, BarrageStep[] source, WeaponEffects effects = WeaponEffects.None, WeaponRuntime firingRuntime = null)
    {
        var timelineStats = new WeaponStats(stats.Cooldown, stats.Speed, float.PositiveInfinity, stats.Radius, stats.Range, stats.Color);
        var sequence = SpawnBuiltin<BarrageSequence>(owner, gun, direction, timelineStats, effects);
        sequence.firingRuntime = firingRuntime;
        Vector2 chainPosition = effects.Has(WeaponEffects.BombChain) ? owner.RandomArenaPosition() : cursor;
        source = WeaponPatternCompiler.Build(source, effects, effects.Has(WeaponEffects.BombChain) ? Random.value : 1f, chainPosition);
        sequence.cursor = cursor;
        sequence.playerPosition = owner.transform.position;
        sequence.steps = new BarrageStep[source.Length];
        sequence.emitted = new bool[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var step = sequence.steps[i] = source[i]?.Snapshot();
            if (step != null && step.followMuzzle)
                owner.BeginLaserAim(step.delay + step.duration, !effects.Has(WeaponEffects.LaserFreeWarning));
        }
        // Emit zero-delay steps immediately; the remaining events use scaled game time.
        sequence.EmitDueSteps();
        return sequence;
    }

    protected override void OnSpawn() { startedAt = Time.time; steps = null; emitted = null; }

    protected override void Tick(float deltaTime) { }

    protected override void LateUpdate()
    {
        // Delayed shots sample the muzzle after movement and dash coroutines for this frame.
        EmitDueSteps();
        base.LateUpdate();
    }

    private void EmitDueSteps()
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
        if (step.followMuzzle || step.fireFromCurrentMuzzle)
        {
            origin = Owner.MuzzlePosition;
            direction = Quaternion.Euler(0, 0, offset) * Owner.AimDirection;
        }
        if (step.overridePosition) origin = step.worldPosition;
        float speed = Stats.Speed * Random.Range(step.speedMultiplier.x, step.speedMultiplier.y);
        float lifetime = step.duration > 0 ? step.duration : float.PositiveInfinity;
        var stats = new WeaponStats(Stats.Cooldown, speed, lifetime,
            Stats.Radius * (step.shape == BarrageShape.Bullet || step.shape == BarrageShape.Bouncer ? step.startSize : 1), Stats.Range, Stats.Color);
        if (step.shape == BarrageShape.OrbitOrb)
        {
            var primary = SpawnBuiltin<OrbitOrb>(Owner, origin, direction, stats, Effects);
            if (Effects.Has(WeaponEffects.OrbDouble))
                SpawnBuiltin<OrbitOrb>(Owner, origin, direction,
                    new WeaponStats(stats.Cooldown, stats.Speed, stats.Lifetime, stats.Radius * .5f, stats.Range, stats.Color), Effects).OrbitAround(primary);
        }
        else if (step.shape == BarrageShape.PrismShot)
            SpawnBuiltin<PrismShot>(Owner,origin,direction,stats,Effects);
        else if (step.shape == BarrageShape.Boomerang)
        {
            var returningStats = new WeaponStats(stats.Cooldown,stats.Speed,float.PositiveInfinity,stats.Radius,stats.Range,stats.Color);
            SpawnBuiltin<Boomerang>(Owner,origin,direction,returningStats,Effects).BindWeapon(firingRuntime);
        }
        else if (step.shape == BarrageShape.OctoShot)
        {
            for(int i=0;i<8;i++)
                SpawnBuiltin<OctoShot>(Owner,origin,Quaternion.Euler(0,0,i*45f)*direction,stats,Effects);
        }
        else if (step.shape == BarrageShape.MeteorDive)
            SpawnBuiltin<MeteorDive>(Owner, origin, direction, stats, Effects).SetTarget(cursor);
        else if (step.shape == BarrageShape.Overdrive)
            SpawnBuiltin<OverdriveProjectile>(Owner, origin, direction, stats, Effects);
        else if (step.shape == BarrageShape.ElectricPulse)
            SpawnBuiltin<ElectricPulseProjectile>(Owner, origin, direction, stats, Effects).Configure(step);
        else if (step.shape == BarrageShape.Bouncer)
            SpawnBuiltin<Bouncer>(Owner, origin, direction, stats, Effects);
        else if (step.shape == BarrageShape.Bullet)
            SpawnBuiltin<BulletProjectile>(Owner, origin, direction, stats, Effects).SetInterception(step.interceptsBullet ? 1 : 0);
        else
        {
            var shape = SpawnBuiltin<BarrageShapeProjectile>(Owner, origin, direction, stats, Effects);
            shape.Configure(step, offset);
        }
    }

    protected override void OnDespawn() { steps = null; emitted = null; firingRuntime = null; }
}
