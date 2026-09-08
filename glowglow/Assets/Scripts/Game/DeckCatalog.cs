using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "GlowGlow/Deck Catalog")]
public sealed class DeckCatalog : ScriptableObject
{
    public const int RequiredDeckSize = 8;
    public const string SaveKey = "DeckComposition.v1";
    [Serializable]
    public sealed class Card
    {
        public string id;
        public WeaponDefinition weapon;
        [TextArea] public string description;
    }

    public Card[] cards = Array.Empty<Card>();

    [Serializable] private sealed class SavedDeck { public List<string> cards; }

    public bool IsSavedDeckValid() => IsDeckValid(PlayerPrefs.GetString(SaveKey, "{}"));

    public bool IsDeckValid(string json)
    {
        SavedDeck saved;
        try { saved = JsonUtility.FromJson<SavedDeck>(json); }
        catch (ArgumentException) { return false; }
        if (saved?.cards == null || saved.cards.Count != RequiredDeckSize || cards == null) return false;
        var ids = new HashSet<string>();
        var weapons = new HashSet<WeaponDefinition>();
        foreach (string id in saved.cards)
        {
            var card = Array.Find(cards, item => item != null && item.id == id);
            if (string.IsNullOrEmpty(id) || card?.weapon == null || !ids.Add(id) || !weapons.Add(card.weapon)) return false;
        }
        return true;
    }

    public List<string> StarterCardIds()
    {
        var ids = new List<string>();
        var weapons = new HashSet<WeaponDefinition>();
        if (cards == null) return ids;
        foreach (var card in cards)
        {
            if (card?.weapon == null || string.IsNullOrWhiteSpace(card.id) || ids.Contains(card.id) || !weapons.Add(card.weapon)) continue;
            ids.Add(card.id);
            if (ids.Count == RequiredDeckSize) break;
        }
        return ids;
    }

    public void EnsureStarterDeck()
    {
        if (PlayerPrefs.HasKey(SaveKey)) return;
        var ids = StarterCardIds();
        if (ids.Count != RequiredDeckSize) return;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SavedDeck { cards = ids }));
        PlayerPrefs.Save();
    }

    public List<WeaponDefinition> LoadSelectedWeapons()
    {
        var weapons = new List<WeaponDefinition>();
        SavedDeck saved;
        try { saved = JsonUtility.FromJson<SavedDeck>(PlayerPrefs.GetString(SaveKey, "{}")); }
        catch (ArgumentException) { return weapons; }
        if (saved?.cards == null || cards == null) return weapons;
        foreach (string id in saved.cards)
        {
            var card = Array.Find(cards, item => item != null && item.id == id);
            if (card?.weapon != null && !weapons.Contains(card.weapon)) weapons.Add(card.weapon);
        }
        return weapons;
    }
}
