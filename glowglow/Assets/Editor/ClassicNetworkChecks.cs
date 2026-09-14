using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ClassicNetworkChecks
{
    public static void RunBatch()
    {
        try
        {
            var initial = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(initial.path))
            {
                System.IO.Directory.CreateDirectory("Temp");
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(initial, "Temp/ClassicValidationHost.unity");
            }
            CombatUpgradeChecks.Run();
            var catalog = AssetDatabase.LoadAssetAtPath<DeckCatalog>("Assets/Settings/Deck Catalog.asset");
            var deck = catalog.cards.Where(c => c?.weapon != null).Select(c => c.weapon).Distinct().Take(8).ToArray();
            var upgrades = Resources.LoadAll<WeaponUpgradeDefinition>("WeaponUpgrades").OrderBy(u => u.name, StringComparer.Ordinal).ToArray();
            for (int seed = 1; seed <= 256; seed++)
            {
                var client = new OpeningDraft(deck, new System.Random(seed), upgrades, true);
                var host = new OpeningDraft(deck, new System.Random(seed), upgrades, true);
                while (!client.IsComplete)
                {
                    for (int i = 0; i < client.Offers.Count; i++)
                        Require(client.Offers[i].Weapon == host.Offers[i].Weapon && client.Offers[i].Upgrade == host.Offers[i].Upgrade,
                            "Offer mismatch after a timeout or manual selection");
                    if (client.RoundNumber != 2) client.Select(seed % client.Offers.Count);
                    client.Confirm(true);
                    host.Select(client.SelectedIndices.Last());
                    host.Confirm();
                }
                Require(client.Acquired.Count == host.Acquired.Count, "Acquired count mismatch");
                for (int i = 0; i < client.Acquired.Count; i++)
                {
                    Require(client.Acquired[i].Definition == host.Acquired[i].Definition, "Weapon mismatch");
                    foreach (var upgrade in upgrades)
                        Require(client.Acquired[i].Upgrades.GetLevel(upgrade) == host.Acquired[i].Upgrades.GetLevel(upgrade), "Upgrade mismatch");
                }
            }
            Require(!catalog.IsDeckValid("{}") && !catalog.IsDeckValid("invalid") && !catalog.IsDeckValid("{\"cards\":[]}"), "Invalid deck accepted");
            Debug.Log("CLASSIC_DRAFT_RECONSTRUCTION_PASSED: 256 seeded drafts with mixed manual and timeout choices");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
