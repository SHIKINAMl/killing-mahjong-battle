using UnityEngine;
using UnityEditor;
using TMPro;

public class ReplaceFontTool : EditorWindow
{
    private TMP_FontAsset newFont;

    [MenuItem("Tools/Replace All Fonts (TextMeshPro)")]
    public static void ShowWindow()
    {
        GetWindow<ReplaceFontTool>("Replace Fonts");
    }

    private void OnGUI()
    {
        GUILayout.Label("Replace ALL TextMeshPro Fonts in Scene", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();

        newFont = (TMP_FontAsset)EditorGUILayout.ObjectField("New Font Asset", newFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space();

        if (GUILayout.Button("Replace Fonts!"))
        {
            if (newFont == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a Font Asset first.", "OK");
                return;
            }

            ReplaceFonts();
        }
    }

    private void ReplaceFonts()
    {
        // シーン内のすべてのTextMeshProUGUI（UI用）を取得
        TextMeshProUGUI[] textComponents = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        int count = 0;
        foreach (var txt in textComponents)
        {
            Undo.RecordObject(txt, "Replace Font"); // Undo（Ctrl+Z）できるように記録
            txt.font = newFont;
            EditorUtility.SetDirty(txt); // 変更を保存対象にする
            count++;
        }

        // 3D用のTextMeshProがある場合も置換
        TextMeshPro[] text3DComponents = FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var txt in text3DComponents)
        {
            Undo.RecordObject(txt, "Replace Font");
            txt.font = newFont;
            EditorUtility.SetDirty(txt);
            count++;
        }

        EditorUtility.DisplayDialog("Success", $"Successfully replaced fonts in {count} text objects!", "OK");
    }
}
