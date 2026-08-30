using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator SequenceRoutine(string roundName, KillingMahjong.EngineData.BettingCompletedInfo bet, Action onMidpoint, Action onComplete)
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

                // 賭けた額のぶん血が減る様子を見せる。
                //
                // **数字はクライアントで計算しない。** 引くのはサーバーで、
                // 引いたあとの値が `bet_completed` の `bets[].health` で届いている
                // （`BettingMessageHandler` が `BettingCompletedInfo` に詰めて渡してくる）。
                // 届かなかったときは Before と同じ値が入っていて、この演出は動かない。
                int startPlayerHp = bet.LocalHpBefore;
                int startEnemyHp = bet.EnemyHpBefore;
                int targetPlayerHp = bet.LocalHpAfter;
                int targetEnemyHp = bet.EnemyHpAfter;

                if (enemyBetObj != null) enemyBetObj.text = "Enemy Bet: <color=red>" + bet.EnemyBet + "</color>";
                if (playerBetObj != null) playerBetObj.text = "Your Bet: <color=red>" + bet.LocalBet + "</color>";

                float tHp = 0;
                while (tHp < hpDeductionDuration)
                {
                    int currentPlayerAnimHp = Mathf.RoundToInt(Mathf.Lerp(startPlayerHp, targetPlayerHp, tHp / hpDeductionDuration));
                    int currentEnemyAnimHp = Mathf.RoundToInt(Mathf.Lerp(startEnemyHp, targetEnemyHp, tHp / hpDeductionDuration));
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
    }
}
