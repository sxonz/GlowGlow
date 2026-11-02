using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Pooled, swept light bullet. Reuses prepared circle/glow artwork.</summary>
public class CelestialBullet : Bullet
{
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    private static int syncedFrame = -1;
    protected SpriteRenderer Visual { get; private set; }
    protected Rect Bounds { get; private set; }
    protected Vector2 Velocity;
    private bool enteredView;
    public override Vector2 LinearVelocity => Velocity;

    protected override void OnSpawn()
    {
        base.OnSpawn();
        Visual = GetComponent<SpriteRenderer>();
        Visual.enabled = true;
        GetComponent<CircleCollider2D>().enabled = true;
        Bounds = CelestialBarrageGeometry.ArenaBounds(Owner);
        enteredView = false;
        Velocity = Direction * Stats.Speed;
    }

    protected override void OnReflected(Vector2 velocity) => Velocity = velocity;

    protected bool MoveSwept(Vector2 destination)
    {
        Vector2 from = transform.position;
        Vector2 travel = destination - from;
        if (AttackReady && travel.sqrMagnitude > .000001f)
        {
            // Only player/mine transforms matter to these casts; synchronize once per frame.
            if (syncedFrame != Time.frameCount)
            {
                Physics2D.SyncTransforms();
                syncedFrame = Time.frameCount;
            }
            int count = gameObject.scene.GetPhysicsScene2D().CircleCast(from, Stats.Radius,
                travel.normalized, travel.magnitude, new ContactFilter2D { useTriggers = true }, hits);
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider != null) TryHit(hits[i].collider);
                if (!IsSpawned) return false;
            }
        }
        transform.position = destination;
        return true;
    }

    protected void CullAfterEntry()
    {
        Vector2 point = transform.position;
        if (Bounds.Contains(point)) enteredView = true;
        if (enteredView && (point.x < Bounds.xMin - 1 || point.x > Bounds.xMax + 1 ||
            point.y < Bounds.yMin - 1 || point.y > Bounds.yMax + 1)) Despawn();
    }

    protected override void Tick(float deltaTime)
    {
        if (MoveSwept((Vector2)transform.position + Velocity * deltaTime)) CullAfterEntry();
    }

    protected virtual void OnTriggerStay2D(Collider2D other) => TryHit(other);
}
