using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// 対局シーンにいる通信クライアント（`WebSocketGameClientSample`）を、
    /// `Resources` から呼べる Prefab に写し取る。
    ///
    /// **なぜ要るのか:**
    /// 合言葉で部屋に入るときは、対局シーンへ移る「前」に接続して結果を確かめる必要がある。
    /// サーバーは合言葉が当たった瞬間にその接続で対局を成立させ、部屋を消してしまうので
    /// （`websocket_server.py` の `_join_private_room`）、確かめてから繋ぎ直すことができない。
    /// タイトルから同じ設定で接続を張れるようにしておく。
    ///
    /// **接続先URLと認証トークンは対局シーンにシリアライズされている。**
    /// 値をコードへ書き写すとトークンがソースにも増えるので、
    /// シーンのオブジェクトをそのまま Prefab 化して運ぶ形にしている。
    ///
    /// 対局シーンの設定（URL・トークン）を変えたら、**このメニューをもう一度実行して作り直すこと。**
    /// </summary>
    public static class GameClientPrefabBuilder
    {
        private const string GameScenePath = "Assets/Scenes/UIテストシーン.unity";
        private const string PrefabDir = "Assets/Resources/Network";
        private const string PrefabPath = PrefabDir + "/GameClient.prefab";

        /// <summary>タイトルから読み込むときのパス（Resources 基準・拡張子なし）。</summary>
        public const string ResourcesPath = "Network/GameClient";

        // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
        [MenuItem("Tools/Network/通信クライアントのPrefabを作る")]
#endif
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("できません",
                    "Play モード中は実行できません。Play を止めてからもう一度実行してください。", "OK");
                return;
            }

            bool openedAdditively = false;
            Scene scene = SceneManager.GetSceneByPath(GameScenePath);

            if (!scene.isLoaded)
            {
                // ユーザーが開いているシーンを切り替えずに済ませる
                scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            try
            {
                WebSocketGameClientSample client = FindClientIn(scene);
                if (client == null)
                {
                    EditorUtility.DisplayDialog("見つかりません",
                        $"{GameScenePath} に WebSocketGameClientSample がありません。", "OK");
                    return;
                }

                if (!Directory.Exists(PrefabDir))
                {
                    Directory.CreateDirectory(PrefabDir);
                    AssetDatabase.Refresh();
                }

                PrefabUtility.SaveAsPrefabAsset(client.gameObject, PrefabPath, out bool ok);
                if (!ok)
                {
                    Debug.LogError($"[GameClientPrefabBuilder] Prefab の保存に失敗しました: {PrefabPath}");
                    return;
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"[GameClientPrefabBuilder] 作成しました: {PrefabPath}" +
                          "（接続先とトークンはシーンの設定をそのまま引き継いでいます）");
            }
            finally
            {
                if (openedAdditively)
                {
                    // 加えて開いただけなので、保存せずに閉じる
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static WebSocketGameClientSample FindClientIn(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<WebSocketGameClientSample>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
