using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KillingMahjong.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>承認済みのフレーム参照と非レイキャスト描画を、既存の部品に焼き込む。</summary>
    public static class UIInteractionFeedbackBaker
    {
        [MenuItem("Tools/UI/操作フレームを既存シーンとUIプレハブへ置く")]
        public static void BakeFromMenu() { Debug.Log(BakeAll()); }
        public static string BakeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を止めてください。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("未保存のシーンを保護するため中止しました。");
            var previous = EditorSceneManager.GetSceneManagerSetup();
            var report = new StringBuilder();
            const string folder = "Assets/Resources/UIFrames";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Resources", "UIFrames");
            if (AssetDatabase.LoadAssetAtPath<UIFrameStyle>(folder + "/PixelStandard.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<UIFrameStyle>(), folder + "/PixelStandard.asset");
            // 外部素材とネットワークプレハブには触れない。
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] {"Assets/Prefabs", "Assets/Preafb"}))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try { int count = BakeRoot(root); if (count > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); report.AppendLine(path + ": " + count); } }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            try
            {
                foreach (var path in new[] {"Assets/Scenes/OpeningScene.unity", "Assets/Scenes/UIテストシーン.unity", "Assets/Scenes/タイトルシーン.unity", "Assets/Scenes/部屋シーン.unity"})
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int count = 0;
                    foreach (var root in scene.GetRootGameObjects()) count += BakeRoot(root);
                    if (count > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                    report.AppendLine(path + ": " + count);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(previous); }
            SceneFirst.Report("UIInteractionFeedbackBaker");
            AssetDatabase.SaveAssets(); return report.ToString();
        }

        public static int BakeRoot(GameObject root)
        {
            int count = 0;
            foreach (var target in root.GetComponentsInChildren<Selectable>(true))
                if (UIInteractionFeedbackInstaller.ShouldAttach(target)) { UIInteractionFeedback.Attach(target.gameObject); count++; }
            foreach (var tile in root.GetComponentsInChildren<TileInteraction>(true))
                if (tile.transform is RectTransform) { UIInteractionFeedback.Attach(tile.gameObject); count++; }
            foreach (var visual in root.GetComponentsInChildren<TileVisual>(true))
                if (visual.transform is RectTransform && visual.GetComponent<TileInteraction>() == null)
                { UIInteractionFeedback.PrepareTileGraphics(visual.gameObject); count++; }
            foreach (var item in root.GetComponentsInChildren<AbilityItemUI>(true))
            { UIInteractionFeedback.Attach(item.gameObject); count++; }
            return count;
        }
    }
}
