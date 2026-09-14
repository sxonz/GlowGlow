using UnityEngine;
using System.Collections.Generic;

/// <summary>Rating changes decisions, never health, movement speed, or weapon stats.</summary>
public sealed class BotPlayerInput : MonoBehaviour, IPlayerInputSource
{
    public const int MinRating = 200, MaxRating = 3000, DefaultRating = 1200;
    public int Rating { get; private set; }
    public static int RatingTier(int rating) => rating switch
    {
        >= 3000 => 6,
        >= 2800 => 5,
        >= 2400 => 4,
        >= 1800 => 3,
        >= 1200 => 2,
        >= 600 => 1,
        _ => 0
    };
    public float ReactionInterval => Mathf.Lerp(.95f, 0f, skill);
    public float PredictionHorizon => Mathf.Lerp(.02f, .6f, skill * skill);
    public float AvoidanceMargin => Mathf.Lerp(.55f, .08f, skill);
    public float EscapeCommitment => Mathf.Lerp(.9f, 0f, skill);
    public float DashProbability => Mathf.Lerp(.01f, 1f, skill);
    public float AimError => Mathf.Lerp(3f, .06f, skill);
    public float FireProbability => Mathf.Lerp(.2f, 1f, skill);
    public float AvoidanceResponse => Mathf.Lerp(.15f, 1f, skill);
    public int EscapeDirections => 8 + Mathf.FloorToInt(56 * skill);
    private float skill, nextDecision, previousObservationAt, strafeUntil;
    private int strafeSign = 1;
    private Vector2 previousTarget;
    private PlayerCombatant self, target;
    private MatchController match;
    private PlayerCommand command;
    private readonly Collider2D[] nearby = new Collider2D[64];
    private readonly List<BarrageShapeProjectile> visibleAttacks = new();
    private readonly List<BulletProjectile> incomingBullets = new();
    private Vector2 escapeDirection;
    private float escapeUntil, movementPace = 1f, radialFeint;

    public void Configure(PlayerCombatant owner, PlayerCombatant opponent, MatchController controller, int rating)
    {
        self = owner; target = opponent; match = controller;
        Rating = Mathf.Clamp(rating, MinRating, MaxRating);
        // Keep 97% of the decision curve continuous. Each tier adds just 0.5%
        // of the full skill range, leaving room for future deck-based difficulty.
        skill = .97f * Mathf.InverseLerp(MinRating, MaxRating, Rating) + .005f * RatingTier(Rating);
        ResetBrain();
    }

    public void ResetBrain()
    {
        command = new PlayerCommand { SelectedSlot = -1 };
        previousTarget = target != null ? (Vector2)target.transform.position : Vector2.zero;
        previousObservationAt = Time.time;
        nextDecision = Time.time + ReactionInterval;
        strafeUntil = Time.time + 1f;
        strafeSign = Random.value < .5f ? -1 : 1;
        escapeDirection = Vector2.zero;
        escapeUntil = 0;
        movementPace = 1f;
        radialFeint = 0;
    }

    public PlayerCommand ReadCommand(Vector2 worldPosition)
    {
        if (match == null || !match.IsPlaying || self == null || target == null)
            return new PlayerCommand { SelectedSlot = -1 };
        if (Time.time >= nextDecision)
        {
            Think(worldPosition);
            nextDecision = Time.time + ReactionInterval;
        }
        var result = command;
        command.DashPressed = false;
        return result;
    }

