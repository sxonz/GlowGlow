using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared spawning, lifetime, hit filtering and per-prefab pooling.</summary>
public abstract class ProjectileBase : MonoBehaviour
{
    private static readonly Dictionary<object, Queue<ProjectileBase>> Pools = new();
    private static readonly HashSet<ProjectileBase> Active = new();
    private readonly HashSet<PlayerCombatant> hitTargets = new();
    private object poolKey;
    private float despawnAt;
    private bool spawned;
    private SpriteRenderer glowSource;
    private SpriteRenderer glow;
    public PlayerCombatant Owner { get; private set; }
    public WeaponStats Stats { get; private set; }
    public WeaponEffects Effects { get; private set; }
    public float RemainingLifetime => Mathf.Max(0, despawnAt - Time.time);
    protected void ExtendLifetime(float seconds) => despawnAt += Mathf.Max(0, seconds);
    public bool IsSpawned => spawned;
    internal void RemoveProjectile() => Despawn();
    protected Vector2 Direction { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPools()
    {
        Pools.Clear();
        Active.Clear();
    }

    public static ProjectileBase Spawn(ProjectileBase prefab, PlayerCombatant owner,
        Vector2 position, Vector2 direction, WeaponStats stats, WeaponEffects effects = WeaponEffects.None)
        => SpawnInternal(prefab != null ? (object)prefab.GetInstanceID() : typeof(BulletProjectile),
            () => prefab != null ? Instantiate(prefab) : new GameObject("Pooled Bullet").AddComponent<BulletProjectile>(),
            owner, position, direction, stats, effects);

    public static T SpawnBuiltin<T>(PlayerCombatant owner, Vector2 position, Vector2 direction, WeaponStats stats, WeaponEffects effects = WeaponEffects.None)
        where T : ProjectileBase
        => (T)SpawnInternal(typeof(T), () => new GameObject(typeof(T).Name).AddComponent<T>(), owner, position, direction, stats, effects);

    private static ProjectileBase SpawnInternal(object key, System.Func<ProjectileBase> create,
        PlayerCombatant owner, Vector2 position, Vector2 direction, WeaponStats stats, WeaponEffects effects)
    {
        if (!Pools.TryGetValue(key, out var pool)) Pools.Add(key, pool = new Queue<ProjectileBase>());
        ProjectileBase projectile = null;
        // Discard pooled objects destroyed during a scene change.
        while (pool.Count > 0 && projectile == null) projectile = pool.Dequeue();
        if (projectile == null)
            projectile = create();
        projectile.poolKey = key;
        projectile.Owner = owner;
        projectile.Stats = stats;
        projectile.Effects = effects;
        projectile.Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
        projectile.transform.SetPositionAndRotation(position, Quaternion.identity);
        projectile.transform.localScale = Vector3.one;
        if (owner != null && projectile.gameObject.scene != owner.gameObject.scene)
            SceneManager.MoveGameObjectToScene(projectile.gameObject, owner.gameObject.scene);
        projectile.hitTargets.Clear();
        projectile.despawnAt = Time.time + stats.Lifetime;
        projectile.spawned = true;
        Active.Add(projectile);
        projectile.OnSpawn();
        if (projectile.glow == null && projectile.TryGetComponent<SpriteRenderer>(out projectile.glowSource))
            projectile.glow = RuntimeShapes.CreateGlow(projectile.glowSource);
        foreach (var child in projectile.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = owner != null ? owner.gameObject.layer : 0;
        projectile.gameObject.SetActive(true);
        return projectile;
    }

    public static void DespawnAll()
    {
        foreach (var projectile in new List<ProjectileBase>(Active))
            if (projectile != null) projectile.Despawn();
        Active.Clear();
    }

    public static void DespawnOwnedBy(PlayerCombatant owner)
    {
        foreach (var projectile in new List<ProjectileBase>(Active))
            if (projectile != null && projectile.Owner == owner) projectile.Despawn();
    }

    protected virtual void Update()
    {
        if (!spawned) return;
        if (Owner == null || !Owner.CanSelectWeapon || Time.time >= despawnAt) { Despawn(); return; }
        Tick(Time.deltaTime);
    }

    protected abstract void OnSpawn();
    protected virtual bool GlowEnabled => true;
    protected virtual void LateUpdate()
    {
        if (glow == null) return;
        if (!GlowEnabled) { glow.enabled = false; return; }
        RuntimeShapes.SyncGlow(glowSource, glow, .42f, .42f);
    }

    protected abstract void Tick(float deltaTime);
    protected virtual bool DespawnOnHit => true;
    protected virtual bool AllowRepeatedHits => false;
    protected virtual void OnDespawn() { }

    protected void TryHit(Collider2D other, float knockback = 0, float speedMultiplier = 1, float slowDuration = 0, bool dealsDamage = true)
    {
        if (!TryRegisterHit(other, out var target)) return;
        Vector2 away = (Vector2)(target.transform.position - transform.position);
        target.ApplyImpact(away.sqrMagnitude > .0001f ? away : Direction, knockback, speedMultiplier, slowDuration);
        if (dealsDamage) target.ReceiveHit(Owner);
        if (DespawnOnHit) Despawn();
    }

    protected void Despawn()
    {
        if (!spawned) return;
        spawned = false;
        Active.Remove(this);
        OnDespawn();
        Owner = null;
        gameObject.SetActive(false);
        Pools[poolKey].Enqueue(this);
    }

    protected bool TryRegisterHit(Collider2D other, out PlayerCombatant target)
    {
        target = other.GetComponent<PlayerCombatant>();
        return spawned && target != null && target != Owner && target.CanBeHit &&
            (AllowRepeatedHits || hitTargets.Add(target));
    }

    protected virtual void OnDestroy() => Active.Remove(this);
}
