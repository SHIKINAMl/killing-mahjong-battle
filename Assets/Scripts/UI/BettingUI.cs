using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

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

        [Header("Settings")]
        [SerializeField] private int bettingUnit = 200;
        [SerializeField] private float maxBetRatio = 0.25f; // 1/4 of initial hp
        [SerializeField] private float slideDuration = 0.5f;

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
            // Setup positions for sliding animation
            if (hpBarPanel != null)
            {
                visiblePos = hpBarPanel.anchoredPosition;
                hiddenPos = new Vector2(visiblePos.x + hpBarPanel.rect.width + 100f, visiblePos.y); // Hide to the right
                hpBarPanel.anchoredPosition = hiddenPos;
            }

            // Get DialogueUI reference
            dialogueUI = FindFirstObjectByType<DialogueUI>();

            if (increaseBetButton != null) increaseBetButton.onClick.AddListener(IncreaseBet);
            if (decreaseBetButton != null) decreaseBetButton.onClick.AddListener(DecreaseBet);
            if (confirmButton != null) confirmButton.onClick.AddListener(ConfirmBet);
        }

        public void ShowBettingPhase(int initialHp, int currentHp, Action<int> onConfirm)
        {
            this.initialMoney = initialHp;
            this.currentMoney = currentHp;
            this.maxBet = Mathf.FloorToInt(initialHp * maxBetRatio);
            this.currentBet = bettingUnit; // Reset bet to minimum (200)
            this.onConfirmAction = onConfirm;
            
            if (confirmButton != null) confirmButton.interactable = true;

            gameObject.SetActive(true);
            UpdateUI();
            
            // Slide In Animation
            if (hpBarPanel != null)
            {
                StopAllCoroutines();
                StartCoroutine(SlidePanel(hiddenPos, visiblePos));
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
                // すでに非アクティブ、または即時非表示指定なら、コルーチンを回さずにそのまま終了
                if (hpBarPanel != null)
                {
                    hpBarPanel.anchoredPosition = hiddenPos;
                }
                gameObject.SetActive(false);
                return;
            }

            if (hpBarPanel != null)
            {
                StopAllCoroutines();
                StartCoroutine(SlidePanel(visiblePos, hiddenPos, () => gameObject.SetActive(false)));
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

        private void UpdateUI()
        {
            if (currentMoneyText != null)
                currentMoneyText.text = $"HP: {currentMoney}";

            if (currentBetText != null)
                currentBetText.text = $"Bet: {currentBet}";

            if (expectedRewardText != null)
            {
                // Dummy logic for expected reward (e.g. 2x the bet)
                int reward = currentBet * 2; 
                expectedRewardText.text = $"Expected Reward: {reward}";
            }
            
            // Disable buttons appropriately
            increaseBetButton.interactable = (currentBet + bettingUnit <= maxBet && currentBet + bettingUnit <= currentMoney);
            decreaseBetButton.interactable = (currentBet - bettingUnit >= bettingUnit);
        }

        private void ConfirmBet()
        {
            // Lock UI
            increaseBetButton.interactable = false;
            decreaseBetButton.interactable = false;
            confirmButton.interactable = false;

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
