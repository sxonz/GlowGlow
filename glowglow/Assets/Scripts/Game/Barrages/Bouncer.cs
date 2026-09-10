using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Bouncer : Bullet
{
    [SerializeField] private Vector2 arenaHalfSize = new(8.7f, 4.65f);
    [SerializeField, Range(0f, 1f)] private float finalSpeedMultiplier = .85f;
    [SerializeField, Min(.01f)] private float fadeDuration = .4f;
    private Vector2 center, limits, heading;
    private SpriteRenderer visual;
    private float spawnedAt, previousAge;
    private readonly RaycastHit2D[] terrainHits = new RaycastHit2D[32];

    protected override void OnSpawn()
    {
        base.OnSpawn();
        visual = GetComponent<SpriteRenderer>();
        spawnedAt = Time.time;
        previousAge = 0;
        center = Vector2.zero;
        Vector2 halfSize = arenaHalfSize;
        if (Owner != null && Owner.IsPreview)
            foreach (var camera in Camera.allCameras)
            {
                if (camera.gameObject.scene != gameObject.scene || !camera.orthographic) continue;
                center = camera.transform.position;
                halfSize = new Vector2(camera.orthographicSize * camera.aspect, camera.orthographicSize);
                break;
            }
        limits = new Vector2(Mathf.Max(.01f, halfSize.x - Stats.Radius), Mathf.Max(.01f, halfSize.y - Stats.Radius));
        Vector2 p = (Vector2)transform.position - center;
        transform.position = center + new Vector2(Mathf.Clamp(p.x, -limits.x, limits.x), Mathf.Clamp(p.y, -limits.y, limits.y));
        heading = Direction;
    }

    protected override void Tick(float deltaTime)
    {
        float age = Time.time - spawnedAt;
        float distance = Stats.Speed * IntegratedSpeed(previousAge, age, Stats.Lifetime, finalSpeedMultiplier);
        MoveBouncing(distance);
        previousAge = age;
        Color color = Stats.Color;
        color.a *= Mathf.SmoothStep(0, 1, Mathf.Clamp01(RemainingLifetime / Mathf.Max(.01f, fadeDuration)));
        visual.color = color;
    }

    internal static float IntegratedSpeed(float from, float to, float lifetime, float finalMultiplier)
    {
        float duration = Mathf.Max(.01f, lifetime), end = Mathf.Clamp01(finalMultiplier);
        float Integral(float t)
        {
            t = Mathf.Max(0, t);
            return t <= duration ? t - (1 - end) * t * t / (2 * duration) : duration * (1 + end) * .5f + (t - duration) * end;
        }
        return Mathf.Max(0, Integral(to) - Integral(from));
    }

    private void MoveBouncing(float remaining)
    {
        Vector2 position = transform.position;
        var filter = new ContactFilter2D { useTriggers = false };
        var physics = gameObject.scene.GetPhysicsScene2D();
        for (int bounce = 0; remaining > .00001f && bounce < 64; bounce++)
        {
            Vector2 local = position - center;
            float tx = Mathf.Abs(heading.x) > .00001f ? (Mathf.Sign(heading.x) * limits.x - local.x) / heading.x : float.PositiveInfinity;
            float ty = Mathf.Abs(heading.y) > .00001f ? (Mathf.Sign(heading.y) * limits.y - local.y) / heading.y : float.PositiveInfinity;
            float boundary = Mathf.Max(0, Mathf.Min(tx, ty));
            bool hitX = tx <= ty + .00001f, hitY = ty <= tx + .00001f;
            float travel = Mathf.Min(remaining, boundary);
            Vector2 normal = new Vector2(hitX ? -Mathf.Sign(heading.x) : 0, hitY ? -Mathf.Sign(heading.y) : 0).normalized;
            bool collision = boundary <= remaining;
            bool terrain = false;
            int count = physics.CircleCast(position, Stats.Radius, heading, travel, filter, terrainHits);
            for (int i = 0; i < count; i++)
            {
                var hit = terrainHits[i];
                if (hit.collider == null || hit.collider.GetComponentInParent<PlayerCombatant>() != null ||
                    hit.collider.GetComponentInParent<ProjectileBase>() != null || hit.distance < .00001f || hit.distance > travel) continue;
                travel = hit.distance;
                normal = hit.normal;
                collision = terrain = true;
            }
            position += heading * travel;
            remaining -= travel;
            if (!collision) break;
            Vector2 incoming = heading;
            heading = !terrain && hitX && hitY ? -heading : Vector2.Reflect(heading, normal);
            if (Effects.Has(WeaponEffects.BouncerRandom))
            {
                Vector2 candidate = Quaternion.Euler(0, 0, Random.Range(-20f, 20f)) * heading;
                bool inward = terrain ? Vector2.Dot(candidate, normal) > .001f :
                    (!hitX || candidate.x * incoming.x <= 0) && (!hitY || candidate.y * incoming.y <= 0);
                if (inward) heading = candidate.normalized;
            }
            if (Effects.Has(WeaponEffects.BouncerLifetime)) ExtendLifetime(.3f);
            // A tiny inward offset prevents counting the same contact on the next iteration.
            position += normal * .0001f;
        }
        transform.position = position;
    }
}
