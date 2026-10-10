using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Keep generated dynamic font data in memory instead of dirtying the Git checkout.
// Intentional font settings changes can be saved through the explicit menu item.
// TMP's generated glyphs and atlas remain in memory and regenerate as needed.
internal sealed class DynamicFontSaveGuard : AssetModificationProcessor
{
    internal const string FontPath = "Assets/Resources/PixelMplus10_DynamicFixed.asset";
    private static bool allowExplicitSave;

    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (allowExplicitSave || !File.Exists(FontPath))
            return paths;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        // Static assets and dynamic assets intended to keep baked data can save.
        if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            return paths;
        var clearOnBuild = new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild");
        if (clearOnBuild == null || !clearOnBuild.boolValue)
            return paths;

        var retained = new List<string>(paths.Length);
        foreach (var path in paths)
        {
            if (!string.Equals(path.Replace('\\', '/'), FontPath, StringComparison.Ordinal))
                retained.Add(path);
        }
        return retained.ToArray();
    }

    // Intentional font/material settings changes still have an explicit route.
    // SaveAssetIfDirty is targeted and does not invoke OnWillSaveAssets.
    [MenuItem("Tools/KillingMahjong/Fonts/Save Dynamic Font Settings Once")]
    private static void SaveFontSettingsOnce()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            return;

        if (!EditorUtility.DisplayDialog("動的フォントの設定を保存",
            "通常は自動生成された文字データの保存を止めています。\n"
            + "フォントやマテリアルの設定を意図して変更した場合だけ保存してください。\n"
            + "保存後は、このファイルのGit差分を確認してください。", "保存", "キャンセル"))
            return;

        try
        {
            allowExplicitSave = true;
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }
        finally
        {
            allowExplicitSave = false;
        }
    }
}
