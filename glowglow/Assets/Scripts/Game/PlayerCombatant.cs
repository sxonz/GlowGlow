using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer), typeof(LocalPlayerInput))]
public sealed partial class PlayerCombatant : MonoBehaviour
{
    public event Action<PlayerCombatant> Hit;
    public int PlayerIndex => playerIndex;
    public int HitsTaken { get; private set; }
    public int HitsRemaining => Mathf.Max(0, 3 - HitsTaken);
    public bool CanBeHit => CanSelectWeapon && !IsMeteorDiving && Time.time >= invulnerableUntil;
    public MeteorDive MeteorDive { get; private set; }
    public bool IsMeteorDiving => MeteorDive != null;
    public Vector2 AimPosition => aimPosition;
    private SpriteRenderer[] diveRenderers;
    private bool[] diveRendererStates;
    private bool diveColliderEnabled;
    private bool diveVisible;
    public bool IsPreview { get; private set; }
    public float MovementMultiplier => (Time.time < slowedUntil ? slowMultiplier : 1f) *
        (Time.time < laserSlowUntil ? .65f : 1f) * PulseControlMultiplier;
    public OverdriveProjectile Overdrive { get; private set; }
    public bool IsOverdriving => Overdrive != null && Overdrive.IsRunning;
    public int ShieldRemaining => NetworkReplica ? networkShield : landingShield + (IsOverdriving ? Overdrive.ShieldRemaining : 0);
    private int landingShield;
    private LineRenderer landingShieldVisual;
    private static Material landingShieldMaterial;

    public void GrantLandingShield()
    {
        landingShield = 1; // Replenish one charge; repeated dives cannot accumulate shields.
        if (landingShieldVisual == null)
        {
            landingShieldVisual = new GameObject("Landing Shield",typeof(LineRenderer)).GetComponent<LineRenderer>();
            landingShieldVisual.transform.SetParent(transform,false);
            landingShieldVisual.gameObject.layer = gameObject.layer;
            if (landingShieldMaterial == null) landingShieldMaterial = new Material(Shader.Find("Sprites/Default"));
            landingShieldVisual.sharedMaterial = landingShieldMaterial;
            landingShieldVisual.useWorldSpace = false;
            landingShieldVisual.loop = true;
            landingShieldVisual.positionCount = 64;
            landingShieldVisual.startWidth = landingShieldVisual.endWidth = .05f;
            landingShieldVisual.startColor = landingShieldVisual.endColor = new Color(.45f,.9f,1f);
            landingShieldVisual.sortingOrder = 7;
            float radius = GetComponent<CircleCollider2D>().radius + .12f;
            for(int i=0;i<64;i++)
            {
                float angle = i*Mathf.PI*2/64;
                landingShieldVisual.SetPosition(i,new Vector3(Mathf.Cos(angle),Mathf.Sin(angle))*radius);
            }
        }
        UpdateLandingShield();
    }

    private void UpdateLandingShield()
    {
        if (landingShieldVisual != null)
            landingShieldVisual.enabled = landingShield > 0 && (!IsMeteorDiving || diveVisible);
    }
    private Vector2 inertiaVelocity;
    private float slowedUntil;
    private float slowMultiplier = 1f;
    private Vector2 knockbackRemaining;
    private Vector2 laserRecoilRemaining;
    private float straightCharge;
    private Vector2 previousDriveDirection;
    private float lastDashEndedAt = float.NegativeInfinity;
    private float dashEndsAt = float.PositiveInfinity;
    public bool IsPostDashWindow => dashing
        ? dashEndsAt - Time.time <= .25f
        : Time.time - lastDashEndedAt <= .35f;
    public Vector2 ArenaHalfSize => arenaExtents;
    public float BaseMoveSpeed => moveSpeed;
    public float BodyRadius => GetComponent<CircleCollider2D>().radius * Mathf.Abs(transform.lossyScale.x);
    private PlayerCombatant pulseSource;
    private Vector2 pulseDirection;
    private float pulseRemaining, pulseTotal, pulseDecay;
    private bool pulsePull, pulseCollisionDamage;
    private float PulseControlMultiplier => pulseRemaining > .001f ? Mathf.Lerp(.25f, 1f, 1f - pulseRemaining / Mathf.Max(.001f, pulseTotal)) : 1f;

