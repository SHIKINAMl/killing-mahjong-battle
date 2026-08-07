using UnityEngine;
using UnityEditor;
using TMPro;

public class FontAutoFixer
{
    [MenuItem("Tools/Auto Fix Missing Fonts (Dynamic)")]
    public static void FixFonts()
    {
        // 1. 元のTTFフォントをロード
        string fontPath = "Assets/Resources/PixelMplus-20130602/PixelMplus-20130602/PixelMplus10-Regular.ttf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);

        if (sourceFont == null)
        {
            Debug.LogError($"Could not find source TTF font at {fontPath}");
            return;
        }

        // 2. 新しい TMP_FontAsset を生成 (Dynamicモード)
        TMP_FontAsset newFontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        newFontAsset.name = "PixelMplus10_Dynamic";
        
        // 念のためDynamic設定を明示
        newFontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        // 3. アセットとして保存
        string savePath = "Assets/Resources/PixelMplus10_Dynamic.asset";
        AssetDatabase.CreateAsset(newFontAsset, savePath);
        AssetDatabase.SaveAssets();

        // 4. シーン内のすべての TMP_Text に適用
        TMP_Text[] textComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (TMP_Text text in textComponents)
        {
            if (text.gameObject.scene.isLoaded) // シーン内のオブジェクトのみ
            {
                Undo.RecordObject(text, "Auto Fix Font");
                text.font = newFontAsset;
                EditorUtility.SetDirty(text);
                count++;
            }
        }

        Debug.Log($"Successfully created Dynamic Font Asset at {savePath} and applied to {count} text objects.");
    }
}
