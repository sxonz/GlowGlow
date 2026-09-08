using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(TitleScreenController))]
public sealed class TitleScreenControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("씬 화면 미리보기", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            string[] fields = { "mainMenu", "soloPanel", "multiplayerPanel", "settingsPanel", "deckPanel" };
            string[] labels = { "메인 메뉴", "싱글플레이", "멀티플레이", "설정", "덱 구성" };
            for (int i = 0; i < fields.Length; i++)
            {
                if (!GUILayout.Button(labels[i])) continue;
                foreach (string field in fields)
                {
                    var panel = serializedObject.FindProperty(field).objectReferenceValue as CanvasGroup;
                    if (panel == null) continue;
                    Undo.RecordObjects(new Object[] { panel, panel.gameObject }, "Preview title panel");
                    panel.gameObject.SetActive(field == fields[i]);
                    panel.alpha = 1;
                    panel.interactable = panel.blocksRaycasts = true;
                }
                Canvas.ForceUpdateCanvases();
                SceneView.RepaintAll();
                EditorSceneManager.MarkSceneDirty(((TitleScreenController)target).gameObject.scene);
            }
        }
    }
}
