using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

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
        [SerializeField] private float lineExpandDuration = 0.3f;
        [SerializeField] private float checkerFadeDuration = 1.0f;
        [SerializeField] private float hpDeductionDuration = 1.5f;

        private void Start()
        {
            ResetVisuals();
        }

        private void ResetVisuals()
        {
            if (fullScreenCheckerImage != null && checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_Progress", 0f);
                fullScreenCheckerImage.material = checkerMaterial;
                fullScreenCheckerImage.gameObject.SetActive(false);
            }
            
            if (horizontalLineRt != null)
            {
                horizontalLineRt.localScale = new Vector3(0, 0, 1); // target (1,1,1)
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
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 1, t / lineInDuration), 2f, 1f);
                t += Time.deltaTime;
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(1, 2f, 1f);

            if (centerText != null)
            {
                centerText.text = "対局開始";
                centerText.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(textWaitDuration);

            Debug.Log("PhaseTransition: Step 2 - Line Expand and Checker Fade In");
            // === 2. 線が上下に広がりつつ、市松模様が画面を埋める ===
            
            // Expand line vertically using localScale
            t = 0;
            while (t < lineExpandDuration)
            {
                float normalizedTime = t / lineExpandDuration;
                float easedT = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
                horizontalLineRt.localScale = new Vector3(1, Mathf.Lerp(2f, 100f, easedT), 1f);
                t += Time.deltaTime;
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(1, 100f, 1f);

            // Enable fullscreen checker
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);

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
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 1, t / lineInDuration), 2f, 1f);
                t += Time.deltaTime;
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(1, 2f, 1f);

            if (centerText != null)
            {
                centerText.text = "先攻"; // 仮判定
                centerText.gameObject.SetActive(true);
            }
            
            yield return new WaitForSeconds(textWaitDuration);
            
            Debug.Log("PhaseTransition: Step 7 - Finish");
            // === 7. 線アウト + 完了 ===
            if (centerText != null) centerText.gameObject.SetActive(false);
            t = 0;
            while (t < lineInDuration)
            {
                horizontalLineRt.localScale = new Vector3(Mathf.Lerp(1, 0, t / lineInDuration), 2f, 1f);
                t += Time.deltaTime;
                yield return null;
            }
            horizontalLineRt.gameObject.SetActive(false);

            Debug.Log("PhaseTransition: Complete Callback invoked");
            onComplete?.Invoke();
        }
    }
}
