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
    /// <summary>牌交換の「場を見る」ボタン（MulliganPeekButtonUI）を、牌交換のある2つのシーンに置く。</summary>
    public static class MulliganPeekButtonBaker
    {
        [MenuItem("Tools/UI/牌交換の場を見るボタンをシーンへ置く")]
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
                    var roots = scene.GetRootGameObjects();
                    var manager = roots.SelectMany(r => r.GetComponentsInChildren<GameUIManager>(true)).First();
                    var skills = roots.SelectMany(r => r.GetComponentsInChildren<GameUISkillController>(true)).First();
                    report.AppendLine(path + ": " + (skills.EnsureMulliganPeekButton(manager) != null ? "置いた" : "置けなかった"));
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("保存できませんでした: " + path);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return report.ToString();
        }
    }
}
