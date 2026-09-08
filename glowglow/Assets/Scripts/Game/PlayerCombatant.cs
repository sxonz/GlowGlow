using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(LocalPlayerInput))]
public sealed class PlayerCombatant : MonoBehaviour
{
    public event Action<PlayerCombatant> Hit;
    public int PlayerIndex => playerIndex;
    public int HitsTaken { get; private set; }
    public int HitsRemaining => Mathf.Max(0, 3 - HitsTaken);
    public bool CanBeHit => CanSelectWeapon && Time.time >= invulnerableUntil;
    public bool IsPreview { get; private set; }
    public float MovementMultiplier => (Time.time < slowedUntil ? slowMultiplier : 1f) * (IsOverdriving ? 1.5f : 1f);
    public OverdriveProjectile Overdrive { get; private set; }
    public bool IsOverdriving => Overdrive != null && Overdrive.IsRunning;
    public int ShieldRemaining => IsOverdriving ? Overdrive.ShieldRemaining : 0;
    private Vector2 inertiaVelocity;
    private float slowedUntil;
    private float slowMultiplier = 1f;
    private Vector2 knockbackRemaining;

    [SerializeField] private float moveSpeed = 5.2f;
    [SerializeField] private int playerIndex;
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = .16f;
    [SerializeField] private float dashCooldown = 1.1f;
    [SerializeField] private float hitInvulnerability = .8f;
    [SerializeField] private WeaponDefinition weapon;
    [SerializeField] private bool useDeckHand;
    [SerializeField] private DeckCatalog deckCatalog;
    [SerializeField] private Vector2 arenaExtents = new(8.2f, 4.35f);

    private LocalPlayerInput input;
    private MatchController match;
    private SpriteRenderer visuals;
    private SpriteRenderer bodyGlow;
    private Transform barrelPivot;
    private SpriteRenderer barrelVisuals;
    private const float BarrelCenter = .46f;
    private const float MuzzleOffset = BarrelCenter + .4f;
    private Vector2 aim = Vector2.right;
    private Vector2 aimPosition;
    private float nextDashAt;
    private float invulnerableUntil;
    private bool dashing;
    public WeaponRuntime CurrentWeapon { get; private set; }
    public WeaponHand Hand { get; private set; }
    public bool CanSelectWeapon => IsPreview || match != null && match.IsPlaying;

    public void ConfigurePreview(WeaponDefinition definition)
    {
        IsPreview = true;
        EquipWeapon(definition);
    }

    public void MovePreview(Vector2 direction, float distance)
    {
        if (IsPreview && Time.deltaTime > 0) MoveControlled(direction, distance * MovementMultiplier / Time.deltaTime, Time.deltaTime);
    }

    public void BeginOverdrive(OverdriveProjectile effect)
    {
        if (Overdrive != null && Overdrive != effect) Overdrive.Cancel();
        Overdrive = effect;
        inertiaVelocity = Vector2.zero;
        UpdateBarrel();
    }

    public void EndOverdrive(OverdriveProjectile effect)
    {
        if (Overdrive != effect) return;
        Overdrive = null;
        inertiaVelocity = Vector2.zero;
        UpdateBarrel();
    }

    public void ApplyImpact(Vector2 direction, float distance, float multiplier, float duration)
    {
        knockbackRemaining += direction.normalized * Mathf.Max(0, distance);
        if (duration <= 0) return;
        slowMultiplier = Time.time < slowedUntil ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
        slowMultiplier = Mathf.Clamp01(slowMultiplier);
        slowedUntil = Mathf.Max(slowedUntil, Time.time + duration);
    }

    public void ConfigureDeck(DeckCatalog catalog)
    {
        deckCatalog = catalog;
        useDeckHand = true;
    }

    public bool SelectWeaponSlot(int index)
    {
        if (!CanSelectWeapon) return false;
        if (Hand == null || Hand.Drawn.Count == 0) return index == 0 && CurrentWeapon != null;
        if (!Hand.Select(index)) return false;
        CurrentWeapon = Hand.Selected;
        return true;
    }

    public void EquipWeapon(WeaponDefinition definition)
    {
        weapon = definition;
        CurrentWeapon = definition != null ? new WeaponRuntime(definition) : null;
    }

    public void Configure(int index, MatchController controller, WeaponDefinition definition, LocalPlayerInput.ControlScheme scheme, Camera camera, Transform target)
    {
        playerIndex = index;
        match = controller;
        EquipWeapon(definition);
        input = GetComponent<LocalPlayerInput>();
        input.Configure(scheme, camera, target);
        visuals = GetComponent<SpriteRenderer>();
    }

    public void AttachMatch(MatchController controller)
    {
        match = controller;
    }

