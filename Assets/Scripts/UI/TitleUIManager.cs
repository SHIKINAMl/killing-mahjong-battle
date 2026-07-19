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
            // 解像度が変わった際に見切れるのを防ぐため、
            // 強制的にCanvasScalerの設定を800x600(4:3)の自動調整モードに上書きする
            var canvasScaler = GetComponentInParent<UnityEngine.UI.CanvasScaler>();
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(800, 600);
                canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 0.5f;
            }
        }

        /// <summary>
        /// 1つ目のボタン（ゲームスタートなど）が押された時の処理
        /// </summary>
        public void OnClickStartButton()
        {
            Debug.Log("ゲーム開始！ " + nextSceneName + " に遷移します。");
            
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.Show();
            }

            StartCoroutine(LoadSceneAsyncCoroutine());
        }

        private System.Collections.IEnumerator LoadSceneAsyncCoroutine()
        {
            // ロードUIの表示を確実に画面に反映させるために1フレーム待機
            yield return null;

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
