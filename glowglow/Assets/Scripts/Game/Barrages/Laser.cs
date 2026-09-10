using UnityEngine;

/// <summary>A muzzle-following beam that hits each target once during its lifetime.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class Laser : ProjectileBase
{
    private static Sprite beamSprite;
    protected override bool DespawnOnHit => false;

    protected override void OnSpawn()
    {
        Owner.BeginLaserAim(Stats.Lifetime);
        Owner.ApplyLaserRecoil();
        if (beamSprite == null)
            beamSprite = RuntimeShapes.Square;
        var renderer = GetComponent<SpriteRenderer>();
        renderer.sprite = beamSprite;
        renderer.color = Stats.Color;
        renderer.sortingOrder = 4;
        transform.position += (Vector3)(Direction * Stats.Range * .5f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(Stats.Range, Stats.Radius * 2f, 1f);
        var collider = GetComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        collider.isTrigger = true;
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.useFullKinematicContacts = true;
        FollowMuzzle();
    }

    protected override void Tick(float deltaTime) => FollowMuzzle();
    protected override void LateUpdate()
    {
        FollowMuzzle();
        base.LateUpdate();
    }
    private void FollowMuzzle()
    {
        if (Owner == null) return;
        Vector2 direction = Owner.AimDirection;
        transform.SetPositionAndRotation(Owner.MuzzlePosition + direction * Stats.Range * .5f,
            Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
    }
    protected virtual void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    protected virtual void OnTriggerStay2D(Collider2D other) => TryHit(other);
}
