using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
public class Bullet : ProjectileBase
{
    private int interceptions;
    public virtual Vector2 LinearVelocity => Direction * Stats.Speed;
    public virtual bool IsSmallBullet => Stats.Radius <= .16f;
    public void SetInterception(int count) => interceptions = Mathf.Max(0, count);
    protected override void OnSpawn()
    {
        interceptions = 0;
        var renderer = GetComponent<SpriteRenderer>();
        if (renderer.sprite == null) renderer.sprite = RuntimeShapes.Circle;
        renderer.color = Stats.Color;
        renderer.sortingOrder = 4;
        transform.localScale = Vector3.one * Stats.Radius * 2f;
        var collider = GetComponent<CircleCollider2D>();
        collider.radius = .5f;
        collider.isTrigger = true;
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
    }

    protected override void Tick(float deltaTime)
    {
        transform.position += (Vector3)(Direction * Stats.Speed * deltaTime);
        if (Owner.IsPreview && Mathf.Abs(transform.position.x) > 14) { Despawn(); return; }
        var camera = Owner.IsPreview ? null : Camera.main;
        if (camera == null) return;
        var viewport = camera.WorldToViewportPoint(transform.position);
        float margin = Stats.Radius / Mathf.Max(.01f, camera.orthographicSize * 2f);
        if (viewport.x < -margin || viewport.x > 1 + margin || viewport.y < -margin || viewport.y > 1 + margin)
            Despawn();
    }
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (TryIntercept(other)) return;
        TryHit(other);
    }

    private bool TryIntercept(Collider2D other)
    {
        if (!IsSpawned || interceptions <= 0) return false;
        var bullet = other.GetComponent<Bullet>();
        if (bullet == null || bullet == this || !bullet.IsSpawned || bullet.Owner == Owner || !bullet.IsSmallBullet) return false;
        interceptions--;
        bullet.RemoveProjectile();
        return true;
    }
}
