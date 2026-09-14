using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small Bullet-type debris with world-down gravity and a fading lifetime.</summary>
public sealed class GravityFragment : Bullet
{
    public const float Gravity = 14f;
    private Vector2 velocity;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    public override Vector2 LinearVelocity => velocity;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        velocity = Direction * Stats.Speed;
    }

    protected override void Tick(float deltaTime)
    {
        Vector2 travel = velocity * deltaTime + Vector2.down * (.5f * Gravity * deltaTime * deltaTime);
        Physics2D.SyncTransforms();
        int count = gameObject.scene.GetPhysicsScene2D().CircleCast(transform.position, Stats.Radius,
            travel.normalized, travel.magnitude, new ContactFilter2D { useTriggers = true }, hits);
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider != null) TryHit(hits[i].collider);
            if (!IsSpawned) return;
        }
        transform.position += (Vector3)travel;
        velocity += Vector2.down * (Gravity * deltaTime);
        var visual = GetComponent<SpriteRenderer>();
        Color color = Stats.Color;
        color.a *= Mathf.Clamp01(RemainingLifetime / .35f);
        visual.color = color;
        if (transform.position.y < -Owner.ArenaHalfSize.y - 1f || Mathf.Abs(transform.position.x) > Owner.ArenaHalfSize.x + 1f)
            Despawn();
    }
}