    [SerializeField] private float moveSpeed = 5.8f;
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
    private IPlayerInputSource inputOverride;
    public void SetInputSource(IPlayerInputSource source) => inputOverride = source;
    private MatchController match;
    private SpriteRenderer visuals;
    private SpriteRenderer bodyGlow;
    private Transform barrelPivot;
    private SpriteRenderer barrelVisuals;
    private const float BarrelCenter = .46f;
    private const float MuzzleOffset = BarrelCenter + .4f;
    private Vector2 aim = Vector2.right;
    private Vector2 aimPosition;
    private float laserAimUntil;
    private float laserSlowUntil;
    private float laserRecoveryUntil;
    private float laserAngularVelocity;
    private int prismAimLocks;
    private bool prismAimRecovering;
    public bool IsPrismAimLocked => prismAimLocks > 0;
    public void BeginPrismAim(Vector2 direction)
    {
        prismAimLocks++;
        prismAimRecovering=false;
        aim=direction.normalized;
    }
    public void EndPrismAim()
    {
        prismAimLocks=Mathf.Max(0,prismAimLocks-1);
        if(prismAimLocks==0) prismAimRecovering=true;
    }
    internal Vector2 UpdatePrismAim(Vector2 target,float deltaTime)
    {
        if(IsPrismAimLocked) return aim;
        var next=(Vector2)Vector3.RotateTowards(aim,target,720*Mathf.Deg2Rad*deltaTime,0);
        if(Vector2.Angle(next,target)<.01f) prismAimRecovering=false;
        return next;
    }
    public Vector2 AimDirection => aim;
    public Vector2 MuzzlePosition
    {
        get { UpdateBarrel(); return barrelPivot.TransformPoint(Vector3.right * MuzzleOffset); }
    }

    public void BeginLaserAim(float duration, bool applySlow = true)
    {
        // Telegraph/body transitions and overlapping shots preserve the current turn momentum.
        if (Time.time >= laserAimUntil) laserAngularVelocity = 0f;
        laserAimUntil = Mathf.Max(laserAimUntil, Time.time + duration);
        if (applySlow) laserSlowUntil = Mathf.Max(laserSlowUntil, Time.time + duration);
        laserRecoveryUntil = Mathf.Max(laserRecoveryUntil, laserAimUntil + .18f);
    }

    public void ApplyLaserRecoil() => laserRecoilRemaining -= aim * .9f;

    public Vector2 RandomArenaPosition() => new Vector2(
        UnityEngine.Random.Range(-arenaExtents.x + .5f, arenaExtents.x - .5f),
        UnityEngine.Random.Range(-arenaExtents.y + .5f, arenaExtents.y - .5f));

    public Vector2 ClampToArena(Vector2 position) => new Vector2(
        Mathf.Clamp(position.x, -arenaExtents.x, arenaExtents.x), Mathf.Clamp(position.y, -arenaExtents.y, arenaExtents.y));

    public void BeginMeteorDive(MeteorDive effect)
    {
        if (MeteorDive != null) MeteorDive.Cancel();
        if (Overdrive != null) Overdrive.Cancel();
        StopAllCoroutines();
        dashing = false;
        lastDashEndedAt = float.NegativeInfinity;
        dashEndsAt = float.PositiveInfinity;
        dashVisual = 0;
        inertiaVelocity = knockbackRemaining = laserRecoilRemaining = Vector2.zero;
        pulseRemaining = 0;
        ClearAfterimages();
        // Cancel a hit-flash before capturing the normal visible body state.
        visuals.enabled = true;
        MeteorDive = effect;
        diveVisible = false;
        diveRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        diveRendererStates = new bool[diveRenderers.Length];
        for (int i = 0; i < diveRenderers.Length; i++) diveRendererStates[i] = diveRenderers[i].enabled;
        var body = GetComponent<CircleCollider2D>();
        diveColliderEnabled = body.enabled;
        body.enabled = false;
        UpdateDiveVisuals();
    }

