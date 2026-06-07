using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
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
            dealWaitTimer = 0f;
            if (loadingText != null)
            {
                loadingText.text = "山牌構築中... 0.0s";
                loadingText.gameObject.SetActive(true);
            }
        }

        private void HandleDealingCompleted()
        {
            isWaitingForDeal = false;
            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (isWaitingForDeal && loadingText != null)
            {
                dealWaitTimer += Time.deltaTime;
                loadingText.text = $"山牌構築中... {dealWaitTimer:F1}s";
            }
        }

        private void ResetVisuals()
        {
            if (fullScreenCheckerImage != null && checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_Progress", 0f);
                
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

                fullScreenCheckerImage.material = checkerMaterial;
                fullScreenCheckerImage.gameObject.SetActive(false);
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

                if (enemyBetObj != null) enemyBetObj.text = "Enemy Bet: " + enemyBetAmount;
                if (playerBetObj != null) playerBetObj.text = "Your Bet: " + playerBetAmount;

                t = 0;
                while (t < hpDeductionDuration)
                {
                    int currentPlayerAnimHp = Mathf.RoundToInt(Mathf.Lerp(dummyInitialPlayerHp, targetPlayerHp, t / hpDeductionDuration));
                    int currentEnemyAnimHp = Mathf.RoundToInt(Mathf.Lerp(dummyInitialEnemyHp, targetEnemyHp, t / hpDeductionDuration));
                    if (enemyHpObj != null) enemyHpObj.text = "Enemy HP: " + currentEnemyAnimHp;
                    if (playerHpObj != null) playerHpObj.text = "Your HP: " + currentPlayerAnimHp;
                    t += Time.deltaTime;
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

            Debug.Log("PhaseTransition: Step 5 - Checker Fade Out");
            // === 5. テキスト消滅、市松模様フェードアウト ===
            if (centerText != null) centerText.gameObject.SetActive(false);

            t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (t / checkerFadeDuration));
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);

            Debug.Log("PhaseTransition: Step 6 - Turn Indicator");

            // === 6. 線が入り「先行/後攻」 ===
            horizontalLineRt.gameObject.SetActive(true);
            horizontalLineRt.localScale = new Vector3(0, 2f, 1f); 
            t = 0;
            while (t < lineInDuration)
            {
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 10f, t / lineInDuration), 2f, 1f);
                t += Time.deltaTime;
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
            t = 0;
            while (t < lineInDuration)
            {
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(10f, 0, t / lineInDuration), 2f, 1f);
                t += Time.deltaTime;
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
    }
}
