using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
public class Bullet : ProjectileBase
{
    protected override void OnSpawn()
    {
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
    protected virtual void OnTriggerEnter2D(Collider2D other) => TryHit(other);
}
