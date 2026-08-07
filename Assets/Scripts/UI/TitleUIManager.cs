using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillingMahjong.UI
{
    public class TitleUIManager : MonoBehaviour
    {
        [Header("遷移先のシーン名")]
        [SerializeField] private string nextSceneName = "UIテストシーン"; // 実際のメインゲームのシーン名に合わせてください

        [Header("設定画面パネル")]
        [SerializeField] private GameObject optionUIPanel;

        private void Start()
        {
            // CanvasScalerの強制上書きは、OptionUIでの解像度設定とコンフリクトするため削除
        }

        private TitleMultiMenuUI multiMenu;

        /// <summary>
        /// 「マルチ」が押された時の処理。
        ///
        /// **シーンの `onClick` はこのメソッド名で配線済みなので、名前は変えないこと。**
        /// 作り直すと配線が外れて「押しても何も起きない」状態になる。
        /// 中身だけを差し替えて、対戦相手の探し方（野良／部屋を作る／あいことば）を
        /// 先に選ばせるようにしている。
        /// </summary>
        public void OnClickStartButton()
        {
            if (multiMenu == null)
            {
                multiMenu = gameObject.AddComponent<TitleMultiMenuUI>();
            }

            multiMenu.Open(mode =>
            {
                Debug.Log($"マルチ開始（{mode}）。{nextSceneName} に遷移します。");
                StartMultiplayScene();
            });
        }

        /// <summary>
        /// 対局シーンへ移る。`MatchJoinRequest` は呼ぶ前に設定しておくこと
        /// （`join` は接続直後に自動で飛ぶので、シーンに入ってからでは間に合わない）。
        /// </summary>
        private void StartMultiplayScene()
        {
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.FadeOutScreen(() => 
                {
                    StartCoroutine(LoadSceneAsyncCoroutine());
                });
            }
            else
            {
                StartCoroutine(LoadSceneAsyncCoroutine());
            }
        }

        private System.Collections.IEnumerator LoadSceneAsyncCoroutine()
        {
            // 暗転完了後、非同期でシーンをロードする
            var asyncOp = SceneManager.LoadSceneAsync(nextSceneName);
            while (!asyncOp.isDone)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 設定ボタンが押された時の処理
        /// </summary>
        public void OnClickOptionButton()
        {
            if (optionUIPanel != null)
            {
                var ui = optionUIPanel.GetComponent<OptionUI>();
                if (ui != null)
                {
                    ui.Open();
                }
                else
                {
                    optionUIPanel.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning("インスペクターで OptionUIPanel 設定されていません");
            }
        }

        /// <summary>
        /// 3つ目のボタン（ゲーム終了など）が押された時の処理
        /// </summary>
        public void OnClickExitButton()
        {
            Debug.Log("ゲームを終了します。");
#if UNITY_EDITOR
            // Unityエディタ上でのプレイモードを終了する
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // ビルドされたゲームを終了する
            Application.Quit();
#endif
        }
    }
}