    public void MoveMeteorDive(Vector2 position)
    {
        transform.position = position;
        diveVisible = true;
        UpdateDiveVisuals();
    }

    public void EndMeteorDive(MeteorDive effect, Vector2 position)
    {
        if (MeteorDive != effect) return;
        transform.position = ClampToArena(position);
        MeteorDive = null;
        for (int i = 0; i < diveRenderers.Length; i++)
            if (diveRenderers[i] != null) diveRenderers[i].enabled = diveRendererStates[i];
        GetComponent<CircleCollider2D>().enabled = diveColliderEnabled;
        diveRenderers = null;
        diveRendererStates = null;
        UpdateBarrel();
    }

    private void UpdateDiveVisuals()
    {
        foreach (var renderer in diveRenderers)
            if (renderer != null) renderer.enabled = diveVisible && renderer == visuals;
        if (diveVisible) RuntimeShapes.SyncGlow(visuals, bodyGlow, .8f, .7f);
    }

    public void ApplyPulseImpulse(PlayerCombatant source, Vector2 center, float range, bool pull, bool empowered)
    {
        Vector2 away = (Vector2)transform.position - center;
        float distance = away.magnitude;
        pulseDirection = distance > .0001f ? away / distance : source.AimDirection;
        pulseSource = source;
        pulsePull = pull;
        pulseCollisionDamage = empowered;
        pulseRemaining = pull ? Mathf.Max(0f, distance - BodyRadius - source.BodyRadius) : Mathf.Max(0f, range - distance);
        if (empowered && !pull) pulseRemaining *= 3f;
        pulseTotal = pulseRemaining;
        pulseDecay = (.12f + Mathf.Min(.28f, pulseTotal * .045f)) / (empowered ? 3f : 1f);
        if (pull && empowered && distance <= BodyRadius + source.BodyRadius + .001f) ReceiveHit(source);
    }

    private void UpdatePulseImpulse(float deltaTime)
    {
        if (pulseRemaining <= .001f) return;
        if (pulseSource == null || !pulseSource.CanSelectWeapon) { pulseRemaining = 0; return; }
        float distance = pulseRemaining * (1f - Mathf.Exp(-deltaTime / Mathf.Max(.02f, pulseDecay)));
        if (pulseRemaining < .01f) distance = pulseRemaining;
        Vector2 direction = pulseDirection;
        float gap = float.PositiveInfinity;
        if (pulsePull)
        {
            Vector2 toward = pulseSource.transform.position - transform.position;
            gap = Mathf.Max(0, toward.magnitude - BodyRadius - pulseSource.BodyRadius);
            direction = toward.sqrMagnitude > .0001f ? toward.normalized : Vector2.zero;
            distance = Mathf.Min(distance, gap);
        }
        Vector2 before = transform.position;
        Vector2 requested = before + direction * distance;
        Vector2 after = ClampToArena(requested);
        transform.position = after;
        bool wall = (requested - after).sqrMagnitude > .0000001f;
        bool reachedSource = pulsePull && gap <= distance + .001f;
        pulseRemaining = Mathf.Max(0, pulseRemaining - distance);
        if (wall || reachedSource)
        {
            if (pulseCollisionDamage && (pulsePull ? reachedSource : wall)) ReceiveHit(pulseSource);
            pulseRemaining = 0;
            pulseCollisionDamage = false;
        }
    }

