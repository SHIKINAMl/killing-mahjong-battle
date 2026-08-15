using UnityEngine;
using UnityEditor;

public class TextureCompressor
{
    // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
    [MenuItem("Tools/Compress Large Textures")]
#endif
    public static void CompressTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Scenes/ZNS3D" });
        int count = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null && importer.maxTextureSize > 1024)
            {
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                count++;
            }
        }
        
        Debug.Log($"Compressed {count} textures in ZNS3D folder.");
    }
}
