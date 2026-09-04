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
    public bool CanBeHit => match != null && match.IsPlaying && Time.time >= invulnerableUntil;

    [SerializeField] private float moveSpeed = 5.2f;
    [SerializeField] private int playerIndex;
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = .16f;
    [SerializeField] private float dashCooldown = 1.1f;
    [SerializeField] private float hitInvulnerability = .8f;
    [SerializeField] private WeaponDefinition weapon;
    [SerializeField] private Vector2 arenaExtents = new(8.2f, 4.35f);

    private LocalPlayerInput input;
    private MatchController match;
    private SpriteRenderer visuals;
    private Vector2 aim = Vector2.right;
    private float nextFireAt;
    private float nextDashAt;
    private float invulnerableUntil;
    private bool dashing;

    public void Configure(int index, MatchController controller, WeaponDefinition definition, LocalPlayerInput.ControlScheme scheme, Camera camera, Transform target)
    {
        playerIndex = index;
        match = controller;
        weapon = definition;
        input = GetComponent<LocalPlayerInput>();
        input.Configure(scheme, camera, target);
        visuals = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        input = GetComponent<LocalPlayerInput>();
        visuals = GetComponent<SpriteRenderer>();
        if (visuals.sprite == null) visuals.sprite = RuntimeShapes.Circle;
    }

    private void Update()
    {
        if (match == null || !match.IsPlaying || dashing) return;
        PlayerCommand command = input.ReadCommand(transform.position);
        if (command.Aim.sqrMagnitude > .01f) aim = command.Aim.normalized;
        if (command.DashPressed && command.Move.sqrMagnitude > .01f && Time.time >= nextDashAt)
            StartCoroutine(Dash(command.Move.normalized));
        else
            Move(command.Move, moveSpeed * Time.deltaTime);

        if (command.Fire && Time.time >= nextFireAt) Fire();
    }

    private void Move(Vector2 direction, float distance)
    {
        Vector2 next = (Vector2)transform.position + direction * distance;
        next.x = Mathf.Clamp(next.x, -arenaExtents.x, arenaExtents.x);
        next.y = Mathf.Clamp(next.y, -arenaExtents.y, arenaExtents.y);
        transform.position = next;
    }

    private IEnumerator Dash(Vector2 direction)
    {
        dashing = true;
        nextDashAt = Time.time + dashCooldown;
        invulnerableUntil = Time.time + dashDuration;
        float end = Time.time + dashDuration;
        while (Time.time < end)
        {
            Move(direction, dashSpeed * Time.deltaTime);
            yield return null;
        }
        dashing = false;
    }

    private void Fire()
    {
        nextFireAt = Time.time + weapon.cooldown;
        Vector2 origin = (Vector2)transform.position + aim * .48f;
        BulletProjectile.Spawn(this, origin, aim, weapon);
    }

    public void ReceiveHit(PlayerCombatant attacker)
    {
        if (!CanBeHit) return;
        HitsTaken++;
        invulnerableUntil = Time.time + hitInvulnerability;
        StartCoroutine(HitFlash());
        Hit?.Invoke(this);
        match.ReportHit(this, attacker);
    }

    public void ResetCombatant(Vector2 position)
    {
        StopAllCoroutines();
        transform.position = position;
        HitsTaken = 0;
        invulnerableUntil = Time.time + .5f;
        nextFireAt = nextDashAt = 0f;
        dashing = false;
        if (visuals != null) visuals.enabled = true;
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