    internal static Vector2 TurnAim(Vector2 current, Vector2 target, ref float angularVelocity, float deltaTime)
    {
        if (deltaTime <= 0f) return current;
        float angle = Mathf.SmoothDampAngle(Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg,
            Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg, ref angularVelocity, .18f, 30f, deltaTime);
        return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }

    internal static Vector2 RecoverAim(Vector2 current, Vector2 target, ref float angularVelocity, float deltaTime)
    {
        if (deltaTime <= 0) return current;
        float angle = Mathf.SmoothDampAngle(Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg,
            Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg, ref angularVelocity, .035f, 1440f, deltaTime);
        return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }
    private float nextDashAt;
    private float invulnerableUntil;
    private bool dashing;
    private Vector2 dashDirection;
    private float dashVisual;
    private const int AfterimageCount = 10;
    private const float AfterimageSpacing = .28f;
    private const float AfterimageLifetime = .16f;
    private Transform afterimageRoot;
    private readonly SpriteRenderer[] afterimages = new SpriteRenderer[AfterimageCount];
    private readonly float[] afterimageTimes = new float[AfterimageCount];
    private readonly float[] afterimageAlphas = new float[AfterimageCount];
    private int nextAfterimage;
    private float afterimageDistance;
    public WeaponRuntime CurrentWeapon { get; private set; }
    public WeaponHand Hand { get; private set; }
    public void EquipDraft(OpeningDraft draft, int deckCount)
    {
        Hand = WeaponHand.FromDraft(draft, deckCount);
        CurrentWeapon = Hand.Selected;
    }
    public bool CanSelectWeapon => IsPreview || match != null && match.IsPlaying;

    public void ConfigurePreview(WeaponDefinition definition)
    {
        IsPreview = true;
        EquipWeapon(definition);
    }

    public void MovePreview(Vector2 direction, float distance)
    {
        if (IsPreview && !IsMeteorDiving && Time.deltaTime > 0) MoveControlled(direction, distance * MovementMultiplier / Time.deltaTime, Time.deltaTime);
    }

    public void BeginOverdrive(OverdriveProjectile effect)
    {
        if (Overdrive != null && Overdrive != effect) Overdrive.Cancel();
        Overdrive = effect;
        inertiaVelocity = Vector2.zero;
        straightCharge = 0f;
        previousDriveDirection = Vector2.zero;
        UpdateBarrel();
    }

    public void EndOverdrive(OverdriveProjectile effect)
    {
        if (Overdrive != effect) return;
        Overdrive = null;
        inertiaVelocity = Vector2.zero;
        straightCharge = 0f;
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
        if (NetworkReplica && GlowGlow.Online.OnlineSession.Current != null)
            return GlowGlow.Online.OnlineSession.Current.SelectSlot(this, index);
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

    public void EquipTrainingWeapon(WeaponRuntime runtime)
    {
        if (match == null || !match.IsTraining) return;
        Hand = null;
        CurrentWeapon = runtime;
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
        PrepareAfterimages();
        UpdateBarrel();
    }

    private void LateUpdate()
    {
        if (NetworkReplica) return;
        UpdateLandingShield();
        if (IsMeteorDiving) { UpdateDiveVisuals(); return; }
        UpdateAfterimages();
        UpdateBarrel();
        RuntimeShapes.SyncGlow(visuals, bodyGlow, .55f, 1.1f);
        // Stretch only the prepared glow sprite, never the body or its hitbox.
        float target = dashing ? 1f : 0f;
        dashVisual = Mathf.MoveTowards(dashVisual, target, Time.deltaTime / (dashing ? .025f : .09f));
        if (!bodyGlow.enabled) return;
        bodyGlow.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg);
        var glowScale = bodyGlow.transform.localScale;
        // Offset by half the added length so the leading edge stays at its resting position.
        Vector3 backwardOffset = bodyGlow.transform.TransformVector(
            Vector3.right * (bodyGlow.sprite.bounds.extents.x * .2f * dashVisual));
        bodyGlow.transform.localScale = new Vector3(glowScale.x * (1 + .2f * dashVisual),
            glowScale.y * (1 - .08f * dashVisual), 1);
        bodyGlow.transform.position -= backwardOffset;
    }