    private void Awake()
    {
        EquipWeapon(weapon);
        input = GetComponent<LocalPlayerInput>();
        visuals = GetComponent<SpriteRenderer>();
        if (visuals.sprite == null) visuals.sprite = RuntimeShapes.Circle;
        bodyGlow = RuntimeShapes.CreateGlow(visuals);
        var ring = transform.Find("Aim Ring");
        if (ring != null && ring.TryGetComponent<SpriteRenderer>(out var ringRenderer) && ringRenderer.sprite == null)
            ringRenderer.sprite = RuntimeShapes.Circle;
        barrelPivot = new GameObject("Barrel Pivot").transform;
        barrelPivot.SetParent(transform, false);
        barrelVisuals = new GameObject("Barrel", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
        barrelVisuals.transform.SetParent(barrelPivot, false);
        barrelVisuals.transform.localPosition = Vector3.right * BarrelCenter;
        barrelVisuals.sprite = RuntimeShapes.Barrel;
        UpdateBarrel();
    }

    private void LateUpdate()
    {
        UpdateBarrel();
        RuntimeShapes.SyncGlow(visuals, bodyGlow, .55f, 1.1f);
    }

    private void UpdateBarrel()
    {
        barrelPivot.right = aim;
        barrelVisuals.sortingLayerID = visuals.sortingLayerID;
        barrelVisuals.sortingOrder = visuals.sortingOrder - 1;
        barrelVisuals.enabled = visuals.enabled && !IsOverdriving;
    }

    private void Update()
    {
        if (!CanSelectWeapon)
        {
            if (Overdrive != null) Overdrive.Cancel();
            return;
        }
        if (knockbackRemaining.sqrMagnitude > .000001f)
        {
            Vector2 displacement = Vector2.ClampMagnitude(knockbackRemaining, Time.deltaTime * 6f);
            Move(displacement.normalized, displacement.magnitude);
            knockbackRemaining -= displacement;
        }
        if (IsPreview) return;
        if (match == null || !match.IsPlaying || dashing) return;
        PlayerCommand command = input.ReadCommand(transform.position);
        aimPosition = new Vector2(Mathf.Clamp(command.AimPosition.x, -arenaExtents.x, arenaExtents.x),
            Mathf.Clamp(command.AimPosition.y, -arenaExtents.y, arenaExtents.y));
        if (command.SelectedSlot >= 0) SelectWeaponSlot(command.SelectedSlot);
        if (command.Aim.sqrMagnitude > .01f) aim = command.Aim.normalized;
        if (command.DashPressed && command.Move.sqrMagnitude > .01f && Time.time >= nextDashAt)
            StartCoroutine(Dash(command.Move.normalized));
        else
            MoveControlled(command.Move, moveSpeed * MovementMultiplier, Time.deltaTime);

        if (command.Fire) Fire();
    }

    private void Move(Vector2 direction, float distance)
    {
        Vector2 next = (Vector2)transform.position + direction * distance;
        next.x = Mathf.Clamp(next.x, -arenaExtents.x, arenaExtents.x);
        next.y = Mathf.Clamp(next.y, -arenaExtents.y, arenaExtents.y);
        transform.position = next;
    }

    private void MoveControlled(Vector2 direction, float speed, float deltaTime)
    {
        if (!IsOverdriving)
        {
            inertiaVelocity = Vector2.zero;
            Move(direction, speed * deltaTime);
            return;
        }
        // Exponential response gives the same acceleration/coasting at any frame rate.
        float blend = 1f - Mathf.Exp(-deltaTime / .22f);
        inertiaVelocity = Vector2.Lerp(inertiaVelocity, Vector2.ClampMagnitude(direction, 1) * speed, blend);
        Vector2 before = transform.position;
        Move(inertiaVelocity.normalized, inertiaVelocity.magnitude * deltaTime);
        Vector2 after = transform.position;
        if (Mathf.Abs(after.x) >= arenaExtents.x && Mathf.Abs(after.x - before.x) < .0001f) inertiaVelocity.x = 0;
        if (Mathf.Abs(after.y) >= arenaExtents.y && Mathf.Abs(after.y - before.y) < .0001f) inertiaVelocity.y = 0;
    }

    private IEnumerator Dash(Vector2 direction)
    {
        dashing = true;
        nextDashAt = Time.time + dashCooldown;
        invulnerableUntil = Time.time + dashDuration;
        float end = Time.time + dashDuration;
        while (Time.time < end)
        {
            Move(direction, dashSpeed * MovementMultiplier * Time.deltaTime);
            yield return null;
        }
        dashing = false;
    }

    private void Fire()
    {
        if (CurrentWeapon == null || IsOverdriving) return;
        UpdateBarrel();
        Vector2 origin = barrelPivot.TransformPoint(Vector3.right * MuzzleOffset);
        CurrentWeapon.Fire(this, origin, aim, aimPosition);
    }

    public void ReceiveHit(PlayerCombatant attacker)
    {
        if (!CanBeHit) return;
        if (IsOverdriving && Overdrive.AbsorbHit())
        {
            invulnerableUntil = Time.time + hitInvulnerability;
            Hit?.Invoke(this);
            return;
        }
        HitsTaken++;
        invulnerableUntil = Time.time + hitInvulnerability;
        if (!IsPreview) StartCoroutine(HitFlash());
        Hit?.Invoke(this);
        if (!IsPreview) match.ReportHit(this, attacker);
    }

    public void ResetCombatant(Vector2 position)
    {
        if (Overdrive != null) Overdrive.Cancel();
        inertiaVelocity = Vector2.zero;
        EquipWeapon(weapon);
        if (useDeckHand)
        {
            Hand = new WeaponHand(deckCatalog != null ? deckCatalog.LoadSelectedWeapons() : null, new System.Random());
            // Drawing is optional until a valid deck exists; keep the original basic shot playable.
            // The basic shot is not inserted into G and does not change the subset rule.
            if (Hand.Selected != null) CurrentWeapon = Hand.Selected;
        }
        StopAllCoroutines();
        transform.position = position;
        HitsTaken = 0;
        invulnerableUntil = Time.time + .5f;
        nextDashAt = 0f;
        dashing = false;
        slowedUntil = 0;
        slowMultiplier = 1;
        knockbackRemaining = Vector2.zero;
        aim = position.x > 0f ? Vector2.left : Vector2.right;
        if (visuals != null) visuals.enabled = true;
        UpdateBarrel();
    }

    private IEnumerator HitFlash()
    {
        for (int i = 0; i < 6; i++)
        {
            visuals.enabled = !visuals.enabled;
            yield return new WaitForSeconds(.08f);
        }
        visuals.enabled = true;
    }
}
