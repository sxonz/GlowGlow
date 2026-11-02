using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Shared spawning, lifetime, hit filtering and per-prefab pooling.</summary>
public abstract class ProjectileBase : MonoBehaviour
{
    private static readonly Dictionary<object, Queue<ProjectileBase>> Pools = new();
    private static readonly HashSet<ProjectileBase> Active = new();
    public static IEnumerable<ProjectileBase> ActiveProjectiles => Active;
    private readonly HashSet<PlayerCombatant> hitTargets = new();
    private HashSet<ulong> mineContacts;
    private static ulong nextSpawnId;
    internal ulong SpawnId { get; private set; }
    private readonly List<Transform> layerTransforms = new(8);
    private readonly List<Renderer> visibilityRenderers = new(8);
    private object poolKey;
    private float despawnAt;
    private float disappearAt;
    private Vector3 disappearScale;
    private Color disappearColor;
    public bool IsDisappearing { get; private set; }
    protected virtual bool FadeOnLifetimeEnd => false;
    protected float SpawnedAt { get; private set; }
    protected virtual float ArmingDuration => 0f;
    protected bool AttackReady => !IsDisappearing && Time.time - SpawnedAt >= ArmingDuration;
    internal void BeginDisappear()
    {
        if (!spawned || IsDisappearing) return;
        IsDisappearing = true;
        disappearAt = Time.time;
        disappearScale = transform.localScale;
        disappearColor = glowSource != null ? glowSource.color : Stats.Color;
        OnBeginDisappear();
    }
    protected virtual void OnBeginDisappear() { }
    protected virtual void OnDisappearVisual(float remaining) { }
    protected void UpdateSpawnGrowth()
    {
        float growth = ArmingDuration > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - SpawnedAt) / ArmingDuration)) : 1;
        transform.localScale = Vector3.one * (Stats.Radius * 2f * Mathf.Max(.001f, growth));
    }
    private bool spawned;
    private SpriteRenderer glowSource;
    private SpriteRenderer glow;
    private Sprite glowSprite;
    private Color glowColor;
    private Vector3 glowScale;
    private int glowOrder, glowLayer;
    public PlayerCombatant Owner { get; private set; }
    public WeaponStats Stats { get; private set; }
    public virtual int Damage => Stats.Damage;
    public WeaponEffects Effects { get; private set; }
    public float RemainingLifetime => Mathf.Max(0, despawnAt - Time.time);
    protected void ExtendLifetime(float seconds) => despawnAt += Mathf.Max(0, seconds);
    public bool IsSpawned => spawned;
    internal void RemoveProjectile() => Despawn();
    protected Vector2 Direction { get; private set; }

    protected void Redirect(PlayerCombatant owner, Vector2 direction)
    {
        Owner = owner;
        Direction = direction.normalized;
        hitTargets.Clear();
        mineContacts?.Clear();
        gameObject.layer = owner.gameObject.layer;
        foreach (var renderer in visibilityRenderers) BarrageVisibility.ApplyOwner(renderer, BarrageVisibility.OwnerTag(owner));
    }

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
        projectile.mineContacts?.Clear();
        projectile.SpawnId = ++nextSpawnId;
        projectile.despawnAt = Time.time + stats.Lifetime;
        projectile.SpawnedAt = Time.time;
        projectile.spawned = true;
        projectile.IsDisappearing = false;
        Active.Add(projectile);
        projectile.OnSpawn();
        if (projectile.glow == null && projectile.TryGetComponent<SpriteRenderer>(out projectile.glowSource))
            projectile.glow = RuntimeShapes.CreateGlow(projectile.glowSource);
        projectile.GetComponentsInChildren(true, projectile.layerTransforms);
        foreach (var child in projectile.layerTransforms)
            child.gameObject.layer = owner != null ? owner.gameObject.layer : 0;
        projectile.GetComponentsInChildren(true, projectile.visibilityRenderers);
        foreach (var renderer in projectile.visibilityRenderers)
            BarrageVisibility.ApplyOwner(renderer, BarrageVisibility.OwnerTag(owner));
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
        if (Owner == null || !Owner.CanSelectWeapon) { Despawn(); return; }
        if (IsDisappearing)
        {
            float remaining = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - disappearAt) / .4f));
            if (remaining <= 0) { Despawn(); return; }
            transform.localScale = disappearScale * remaining;
            if (glowSource != null)
            {
                var color = disappearColor; color.a *= remaining; glowSource.color = color;
            }
            OnDisappearVisual(remaining);
            return;
        }
        if (Time.time >= despawnAt)
        {
            OnLifetimeExpired();
            if (!spawned || IsDisappearing) return;
            if (FadeOnLifetimeEnd) BeginDisappear(); else Despawn();
            return;
        }
        Tick(Time.deltaTime);
    }

    protected abstract void OnSpawn();
    protected virtual bool GlowEnabled => true;
    protected virtual void LateUpdate()
    {
        if (glow == null) return;
        if (!GlowEnabled) { glow.enabled = false; return; }
        var sprite = glowSource.sprite;
        var scale = glowSource.transform.lossyScale;
        var color = glowSource.color;
        bool visible = glowSource.enabled && sprite != null;
        if (glow.enabled == visible && glowSprite == sprite && glowScale == scale && glowColor == color
            && glowOrder == glowSource.sortingOrder && glowLayer == glowSource.sortingLayerID) return;
        RuntimeShapes.SyncGlow(glowSource, glow, .42f, .42f);
        glowSprite = sprite; glowScale = scale; glowColor = color;
        glowOrder = glowSource.sortingOrder; glowLayer = glowSource.sortingLayerID;
    }

    protected abstract void Tick(float deltaTime);
    protected virtual void OnLifetimeExpired() { }
    protected virtual bool DespawnOnHit => true;
    protected virtual bool AllowRepeatedHits => false;
    protected virtual void OnDespawn() { }

    protected void TryHit(Collider2D other, float knockback = 0, float speedMultiplier = 1, float slowDuration = 0, bool dealsDamage = true)
    {
        if (dealsDamage && TryHitMine(other)) return;
        if (!TryRegisterHit(other, out var target, dealsDamage)) return;
        Vector2 away = (Vector2)(target.transform.position - transform.position);
        target.ApplyImpact(away.sqrMagnitude > .0001f ? away : Direction, knockback, speedMultiplier, slowDuration);
        if (dealsDamage) target.ReceiveHit(Owner, Damage);
        if (DespawnOnHit) Despawn();
    }

    // Each attack can affect a particular mine spawn only once, including persistent beams.
    // Spawn IDs distinguish a reused pooled mine from its previous deployment.
    protected bool TryRegisterMineContact(Collider2D other, out MineProjectile mine, bool enemyOnly = true)
    {
        mine = null;
        if (!spawned || !AttackReady || !other.TryGetComponent(out mine) || !mine.IsSpawned ||
            mine == this || enemyOnly && mine.Owner == Owner) return false;
        mineContacts ??= new HashSet<ulong>();
        return mineContacts.Add(mine.SpawnId);
    }

    protected bool TryHitMine(Collider2D other, bool consumeOnHit = true)
    {
        if (!TryRegisterMineContact(other, out var mine)) return false;
        mine.ReceiveAttack();
        if (consumeOnHit && DespawnOnHit) Despawn();
        return true;
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

    protected bool TryRegisterHit(Collider2D other, out PlayerCombatant target, bool dealsDamage = true)
    {
        target = other.GetComponent<PlayerCombatant>();
        return spawned && AttackReady && target != null && target != Owner &&
            (dealsDamage ? target.CanReceiveDamage(Damage) : target.CanBeHit) &&
            (AllowRepeatedHits || hitTargets.Add(target));
    }

    protected virtual void OnDestroy() => Active.Remove(this);
}
