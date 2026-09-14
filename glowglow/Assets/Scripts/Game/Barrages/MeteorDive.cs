using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Two-second cursor targeting, followed by a vertical player dive and eruption.</summary>
[DefaultExecutionOrder(100)]
public sealed class MeteorDive : ProjectileBase
{
    public const float WarningDuration = 2f;
    public const float DiveDuration = .2f;
    public const float ExplosionRadius = 1.2f;
    public const int FragmentCount = 12;
    public const float FragmentUpwardSpeedMultiplier = 1.25f;
    public const float TrackingSpeedMultiplier = .9f;
    private float startedAt;
    private float descentStartAge;
    private int spawnedFrame;
    private float desiredX;
    private Vector2 previousDivePosition;
    private readonly RaycastHit2D[] diveHits = new RaycastHit2D[32];
    private Vector2 returnPosition, target, diveStart;
    private bool descending, landed;
    private SpriteRenderer warning, marker, streak;
    private readonly MeteorMiniTank[] miniTanks = new MeteorMiniTank[2];
    public Vector2 Target => target;
    public bool IsDescending => descending;
    protected override bool DespawnOnHit => false;

    protected override void OnSpawn()
    {
        startedAt = Time.time;
        spawnedFrame = Time.frameCount;
        descentStartAge = WarningDuration;
        descending = landed = false;
        miniTanks[0] = miniTanks[1] = null;
        returnPosition = Owner.transform.position;
        target = new Vector2(returnPosition.x, -Owner.ArenaHalfSize.y);
        desiredX = target.x;
        if (warning == null)
        {
            warning = MakeSprite("Vertical Warning", RuntimeShapes.Square, 3);
            marker = MakeSprite("Landing Point", RuntimeShapes.Circle, 3);
            streak = MakeSprite("Dive Streak", RuntimeShapes.SoftGlow, 4);
        }
        warning.enabled = marker.enabled = true;
        streak.enabled = false;
        Owner.BeginMeteorDive(this);
        DrawWarning();
    }

