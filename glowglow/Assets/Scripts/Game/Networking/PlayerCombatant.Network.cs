using UnityEngine;
using GlowGlow.Online;

public sealed partial class PlayerCombatant
{
    public bool NetworkReplica { get; private set; }
    private int networkShield;

    public void SetNetworkReplica()
    {
        NetworkReplica = true;
        StopAllCoroutines();
        GetComponent<CircleCollider2D>().enabled = false;
    }

    public ClassicPlayerState CaptureNetworkState()
    {
        int count = Hand?.Drawn.Count ?? 0;
        var cooldowns = new float[count];
        var durations = new float[count];
        for (int i = 0; i < count; i++)
        {
            cooldowns[i] = Hand.Drawn[i].CooldownRemaining;
            durations[i] = Hand.Drawn[i].CooldownDuration;
        }
        return new ClassicPlayerState { Position = transform.position, Aim = aim,
            Hits = HitsTaken, Shield = ShieldRemaining, Selected = Hand?.SelectedIndex ?? -1,
            Cooldowns = cooldowns, Durations = durations };
    }

    public void ApplyNetworkState(ClassicPlayerState state)
    {
        if (!NetworkReplica) return;
        transform.position = state.Position;
        aim = state.Aim;
        HitsTaken = Mathf.Clamp(state.Hits, 0, 3);
        networkShield = Mathf.Max(0, state.Shield);
        if (Hand == null) return;
        if (Hand.Select(state.Selected)) CurrentWeapon = Hand.Selected;
        int count = Mathf.Min(Hand.Drawn.Count, Mathf.Min(state.Cooldowns?.Length ?? 0, state.Durations?.Length ?? 0));
        for (int i = 0; i < count; i++) Hand.Drawn[i].ApplyNetworkCooldown(state.Cooldowns[i], state.Durations[i]);
    }
}
