using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KillingMahjong.UI;

namespace KillingMahjong.EditorTools
{
    public static class HandSelectionFeedbackBaker
    {
        [MenuItem("Tools/UI/手牌の追加確認と待ちなし枠をシーンへ置く")]
        public static void BakeMenu() { Debug.Log(Bake()); }
        public static string Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を止めてください。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("未保存シーンを保護するため中止しました。");
            var setup = EditorSceneManager.GetSceneManagerSetup(); var report = new StringBuilder();
            try
            {
                foreach (string path in new[] { "Assets/Scenes/UIテストシーン.unity", "Assets/Scenes/OpeningScene.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameUIManager>(true)).First();
                    HandWaitRevealUI.Ensure(manager.transform).gameObject.SetActive(false);
                    HandWeakConfirmationUI.Ensure(manager.transform, manager.HandUI.DecideButtonRect).Hide();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存できませんでした: " + path);
                    report.AppendLine(path + ": " + SceneFirst.Report("HandSelectionFeedback"));
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            // 試写にも同じ部品を保存。舞台全体の更新や他のシーンの変更はしない。
            const string prefabPath = "Assets/Resources/Presentation/EffectPreviewRig.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                HandWaitRevealUI.Ensure(root.transform).gameObject.SetActive(false);
                var template = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).FirstOrDefault(b => b.name == "DecideButton");
                HandWeakConfirmationUI.Ensure(root.transform, template != null ? (RectTransform)template.transform : null).Hide();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            SceneFirst.Report("HandSelectionFeedbackPreview");
            return report.ToString();
        }
    }
}
