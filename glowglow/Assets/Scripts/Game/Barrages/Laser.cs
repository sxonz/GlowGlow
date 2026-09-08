using UnityEngine;

/// <summary>A stationary beam that hits each target once during its lifetime.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(Rigidbody2D))]
public class Laser : ProjectileBase
{
    private static Sprite beamSprite;
    protected override bool DespawnOnHit => false;

    protected override void OnSpawn()
    {
        if (beamSprite == null)
            beamSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
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
    }

    protected override void Tick(float deltaTime) { }
    protected virtual void OnTriggerEnter2D(Collider2D other) => TryHit(other);
    protected virtual void OnTriggerStay2D(Collider2D other) => TryHit(other);
}
