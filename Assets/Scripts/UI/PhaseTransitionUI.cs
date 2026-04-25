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
        public void PlayCenterTextAnim(string text, float duration = 1.5f)
        {
            StartCoroutine(CenterTextAnimRoutine(text, duration));
        }

        private IEnumerator CenterTextAnimRoutine(string text, float duration)
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
        }
    }
}
