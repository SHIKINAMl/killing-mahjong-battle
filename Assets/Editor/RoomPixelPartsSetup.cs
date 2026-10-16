using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>個別の背景PNGを、部屋の既存オブジェクトへ割り当てる。</summary>
    public static class RoomPixelPartsSetup
    {
        [Serializable]
        private sealed class Part
        {
            public string name;
            public string layer;
            public float[] color;
        }

        [MenuItem("Tools/UI/部屋の個別ドット素材を配置")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("再生を止めてから実行してください。");
            const string scenePath = "Assets/Scenes/部屋シーン.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath);
            Transform content = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "RoomScreen") content = root.transform.Find("RoomContent");
            if (content == null) throw new InvalidOperationException("RoomContentがありません。");
            var parts = JsonConvert.DeserializeObject<List<Part>>(
                File.ReadAllText("tools/RoomPixelArt/parts.json"));

            // 参照不足のままシーンを書き換えない。
            foreach (var part in parts)
            {
                string path = AssetPath(part);
                if (!File.Exists(path) || content.Find(part.layer + "/" + part.name) == null)
                    throw new InvalidOperationException("部品がありません: " + part.name);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                    throw new InvalidOperationException("Spriteの読み込みに失敗: " + path);
            }
            foreach (var part in parts)
            {
                var image = content.Find(part.layer + "/" + part.name).GetComponent<Image>();
                Undo.RecordObject(image, "部屋のドット素材を配置");
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath(part));
                image.type = Image.Type.Simple;
                // 光の画像は白いマスク。元の色とalphaを残し、既存の明滅を使う。
                image.color = part.name.EndsWith("Glow", StringComparison.Ordinal)
                    ? new Color(part.color[0], part.color[1], part.color[2], part.color[3])
                    : new Color(1f, 1f, 1f, part.color[3]);
                EditorUtility.SetDirty(image);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[RoomPixelParts] 個別素材を配置しました: " + parts.Count);
        }

        private static string AssetPath(Part part) =>
            "Assets/Resources/Room/PixelParts/" + part.name + ".png";
    }
}
