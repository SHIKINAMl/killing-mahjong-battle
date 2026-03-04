using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("Main Dialogue")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject dialoguePanel;

        [Header("Log")]
        [SerializeField] private GameObject logPanel;
        [SerializeField] private Button toggleLogButton;
        [SerializeField] private Button closeLogBackgroundButton; // 追加: 全画面背景のボタン
        [SerializeField] private Transform logContainer; // 各ログを入れる親(ScrollRectのContentなど)
        [SerializeField] private GameObject logItemPrefab; // 1つ1つのログを表示するプレハブ（TextやUI四角などを持つ）

        private List<string> dialogueHistory = new List<string>();

        public bool IsLogOpen => logPanel != null && logPanel.activeSelf;

        private void Start()
        {
            if (toggleLogButton != null) toggleLogButton.onClick.AddListener(ToggleLog);
            if (closeLogBackgroundButton != null) closeLogBackgroundButton.onClick.AddListener(CloseLog);
            logPanel.SetActive(false);
        }

        public void ShowText(string text)
        {
            StopAllCoroutines(); // 既存の文字送り演出などがあれば即座にキャンセルする

            if (dialogueText != null)
                dialogueText.text = text;
            
            AddToLog(text);
            dialoguePanel.SetActive(true);
        }

        private void AddToLog(string text)
        {
            dialogueHistory.Add(text);
            
            if (logContainer != null && logItemPrefab != null)
            {
                // 新しいログ枠（四角）を生成して配置
                GameObject newLogItem = Instantiate(logItemPrefab, logContainer);
                
                // プレハブ内の TextMeshProUGUI を探してテキストをセットする
                // 仮に直下に TextMeshProUGUI がある、あるいは子オブジェクトにある想定
                TextMeshProUGUI tmp = newLogItem.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = text;
                }
            }
        }

        public void ToggleLog()
        {
            if (logPanel.activeSelf) CloseLog();
            else OpenLog();
        }

        public void OpenLog()
        {
            logPanel.SetActive(true);
        }

        public void CloseLog()
        {
            logPanel.SetActive(false);

            // ログが閉じられたら、GameUIManager側で止まっていたリアクションの消化を再開する
            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager != null)
            {
                uiManager.ProcessNextReaction(); // 止まっていた場合、ここから再開される
            }
        }
    }
}