    private void PrepareAfterimages()
    {
        // Separate world-space renderers reuse the prepared body sprite; no barrel or collider is copied.
        afterimageRoot = new GameObject(name + " Body Afterimages").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(afterimageRoot.gameObject, gameObject.scene);
        for (int i = 0; i < AfterimageCount; i++)
        {
            var ghost = new GameObject("Body Afterimage " + i, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            ghost.transform.SetParent(afterimageRoot, false);
            ghost.enabled = false;
            afterimages[i] = ghost;
        }
    }

    private void LeaveAfterimages(Vector3 from, Vector3 to)
    {
        float distance = Vector3.Distance(from, to);
        if (distance < .00001f) return;
        // Sample distance along the actual, clamped path so low frame rates and arena walls leave no gaps or piles.
        for (float offset = AfterimageSpacing - afterimageDistance; offset <= distance; offset += AfterimageSpacing)
        {
            if (!visuals.enabled) break;
            var ghost = afterimages[nextAfterimage];
            ghost.sprite = visuals.sprite;
            ghost.sharedMaterial = visuals.sharedMaterial;
            ghost.flipX = visuals.flipX;
            ghost.flipY = visuals.flipY;
            ghost.sortingLayerID = visuals.sortingLayerID;
            ghost.sortingOrder = visuals.sortingOrder - 2;
            ghost.transform.SetPositionAndRotation(Vector3.Lerp(from, to, offset / distance), visuals.transform.rotation);
            ghost.transform.localScale = visuals.transform.lossyScale;
            var color = visuals.color;
            color.a *= .26f;
            ghost.color = color;
            ghost.enabled = true;
            afterimageAlphas[nextAfterimage] = color.a;
            afterimageTimes[nextAfterimage] = Time.time;
            nextAfterimage = (nextAfterimage + 1) % AfterimageCount;
        }
        afterimageDistance = (afterimageDistance + distance) % AfterimageSpacing;
    }

    private void UpdateAfterimages()
    {
        if (!CanSelectWeapon) { ClearAfterimages(); return; }
        for (int i = 0; i < AfterimageCount; i++)
        {
            var ghost = afterimages[i];
            if (ghost == null || !ghost.enabled) continue;
            float fade = Mathf.Clamp01(1 - (Time.time - afterimageTimes[i]) / AfterimageLifetime);
            var color = ghost.color;
            color.a = afterimageAlphas[i] * fade * fade;
            ghost.color = color;
            ghost.enabled = fade > 0;
        }
    }

    private void ClearAfterimages()
    {
        foreach (var ghost in afterimages)
            if (ghost != null) ghost.enabled = false;
        nextAfterimage = 0;
        afterimageDistance = 0;
    }

    private void OnDisable()
    {
        if (MeteorDive != null) MeteorDive.Cancel();
        ClearAfterimages();
    }

    private void OnDestroy()
    {
        if (afterimageRoot != null) Destroy(afterimageRoot.gameObject);
    }

    private void UpdateBarrel()
    {
        barrelPivot.right = aim;
        barrelVisuals.sortingLayerID = visuals.sortingLayerID;
        barrelVisuals.sortingOrder = visuals.sortingOrder - 1;
        barrelVisuals.enabled = visuals.enabled && !IsOverdriving && !IsMeteorDiving;
    }

    private void Update()
    {
        if (NetworkReplica) return;
        if (!CanSelectWeapon)
        {
            if (MeteorDive != null) MeteorDive.Cancel();
            if (Overdrive != null) Overdrive.Cancel();
            pulseRemaining = 0;
            return;
        }
        if (IsMeteorDiving)
        {
            if (!IsPreview)
            {
                var diveCommand = (inputOverride ?? input).ReadCommand(transform.position);
                aimPosition = ClampToArena(diveCommand.AimPosition);
                if (diveCommand.FirePressed) MeteorDive.RequestDive();
            }
            return;
        }
        UpdatePulseImpulse(Time.deltaTime);
        UpdateImpacts(Time.deltaTime);
        if (IsPreview) return;
        if (match == null || !match.IsPlaying) return;
        PlayerCommand command = match.IsTraining && playerIndex == match.PlayerTwo.PlayerIndex
            ? new PlayerCommand { SelectedSlot = -1, Aim = Vector2.left }
            : (inputOverride ?? input).ReadCommand(transform.position);
        aimPosition = new Vector2(Mathf.Clamp(command.AimPosition.x, -arenaExtents.x, arenaExtents.x),
            Mathf.Clamp(command.AimPosition.y, -arenaExtents.y, arenaExtents.y));
        if (command.SelectedSlot >= 0) SelectWeaponSlot(command.SelectedSlot);
        else if (command.WeaponCycle != 0)
        {
            if (match.IsTraining)
                match.GetComponent<TrainingGround>().CycleWeapon(command.WeaponCycle);
            else if (Hand != null && Hand.Drawn.Count > 0)
                SelectWeaponSlot((Hand.SelectedIndex + command.WeaponCycle + Hand.Drawn.Count) % Hand.Drawn.Count);
        }
        Vector2 targetAim = command.Aim.sqrMagnitude > .01f ? command.Aim.normalized : aim;
        if (IsPrismAimLocked || prismAimRecovering)
            aim = UpdatePrismAim(targetAim,Time.deltaTime);
        else if (Time.time < laserAimUntil)
            aim = TurnAim(aim, targetAim, ref laserAngularVelocity, Time.deltaTime);
        else if (Time.time < laserRecoveryUntil && Time.deltaTime > 0)
            aim = RecoverAim(aim, targetAim, ref laserAngularVelocity, Time.deltaTime);
        else
        {
            laserAngularVelocity = 0f;
            aim = targetAim;
        }
        if (!dashing && command.DashPressed && command.Move.sqrMagnitude > .01f && Time.time >= nextDashAt)
            StartCoroutine(Dash(command.Move.normalized));
        else if (!dashing)
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

    private void UpdateImpacts(float deltaTime)
    {
        ConsumeImpact(ref knockbackRemaining, 6f, deltaTime);
        // A short, fast recoil impulse still pushes backward against forward movement.
        ConsumeImpact(ref laserRecoilRemaining, 12f, deltaTime);
    }

    private void ConsumeImpact(ref Vector2 remaining, float speed, float deltaTime)
    {
        if (remaining.sqrMagnitude <= .000001f) return;
        Vector2 displacement = Vector2.ClampMagnitude(remaining, deltaTime * speed);
        Move(displacement.normalized, displacement.magnitude);
        remaining -= displacement;
    }

    private void MoveControlled(Vector2 direction, float speed, float deltaTime)
    {
        if (!IsOverdriving)
        {
            inertiaVelocity = Vector2.zero;
            Move(direction, speed * deltaTime);
            return;
        }
        bool hasInput = direction.sqrMagnitude > .01f;
        bool straight = hasInput && previousDriveDirection.sqrMagnitude > .01f && Vector2.Dot(direction.normalized, previousDriveDirection) > .985f;
        straightCharge = Mathf.MoveTowards(straightCharge, straight ? 1f : 0f,
            deltaTime * (straight ? 1f / Overdrive.AccelerationTime : 3f));
        if (hasInput) previousDriveDirection = direction.normalized;
        float ramp = Mathf.Lerp(1f, Overdrive.TopSpeedMultiplier, straightCharge);
        // Faster straight travel retains more momentum when releasing/reversing input.
        float speedCharge = Mathf.InverseLerp(speed, speed * Overdrive.TopSpeedMultiplier, inertiaVelocity.magnitude);
        float inertiaTime = Mathf.Lerp(.16f, .38f, Mathf.Max(straightCharge, speedCharge)) * Overdrive.InertiaMultiplier;
        float blend = 1f - Mathf.Exp(-deltaTime / Mathf.Max(.02f, inertiaTime));
        inertiaVelocity = Vector2.Lerp(inertiaVelocity, Vector2.ClampMagnitude(direction, 1) * speed * ramp, blend);
        Vector2 before = transform.position;
        Move(inertiaVelocity.normalized, inertiaVelocity.magnitude * deltaTime);
        Vector2 after = transform.position;
        if (hasInput && (after - before).sqrMagnitude < .0000001f) straightCharge = 0;
        if (Mathf.Abs(after.x) >= arenaExtents.x && Mathf.Abs(after.x - before.x) < .0001f) inertiaVelocity.x = 0;
        if (Mathf.Abs(after.y) >= arenaExtents.y && Mathf.Abs(after.y - before.y) < .0001f) inertiaVelocity.y = 0;
    }

    private IEnumerator Dash(Vector2 direction)
    {
        dashing = true;
        dashDirection = direction;
        afterimageDistance = AfterimageSpacing;
        inertiaVelocity = Vector2.zero;
        nextDashAt = Time.time + dashCooldown;
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + dashDuration);
        float duration = Mathf.Max(.01f, dashDuration);
        dashEndsAt = Time.time + duration;
        float elapsed = 0;
        while (elapsed < duration && CanSelectWeapon)
        {
            float previous = elapsed / duration;
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float current = elapsed / duration;
            // Integrate a 1.65 -> 0.35 speed ramp: sharp launch, soft landing,
            // with the same total dash distance at every frame rate.
            float distance = (current - previous) * (1.65f - .65f * (current + previous));
            Vector3 before = transform.position;
            Move(direction, dashSpeed * duration * distance * MovementMultiplier);
            LeaveAfterimages(before, transform.position);
            yield return null;
        }
        dashing = false;
        lastDashEndedAt = Time.time;
    }

    private void Fire()
    {
        if (CurrentWeapon == null || IsOverdriving || IsMeteorDiving) return;
        UpdateBarrel();
        Vector2 origin = barrelPivot.TransformPoint(Vector3.right * MuzzleOffset);
        CurrentWeapon.Fire(this, origin, aim, aimPosition);
    }

    public void ReceiveHit(PlayerCombatant attacker)
    {
        if (NetworkReplica) return;
        if (!CanBeHit) return;
        if (landingShield > 0)
        {
            landingShield = 0;
            UpdateLandingShield();
            invulnerableUntil = Time.time + hitInvulnerability;
            Hit?.Invoke(this);
            return;
        }
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
        landingShield = 0;
        UpdateLandingShield();
        if (MeteorDive != null) MeteorDive.Cancel();
        laserAimUntil = 0;
        prismAimLocks=0;
        prismAimRecovering=false;
        laserSlowUntil = laserRecoveryUntil = 0;
        laserAngularVelocity = 0;
        if (Overdrive != null) Overdrive.Cancel();
        inertiaVelocity = Vector2.zero;
        straightCharge = 0;
        previousDriveDirection = Vector2.zero;
        lastDashEndedAt = float.NegativeInfinity;
        dashEndsAt = float.PositiveInfinity;
        pulseRemaining = pulseTotal = 0;
        pulseSource = null;
        pulseCollisionDamage = false;
        EquipWeapon(weapon);
        if (useDeckHand)
        {
            Hand = null;
        }
        StopAllCoroutines();
        ClearAfterimages();
        transform.position = position;
        HitsTaken = 0;
        invulnerableUntil = Time.time + .5f;
        nextDashAt = 0f;
        dashing = false;
        dashVisual = 0;
        dashDirection = Vector2.zero;
        slowedUntil = 0;
        slowMultiplier = 1;
        knockbackRemaining = Vector2.zero;
        laserRecoilRemaining = Vector2.zero;
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
