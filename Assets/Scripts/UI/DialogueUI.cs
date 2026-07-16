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
        private GameObject nextRoundButtonObj;

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
            {
                dialogueText.text = text;
                // セリフの折り返しを有効化
                dialogueText.enableWordWrapping = true;
                // 長すぎる場合は省略記号などを出さずに、単に下にはみ出させる（UI枠内に収めるための基本設定）
                dialogueText.overflowMode = TextOverflowModes.Overflow;
            }
            
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

                // ログが追加されたら一番下まで自動スクロールさせる
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(ScrollToBottom());
                }
            }
        }

        private System.Collections.IEnumerator ScrollToBottom()
        {
            // UIのレイアウト更新を1フレーム待つ
            yield return new WaitForEndOfFrame();
            
            if (logContainer != null)
            {
                ScrollRect scrollRect = logContainer.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    // 0が一番下、1が一番上
                    scrollRect.verticalNormalizedPosition = 0f;
                }
            }
        }

        public void ToggleLog()
        {
            if (logPanel.activeSelf) CloseLog();
            else OpenLog();
        }

        public void SetBackgroundRaycast(bool block)
        {
            if (dialoguePanel != null)
            {
                var img = dialoguePanel.GetComponent<Image>();
                if (img != null) img.raycastTarget = block;
            }
        }

        public void OpenLog()
        {
            logPanel.SetActive(true);
        }

        public void CloseLog()
        {
            logPanel.SetActive(false);

            // ログが閉じられたら、GameUIManager側で止まっていたリアクションの消化を再開する
            var reactionController = KillingMahjong.Managers.ReactionController.Instance;
            if (reactionController != null)
            {
                reactionController.ProcessNextReaction(); // 止まっていた場合、ここから再開される
            }
        }

        public void ShowNextRoundButton(System.Action onClick)
        {
            if (nextRoundButtonObj == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return;

                nextRoundButtonObj = new GameObject("NextRoundOKButton");
                nextRoundButtonObj.transform.SetParent(transform, false);
                
                var rt = nextRoundButtonObj.AddComponent<RectTransform>();
                // 右下付近に配置
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-100, 100);
                rt.sizeDelta = new Vector2(250, 80);

                var img = nextRoundButtonObj.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

                var btn = nextRoundButtonObj.AddComponent<Button>();

                var txtObj = new GameObject("Text");
                txtObj.transform.SetParent(nextRoundButtonObj.transform, false);
                var txtRt = txtObj.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                var txt = txtObj.AddComponent<TextMeshProUGUI>();
                txt.text = "OK";
                txt.color = Color.white;
                txt.fontSize = KillingMahjong.Common.UITypography.BodyLarge;
                txt.alignment = TextAlignmentOptions.Center;

                btn.onClick.AddListener(() => {
                    HideNextRoundButton();
                    onClick?.Invoke();
                });
            }

            nextRoundButtonObj.SetActive(true);
        }

        public void HideNextRoundButton()
        {
            if (nextRoundButtonObj != null)
            {
                nextRoundButtonObj.SetActive(false);
            }
        }
    }
}
