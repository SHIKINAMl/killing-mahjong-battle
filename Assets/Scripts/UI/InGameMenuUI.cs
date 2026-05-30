using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

namespace KillingMahjong.UI
{
    public class InGameMenuUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject menuPanel; // メニュー全体のパネル（背景含む）
        [SerializeField] private Button settingsButton; // 左上の歯車ボタン
        [SerializeField] private Button closeMenuButton; // メニュー内の「閉じる」ボタン
        [SerializeField] private Button returnToTitleButton; // 「タイトルへ戻る」ボタン
        
        [Header("Settings")]
        [SerializeField] private string titleSceneName = "タイトルシーン"; // 遷移先のシーン名
        [SerializeField] private string disconnectActionType = "leave"; // サーバーへ送る切断アクションのtype名

        private void Start()
        {
            // 初期状態は非表示
            if (menuPanel != null) menuPanel.SetActive(false);

            if (settingsButton != null) settingsButton.onClick.AddListener(OpenMenu);
            if (closeMenuButton != null) closeMenuButton.onClick.AddListener(CloseMenu);
            if (returnToTitleButton != null) returnToTitleButton.onClick.AddListener(ReturnToTitle);
        }

        public void OpenMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(true);
        }

        public void CloseMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        private async void ReturnToTitle()
        {
            // ボタンを無効化して連打防止
            if (returnToTitleButton != null) returnToTitleButton.interactable = false;

            // サーバーへ切断（あるいは離脱）の通知を送る
            var wsClient = FindFirstObjectByType<WebSocketGameClientSample>();
            if (wsClient != null && wsClient.IsConnected)
            {
                // 明示的な切断メッセージを送信（Python側で必要な場合）
                if (!string.IsNullOrEmpty(disconnectActionType))
                {
                    string json = $"{{\"type\":\"{disconnectActionType}\"}}";
                    await wsClient.SendAsync(json);
                    
                    // 少し待機してメッセージの送信を確実にする
                    await Task.Delay(100);
                }
                
                // WebSocketを閉じる
                await wsClient.DisconnectAsync();
            }
            else
            {
                // DebugClient等の場合
                var debugClient = FindFirstObjectByType<Network.DebugWebSocketClient>();
                if (debugClient != null)
                {
                    Debug.Log("DebugClient: Returned to Title");
                }
            }

            // タイトルシーンへ遷移
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
