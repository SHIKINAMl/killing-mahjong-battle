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

        // ---- 予想報酬の行（調整値。シーンではなくここを触る）----
        //
        // **賭け金と同じテキストに詰め込まない。** `currentBetText`（BetMoneyText）は
        // 200x50・fontSize 25 の1行ぶんしかなく、2行目を足すと背景の枠からはみ出して
        // 下半分が潰れる。賭け金フェイズで唯一の判断材料がそれで読めなくなっていた。

        /// <summary>
        /// 賭け金テキストの中心からどれだけ下げるか。
        ///
        /// **枠の下と Confirm の間はほとんど空いていない**（実測で 6.25 しかない）。
        /// 単純に「枠の高さぶん下げる」と Confirm の裏に回って読めなくなるので、
        /// 実機で合わせたこの値をそのまま持つ。
        /// </summary>
        private const float RewardOffsetY = 20f;

        /// <summary>行の高さ。賭け金と同じ 50 にすると文字が下に寄って Confirm に被る</summary>
        private const float RewardHeight = 20f;

        /// <summary>賭け金より一回り小さく。従属情報だと見た目で分かるように</summary>
        private const float RewardFontScale = 0.7f;

        /// <summary>賭け金の白と区別するための色</summary>
        private static readonly Color RewardColor = new Color(1f, 0.85f, 0.4f, 1f);

        private TextMeshProUGUI _runtimeRewardText;

        /// <summary>
        /// 予想報酬を出すテキストを返す。
        ///
        /// `expectedRewardText` はインスペクタ用に用意されているが**シーンで未割り当て**で、
        /// そのため長らく一度も使われていなかった。割り当ててあればそれを使い、
        /// 無ければ賭け金の下に実行時に作る。
        ///
        /// **シーンには置かない。** 対局シーンが2つ（UIテストシーン / OpeningScene）
        /// あるので、置くと片方だけ直し忘れる。
        /// </summary>
        private TextMeshProUGUI EnsureExpectedRewardText()
        {
            if (expectedRewardText != null) return expectedRewardText;
            if (_runtimeRewardText != null) return _runtimeRewardText;
            if (currentBetText == null) return null;

            var src = currentBetText.rectTransform;
            if (src.parent == null) return null;

            var go = new GameObject("ExpectedRewardText", typeof(RectTransform));
            go.transform.SetParent(src.parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = new Vector2(src.sizeDelta.x, RewardHeight);
            rt.localScale = src.localScale;
            rt.anchoredPosition = src.anchoredPosition + new Vector2(0f, -RewardOffsetY);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = currentBetText.font;
            tmp.fontSize = currentBetText.fontSize * RewardFontScale;
            tmp.color = RewardColor;
            tmp.alignment = currentBetText.alignment;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            _runtimeRewardText = tmp;
            return _runtimeRewardText;
        }

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

        /// <summary>
        /// 賭け金画面の見出しを日本語にする。
        ///
        /// 「CurrentMoney」「BetMoney」はシーンに直接置かれた固定テキストで、
        /// **対局シーンが2つある**ため片方だけ直す事故が起きる。シーンを触らず、
        /// 実行時に文字列で拾って差し替える。
        ///
        /// 「Money」は血を賭けるという設定とも噛み合わないので、所持金／賭け金に改めている。
        /// 数字側（currentMoneyText / currentBetText）は見出しが付いたぶん単位を落とし、
        /// 数字だけを大きく見せる。
        /// </summary>
        private void ApplyJapaneseLabels()
        {
            var root = (hpBarPanel != null) ? hpBarPanel : transform as RectTransform;
            if (root != null)
            {
                foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    var s = t.text;
                    if (string.IsNullOrEmpty(s)) continue;
                    if (s.StartsWith("CurrentMoney")) t.text = "所持金";
                    else if (s.StartsWith("BetMoney")) t.text = "賭け金";
                }
            }

            SetButtonLabel(confirmButton, "確定");
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = label;
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

            ApplyJapaneseLabels();

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
            // 賭け金の上限と単位はサーバーが決める。bet_accepted で受け取った値があれば
            // そちらを使い、まだ来ていない初回だけ GameRules の既定値に頼る
            var rules = GameRules.GetRuleSet(specialVictoryCount);
            var board = Managers.BoardStateManager.Instance;
            bool useServer = board != null && board.HasServerBetRules;

            this.bettingUnit = useServer ? board.ServerBetUnit : rules.BetUnit;
            this.maxBet = useServer ? board.ServerBetMax : rules.BetMax;
            this.initialMoney = initialHp;
            this.currentMoney = currentHp;
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
            // 見出しは ApplyJapaneseLabels が「所持金」に直しているので、ここは数字だけ
            if (currentMoneyText != null)
                currentMoneyText.text = currentMoney.ToString();

            bool isTenpai = Managers.BoardStateManager.Instance != null && 
                            Managers.BoardStateManager.Instance.LocalWaitDataList != null && 
                            Managers.BoardStateManager.Instance.LocalWaitDataList.Count > 0;

            // **飜数も満貫かどうかも、サーバーが `is_tenpai` で返した値をそのまま使う。
            // クライアントで数え直さない。**
            //
            // 以前は `GameRules.CalculateTotalHan` で役名から数え直していたが、
            // サーバーは強化ぶんを付けて「断么九+1」のような文字列で返すため
            // `GetBaseHan` の文字列一致に掛からず加算されない。その結果、
            // サーバーが han=5 / mangan_or_more=true と言っている手が
            // 4飜以下と判定され、予想報酬が出ないことがあった（実機で確認）。
            //
            // 手牌決定ダイアログのランク表示（GameUIHandSelectionController）も
            // もともと `wait.han` と `wait.mangan_or_more` を見ているので、そちらに揃える。
            int maxHan = 0;
            bool isManganOrMore = false;
            if (isTenpai)
            {
                foreach (var wait in Managers.BoardStateManager.Instance.LocalWaitDataList)
                {
                    if (wait.han > maxHan) maxHan = wait.han;
                    if (wait.mangan_or_more) isManganOrMore = true;
                }
            }

            // 賭け金の枠は1行ぶんしかないので、ここには賭け金だけを入れる。
            // 「賭け金」の見出しは枠の上に出ているので、単位は付けない
            if (currentBetText != null)
            {
                currentBetText.text = currentBet.ToString();
            }

            var rewardText = EnsureExpectedRewardText();
            if (rewardText != null)
            {
                if (isTenpai && isManganOrMore)
                {
                    // 実際の精算と同じ計算を使う（端数処理が違うと表示と結果がずれる）
                    int reward = GameRules.CalculateWinnerGain(currentBet, maxHan);
                    rewardText.text = $"予想報酬: {reward}";
                }
                else
                {
                    rewardText.text = "";
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