    private SpriteRenderer MakeSprite(string label, Sprite sprite, int order)
    {
        var visual = new GameObject(label, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        visual.transform.SetParent(transform, false);
        visual.sprite = sprite;
        visual.sortingOrder = order;
        return visual;
    }

    public void SetTarget(Vector2 cursor)
    {
        if (descending) return;
        desiredX = Owner.ClampToArena(cursor).x;
    }

    private void AdvanceTarget(float deltaTime)
    {
        target.x = Mathf.MoveTowards(target.x, desiredX, Owner.BaseMoveSpeed * TrackingSpeedMultiplier *
            (Effects.Has(WeaponEffects.MeteorTracking) ? 1.1f : 1f) * Mathf.Max(0, deltaTime));
        target.y = -Owner.ArenaHalfSize.y;
        DrawWarning();
    }

    private float Top => Owner.ArenaHalfSize.y + Owner.BodyRadius + .6f;

    public bool RequestDive()
    {
        // The activation click cannot also confirm the dive, and paused input is ignored.
        if (!IsSpawned || descending || Time.frameCount <= spawnedFrame || Time.timeScale <= 0) return false;
        BeginDescent(Time.time - startedAt);
        return true;
    }

    private void BeginDescent(float age)
    {
        if (Effects.Has(WeaponEffects.MeteorShield)) Owner.GrantLandingShield();
        descending = true;
        descentStartAge = age;
        diveStart = new Vector2(target.x, Top);
        previousDivePosition = diveStart;
        warning.enabled = marker.enabled = false;
        streak.enabled = true;
        if (Effects.Has(WeaponEffects.MeteorMiniTanks))
            for (int i = 0; i < 2; i++)
            {
                Vector2 destination = Owner.ClampToArena(target + Vector2.right * (i == 0 ? -1.5f : 1.5f));
                var stats = new WeaponStats(Stats.Cooldown,0,DiveDuration+.1f,Owner.BodyRadius*.5f,Stats.Range,Stats.Color);
                miniTanks[i] = SpawnBuiltin<MeteorMiniTank>(Owner,new Vector2(destination.x,Top),Vector2.down,stats);
                miniTanks[i].Configure(destination);
            }
    }

    private void DrawWarning()
    {
        float top = Top;
        float progress = Mathf.Clamp01((Time.time - startedAt) / WarningDuration);
        warning.transform.position = new Vector3(target.x, (top + target.y) * .5f);
        warning.transform.localScale = new Vector3(Mathf.Lerp(.035f, .09f, progress), top - target.y, 1);
        warning.color = new Color(Stats.Color.r, Stats.Color.g, Stats.Color.b, .45f + progress * .45f);
        marker.transform.position = target;
        marker.transform.localScale = Vector3.one * Mathf.Lerp(.3f, .55f, progress);
        marker.color = new Color(Stats.Color.r, Stats.Color.g, Stats.Color.b, .65f);
    }

    protected override void Tick(float deltaTime)
    {
        float age = Time.time - startedAt;
        if (!descending)
        {
            if (!Owner.IsPreview) SetTarget(Owner.AimPosition);
            AdvanceTarget(Mathf.Min(deltaTime, Mathf.Max(0, WarningDuration - (age - deltaTime))));
            if (age < WarningDuration) return;
            BeginDescent(WarningDuration);
        }
        float progress = Mathf.Clamp01((age - descentStartAge) / DiveDuration);
        Vector2 position = Vector2.Lerp(diveStart, target, progress * progress);
        HitAlongDive(previousDivePosition, position);
        if (!IsSpawned) return; // A lethal hit can end the match and cancel this dive synchronously.
        previousDivePosition = position;
        Owner.MoveMeteorDive(position);
        streak.transform.position = position + Vector2.up * .65f;
        streak.transform.localScale = new Vector3(.45f, 1.8f, 1);
        streak.color = new Color(Stats.Color.r, Stats.Color.g, Stats.Color.b, .8f);
        if (progress < 1f) return;
        landed = true;
        Erupt();
        Despawn();
    }

    private void HitAlongDive(Vector2 from, Vector2 to)
    {
        // Sweep the whole traveled segment so the fast plunge cannot skip enemies between frames.
        Vector2 travel = to - from;
        Physics2D.SyncTransforms();
        int count = gameObject.scene.GetPhysicsScene2D().CircleCast(from, Owner.BodyRadius,
            travel.normalized, travel.magnitude, new ContactFilter2D { useTriggers = true }, diveHits);
        for (int i = 0; i < count; i++)
        {
            if (diveHits[i].collider != null) TryHit(diveHits[i].collider);
            if (!IsSpawned) return;
        }
    }

    private void Erupt()
        => EmitEruption(Owner,target,Stats,1f);

    public static void EmitEruption(PlayerCombatant owner,Vector2 position,WeaponStats source,float scale)
    {
        var blastStats = new WeaponStats(source.Cooldown, 0, .45f, ExplosionRadius*scale, source.Range, source.Color);
        var explosion = SpawnBuiltin<BarrageShapeProjectile>(owner, position, Vector2.up, blastStats);
        explosion.Configure(new BarrageStep
        {
            shape = BarrageShape.Circle, position = BarragePosition.Cursor, duration = .45f,
            dimensions = Vector2.one, startSize = 1, peakSize = 1, endSize = 0,
            shrinkTime = .45f, flash = true, dealsDamage = true
        });
        int count = Mathf.RoundToInt(FragmentCount*scale);
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Lerp(35f, 145f, (i + Random.value) / count) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 launchVelocity = direction * Random.Range(5.5f, 9f);
            launchVelocity.y *= FragmentUpwardSpeedMultiplier;
            var stats = new WeaponStats(source.Cooldown, launchVelocity.magnitude, 2.5f,
                Random.Range(.065f, .1f)*scale, source.Range, source.Color);
            SpawnBuiltin<GravityFragment>(owner, position + Vector2.up * .1f, launchVelocity, stats);
        }
    }

    public void Cancel() => Despawn();
    protected override void OnDespawn()
    {
        if (!landed) foreach(var mini in miniTanks) if(mini != null && mini.IsSpawned) mini.RemoveProjectile();
        warning.enabled = marker.enabled = streak.enabled = false;
        if (Owner != null) Owner.EndMeteorDive(this, landed ? target : returnPosition);
    }
}
