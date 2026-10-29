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
    /// <summary>ログの「最新へ戻る」▼（LogLatestButtonUI）を、ログのある2つのシーンと試写の舞台に置く。</summary>
    public static class LogLatestButtonBaker
    {
        [MenuItem("Tools/UI/ログの最新へ戻るボタンをシーンへ置く")]
        public static void BakeMenu() { Debug.Log(Bake()); }

        public static string Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を止めてください。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("未保存シーンを保護するため中止しました。");
            var setup = EditorSceneManager.GetSceneManagerSetup(); var report = new StringBuilder();
            try
            {
                foreach (string path in new[] { "Assets/Scenes/OpeningScene.unity", "Assets/Scenes/UIテストシーン.unity" })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var dialogue = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DialogueUI>(true)).First();
                    report.AppendLine(path + ": " + (dialogue.EnsureLogLatestButton() != null ? "置いた" : "ログの枠が無い"));
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存できませんでした: " + path);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            const string prefabPath = "Assets/Resources/Presentation/EffectPreviewRig.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var dialogue = root.GetComponentInChildren<DialogueUI>(true);
                report.AppendLine(prefabPath + ": " + (dialogue != null && dialogue.EnsureLogLatestButton() != null ? "置いた" : "ログの枠が無い"));
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return report.ToString();
        }
    }
}
