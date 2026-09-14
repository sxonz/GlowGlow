using System;
using System.Collections.Generic;

/// <summary>Three rounds: choose one of three offers; upgrades affect only acquired runtimes.</summary>
public sealed class OpeningDraft
{
    public const int OfferCount = 3;
    public const int RoundCount = 3;
    public sealed class Offer
    {
        public WeaponDefinition Weapon { get; }
        public WeaponRuntime Target { get; }
        public WeaponUpgradeDefinition Upgrade { get; }
        public bool IsUpgrade => Upgrade != null;
        internal Offer(WeaponDefinition weapon) { Weapon = weapon; }
        internal Offer(WeaponRuntime target, WeaponUpgradeDefinition upgrade)
        { Weapon = target.Definition; Target = target; Upgrade = upgrade; }
    }

    private readonly List<WeaponDefinition> deck = new();
    private readonly List<WeaponUpgradeDefinition> upgrades = new();
    private readonly List<WeaponRuntime> acquired = new();
    private readonly List<Offer> offers = new();
    private readonly List<Offer> choices = new();
    private readonly List<int> selectedIndices = new();
    public IReadOnlyList<int> SelectedIndices => selectedIndices;
    private readonly Random random;
    private readonly bool stableTimeoutChoices;
    private int completedRounds;
    public IReadOnlyList<Offer> Offers => offers;
    public IReadOnlyList<Offer> Choices => choices;
    public IReadOnlyList<WeaponRuntime> Acquired => acquired;
    public IReadOnlyList<WeaponUpgradeDefinition> UpgradeDefinitions => upgrades;
    public int RoundNumber => Math.Min(completedRounds + 1, RoundCount);
    public int SelectedIndex { get; private set; } = -1;
    public bool IsComplete => completedRounds == RoundCount;

    public OpeningDraft(IEnumerable<WeaponDefinition> source, Random random,
        IEnumerable<WeaponUpgradeDefinition> upgradeDefinitions, bool stableTimeoutChoices = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        this.stableTimeoutChoices = stableTimeoutChoices;
        foreach (var weapon in source)
            if (weapon != null && !deck.Contains(weapon)) deck.Add(weapon);
        if (deck.Count < OfferCount + RoundCount - 1)
            throw new ArgumentException("At least five unique weapons are required for three rounds.");
        if (upgradeDefinitions != null)
            foreach (var upgrade in upgradeDefinitions)
                if (upgrade != null && !upgrades.Contains(upgrade)) upgrades.Add(upgrade);
        Deal();
    }

    public bool Select(int index)
    {
        if (IsComplete || index < 0 || index >= offers.Count) return false;
        SelectedIndex = index;
        return true;
    }

    public bool Confirm(bool autoPick = false)
    {
        if (IsComplete) return false;
        if (SelectedIndex < 0)
        {
            if (!autoPick) return false;
            // Keep the offer RNG independent of timeout selection, so a network peer
            // can reconstruct a draft using its seed and the three chosen indices.
            SelectedIndex = stableTimeoutChoices ? 0 : random.Next(offers.Count);
        }
        var offer = offers[SelectedIndex];
        if (offer.IsUpgrade)
        {
            if (!offer.Target.TryUpgrade(offer.Upgrade)) return false;
        }
        else acquired.Add(new WeaponRuntime(offer.Weapon));
        choices.Add(offer);
        selectedIndices.Add(SelectedIndex);
        completedRounds++;
        SelectedIndex = -1;
        offers.Clear();
        if (!IsComplete) Deal();
        return true;
    }

    public IReadOnlyList<WeaponRuntime> Complete()
    {
        if (!IsComplete) throw new InvalidOperationException("All three selection rounds must finish before combat.");
        return acquired;
    }

    private void Deal()
    {
        var newWeapons = new List<Offer>();
        foreach (var weapon in deck)
            if (!acquired.Exists(runtime => runtime.Definition == weapon)) newWeapons.Add(new Offer(weapon));
        var improvements = new List<Offer>();
        foreach (var runtime in acquired)
        foreach (var upgrade in upgrades)
            // Each upgrade is independently eligible. displaySlot never gates acquisition.
            if (upgrade.CanApplyTo(runtime.Definition) && runtime.Upgrades.GetLevel(upgrade) < upgrade.maxLevel)
                improvements.Add(new Offer(runtime, upgrade));
        // Later rounds always show both types when an eligible upgrade exists.
        if (improvements.Count > 0)
        {
            TakeRandom(newWeapons);
            TakeRandom(improvements);
        }
        newWeapons.AddRange(improvements);
        while (offers.Count < OfferCount) TakeRandom(newWeapons);
        for (int i = offers.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (offers[i], offers[j]) = (offers[j], offers[i]);
        }
    }

    private void TakeRandom(List<Offer> pool)
    {
        int index = random.Next(pool.Count);
        offers.Add(pool[index]);
        pool.RemoveAt(index);
    }
}
