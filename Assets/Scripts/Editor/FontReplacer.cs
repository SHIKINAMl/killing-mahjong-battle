using UnityEngine;
using UnityEditor;
using TMPro;

public class FontReplacer : EditorWindow
{
    private TMP_FontAsset targetFont;

    [MenuItem("Tools/Replace Missing Fonts")]
    public static void ShowWindow()
    {
        GetWindow<FontReplacer>("Replace Fonts");
    }

    void OnGUI()
    {
        GUILayout.Label("Select a Font Asset to apply to all TextMeshPro objects in the scene:", EditorStyles.wordWrappedLabel);
        targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Target Font", targetFont, typeof(TMP_FontAsset), false);

        if (GUILayout.Button("Replace All Fonts in Scene"))
        {
            if (targetFont == null)
            {
                Debug.LogError("Please select a Target Font first.");
                return;
            }

            ReplaceFontsInScene(targetFont);
        }
    }

    private void ReplaceFontsInScene(TMP_FontAsset newFont)
    {
        TMP_Text[] textComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (TMP_Text text in textComponents)
        {
            // Only apply to objects in the scene (not prefabs)
            if (text.gameObject.scene.isLoaded)
            {
                Undo.RecordObject(text, "Replace Font");
                text.font = newFont;
                EditorUtility.SetDirty(text);
                count++;
            }
        }

        Debug.Log($"Replaced fonts for {count} TextMeshPro objects in the scene.");
    }
}