    private void Think(Vector2 position)
    {
        Vector2 observed = target.transform.position;
        float elapsed = Mathf.Max(.01f, Time.time - previousObservationAt);
        Vector2 velocity = Vector2.ClampMagnitude((observed - previousTarget) / elapsed, 15f);
        previousTarget = observed;
        previousObservationAt = Time.time;
        Vector2 toward = observed - position;
        float distance = toward.magnitude;
        Vector2 forward = distance > .01f ? toward / distance : Vector2.left;
        int slot = ChooseWeapon(distance);
        var weapon = slot >= 0 ? self.Hand.Drawn[slot] : self.CurrentWeapon;
        float desiredDistance = self.IsOverdriving ? .6f : PreferredDistance(weapon);
        if (Time.time >= strafeUntil)
        {
            strafeSign = -strafeSign;
            movementPace = Random.value < .22f ? .12f : Random.Range(.65f, 1f);
            radialFeint = Random.Range(-.65f, .65f) * Mathf.Lerp(.3f, 1f, skill);
            strafeUntil = Time.time + (movementPace < .2f ? Random.Range(.12f, .24f) : Random.Range(.35f, 1.15f));
        }
        Vector2 side = new Vector2(-forward.y, forward.x) * strafeSign;
        Vector2 move = (forward * Mathf.Clamp((distance - desiredDistance) * .8f + radialFeint, -1f, 1f) + side * Mathf.Lerp(.2f, .85f, skill)) * movementPace;
        Vector2 edge = self.ArenaHalfSize - new Vector2(Mathf.Abs(position.x), Mathf.Abs(position.y));
        if (edge.x < 1f) move.x -= Mathf.Sign(position.x) * (1.2f - edge.x);
        if (edge.y < 1f) move.y -= Mathf.Sign(position.y) * (1.2f - edge.y);
        Vector2 dodge = Vector2.zero;
        float danger = 0;
        var filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = true;
        int count = Physics2D.OverlapCircle(position, Mathf.Lerp(1.2f, 3f, skill), filter, nearby);
        for (int i = 0; i < count; i++)
        {
            var projectile = nearby[i].GetComponentInParent<ProjectileBase>();
            if (projectile == null || !projectile.IsSpawned || projectile.Owner == self) continue;
            if (projectile is BarrageShapeProjectile) continue;
            Vector2 away = position - (Vector2)nearby[i].ClosestPoint(position);
            if (away.sqrMagnitude < .001f) away = side;
            float weight = 1f / Mathf.Max(.25f, away.magnitude);
            dodge += away.normalized * weight;
            danger = Mathf.Max(danger, weight);
        }
        if (dodge.sqrMagnitude > .01f) move += dodge.normalized * Mathf.Lerp(.05f, 2.4f, skill);
        bool urgentTelegraph;
        if (TryAvoidAttacks(position, move, out var escape, out urgentTelegraph))
            move = Vector2.Lerp(Vector2.ClampMagnitude(move, 1), escape, AvoidanceResponse);
        float travel = weapon != null ? distance / Mathf.Max(1, weapon.Stats.Speed) : 0;
        float lead = Mathf.Min(.6f, travel) * Mathf.Lerp(0f, .85f, skill);
        Vector2 aimPosition = self.ClampToArena(observed + velocity * lead + Random.insideUnitCircle * AimError);
        command = new PlayerCommand
        {
            Move = Vector2.ClampMagnitude(move, 1),
            AimPosition = aimPosition,
            Aim = (aimPosition - position).normalized,
            SelectedSlot = slot,
            Fire = weapon != null && distance <= FireRange(weapon) && Random.value < FireProbability,
            DashPressed = (danger > 1f || urgentTelegraph) && Random.value < DashProbability
        };
    }

