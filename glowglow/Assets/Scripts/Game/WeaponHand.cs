using System.Collections.Generic;
using UnityEngine;

/// <summary>D is the unique deck; G is a draw without replacement and always smaller than D.</summary>
public sealed class WeaponHand
{
    public const int SlotCount = 5;
    private readonly List<WeaponRuntime> drawn = new List<WeaponRuntime>();
    public IReadOnlyList<WeaponRuntime> Drawn => drawn;
    public int DeckCount { get; }
    public int SelectedIndex { get; private set; } = -1;
    public WeaponRuntime Selected => SelectedIndex >= 0 ? drawn[SelectedIndex] : null;

    public WeaponHand(IEnumerable<WeaponDefinition> deck, System.Random random)
    {
        var pool = new List<WeaponDefinition>();
        if (deck != null)
            foreach (var weapon in deck)
                if (weapon != null && !pool.Contains(weapon)) pool.Add(weapon);
        DeckCount = pool.Count;
        int count = Mathf.Min(SlotCount, Mathf.Max(0, DeckCount - 1));
        for (int i = 0; i < count; i++)
        {
            int index = random.Next(pool.Count);
            drawn.Add(new WeaponRuntime(pool[index]));
            pool.RemoveAt(index);
        }
        if (drawn.Count > 0) SelectedIndex = 0;
    }

    public bool Select(int index)
    {
        if (index < 0 || index >= drawn.Count) return false;
        SelectedIndex = index;
        return true;
    }
}
