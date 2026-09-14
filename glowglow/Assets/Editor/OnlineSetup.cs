using System;
using System.IO;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEngine;

public static class OnlineSetup
{
    [MenuItem("GlowGlow/Online/Build Classic Network Assets")]
    public static void Bake()
    {
        Directory.CreateDirectory("Assets/Resources/Online");
        AssetDatabase.Refresh();
        var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Resources/Online/Prefabs.asset");
        if (collection == null)
        {
            collection = ScriptableObject.CreateInstance<SinglePrefabObjects>();
            AssetDatabase.CreateAsset(collection, "Assets/Resources/Online/Prefabs.asset");
        }
        collection.Clear();
        EditorUtility.SetDirty(collection);
        var root = new GameObject("Classic NetworkManager"); root.SetActive(false);
        var tugboat = root.AddComponent<Tugboat>();
        var steam = root.AddComponent<FishySteamworks.FishySteamworks>();
        var fields = new SerializedObject(steam);
        fields.FindProperty("_peerToPeer").boolValue = true;
        fields.FindProperty("_maximumClients").intValue = 2;
        fields.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<TransportManager>().Transport = tugboat;
        var manager = root.AddComponent<NetworkManager>();
        manager.SpawnablePrefabs = collection;
        fields = new SerializedObject(manager);
        fields.FindProperty("_dontDestroyOnLoad").boolValue = true;
        fields.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Online/NetworkManager.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("CLASSIC_NETWORK_ASSETS_READY");
    }

    public static void BuildTestBatch()
    {
        try
        {
            Bake();
            string output = "Builds/ClassicOnline/GlowGlow.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Title.unity", "Assets/Scenes/Arena.unity" },
                locationPathName = output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "steam_appid.txt"), "480\n");
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

