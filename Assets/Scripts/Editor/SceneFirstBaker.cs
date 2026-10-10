using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KillingMahjong.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// 再生時にコードで組み立てていた画面の部品を、**シーンに置く**（2026-10-09）。
    ///
    /// タイトルのボタン・部屋の画面・対局メニューは、<see cref="SceneFirst"/> の
    /// 「シーンに置いてあればそれを使い、無ければ作る」で動いている。
    /// ここは、その「無ければ作る」を再生していないときに走らせて、結果をシーンに保存する。
    ///
    /// **置いてある物には触らない。** 足りない物だけが増える。
    /// シーンで位置や色を直してあっても、ここを実行して元に戻ることは無い。
    ///
    /// 使うとき:
    ///   コードに部品を足した（`SceneFirst.Child(...)` を1つ増やした）あと。
    ///   再生したときに「シーンに無かったので、再生時に作りました」の警告が出たとき。
    ///
    /// 対局メニューはタイトルと部屋の両方で使うので、Prefab にして両方のシーンへ置く。
    ///
    /// 対局のシーン（本編とチュートリアル）は別の口（<see cref="BakeMatch"/>）。
    /// 役強化の「強める役を選ぶ画面」を、Prefab にして両方へ置く。
    /// </summary>
    public static class SceneFirstBaker
    {
        private const string TitleScenePath = "Assets/Scenes/タイトルシーン.unity";
        private const string RoomScenePath = "Assets/Scenes/部屋シーン.unity";
        private const string MenuPrefabPath = "Assets/Prefabs/UI/MultiMenu.prefab";

        // 対局のシーンは2つある（OpeningScene＝チュートリアル、UIテストシーン＝本編のオンライン対局）。
        // 同じ物を両方に置くので Prefab にする
        private static readonly string[] MatchScenePaths =
        {
            "Assets/Scenes/OpeningScene.unity",
            "Assets/Scenes/UIテストシーン.unity",
        };
        private const string YakuSelectionPrefabPath = "Assets/Prefabs/UI/YakuSelection.prefab";

        [MenuItem("Tools/UI/実行時UIをシーンへ置く（タイトル・部屋・対局メニュー）")]
        private static void BakeFromMenu()
        {
            Debug.Log("[SceneFirstBaker]\n" + BakeAll());
        }

        /// <summary>タイトルシーンと部屋シーンに、足りない部品を置いて保存する。</summary>
        /// <returns>何を置いたかの報告</returns>
        public static string BakeAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "再生中は実行できません。";

            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty) return "開いているシーンに保存していない変更があります。保存してから実行してください。";
            string original = active.path;

            var report = new StringBuilder();
            report.AppendLine(BakeScene(TitleScenePath));
            report.AppendLine(BakeScene(RoomScenePath));

            // 始める前に開いていたシーンへ戻す
            if (!string.IsNullOrEmpty(original) && SceneManager.GetActiveScene().path != original)
            {
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            return report.ToString();
        }

        [MenuItem("Tools/UI/実行時UIをシーンへ置く（対局）")]
        private static void BakeMatchFromMenu()
        {
            Debug.Log("[SceneFirstBaker]\n" + BakeMatch());
        }

        /// <summary>
        /// 対局のシーン2つに、足りない部品を置いて保存する。いまは役強化の「強める役を選ぶ画面」
        /// （<see cref="YakuSelectionUI"/>）だけ。
        /// </summary>
        /// <returns>何を置いたかの報告</returns>
        public static string BakeMatch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "再生中は実行できません。";

            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty) return "開いているシーンに保存していない変更があります。保存してから実行してください。";
            string original = active.path;

            var report = new StringBuilder();
            foreach (string path in MatchScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.Append(path).Append(": 役を選ぶ画面=").Append(BakeYakuSelection(scene));
                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                report.AppendLine(saved ? " / 保存しました" : " / **保存に失敗しました**");
            }

            // 始める前に開いていたシーンへ戻す
            if (!string.IsNullOrEmpty(original) && SceneManager.GetActiveScene().path != original)
            {
                EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            }
            return report.ToString();
        }

        /// <summary>役を選ぶ画面を置く。Prefab が無ければここで作り、あればその実体をシーンに置く。</summary>
        private static string BakeYakuSelection(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(YakuSelectionPrefabPath);
            GameObject root = SceneFirst.Find(YakuSelectionUI.RootName);

            string placed = "";
            if (root == null && prefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                root.name = YakuSelectionUI.RootName;
                placed = "Prefab をシーンに置いた。";
            }

            string made = YakuSelectionUI.BakeForEditor();

            root = SceneFirst.Find(YakuSelectionUI.RootName);
            if (root == null) return placed + "作れませんでした。";

            if (prefab == null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, YakuSelectionPrefabPath, InteractionMode.AutomatedAction);
                placed += "Prefab を作った（" + YakuSelectionPrefabPath + "）。";
            }
            else if (made.Length > 0 && PrefabUtility.IsPartOfPrefabInstance(root))
            {
                // このシーンで足した部品を Prefab へ戻す。戻さないと、もう片方のシーンには出ない
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                placed += "足した部品を Prefab に反映した。";
            }

            return placed + Describe(made);
        }

        private static string BakeScene(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var manager = Object.FindFirstObjectByType<TitleUIManager>(FindObjectsInactive.Include);
            if (manager == null) return path + ": TitleUIManager が無いので何もしていません。";

            var report = new StringBuilder(path + ": ");

            if (manager.StartsInRoom)
            {
                // 部屋の画面。RoomScreenUI は再生時に付く部品なので、借りの入れ物に付けて呼ぶ
                var holder = NewHolder();
                string made = holder.AddComponent<RoomScreenUI>().BakeForEditor();
                Object.DestroyImmediate(holder);
                report.Append("部屋=").Append(Describe(made));

                // 「見本の写し」だった頃に保存された、中身の無い部品（歩きの部品など、
                // 再生時に付け直すもの）が残っていると、読み込むたびに警告が出る。外しておく
                int removed = StripBrokenComponents(SceneFirst.Find("RoomScreen"));
                if (removed > 0) report.Append("（中身の無い部品を ").Append(removed).Append(" 個外した）");
            }
            else
            {
                string made = manager.BakeTitleButtonsForEditor();
                report.Append("タイトルのボタン=").Append(Describe(made));
            }

            report.Append(" / 対局メニュー=").Append(BakeMenu(scene));

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            report.Append(saved ? " / 保存しました" : " / **保存に失敗しました**");
            return report.ToString();
        }

        /// <summary>対局メニューを置く。Prefab が無ければここで作り、あればその実体をシーンに置く。</summary>
        private static string BakeMenu(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefabPath);
            GameObject root = SceneFirst.Find(TitleMultiMenuUI.RootName);

            string placed = "";
            if (root == null && prefab != null)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                root.name = TitleMultiMenuUI.RootName;
                placed = "Prefab をシーンに置いた。";
            }

            var holder = NewHolder();
            string made = holder.AddComponent<TitleMultiMenuUI>().BakeForEditor();
            Object.DestroyImmediate(holder);

            root = SceneFirst.Find(TitleMultiMenuUI.RootName);
            if (root == null) return placed + "作れませんでした。";

            if (prefab == null)
            {
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, MenuPrefabPath, InteractionMode.AutomatedAction);
                placed += "Prefab を作った（" + MenuPrefabPath + "）。";
            }
            else if (made.Length > 0 && PrefabUtility.IsPartOfPrefabInstance(root))
            {
                // このシーンで足した部品を Prefab へ戻す。戻さないと、もう片方のシーンには出ない
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
                placed += "足した部品を Prefab に反映した。";
            }

            return placed + Describe(made);
        }

        /// <summary>
        /// スクリプトが見つからない部品（Inspector で「Missing Script」と出るもの）を外す。
        ///
        /// 部屋の女の子には、歩きの部品（RoomGirlWalker）が「中身の無い部品」として保存されていた。
        /// 当時そのクラスが RoomScreenUI.cs に同居していて、Unity が保存できなかったため。
        /// 専用の関数（RemoveMonoBehavioursWithMissingScript）では外れなかったので、
        /// 部品の一覧から、中身を指していない行を直接消す。
        /// </summary>
        private static int StripBrokenComponents(GameObject root)
        {
            if (root == null) return 0;

            int removed = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                // 上で外れなかった物。GetComponents が null を返す部品が残っていれば、一覧から消す
                Component[] all = t.GetComponents<Component>();
                bool hasBroken = false;
                foreach (var c in all) if (c == null) hasBroken = true;
                if (!hasBroken) continue;

                var so = new SerializedObject(t.gameObject);
                SerializedProperty list = so.FindProperty("m_Component");
                for (int i = list.arraySize - 1; i >= 0; i--)
                {
                    if (i < all.Length && all[i] == null)
                    {
                        list.DeleteArrayElementAtIndex(i);
                        removed++;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return removed;
        }

        private static GameObject NewHolder()
        {
            var holder = new GameObject("~SceneFirstBaker");
            holder.hideFlags = HideFlags.HideAndDontSave;
            return holder;
        }

        private static string Describe(string made)
        {
            return made.Length == 0 ? "足りない物なし" : "置いた物: " + made;
        }
    }
}
