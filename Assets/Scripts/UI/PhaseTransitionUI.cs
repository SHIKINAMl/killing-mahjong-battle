using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using KillingMahjong.Network;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class PhaseTransitionUI : MonoBehaviour
    {
        [Header("Animation Setup")]
        [SerializeField] private Material checkerMaterial; // Material using UI/CheckerboardTransition
        [SerializeField] private Image fullScreenCheckerImage;
        [SerializeField] private RectTransform horizontalLineRt; // The line that expands
        
        [Header("Text References")]
        [SerializeField] private TextMeshProUGUI centerText; // Used for "対局開始", "1 Round", "先行/後攻"
        [SerializeField] private TextMeshProUGUI loadingText; // "対戦相手を待機中..." など用
        [SerializeField] private TextMeshProUGUI promptText; // "手牌を選んでください" などのプロンプト用

        [Header("Bet & HP Deduction Setup")]
        [SerializeField] private GameObject hpBetContainer;
        [SerializeField] private TextMeshProUGUI enemyBetObj;
        [SerializeField] private TextMeshProUGUI playerBetObj;
        [SerializeField] private TextMeshProUGUI enemyHpObj;
        [SerializeField] private TextMeshProUGUI playerHpObj;

        [Header("Animation Durations")]
        [SerializeField] private float lineInDuration = 0.5f;
        [SerializeField] private float textWaitDuration = 1.0f;
        [SerializeField] private float checkerFadeDuration = 1.0f;
        [SerializeField] private float hpDeductionDuration = 1.5f;

        private bool isWaitingForDeal = false;
        private float dealWaitTimer = 0f;

        private void Start()
        {
            // UIの被り対策: トランジション演出を最前面に表示するためCanvasを追加してSortingOrderを高く設定
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.PhaseTransitionBase;
            
            // レイキャストを有効にする場合（必要に応じて）
            UnityEngine.UI.GraphicRaycaster raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            ResetVisuals();

            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(false);
            }

            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnDealingStarted += HandleDealingStarted;
                NetworkMessageHandler.Instance.OnDealingCompleted += HandleDealingCompleted;
            }
        }

        private void OnDestroy()
        {
            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnDealingStarted -= HandleDealingStarted;
                NetworkMessageHandler.Instance.OnDealingCompleted -= HandleDealingCompleted;
            }
        }

        private void HandleDealingStarted()
        {
            isWaitingForDeal = true;
            // Removed: loadingText
        }

        private void HandleDealingCompleted()
        {
            isWaitingForDeal = false;
            // 山牌構築完了後、画面が暗転していれば晴らす
            PlayRoundStartFadeOut();
        }

        private void Update()
        {
            // Removed: loadingText timer
        }

        private void ResetVisuals()
        {
            if (fullScreenCheckerImage != null && checkerMaterial != null)
            {
                if (!isDarkened)
                {
                    checkerMaterial.SetFloat("_Progress", 0f);
                    fullScreenCheckerImage.gameObject.SetActive(false);
                }
                
                // 確実に画面(親Canvas)全体を覆うようにアンカー設定を強制し、
                // さらに万が一親コンテナが画面より小さい場合に備えて圧倒的なスケールをかける
                RectTransform rt = fullScreenCheckerImage.rectTransform;
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = new Vector3(10f, 10f, 1f); // 画面を10倍で覆う
                }
            }
            
            if (horizontalLineRt != null)
            {
                horizontalLineRt.localScale = new Vector3(0, 0, 1); // target (1,1,1)
                
                // 横線が画面の左右端まで確実に届くように横方向ストレッチを設定
                horizontalLineRt.anchorMin = new Vector2(0, 0.5f);
                horizontalLineRt.anchorMax = new Vector2(1, 0.5f);
                horizontalLineRt.sizeDelta = new Vector2(0, horizontalLineRt.sizeDelta.y);
                horizontalLineRt.anchoredPosition = Vector2.zero;

                horizontalLineRt.gameObject.SetActive(false);
            }

            if (centerText != null) centerText.gameObject.SetActive(false);
            if (hpBetContainer != null) hpBetContainer.SetActive(false);
        }

        private PlayerInfoUI targetPlayerInfoUI;

        public void PlayTransition(string roundName, PlayerInfoUI playerInfoUI, int playerBet, int enemyBet, int playerInitialHp, int enemyInitialHp, Action onMidpoint, Action onComplete)
        {
            this.targetPlayerInfoUI = playerInfoUI;
            StartCoroutine(SequenceRoutine(roundName, playerBet, enemyBet, playerInitialHp, enemyInitialHp, onMidpoint, onComplete));
        }

        private IEnumerator SequenceRoutine(string roundName, int playerBetAmount, int enemyBetAmount, int dummyInitialPlayerHp, int dummyInitialEnemyHp, Action onMidpoint, Action onComplete)
        {
            ResetVisuals();

            // トランジション（対局開始演出）が開始された瞬間に敵のHPなどのUIを非表示にする
            // if (targetPlayerInfoUI != null)
            // {
            //     targetPlayerInfoUI.gameObject.SetActive(false);
            // }

            if (!isDarkened)
            {
                Debug.Log("PhaseTransition: Step 1 - Line In");
                // === 1. 一本線が入る + 「対局開始」 ===
                horizontalLineRt.gameObject.SetActive(true);
                horizontalLineRt.localScale = new Vector3(0, 2f, 1f); // Increased line width

                float t = 0;
                while (t < lineInDuration)
                {
                    // 横幅を10倍(10f)にして確実に画面外まで届かせる
                    horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 10f, t / lineInDuration), 2f, 1f);
                    t += Time.deltaTime;
                    yield return null;
                }
                horizontalLineRt.localScale = new Vector3(10f, 2f, 1f);

                if (centerText != null)
                {
                    centerText.text = "対局開始";
                    centerText.gameObject.SetActive(true);
                }
                yield return new WaitForSeconds(textWaitDuration);

                Debug.Log("PhaseTransition: Step 2 - Line Expand and Checker Fade In");
                // === 2. 線を中心に、市松模様が上下に広がり画面を埋める ===
                
                // Enable fullscreen checker
                if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
                if (checkerMaterial != null)
                {
                    checkerMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
                    checkerMaterial.SetFloat("_Progress", 0f);
                }

                t = 0;
                while (t < checkerFadeDuration)
                {
                    if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", t / checkerFadeDuration);
                    t += Time.deltaTime;
                    yield return null;
                }
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);
                
                // 画面が黒で覆われたので線を隠してサイズをリセット
                horizontalLineRt.gameObject.SetActive(false);
                horizontalLineRt.localScale = new Vector3(1, 0.15f, 1f);
            }
            
            // Midpoint Callback (Behind the scenes UI toggles)
            Debug.Log("PhaseTransition: Midpoint invoked");
            onMidpoint?.Invoke();

            Debug.Log("PhaseTransition: Step 3 - Round Text");
            // === 3. 「対局開始」が消えて「1 Round」になる ===
            if (centerText != null) centerText.text = roundName; // e.g. "1 Round"
            
            Debug.Log("PhaseTransition: Step 4 - HP Deduction");
            // === 4. 賭け金とHP増減表示 ===
            if (hpBetContainer != null)
            {
                hpBetContainer.SetActive(true);
                // 仮のデータアニメーション
                // 実際はGameUIManager等からデータを引数で渡しますが、ここではモックします
                int targetPlayerHp = dummyInitialPlayerHp - playerBetAmount;
                int targetEnemyHp = dummyInitialEnemyHp - enemyBetAmount;

                if (enemyBetObj != null) enemyBetObj.text = "Enemy Bet: <color=red>" + enemyBetAmount + "</color>";
                if (playerBetObj != null) playerBetObj.text = "Your Bet: <color=red>" + playerBetAmount + "</color>";

                float tHp = 0;
                while (tHp < hpDeductionDuration)
                {
                    int currentPlayerAnimHp = Mathf.RoundToInt(Mathf.Lerp(dummyInitialPlayerHp, targetPlayerHp, tHp / hpDeductionDuration));
                    int currentEnemyAnimHp = Mathf.RoundToInt(Mathf.Lerp(dummyInitialEnemyHp, targetEnemyHp, tHp / hpDeductionDuration));
                    if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + currentEnemyAnimHp;
                    if (playerHpObj != null) playerHpObj.text = "Your HP: " + currentPlayerAnimHp;
                    tHp += Time.deltaTime;
                    yield return null;
                }
                if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + targetEnemyHp;
                if (playerHpObj != null) playerHpObj.text = "Your HP: " + targetPlayerHp;
                
                yield return new WaitForSeconds(1.0f);
                hpBetContainer.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            Debug.Log("PhaseTransition: Step 5 - Checker Fade Out (Skipped if Darkened)");
            // === 5. テキスト消滅、市松模様フェードアウト ===
            if (centerText != null) centerText.gameObject.SetActive(false);

            if (!isDarkened)
            {
                float tFade = 0;
                while (tFade < checkerFadeDuration)
                {
                    if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (tFade / checkerFadeDuration));
                    tFade += Time.deltaTime;
                    yield return null;
                }
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
                if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);
            }

            Debug.Log("PhaseTransition: Step 6 - Turn Indicator");

            // === 6. 線が入り「先行/後攻」 ===
            horizontalLineRt.gameObject.SetActive(true);
            horizontalLineRt.localScale = new Vector3(0, 2f, 1f); 
            float tTurnLine = 0;
            while (tTurnLine < lineInDuration)
            {
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 10f, tTurnLine / lineInDuration), 2f, 1f);
                tTurnLine += Time.deltaTime;
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(10f, 2f, 1f);

            if (centerText != null)
            {
                bool isFirst = KillingMahjong.Managers.BoardStateManager.Instance.IsLocalTurn;
                centerText.text = isFirst ? "先攻" : "後攻";
                centerText.gameObject.SetActive(true);
            }
            
            yield return new WaitForSeconds(textWaitDuration);
            
            Debug.Log("PhaseTransition: Step 7 - Finish");
            // === 7. 線アウト + 完了 ===
            if (centerText != null) centerText.gameObject.SetActive(false);
            float tOut = 0;
            while (tOut < lineInDuration)
            {
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(10f, 0, tOut / lineInDuration), 2f, 1f);
                tOut += Time.deltaTime;
                yield return null;
            }
            horizontalLineRt.gameObject.SetActive(false);

            Debug.Log("PhaseTransition: Complete Callback invoked");
            onComplete?.Invoke();
        }
        public void PlayCenterTextAnim(string text, float duration = 1.5f, Action onComplete = null)
        {
            StartCoroutine(CenterTextAnimRoutine(text, duration, onComplete));
        }

        public IEnumerator PlayCenterTextAnimRoutine(string text, float duration = 1.5f, Action onComplete = null)
        {
            yield return StartCoroutine(CenterTextAnimRoutine(text, duration, onComplete));
        }

        private IEnumerator CenterTextAnimRoutine(string text, float duration, Action onComplete = null)
        {
            if (horizontalLineRt != null)
            {
                horizontalLineRt.gameObject.SetActive(true);
                horizontalLineRt.localScale = new Vector3(0, 2f, 1f); 
            }

            float t = 0;
            while (t < lineInDuration)
            {
                if (horizontalLineRt != null)
                {
                    horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 10f, t / lineInDuration), 2f, 1f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (horizontalLineRt != null) horizontalLineRt.localScale = new Vector3(10f, 2f, 1f);

            if (centerText != null)
            {
                centerText.text = text;
                centerText.gameObject.SetActive(true);
            }
            
            yield return new WaitForSeconds(duration);
            
            if (centerText != null) centerText.gameObject.SetActive(false);
            
            t = 0;
            while (t < lineInDuration)
            {
                if (horizontalLineRt != null)
                {
                    horizontalLineRt.localScale = new Vector3(Mathf.Lerp(10f, 0, t / lineInDuration), 2f, 1f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (horizontalLineRt != null) horizontalLineRt.gameObject.SetActive(false);

            onComplete?.Invoke();
        }

        private bool isDarkened = false;

        public bool IsDarkenTransitioning { get; private set; }

        public void PlayRoundStartDarken(string text, Action onDarkened = null)
        {
            if (isDarkened)
            {
                onDarkened?.Invoke();
                return;
            }
            isDarkened = true;
            IsDarkenTransitioning = true;
            StartCoroutine(RoundStartDarkenRoutine(text, onDarkened));
        }

        private IEnumerator RoundStartDarkenRoutine(string text, Action onDarkened)
        {
            ResetVisuals();

            // 市松模様フェードイン (暗転)
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
            if (checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
                checkerMaterial.SetFloat("_Progress", 0f);
            }

            float t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", t / checkerFadeDuration);
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);

            IsDarkenTransitioning = false;
            
            // 暗転完了のコールバック（ここで盤面をクリアする）
            onDarkened?.Invoke();

            // ドン！とテキスト表示
            if (centerText != null)
            {
                centerText.text = text;
                centerText.gameObject.SetActive(true);
                centerText.color = Color.white;
                
                t = 0;
                float duration = 0.4f;
                Vector3 initialScale = new Vector3(3f, 3f, 1f);
                Vector3 targetScale = Vector3.one;
                
                while (t < duration)
                {
                    float progress = t / duration;
                    float scaleProgress = 1f - Mathf.Pow(1f - progress, 4f); 
                    centerText.transform.localScale = Vector3.LerpUnclamped(initialScale, targetScale, scaleProgress);
                    t += Time.deltaTime;
                    yield return null;
                }
                centerText.transform.localScale = targetScale;
                
                // 画面揺れ（着弾の衝撃）
                StartCoroutine(ScreenShakeRoutine(0.2f, 20f));
            }
        }

        public void PlayRoundStartFadeOut(Action onComplete = null)
        {
            if (!isDarkened)
            {
                onComplete?.Invoke();
                return;
            }
            isDarkened = false;
            StartCoroutine(RoundStartFadeOutRoutine(onComplete));
        }

        public void ChangeDarkenText(string text)
        {
            if (isDarkened && centerText != null)
            {
                centerText.text = text;
            }
        }

        private IEnumerator RoundStartFadeOutRoutine(Action onComplete)
        {
            // テキストを隠す
            if (centerText != null) centerText.gameObject.SetActive(false);
            if (horizontalLineRt != null) horizontalLineRt.gameObject.SetActive(false);

            // 市松模様フェードアウト (晴れる)
            float t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (t / checkerFadeDuration));
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);

            onComplete?.Invoke();
        }

        /// <summary>
        /// ロン後の点数精算演出を再生する。
        /// 掛け金フェイズのHP減少演出と対になる形で、勝者のHPが増加し敗者のHPが減少するアニメーション。
        /// </summary>
        /// <param name="isLocalWin">ローカルプレイヤーが勝者かどうか</param>
        /// <param name="winnerGain">勝者の獲得点数</param>
        /// <param name="loserLoss">敗者の喪失点数</param>
        /// <param name="prevLocalHp">演出開始時のローカルプレイヤーHP（精算前）</param>
        /// <param name="prevEnemyHp">演出開始時の敵プレイヤーHP（精算前）</param>
        /// <param name="newLocalHp">精算後のローカルプレイヤーHP</param>
        /// <param name="newEnemyHp">精算後の敵プレイヤーHP</param>
        /// <param name="resultLabel">表示する精算ラベル（例: "満貫"）</param>
        /// <param name="onComplete">演出完了コールバック</param>
        [Header("Effects Settings")]
        [SerializeField] private Sprite bloodSplatterSprite;
        [SerializeField] private Color dimmerColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private Sprite playerCutinSprite;
        [SerializeField] private Sprite playerTroubledSprite;

        public void PlayScoreSettlementAnimation(
            bool isLocalWin,
            int winnerGain,
            int loserLoss,
            int prevLocalHp,
            int prevEnemyHp,
            int newLocalHp,
            int newEnemyHp,
            string resultLabel,
            Action onComplete)
        {
            StartCoroutine(ScoreSettlementRoutine(
                isLocalWin, winnerGain, loserLoss,
                prevLocalHp, prevEnemyHp,
                newLocalHp, newEnemyHp,
                resultLabel, onComplete));
        }

        private IEnumerator ScreenShakeRoutine(float duration, float magnitude)
        {
            Vector3 originalPos = transform.localPosition;
            float elapsed = 0.0f;
            
            while (elapsed < duration)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                
                transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            transform.localPosition = originalPos;
        }

        private IEnumerator ScoreSettlementRoutine(
            bool isLocalWin,
            int winnerGain,
            int loserLoss,
            int prevLocalHp,
            int prevEnemyHp,
            int newLocalHp,
            int newEnemyHp,
            string resultLabel,
            Action onComplete)
        {
            ResetVisuals();

            // 1. 半透明の暗転（ディマー）を作成・表示
            GameObject dimmerObj = new GameObject("DimmerOverlay");
            dimmerObj.transform.SetParent(transform, false);
            dimmerObj.transform.SetAsFirstSibling();
            var dimmerImage = dimmerObj.AddComponent<Image>();
            dimmerImage.color = new Color(0, 0, 0, 0); // 初期は透明
            var dimmerRt = dimmerObj.GetComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.sizeDelta = Vector2.zero;

            // フェードイン
            float t = 0;
            while (t < 0.3f)
            {
                dimmerImage.color = Color.Lerp(new Color(0, 0, 0, 0), dimmerColor, t / 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            dimmerImage.color = dimmerColor;

            // 2. 役名のバウンド表示
            if (centerText != null)
            {
                centerText.text = resultLabel;
                centerText.gameObject.SetActive(true);
                centerText.color = Color.red;
                
                // ドンッ！とスタンプのように出現するアニメーション
                t = 0;
                float duration = 0.4f;
                Vector3 initialScale = new Vector3(3f, 3f, 1f);
                Vector3 targetScale = Vector3.one;
                
                while (t < duration)
                {
                    float progress = t / duration;
                    // EaseInCubic または Overshoot っぽい動き
                    float scaleProgress = 1f - Mathf.Pow(1f - progress, 4f); 
                    centerText.transform.localScale = Vector3.LerpUnclamped(initialScale, targetScale, scaleProgress);
                    t += Time.deltaTime;
                    yield return null;
                }
                centerText.transform.localScale = targetScale;
                
                // 画面揺れ（着弾の衝撃）
                StartCoroutine(ScreenShakeRoutine(0.2f, 20f));
            }
            
            yield return new WaitForSeconds(1.0f);
            if (centerText != null) centerText.gameObject.SetActive(false);

            // 3. HP表示と血飛沫＆画面揺れ
            if (hpBetContainer != null)
            {
                hpBetContainer.SetActive(true);

                if (isLocalWin)
                {
                    if (playerBetObj != null) playerBetObj.text = $"獲得: +{winnerGain}";
                    if (enemyBetObj != null) enemyBetObj.text = $"喪失: -{loserLoss}";
                }
                else
                {
                    if (playerBetObj != null) playerBetObj.text = $"喪失: -{loserLoss}";
                    if (enemyBetObj != null) enemyBetObj.text = $"獲得: +{winnerGain}";
                }

                // 血飛沫画像の生成
                GameObject splatterObj = null;
                Image splatterImage = null;
                if (bloodSplatterSprite != null)
                {
                    splatterObj = new GameObject("BloodSplatter");
                    splatterObj.transform.SetParent(transform, false);
                    splatterImage = splatterObj.AddComponent<Image>();
                    splatterImage.sprite = bloodSplatterSprite;
                    splatterImage.preserveAspect = true;
                    
                    RectTransform srt = splatterObj.GetComponent<RectTransform>();
                    srt.sizeDelta = new Vector2(800, 800);
                    
                    // 敗者側に血飛沫を配置（簡易的に上下で位置を分ける）
                    srt.anchoredPosition = isLocalWin ? new Vector2(0, 300) : new Vector2(0, -300);
                    srt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360));
                    splatterImage.color = new Color(1, 1, 1, 0); // 初期透明
                }

                // 激しい画面揺れと血飛沫表示
                StartCoroutine(ScreenShakeRoutine(0.5f, 30f));
                
                if (splatterImage != null)
                {
                    splatterImage.color = new Color(1, 1, 1, 0.8f);
                }

                // HPカウントアニメーション
                t = 0;
                while (t < hpDeductionDuration)
                {
                    float progress = t / hpDeductionDuration;
                    float eased = 1f - Mathf.Pow(1f - progress, 3f);

                    int currentPlayerAnimHp = Mathf.RoundToInt(Mathf.Lerp(prevLocalHp, newLocalHp, eased));
                    int currentEnemyAnimHp = Mathf.RoundToInt(Mathf.Lerp(prevEnemyHp, newEnemyHp, eased));

                    if (playerHpObj != null) playerHpObj.text = "Your HP: " + currentPlayerAnimHp;
                    if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + currentEnemyAnimHp;

                    // 血飛沫のフェードアウト
                    if (splatterImage != null && progress > 0.5f)
                    {
                        float fadeOutProgress = (progress - 0.5f) * 2f;
                        splatterImage.color = new Color(1, 1, 1, 0.8f * (1f - fadeOutProgress));
                    }

                    t += Time.deltaTime;
                    yield return null;
                }
                
                if (playerHpObj != null) playerHpObj.text = "Your HP: " + newLocalHp;
                if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + newEnemyHp;
                
                if (splatterObj != null) Destroy(splatterObj);

                yield return new WaitForSeconds(1.5f);
                hpBetContainer.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            // ディマーフェードアウト
            t = 0;
            while (t < 0.3f)
            {
                dimmerImage.color = Color.Lerp(dimmerColor, new Color(0, 0, 0, 0), t / 0.3f);
                t += Time.deltaTime;
                yield return null;
            }
            Destroy(dimmerObj);

            Debug.Log("[ScoreSettlement] Complete");
            onComplete?.Invoke();
        }

        public void PlayDrawTransition(Action onMidpoint, Action onComplete)
        {
            StartCoroutine(DrawTransitionRoutine(onMidpoint, onComplete));
        }

        private IEnumerator DrawTransitionRoutine(Action onMidpoint, Action onComplete)
        {
            ResetVisuals();

            // === 1. 一本線イン + 「流局」テキスト ===
            Debug.Log("[DrawTransition] Step 1 - Line In");
            if (horizontalLineRt != null)
            {
                horizontalLineRt.gameObject.SetActive(true);
                horizontalLineRt.localScale = new Vector3(0, 2f, 1f);
            }

            float t = 0;
            while (t < lineInDuration)
            {
                if (horizontalLineRt != null)
                {
                    horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 10f, t / lineInDuration), 2f, 1f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (horizontalLineRt != null) horizontalLineRt.localScale = new Vector3(10f, 2f, 1f);

            if (centerText != null)
            {
                centerText.text = "流局";
                centerText.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(textWaitDuration);

            // === 2. 市松模様フェードイン (暗転) ===
            Debug.Log("[DrawTransition] Step 2 - Checker Fade In");
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
            if (checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
                checkerMaterial.SetFloat("_Progress", 0f);
            }

            t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", t / checkerFadeDuration);
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);

            // === 3. 暗転中のコールバック (UIリセットなど) ===
            Debug.Log("[DrawTransition] Midpoint invoked");
            onMidpoint?.Invoke();

            isDarkened = true; // 暗転状態を記録

            // 少し暗転状態で待機
            yield return new WaitForSeconds(1.0f);

            // フェードアウトせずに暗転状態を維持したまま完了とする
            Debug.Log("[DrawTransition] Complete (stay dark)");
            onComplete?.Invoke();
        }

        public void PlayPromptText(string text, float duration = 2.0f)
        {
            StartCoroutine(PlayPromptTextRoutine(text, duration));
        }

        private IEnumerator PlayPromptTextRoutine(string text, float duration)
        {
            if (promptText == null)
            {
                Debug.LogWarning("[PhaseTransitionUI] promptText is not assigned in the inspector!");
                yield break;
            }

            promptText.text = text;
            promptText.gameObject.SetActive(true);
            promptText.color = new Color(1, 1, 1, 0); // 初期は透明

            RectTransform tmpRt = promptText.GetComponent<RectTransform>();

            // フェードイン＆少しスケールダウンしてドスッという感じにする
            float t = 0;
            while(t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                promptText.color = new Color(1, 1, 1, progress);
                tmpRt.localScale = Vector3.Lerp(new Vector3(1.5f, 1.5f, 1f), Vector3.one, progress);
                yield return null;
            }
            promptText.color = Color.white;
            tmpRt.localScale = Vector3.one;

            // 待機
            yield return new WaitForSeconds(duration);

            // フェードアウト
            t = 0;
            while(t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                promptText.color = new Color(1, 1, 1, 1f - progress);
                yield return null;
            }

            promptText.gameObject.SetActive(false);
        }

        public void PlaySkillCutinAnimation(string skillName, bool isLocalPlayer, CharacterData characterData = null, float duration = 2.0f, Action onComplete = null)
        {
            StartCoroutine(PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, characterData, duration, onComplete));
        }

        public IEnumerator PlaySkillCutinAnimationRoutine(string skillName, bool isLocalPlayer, CharacterData characterData = null, float duration = 2.0f, Action onComplete = null, string subText = null)
        {
            ResetVisuals();

            // 1. コンテナ作成
            GameObject container = new GameObject("DeathGameCutinContainer");
            container.transform.SetParent(transform, false);
            container.transform.SetAsLastSibling();
            RectTransform containerRt = container.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;

            Canvas containerCanvas = container.AddComponent<Canvas>();
            container.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            containerCanvas.overrideSorting = true;
            containerCanvas.sortingOrder = UISortingOrders.PhaseTransitionTop;

            // 2. 即時ディマー（背景が少し見えるように半透明）
            GameObject dimmer = new GameObject("Dimmer");
            dimmer.transform.SetParent(containerRt, false);
            Image dimmerImg = dimmer.AddComponent<Image>();
            dimmerImg.color = new Color(0, 0, 0, 0.5f);
            RectTransform dimmerRt = dimmer.GetComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.sizeDelta = Vector2.zero;

            // 3. 大迫力の斜め背景エフェクト（左下から右上へ）と血飛沫
            List<RectTransform> bgElements = new List<RectTransform>();
            List<Vector2> bgTargetPos = new List<Vector2>();
            List<Vector2> bgStartPos = new List<Vector2>();

            // 斜めの帯（カットイン）背景
            GameObject bgStripeObj = new GameObject("BgStripe");
            bgStripeObj.transform.SetParent(containerRt, false);
            Image stripeImg = bgStripeObj.AddComponent<Image>();
            
            if (isLocalPlayer)
            {
                stripeImg.color = new Color32(10, 80, 200, 255); // 鮮やかな青
            }
            else
            {
                stripeImg.color = new Color32(180, 10, 10, 255); // 鮮やかな赤
            }

            RectTransform stripeRt = bgStripeObj.GetComponent<RectTransform>();
            // 画面を覆い尽くす長方形から、帯状（バナー）に変更
            stripeRt.sizeDelta = new Vector2(6000f, 600f);
            stripeRt.localRotation = Quaternion.Euler(0, 0, 15f); // 傾きを少し緩やかに
            
            // 下から斜めに突き抜けるように配置
            Vector2 stripeTarget = new Vector2(0, 0);
            Vector2 stripeStart = new Vector2(0, -3000f);
            stripeRt.anchoredPosition = stripeStart;

            bgElements.Add(stripeRt);
            bgStartPos.Add(stripeStart);
            bgTargetPos.Add(stripeTarget);

            // 追加の血飛沫（少しだけ）
            if (bloodSplatterSprite != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    GameObject splatterObj = new GameObject($"Splatter_{i}");
                    splatterObj.transform.SetParent(containerRt, false);
                    Image spImg = splatterObj.AddComponent<Image>();
                    spImg.sprite = bloodSplatterSprite;
                    spImg.color = isLocalPlayer ? new Color32(10, 80, 200, 180) : new Color32(180, 10, 10, 180);
                    spImg.preserveAspect = true;

                    RectTransform spRt = splatterObj.GetComponent<RectTransform>();
                    float size = UnityEngine.Random.Range(600, 1200);
                    spRt.sizeDelta = new Vector2(size, size);
                    spRt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360f));
                    
                    Vector2 spTarget = new Vector2(UnityEngine.Random.Range(-300f, 300f), UnityEngine.Random.Range(-200f, 200f));
                    Vector2 spStart = spTarget + new Vector2(0, -2500f);
                    
                    bgElements.Add(spRt);
                    bgStartPos.Add(spStart);
                    bgTargetPos.Add(spTarget);
                }
            }

            // 4. キャラクター立ち絵
            Sprite bodySprite = null;
            Sprite faceSprite = null;

            if (characterData != null)
            {
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == characterData.defaultBodyId);
                bodySprite = bodyMatch != null ? bodyMatch.sprite : characterData.normalSprite;

                var faceMatch = characterData.faceSprites?.Find(x => x.id == characterData.defaultFaceId);
                if (faceMatch != null) faceSprite = faceMatch.sprite;
            }
            else
            {
                bodySprite = isLocalPlayer ? playerCutinSprite : playerTroubledSprite;
            }

            GameObject portraitObj = null;
            RectTransform portraitRt = null;
            Vector2 portraitTargetPos = Vector2.zero;
            Vector2 portraitStartPos = Vector2.zero;

            if (bodySprite != null)
            {
                portraitObj = new GameObject("Portrait");
                portraitObj.transform.SetParent(containerRt, false);
                Image portraitImg = portraitObj.AddComponent<Image>();
                portraitImg.sprite = bodySprite;
                portraitImg.SetNativeSize();

                portraitRt = portraitObj.GetComponent<RectTransform>();
                portraitRt.pivot = new Vector2(0.5f, 0f); // 下端中央
                
                // SetNativeSizeの直後だとrect.heightが未確定な場合があるため、spriteの実際のサイズからスケールを計算する
                float targetHeight = 800f; // さらに少し小さめにして確実に頭が収まるようにする
                float actualHeight = bodySprite.rect.height;
                float scale = actualHeight > 0 ? targetHeight / actualHeight : 1f;
                portraitRt.localScale = new Vector3(scale, scale, 1f);
                
                // 常に左下に配置（自分の顔のみ出るため）
                portraitRt.anchorMin = new Vector2(0f, 0f);
                portraitRt.anchorMax = new Vector2(0f, 0f);
                portraitTargetPos = new Vector2(250, -150); // 少し下に移動して頭頂部が見切れないようにする
                portraitStartPos = portraitTargetPos + new Vector2(0, -800); // 下から上がってくる
                
                portraitRt.anchoredPosition = portraitStartPos; 
                
                // 立ち絵にテーマカラーのドロップシャドウ
                Shadow pShadow = portraitObj.AddComponent<Shadow>();
                pShadow.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                pShadow.effectDistance = new Vector2(20, -20);

                // 顔画像も合成
                if (faceSprite != null)
                {
                    GameObject faceObj = new GameObject("Face");
                    faceObj.transform.SetParent(portraitRt, false);
                    Image faceImg = faceObj.AddComponent<Image>();
                    faceImg.sprite = faceSprite;
                    faceImg.SetNativeSize();

                    RectTransform faceRt = faceObj.GetComponent<RectTransform>();
                    faceRt.pivot = new Vector2(0.5f, 0f);
                    faceRt.anchorMin = new Vector2(0.5f, 0f);
                    faceRt.anchorMax = new Vector2(0.5f, 0f);
                    faceRt.anchoredPosition = Vector2.zero;
                }
            }

            // 5. メインテキスト（少し傾ける）
            GameObject mainTextObj = new GameObject("MainText");
            mainTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI mainText = mainTextObj.AddComponent<TextMeshProUGUI>();
            mainText.text = skillName;
            mainText.fontSize = 90; // スケールを1にする代わりにフォントサイズを大きくする
            mainText.color = new Color32(255, 255, 255, 0); // 白字
            mainText.fontStyle = FontStyles.Bold;
            mainText.alignment = TextAlignmentOptions.Right;
            if (centerText != null) mainText.font = centerText.font;

            // 黒い影
            Shadow txtShadow1 = mainTextObj.AddComponent<Shadow>();
            txtShadow1.effectColor = new Color(0, 0, 0, 1f);
            txtShadow1.effectDistance = new Vector2(10, -10);
            
            // テーマカラーの影（ズレ）
            Shadow txtShadow2 = mainTextObj.AddComponent<Shadow>();
            txtShadow2.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
            txtShadow2.effectDistance = new Vector2(-10, 8);

            RectTransform mainRt = mainText.GetComponent<RectTransform>();
            mainRt.anchorMin = new Vector2(1f, 1f);
            mainRt.anchorMax = new Vector2(1f, 1f);
            mainRt.pivot = new Vector2(1f, 1f);
            mainRt.sizeDelta = new Vector2(500, 150); // 幅を絞る
            mainRt.anchoredPosition = new Vector2(-100, string.IsNullOrEmpty(subText) ? -120 : -80); // さらに下・左へ移動
            mainRt.localRotation = Quaternion.Euler(0, 0, -10f); // 少し傾ける
            mainRt.localScale = Vector3.one; // スケールは1にする

            // 6. サブテキスト（役の名前など）
            TextMeshProUGUI subTextUI = null;
            CanvasGroup subCg = null;
            if (!string.IsNullOrEmpty(subText))
            {
                GameObject subObj = new GameObject("SubText");
                subObj.transform.SetParent(containerRt, false);
                subCg = subObj.AddComponent<CanvasGroup>();
                subCg.alpha = 0f;
                subTextUI = subObj.AddComponent<TextMeshProUGUI>();
                subTextUI.text = subText;
                subTextUI.fontSize = 60; // 適切なサイズに
                subTextUI.color = new Color32(255, 255, 255, 255);
                subTextUI.fontStyle = FontStyles.Bold;
                subTextUI.alignment = TextAlignmentOptions.Right;
                if (centerText != null) subTextUI.font = centerText.font;

                Shadow s1 = subObj.AddComponent<Shadow>();
                s1.effectColor = new Color(0, 0, 0, 1f);
                s1.effectDistance = new Vector2(10, -10);

                Shadow s2 = subObj.AddComponent<Shadow>();
                s2.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                s2.effectDistance = new Vector2(-10, 8);

                RectTransform subRt = subTextUI.GetComponent<RectTransform>();
                subRt.anchorMin = new Vector2(1f, 1f);
                subRt.anchorMax = new Vector2(1f, 1f);
                subRt.pivot = new Vector2(1f, 1f);
                subRt.sizeDelta = new Vector2(500, 100);
                subRt.anchoredPosition = new Vector2(-100, -180); // メインテキストの下へ
                subRt.localRotation = Quaternion.Euler(0, 0, -10f); // メインテキストと同じ傾き
                subRt.localScale = Vector3.one;
            }

            // --- 暴力的なアニメーション開始 ---
            float t = 0;
            float impactDuration = 0.15f; 

            // スライドを廃止し、背景要素と立ち絵は下から「ばっ！」と突き上げる
            for (int i = 0; i < bgElements.Count; i++) bgElements[i].anchoredPosition = bgStartPos[i];
            if (portraitRt != null) portraitRt.anchoredPosition = portraitStartPos;

            // 画面揺れ
            StartCoroutine(ScreenShakeRoutine(0.2f, 15f));

            while (t < impactDuration)
            {
                float progress = t / impactDuration;
                float easeIn = Mathf.Pow(progress, 3f);

                // 文字が叩きつけられる演出（スケール5から1へ）
                mainRt.localScale = Vector3.LerpUnclamped(new Vector3(5f, 5f, 1f), Vector3.one, easeIn);
                mainText.color = new Color32(255, 255, 255, (byte)(255 * progress));

                // 背景と立ち絵の下からのスライドイン
                for (int i = 0; i < bgElements.Count; i++)
                {
                    bgElements[i].anchoredPosition = Vector2.LerpUnclamped(bgStartPos[i], bgTargetPos[i], easeIn);
                }
                if (portraitRt != null)
                {
                    portraitRt.anchoredPosition = Vector2.LerpUnclamped(portraitStartPos, portraitTargetPos, easeIn);
                }

                if (subCg != null)
                {
                    subCg.alpha = progress; // サブテキストはフェードインのみ（叩きつけない）
                }
                
                t += Time.deltaTime;
                yield return null;
            }
            
            mainRt.localScale = Vector3.one;
            mainText.color = Color.white;

            // 着弾の瞬間に揺れ（ダメ押し）
            StartCoroutine(ScreenShakeRoutine(0.15f, 20f));

            // 少し待機（文字を見せる時間）
            float waitTime = Mathf.Max(0, duration - impactDuration - 0.2f);
            yield return new WaitForSeconds(waitTime);

            // 退出アニメーション（一瞬でガラスが割れるように消える、または画面外へ吹き飛ぶ）
            t = 0;
            float outDuration = 0.2f;
            while (t < outDuration)
            {
                float progress = t / outDuration;
                float easeIn = progress * progress * progress;

                dimmerImg.color = new Color(0, 0, 0, 0.85f * (1f - progress));

                // 外側に吹き飛ぶ
                for (int i = 0; i < bgElements.Count; i++)
                {
                    bgElements[i].anchoredPosition = Vector2.Lerp(bgTargetPos[i], bgStartPos[i], easeIn);
                }
                if (portraitRt != null)
                {
                    portraitRt.anchoredPosition = Vector2.Lerp(portraitTargetPos, portraitStartPos, easeIn);
                }
                
                // メインテキストはさらに傾きながら下へ落ちる
                mainRt.anchoredPosition = new Vector2(0, (string.IsNullOrEmpty(subText) ? 50 : 150) - (1000 * easeIn));
                mainRt.localRotation = Quaternion.Euler(0, 0, -15f - (30f * easeIn));

                if (subCg != null)
                {
                    subCg.alpha = 1f - progress;
                }
                
                t += Time.deltaTime;
                yield return null;
            }

            Destroy(container);
            onComplete?.Invoke();
        }
    }
}
