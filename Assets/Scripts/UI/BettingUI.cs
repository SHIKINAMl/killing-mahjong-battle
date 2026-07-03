using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class BettingUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform hpBarPanel; // Panel to slide in from right
        [SerializeField] private TextMeshProUGUI currentMoneyText;
        [SerializeField] private TextMeshProUGUI currentBetText;
        [SerializeField] private TextMeshProUGUI expectedRewardText;
        
        [SerializeField] private Button increaseBetButton;
        [SerializeField] private Button decreaseBetButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button fullBetButton;

        [Header("Settings")]
        [SerializeField] private float slideDuration = 0.5f;

        private int bettingUnit = 200;

        [Header("Auto Dialogue Settings")]
        [SerializeField] private float dialogueInterval = 5.0f;
        private string[] enemyDialogueLines = new string[]
        {
            "ふふっ、そんな麻雀で私に勝てるとでも？",
            "ん〜？ どーしたのー？ 早く賭けなよぉ♡",
            "震えてるよ？ 怖いの？",
            "全額いっちゃう？ いっくわけないかぁ〜雑魚だもんね♡",
            "ざぁこ♡ ざぁこ♡ よわよわ雀士♡"
        };
        private DialogueUI dialogueUI;
        private Coroutine autoDialogueCoroutine;

        private int initialMoney = 20000;
        private int currentMoney = 20000;
        private int currentBet = 0;
        private int maxBet = 0;

        private Vector2 hiddenPos; // Off-screen right
        private Vector2 visiblePos; // On-screen
        
        private Action<int> onConfirmAction;

        private void Awake()
        {
            // Setup positions (スライドイン廃止のため、現在位置をvisiblePosとして保持するだけにする)
            if (hpBarPanel != null)
            {
                visiblePos = hpBarPanel.anchoredPosition;
                hiddenPos = visiblePos; // もう隠さないので同じ位置にしておく
            }

            // Get DialogueUI reference
            dialogueUI = FindFirstObjectByType<DialogueUI>();

            if (increaseBetButton != null) increaseBetButton.onClick.AddListener(IncreaseBet);
            if (decreaseBetButton != null) decreaseBetButton.onClick.AddListener(DecreaseBet);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmBet);

            if (fullBetButton != null) fullBetButton.onClick.AddListener(FullBet);

            // Fix raycast target overlap for buttons
            DisableRaycastForButtonTexts(increaseBetButton);
            DisableRaycastForButtonTexts(decreaseBetButton);
            DisableRaycastForButtonTexts(confirmButton);
            DisableRaycastForButtonTexts(fullBetButton);
        }

        private void DisableRaycastForButtonTexts(Button btn)
        {
            if (btn != null)
            {
                var tmpros = btn.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var t in tmpros) t.raycastTarget = false;
                var texts = btn.GetComponentsInChildren<Text>();
                foreach (var t in texts) t.raycastTarget = false;
            }
        }

        public void ShowBettingPhase(int initialHp, int currentHp, int specialVictoryCount, Action<int> onConfirm)
        {
            var rules = GameRules.GetRuleSet(specialVictoryCount);
            this.bettingUnit = rules.BetUnit;
            this.initialMoney = initialHp;
            this.currentMoney = currentHp;
            this.maxBet = rules.BetMax;
            this.currentBet = bettingUnit; // Reset bet to minimum
            this.onConfirmAction = onConfirm;
            
            if (confirmButton != null) confirmButton.interactable = true;

            // PlayerInfoUIの背面に隠れないように、自身のCanvasのSortingOrderを引き上げる
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingLayerName = "UI";
                canvas.sortingOrder = UISortingOrders.BettingPanel;
            }

            gameObject.SetActive(true);
            UpdateUI();
            
            // Slide In Animationを廃止して即時表示
            if (hpBarPanel != null)
            {
                StopAllCoroutines();
                hpBarPanel.anchoredPosition = visiblePos;
            }
            
            // Start Auto Dialogue
            if (dialogueUI != null && enemyDialogueLines.Length > 0)
            {
                autoDialogueCoroutine = StartCoroutine(AutoDialogueRoutine());
            }
        }

        public void HideBettingPhase(bool immediate = false)
        {
            if (autoDialogueCoroutine != null)
            {
                StopCoroutine(autoDialogueCoroutine);
                autoDialogueCoroutine = null;
            }

            if (immediate || !gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            // スライドアウトアニメーションを廃止して即時非表示
            if (hpBarPanel != null)
            {
                StopAllCoroutines();
                gameObject.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void IncreaseBet()
        {
            Debug.Log($"[BettingUI] IncreaseBet Called. currentBet: {currentBet}, maxBet: {maxBet}, currentMoney: {currentMoney}");
            if (currentBet + bettingUnit <= maxBet && currentBet + bettingUnit <= currentMoney)
            {
                currentBet += bettingUnit;
                UpdateUI();
            }
            else
            {
                Debug.LogWarning("[BettingUI] Cannot increase bet (Limit reached).");
            }
        }

        private void DecreaseBet()
        {
            Debug.Log($"[BettingUI] DecreaseBet Called. currentBet: {currentBet}");
            if (currentBet - bettingUnit >= bettingUnit)
            {
                currentBet -= bettingUnit;
                UpdateUI();
            }
            else
            {
                Debug.LogWarning("[BettingUI] Cannot decrease bet (Minimum limit).");
            }
        }

        private void FullBet()
        {
            int maxAllowed = Mathf.Min(maxBet, currentMoney);
            int validMax = Mathf.FloorToInt((float)maxAllowed / bettingUnit) * bettingUnit;
            if (validMax >= bettingUnit)
            {
                currentBet = validMax;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (currentMoneyText != null)
                currentMoneyText.text = $"HP: {currentMoney}";

            float expectedMultiplier = 1.0f;
            if (Managers.BoardStateManager.Instance != null && Managers.BoardStateManager.Instance.LocalWaitDataList != null && Managers.BoardStateManager.Instance.LocalWaitDataList.Count > 0)
            {
                int maxHan = 0;
                foreach (var wait in Managers.BoardStateManager.Instance.LocalWaitDataList)
                {
                    int han = GameRules.CalculateTotalHan(wait.yaku, Managers.BoardStateManager.Instance.LocalBoostHandBonus);
                    if (han > maxHan) maxHan = han;
                }
                expectedMultiplier = GameRules.GetMultiplier(maxHan);
            }

            int reward = Mathf.FloorToInt(currentBet * expectedMultiplier);

            if (currentBetText != null)
            {
                currentBetText.text = $"Bet: {currentBet}\n<size=70%>予想報酬: {reward}</size>";
            }

            if (expectedRewardText != null)
            {
                expectedRewardText.text = $"Expected Reward: {reward}";
            }
            
            // Disable buttons appropriately
            increaseBetButton.interactable = (currentBet + bettingUnit <= maxBet && currentBet + bettingUnit <= currentMoney);
            decreaseBetButton.interactable = (currentBet - bettingUnit >= bettingUnit);
            
            if (fullBetButton != null)
            {
                int maxAllowed = Mathf.Min(maxBet, currentMoney);
                int validMax = Mathf.FloorToInt((float)maxAllowed / bettingUnit) * bettingUnit;
                fullBetButton.interactable = (currentBet < validMax);
            }
        }

        private void ConfirmBet()
        {
            // Lock UI
            if (increaseBetButton != null) increaseBetButton.interactable = false;
            if (decreaseBetButton != null) decreaseBetButton.interactable = false;
            if (fullBetButton != null) fullBetButton.interactable = false;
            if (confirmButton != null) confirmButton.interactable = false;

            onConfirmAction?.Invoke(currentBet);
        }

        private IEnumerator SlidePanel(Vector2 start, Vector2 end, Action onComplete = null)
        {
            float elapsedTime = 0f;
            hpBarPanel.anchoredPosition = start;

            while (elapsedTime < slideDuration)
            {
                hpBarPanel.anchoredPosition = Vector2.Lerp(start, end, elapsedTime / slideDuration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            hpBarPanel.anchoredPosition = end;
            onComplete?.Invoke();
        }

        private IEnumerator AutoDialogueRoutine()
        {
            // Initial delay or immediate text
            yield return new WaitForSeconds(1.0f);

            while (true)
            {
                // Select a random line
                int randomIndex = UnityEngine.Random.Range(0, enemyDialogueLines.Length);
                string textToTalk = enemyDialogueLines[randomIndex];
                
                // Show the dialogue
                dialogueUI.ShowText(textToTalk);

                // Wait for the next interval
                yield return new WaitForSeconds(dialogueInterval);
            }
        }
    }
}