    private bool TryAvoidAttacks(Vector2 position, Vector2 preferred, out Vector2 movement, out bool urgent)
    {
        visibleAttacks.Clear();
        incomingBullets.Clear();
        urgent = false;
        movement = preferred;
        float speed = 5.8f * self.MovementMultiplier;
        float margin = self.BodyRadius + AvoidanceMargin;
        float predictionHorizon = PredictionHorizon;
        bool threatened = false;
        foreach (var projectile in ProjectileBase.ActiveProjectiles)
        {
            if (!projectile.IsSpawned || projectile.Owner == self || projectile.gameObject.scene != gameObject.scene) continue;
            if (projectile is BulletProjectile bullet &&
                Vector2.Distance(position, bullet.transform.position) < (bullet.Stats.Speed + speed) * predictionHorizon + margin + 1f)
            {
                incomingBullets.Add(bullet);
                float bulletClearance = BulletClearance(bullet, position, Vector2.ClampMagnitude(preferred, 1) * speed, margin, predictionHorizon);
                threatened |= bulletClearance < .15f;
                urgent |= BulletClearance(bullet, position, Vector2.zero, margin, Mathf.Min(.15f, predictionHorizon)) < 0;
            }
            if (projectile is not BarrageShapeProjectile attack) continue;
            visibleAttacks.Add(attack);
            float clearance = attack.DangerClearance(position, margin);
            bool entering = attack.DangerClearance(self.ClampToArena(position + preferred * speed * .3f), margin) < 0;
            if (clearance < .35f || entering)
            {
                threatened = true;
                urgent |= clearance < 0 && (!attack.IsTelegraph || attack.RemainingLifetime < .4f);
            }
        }
        if (!threatened) return false;

        // Score whole escape paths against every currently visible warning/attack.
        // Perpendicular beam escapes change the firing angle; bomb escapes seek the
        // nearest open space. Clamping the path prevents choosing a wall as an exit.
        float bestScore = float.NegativeInfinity;
        Vector2 best = Vector2.zero;
        int directions = EscapeDirections;
        for (int i = 0; i <= directions; i++)
        {
            float angle = i * Mathf.PI * 2f / directions;
            Vector2 direction = i == directions ? Vector2.zero : new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float score = Vector2.Dot(direction, preferred.normalized) * .12f;
            if (Time.time < escapeUntil) score += Vector2.Dot(direction, escapeDirection) * EscapeCommitment;
            Vector2 effectiveVelocity = (self.ClampToArena(position + direction * speed * predictionHorizon) - position) / predictionHorizon;
            foreach (var bullet in incomingBullets)
            {
                float clearance = BulletClearance(bullet, position, effectiveVelocity, margin, predictionHorizon);
                score += Mathf.Clamp(clearance, -2, 1f) * 3f;
                if (clearance < 0) score -= 35f;
            }
            for (int sample = 1; sample <= 3; sample++)
            {
                float horizon = sample * .18f;
                Vector2 requested = position + direction * speed * horizon;
                Vector2 predicted = self.ClampToArena(requested);
                score -= Vector2.Distance(predicted, requested) * 6f;
                foreach (var attack in visibleAttacks)
                {
                    float clearance = attack.DangerClearance(predicted, margin);
                    float weight = attack.IsTelegraph ? 1f : 1.5f;
                    score += Mathf.Clamp(clearance, -4, 1f) * weight * sample;
                    if (clearance < 0) score -= 3f * weight;
                }
            }
            if (score > bestScore) { bestScore = score; best = direction; }
        }
        if (Time.time >= escapeUntil || Vector2.Dot(best, escapeDirection) < .5f)
        {
            escapeDirection = best;
            escapeUntil = Time.time + EscapeCommitment;
        }
        movement = best;
        return true;
    }

    private static float BulletClearance(BulletProjectile bullet, Vector2 position, Vector2 velocity, float margin, float horizon)
    {
        // Swept closest approach catches fast bullets that pass between sample frames.
        Vector2 relativePosition = (Vector2)bullet.transform.position - position;
        Vector2 relativeVelocity = bullet.LinearVelocity - velocity;
        float time = relativeVelocity.sqrMagnitude > .0001f
            ? Mathf.Clamp(-Vector2.Dot(relativePosition, relativeVelocity) / relativeVelocity.sqrMagnitude, 0, horizon) : 0;
        return (relativePosition + relativeVelocity * time).magnitude - bullet.Stats.Radius - margin;
    }

    private int ChooseWeapon(float distance)
    {
        if (self.Hand == null) return -1;
        int best = self.Hand.SelectedIndex;
        float score = float.NegativeInfinity;
        for (int i = 0; i < self.Hand.Drawn.Count; i++)
        {
            var weapon = self.Hand.Drawn[i];
            float candidate = -Mathf.Abs(distance - PreferredDistance(weapon)) - weapon.CooldownRemaining * 3f;
            if (candidate > score) { score = candidate; best = i; }
        }
        return best;
    }

    private static float PreferredDistance(WeaponRuntime weapon)
    {
        if (weapon?.Definition.steps != null)
            foreach (var step in weapon.Definition.steps)
            {
                if (step == null) continue;
                if (step.shape == BarrageShape.Overdrive) return 1f;
                if (step.shape == BarrageShape.ElectricPulse) return 2f;
                if (step.shape == BarrageShape.OrbitOrb) return 1.5f;
                if (step.followMuzzle) return 6f;
            }
        return 4.5f;
    }

    private static float FireRange(WeaponRuntime weapon)
    {
        float preferred = PreferredDistance(weapon);
        return preferred <= 2f ? preferred + 1f : Mathf.Max(8f, weapon.Stats.Range);
    }
}
