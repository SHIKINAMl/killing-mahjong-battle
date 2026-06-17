using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using KillingMahjong.Network;

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

        [Header("Loading UI Settings")]
        [SerializeField] private Vector2 loadingTextPosition = new Vector2(-50, 50);
        [SerializeField] private Color loadingTextColor = Color.white;

        private bool isWaitingForDeal = false;
        private float dealWaitTimer = 0f;
        private TextMeshProUGUI loadingText;

        private void Start()
        {
            // UIの被り対策: トランジション演出を最前面に表示するためCanvasを追加してSortingOrderを高く設定
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 19; // 最前面に設定
            
            // レイキャストを有効にする場合（必要に応じて）
            UnityEngine.UI.GraphicRaycaster raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            ResetVisuals();

            if (centerText != null && loadingText == null)
            {
                loadingText = Instantiate(centerText, centerText.transform.parent);
                loadingText.gameObject.name = "LoadingText";
                RectTransform rt = loadingText.GetComponent<RectTransform>();
                
                // 右下に配置 (Inspectorから位置調整可能)
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = loadingTextPosition; // 変更点
                
                loadingText.enableAutoSizing = false;
                loadingText.fontSize = 60;
                loadingText.color = loadingTextColor; // 変更点
                loadingText.alignment = TextAlignmentOptions.BottomRight;
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
            if (targetPlayerInfoUI != null)
            {
                targetPlayerInfoUI.gameObject.SetActive(false);
            }

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

        public void PlayRoundStartDarken(string text)
        {
            if (isDarkened) return;
            isDarkened = true;
            IsDarkenTransitioning = true;
            StartCoroutine(RoundStartDarkenRoutine(text));
        }

        private IEnumerator RoundStartDarkenRoutine(string text)
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
        [SerializeField] private Sprite enemyCutinSprite;

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

            // 線とテキストを隠す
            if (horizontalLineRt != null)
            {
                horizontalLineRt.gameObject.SetActive(false);
                horizontalLineRt.localScale = new Vector3(1, 0.15f, 1f);
            }
            if (centerText != null) centerText.gameObject.SetActive(false);

            // === 3. 暗転中のコールバック (UIリセットなど) ===
            Debug.Log("[DrawTransition] Midpoint invoked");
            onMidpoint?.Invoke();

            // 少し暗転状態で待機
            yield return new WaitForSeconds(1.0f);

            // === 4. 市松模様フェードアウト (晴れる) ===
            Debug.Log("[DrawTransition] Step 4 - Checker Fade Out");
            t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (t / checkerFadeDuration));
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);

            Debug.Log("[DrawTransition] Complete");
            onComplete?.Invoke();
        }

        public void PlaySkillCutinAnimation(string skillName, bool isLocalPlayer, float duration = 2.0f, Action onComplete = null)
        {
            StartCoroutine(PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, duration, onComplete));
        }

        public IEnumerator PlaySkillCutinAnimationRoutine(string skillName, bool isLocalPlayer, float duration = 2.0f, Action onComplete = null, string subText = null)
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

            // 2. 即時ディマー（真っ暗に近い）
            GameObject dimmer = new GameObject("Dimmer");
            dimmer.transform.SetParent(containerRt, false);
            Image dimmerImg = dimmer.AddComponent<Image>();
            dimmerImg.color = new Color(0, 0, 0, 0.85f);
            RectTransform dimmerRt = dimmer.GetComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.sizeDelta = Vector2.zero;

            // 3. ランダムな血飛沫と図形（背景）を生成（量を抑える）
            int splatterCount = 2; // 5から2に減らしてスッキリさせる
            List<RectTransform> bgElements = new List<RectTransform>();
            List<Vector2> bgTargetPos = new List<Vector2>();
            List<Vector2> bgStartPos = new List<Vector2>();

            for (int i = 0; i < splatterCount; i++)
            {
                GameObject bgObj = new GameObject($"BgElement_{i}");
                bgObj.transform.SetParent(containerRt, false);
                Image img = bgObj.AddComponent<Image>();
                
                // 血飛沫か長方形のスラッシュかランダム
                bool isSplatter = bloodSplatterSprite != null && UnityEngine.Random.value > 0.4f;
                if (isSplatter)
                {
                    img.sprite = bloodSplatterSprite;
                    img.preserveAspect = true;
                }

                // プレイヤーと敵でテーマカラーを完全に分ける
                float colorRand = UnityEngine.Random.value;
                if (isLocalPlayer)
                {
                    // 自分のターン：冷静なブルーテーマ
                    if (colorRand > 0.7f) img.color = new Color32(10, 80, 200, 255); // 鮮やかな青
                    else if (colorRand > 0.2f) img.color = new Color32(5, 15, 40, 255); // 深いネイビー（黒の代わり）
                    else img.color = new Color32(200, 200, 255, 180); // 白（青み）
                }
                else
                {
                    // 相手のターン：狂気のレッドテーマ
                    if (colorRand > 0.7f) img.color = new Color32(180, 10, 10, 255); // 赤
                    else if (colorRand > 0.2f) img.color = new Color32(15, 15, 15, 255); // 黒
                    else img.color = new Color32(200, 200, 200, 180); // 白
                }

                RectTransform rt = bgObj.GetComponent<RectTransform>();
                float size = isSplatter ? UnityEngine.Random.Range(600, 1000) : UnityEngine.Random.Range(400, 1000);
                rt.sizeDelta = isSplatter ? new Vector2(size, size) : new Vector2(Screen.width * 2.5f, size / 4f);
                
                // 回転（少しマイルドに）
                rt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-20f, 20f));

                // 位置（画面中心付近から散らす）
                Vector2 targetPos = new Vector2(UnityEngine.Random.Range(-400f, 400f), UnityEngine.Random.Range(-300f, 300f));
                Vector2 startPos = targetPos + new Vector2(UnityEngine.Random.Range(-800f, 800f), UnityEngine.Random.Range(-800f, 800f)); // バラバラの方向から飛んでくる
                
                rt.anchoredPosition = startPos;
                
                bgElements.Add(rt);
                bgStartPos.Add(startPos);
                bgTargetPos.Add(targetPos);
            }

            // 4. キャラクター立ち絵
            Sprite cutinSprite = isLocalPlayer ? playerCutinSprite : enemyCutinSprite;
            GameObject portraitObj = null;
            RectTransform portraitRt = null;
            Vector2 portraitTargetPos = Vector2.zero;
            Vector2 portraitStartPos = Vector2.zero;

            if (cutinSprite != null)
            {
                portraitObj = new GameObject("Portrait");
                portraitObj.transform.SetParent(containerRt, false);
                Image portraitImg = portraitObj.AddComponent<Image>();
                portraitImg.sprite = cutinSprite;
                portraitImg.preserveAspect = true;

                portraitRt = portraitObj.GetComponent<RectTransform>();
                portraitRt.pivot = new Vector2(0.5f, 0f); // 下端中央
                portraitRt.sizeDelta = new Vector2(700, 900);
                
                if (isLocalPlayer)
                {
                    // 自分の場合は左下に配置
                    portraitRt.anchorMin = new Vector2(0f, 0f);
                    portraitRt.anchorMax = new Vector2(0f, 0f);
                    portraitTargetPos = new Vector2(350, -50); 
                }
                else
                {
                    // 相手の場合は右下に配置し、画像を左右反転（敵側から向かってくる感）
                    portraitRt.anchorMin = new Vector2(1f, 0f);
                    portraitRt.anchorMax = new Vector2(1f, 0f);
                    portraitTargetPos = new Vector2(-350, -50);
                    portraitRt.localScale = new Vector3(-1f, 1f, 1f);
                }

                portraitRt.anchoredPosition = portraitTargetPos; 
                
                // 立ち絵にテーマカラーのドロップシャドウ
                Shadow pShadow = portraitObj.AddComponent<Shadow>();
                pShadow.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                pShadow.effectDistance = new Vector2(20, -20);
            }

            // 5. メインテキスト（少し傾ける）
            GameObject mainTextObj = new GameObject("MainText");
            mainTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI mainText = mainTextObj.AddComponent<TextMeshProUGUI>();
            mainText.text = skillName;
            mainText.fontSize = 120; // かなり小さく変更
            mainText.color = new Color32(255, 255, 255, 0); // 白字
            mainText.fontStyle = FontStyles.Bold;
            mainText.alignment = TextAlignmentOptions.Center;
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
            mainRt.sizeDelta = new Vector2(2000, 800);
            mainRt.anchoredPosition = new Vector2(0, string.IsNullOrEmpty(subText) ? 0 : 50); // より下に配置
            mainRt.localRotation = Quaternion.Euler(0, 0, -15f); // 以前の傾きに戻す
            mainRt.localScale = new Vector3(5f, 5f, 1f); // 以前のスケールに戻す

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
                subTextUI.fontSize = 80; // かなり小さく変更
                subTextUI.color = new Color32(255, 255, 255, 255);
                subTextUI.fontStyle = FontStyles.Bold;
                subTextUI.alignment = TextAlignmentOptions.Center;
                if (centerText != null) subTextUI.font = centerText.font;

                Shadow s1 = subObj.AddComponent<Shadow>();
                s1.effectColor = new Color(0, 0, 0, 1f);
                s1.effectDistance = new Vector2(10, -10);

                Shadow s2 = subObj.AddComponent<Shadow>();
                s2.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                s2.effectDistance = new Vector2(-10, 8);

                RectTransform subRt = subTextUI.GetComponent<RectTransform>();
                subRt.sizeDelta = new Vector2(2000, 400);
                subRt.anchoredPosition = new Vector2(0, -150); // メインテキストに合わせて下げる
                subRt.localRotation = Quaternion.identity; // 水平
                subRt.localScale = Vector3.one;
            }

            // --- 暴力的なアニメーション開始 ---
            float t = 0;
            float impactDuration = 0.15f; 

            // スライドを廃止し、背景要素は最初から定位置に「ばっ！」と表示する
            for (int i = 0; i < bgElements.Count; i++) bgElements[i].anchoredPosition = bgTargetPos[i];

            // 画面揺れ
            StartCoroutine(ScreenShakeRoutine(0.2f, 15f));

            while (t < impactDuration)
            {
                float progress = t / impactDuration;
                float easeIn = Mathf.Pow(progress, 3f);

                // 文字が叩きつけられる演出だけは残す（勢いが出るため）
                mainRt.localScale = Vector3.LerpUnclamped(new Vector3(5f, 5f, 1f), Vector3.one, easeIn);
                mainText.color = new Color32(255, 255, 255, (byte)(255 * progress));

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
