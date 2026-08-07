using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public class GlobalFontFixer
{
    [MenuItem("Tools/Global Fix All Fonts (All Scenes & Prefabs)")]
    public static void FixAllFontsGlobal()
    {
        // 1. 新しい安全なDynamicフォントアセットを生成
        string ttfPath = "Assets/Resources/PixelMplus-20130602/PixelMplus-20130602/PixelMplus10-Regular.ttf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);

        if (sourceFont == null)
        {
            Debug.LogError($"元のTTFフォントが見つかりません: {ttfPath}");
            return;
        }

        string newAssetPath = "Assets/Resources/PixelMplus10_DynamicFixed.asset";
        TMP_FontAsset newFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(newAssetPath);

        if (newFontAsset == null)
        {
            // 動的モードでアセットを生成
            newFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024);
            newFontAsset.name = "PixelMplus10_DynamicFixed";
            newFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            // アセットを作成
            AssetDatabase.CreateAsset(newFontAsset, newAssetPath);

            // 【重要】マテリアルとテクスチャをサブアセットとして保存しないとエラーになる
            if (newFontAsset.material != null)
            {
                newFontAsset.material.name = newFontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(newFontAsset.material, newFontAsset);
            }
            if (newFontAsset.atlasTexture != null)
            {
                newFontAsset.atlasTexture.name = newFontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(newFontAsset.atlasTexture, newFontAsset);
            }
        }
        else
        {
            newFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            EditorUtility.SetDirty(newFontAsset);
        }
        AssetDatabase.SaveAssets();

        int replacedCount = 0;

        // 2. すべてのプレハブ内のフォントを置換
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            TMP_Text[] texts = prefab.GetComponentsInChildren<TMP_Text>(true);

            bool changed = false;
            foreach (var t in texts)
            {
                if (t.font != newFontAsset)
                {
                    t.font = newFontAsset;
                    changed = true;
                    replacedCount++;
                }
            }
            if (changed)
            {
                EditorUtility.SetDirty(prefab);
            }
        }

        // 3. すべてのシーン内のフォントを置換 (現在のシーンを保存して開いていく)
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Backupフォルダなどのシーンは無視
            if (path.Contains("Backup")) continue;

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(path);
            TMP_Text[] texts = Object.FindObjectsOfType<TMP_Text>(true);

            bool changed = false;
            foreach (var t in texts)
            {
                if (t.font != newFontAsset)
                {
                    t.font = newFontAsset;
                    changed = true;
                    replacedCount++;
                }
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        Debug.Log($"<color=green>【修復完了】</color> プロジェクト全体の全シーン・全プレハブのフォントを修復しました！ (計 {replacedCount} 個のテキストを置換)");
    }
}
