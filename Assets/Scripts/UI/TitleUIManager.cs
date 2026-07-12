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

        /// <summary>
        /// 1つ目のボタン（ゲームスタートなど）が押された時の処理
        /// </summary>
        public void OnClickStartButton()
        {
            Debug.Log("ゲーム開始！ " + nextSceneName + " に遷移します。");
            SceneManager.LoadScene(nextSceneName);
        }

        /// <summary>
        /// 設定ボタンが押された時の処理
        /// </summary>
        public void OnClickOptionButton()
        {
            if (optionUIPanel != null)
            {
                optionUIPanel.SetActive(true);
            }
            else
            {
                Debug.LogWarning("インスペクターで OptionUIPanel が設定されていません！");
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
