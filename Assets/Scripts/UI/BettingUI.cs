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

        private Vector2 visiblePos; // On-screen
        
        private Action<int> onConfirmAction;

        private GameObject bettingDimmer;

        private void CreateDimmer()
        {
            if (bettingDimmer != null) return;
            
            bettingDimmer = new GameObject("BettingDimmer");
            var rt = bettingDimmer.AddComponent<RectTransform>();
            
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
            Transform parentTransform = rootCanvas != null ? rootCanvas.transform : transform;
            
            bettingDimmer.transform.SetParent(parentTransform, false);
            
            Canvas dimmerCanvas = bettingDimmer.AddComponent<Canvas>();
            dimmerCanvas.overrideSorting = true;
            dimmerCanvas.sortingOrder = UISortingOrders.BettingDimmer; // BettingPanel より奥にする
            
            bettingDimmer.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            Image bg = bettingDimmer.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f); // 半透明の黒（スマホ以外が暗くなる）
            
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private void ShowDimmer()
        {
            if (bettingDimmer == null) CreateDimmer();
            bettingDimmer.SetActive(true);
        }

        private void HideDimmer()
        {
            if (bettingDimmer != null) bettingDimmer.SetActive(false);
        }

        private void Awake()
        {
            // Setup positions (スライドイン廃止のため、現在位置をvisiblePosとして保持するだけにする)
            if (hpBarPanel != null)
            {
                visiblePos = hpBarPanel.anchoredPosition;
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

            // 固定賭け金モード（チュートリアル）で無効化された場合に備えて毎回戻す
            if (increaseBetButton != null) increaseBetButton.interactable = true;
            if (decreaseBetButton != null) decreaseBetButton.interactable = true;
            if (fullBetButton != null) fullBetButton.interactable = true;

            // PlayerInfoUIの背面に隠れないように、自身のCanvasのSortingOrderを引き上げる
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = UISortingOrders.BettingPanel; // 敵のdialogより前に出す
            }

            gameObject.SetActive(true);
            ShowDimmer();
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

        /// <summary>矢印UIの誘導先として使う決定ボタンの RectTransform。</summary>
        public RectTransform ConfirmButtonRect =>
            confirmButton != null ? confirmButton.GetComponent<RectTransform>() : null;

        /// <summary>
        /// 賭け金を固定して賭けフェイズを表示する（チュートリアル用）。
        /// 増減・全賭けボタンを無効化し、決定ボタンだけを押せる状態にする。
        /// 煽りの自動セリフはチュートリアル側の台本と競合するため止める。
        /// </summary>
        public void ShowFixedBettingPhase(int initialHp, int currentHp, int fixedBet, Action<int> onConfirm)
        {
            ShowBettingPhase(initialHp, currentHp, 0, onConfirm);

            if (autoDialogueCoroutine != null)
            {
                StopCoroutine(autoDialogueCoroutine);
                autoDialogueCoroutine = null;
            }

            currentBet = Mathf.Max(0, fixedBet);

            if (increaseBetButton != null) increaseBetButton.interactable = false;
            if (decreaseBetButton != null) decreaseBetButton.interactable = false;
            if (fullBetButton != null) fullBetButton.interactable = false;

            UpdateUI();
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
                HideDimmer();
                return;
            }

            if (hpBarPanel != null)
            {
                StopAllCoroutines();
                gameObject.SetActive(false);
                HideDimmer();
            }
            else
            {
                gameObject.SetActive(false);
                HideDimmer();
            }
        }

        /// <summary>賭け金の「上限に対する現在値の割合」。SEのピッチに使う。</summary>
        private float BetRatio
        {
            get
            {
                int maxAllowed = Mathf.Min(maxBet, currentMoney);
                if (maxAllowed <= 0) return 0f;
                return Mathf.Clamp01((float)currentBet / maxAllowed);
            }
        }

        private void PlayBetTick()
        {
            var audio = Managers.AudioManager.Instance;
            if (audio != null) audio.PlayBetTickSE(BetRatio);
        }

        private void IncreaseBet()
        {
            Debug.Log($"[BettingUI] IncreaseBet Called. currentBet: {currentBet}, maxBet: {maxBet}, currentMoney: {currentMoney}");
            if (currentBet + bettingUnit <= maxBet && currentBet + bettingUnit <= currentMoney)
            {
                currentBet += bettingUnit;
                UpdateUI();
                PlayBetTick();
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
                PlayBetTick();
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
                PlayBetTick();
            }
        }

        private void UpdateUI()
        {
            if (currentMoneyText != null)
                currentMoneyText.text = $"HP: {currentMoney}";

            bool isTenpai = Managers.BoardStateManager.Instance != null && 
                            Managers.BoardStateManager.Instance.LocalWaitDataList != null && 
                            Managers.BoardStateManager.Instance.LocalWaitDataList.Count > 0;

            int maxHan = 0;
            float expectedMultiplier = 1.0f;
            if (isTenpai)
            {
                foreach (var wait in Managers.BoardStateManager.Instance.LocalWaitDataList)
                {
                    int han = GameRules.CalculateTotalHan(wait.yaku, Managers.BoardStateManager.Instance.LocalBoostHandBonus);
                    if (han > maxHan) maxHan = han;
                }
                expectedMultiplier = GameRules.GetMultiplier(maxHan);
            }

            // ノーテン、または満貫未満(通常5飜未満を指す。簡略ルールで4飜満貫の場合も考慮し、ここでは5飜以上を満貫とみなす。
            // もしこのゲームが4飜満貫を採用しているなら4にするが、一般的には5以上を明示的に満貫と扱うことが多い)
            // ただし、もし倍率が跳満以上(>1.0f)であれば確実に表示する
            bool isManganOrMore = maxHan >= 5 || expectedMultiplier > 1.0f;

            if (currentBetText != null)
            {
                if (isTenpai && isManganOrMore)
                {
                    int reward = Mathf.FloorToInt(currentBet * expectedMultiplier);
                    currentBetText.text = $"Bet: {currentBet}\n<size=70%>予想報酬: {reward}</size>";
                }
                else
                {
                    currentBetText.text = $"Bet: {currentBet}";
                }
            }

            if (expectedRewardText != null)
            {
                if (isTenpai && isManganOrMore)
                {
                    int reward = Mathf.FloorToInt(currentBet * expectedMultiplier);
                    expectedRewardText.text = $"Expected Reward: {reward}";
                }
                else
                {
                    expectedRewardText.text = "";
                }
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

            var audio = Managers.AudioManager.Instance;
            if (audio != null) audio.PlayBetConfirmSE();

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
