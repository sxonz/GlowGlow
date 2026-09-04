using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class BulletProjectile : MonoBehaviour
{
    private static readonly Queue<BulletProjectile> Pool = new();
    private PlayerCombatant owner;
    private Vector2 velocity;
    private float despawnAt;

    public static BulletProjectile Spawn(PlayerCombatant source, Vector2 position, Vector2 direction, WeaponDefinition weapon)
    {
        var bullet = Pool.Count > 0 ? Pool.Dequeue() : Create();
        bullet.gameObject.SetActive(true);
        bullet.owner = source;
        bullet.transform.position = position;
        bullet.transform.localScale = Vector3.one * weapon.projectileRadius * 2f;
        bullet.velocity = direction.normalized * weapon.projectileSpeed;
        bullet.despawnAt = Time.time + weapon.projectileLifetime;
        bullet.GetComponent<SpriteRenderer>().color = weapon.color;
        return bullet;
    }

    private static BulletProjectile Create()
    {
        var go = new GameObject("Pooled Bullet", typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(BulletProjectile));
        go.GetComponent<SpriteRenderer>().sprite = RuntimeShapes.Circle;
        go.GetComponent<SpriteRenderer>().sortingOrder = 4;
        go.GetComponent<CircleCollider2D>().isTrigger = true;
        return go.GetComponent<BulletProjectile>();
    }

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        if (Time.time >= despawnAt) Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var target = other.GetComponent<PlayerCombatant>();
        if (target == null || target == owner || !target.CanBeHit) return;
        target.ReceiveHit(owner);
        Despawn();
    }

    private void Despawn()
    {
        if (!gameObject.activeSelf) return;
        gameObject.SetActive(false);
        Pool.Enqueue(this);
    }
}
