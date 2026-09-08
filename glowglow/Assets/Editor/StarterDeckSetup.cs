using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StarterDeckSetup
{
    [Serializable] private sealed class DeckData { public List<string> cards; }

    public static void UpgradeBatch()
    {
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
            var ids = catalog.StarterCardIds();
            if (ids.Count != 8 || catalog.cards.Length != 8) throw new Exception("Expected eight starter weapons.");
            bool Valid(List<string> values) => catalog.IsDeckValid(JsonUtility.ToJson(new DeckData { cards = values }));
            if (!Valid(ids) || Valid(ids.GetRange(0, 7)) || Valid(new List<string>())) throw new Exception("Eight-card gate failed.");
            var tooMany = new List<string>(ids) { ids[0] };
            var duplicate = new List<string>(ids); duplicate[7] = ids[0];
            var unknown = new List<string>(ids); unknown[7] = "unknown-card";
            if (Valid(tooMany) || Valid(duplicate) || Valid(unknown) || catalog.IsDeckValid("{}")) throw new Exception("Invalid deck accepted.");
            var deck = new List<WeaponDefinition>();
            foreach (var card in catalog.cards) deck.Add(card.weapon);
            var hand = new WeaponHand(deck, new System.Random(17));
            if (hand.DeckCount != 8 || hand.Drawn.Count != 5) throw new Exception("Expected five drawn weapons out of eight.");

            TitleSceneUpgrade.Upgrade();
            var screen = UnityEngine.Object.FindFirstObjectByType<DeckScreen>(FindObjectsInactive.Include);
            if (screen.DeckSlotCapacity != 8) throw new Exception("Eight scene deck slots were not baked.");
            var arena = EditorSceneManager.OpenScene("Assets/Scenes/Arena.unity", OpenSceneMode.Single);
            var match = UnityEngine.Object.FindFirstObjectByType<MatchController>();
            match.ConfigureDeckRules(catalog);
            EditorSceneManager.MarkSceneDirty(arena);
            EditorSceneManager.SaveScene(arena);
            AssetDatabase.SaveAssets();
            Debug.Log("STARTER_DECK_TESTS_PASSED: 8 starters; accept 8; reject 0/7/9/duplicates/unknown; draw 5 of 8; eight scene deck slots.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
