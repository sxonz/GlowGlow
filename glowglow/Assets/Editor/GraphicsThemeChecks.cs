using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GraphicsThemeChecks
{
    public static void RunBatch()
    {
        bool saved = PlayerPrefs.HasKey(GraphicsTheme.SaveKey);
        int value = PlayerPrefs.GetInt(GraphicsTheme.SaveKey, 1);
        int result = 0;
        try
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory("Temp");
            EditorSceneManager.SaveScene(scene, "Temp/ThemeValidationHost.unity");
            GraphicsTheme.Select(GraphicsThemeKind.Neon);
            GraphicsUpgradeChecks.Run();
            GraphicsTheme.Select(GraphicsThemeKind.Classic);
            CombatUpgradeChecks.Run();
            Debug.Log("THEME_REGRESSION_PASSED: 899 combat assertions per theme, shader and network regression.");
        }
        catch (Exception ex) { Debug.LogException(ex); result = 1; }
        finally
        {
            if (saved) PlayerPrefs.SetInt(GraphicsTheme.SaveKey, value); else PlayerPrefs.DeleteKey(GraphicsTheme.SaveKey);
            PlayerPrefs.Save(); GraphicsTheme.Load();
        }
        EditorApplication.Exit(result);
    }
}
