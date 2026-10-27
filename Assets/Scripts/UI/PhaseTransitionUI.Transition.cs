using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        /// <summary>
        /// 開戦の着弾を出す時点。市松模様のフェードアウトのうち、どれだけ進んだところで出すか。
        /// 0.55 は「盤面が見えてきたが、まだ晴れ切ってはいない」あたり（fade は 1.0 秒）。
        /// </summary>
        private const float BattleStartImpactAtFadeRatio = 0.55f;

        /// <summary>賭け金の行・VS・残りの行をずらす間隔。</summary>
        private const float HpRowStagger = 1f / 6f;

        private static string BetLine(string label, int amount) => "<size=60%>" + label + "</size> <color=#E0303C>" + amount + "</color>";

        private static string RestLine(int hp) => "<size=60%>残り</size> " + hp;

        private IEnumerator SequenceRoutine(string roundName, KillingMahjong.EngineData.BettingCompletedInfo bet, Action onMidpoint, Action onComplete, bool? displayIsLocalTurn)
        {
            ResetVisuals();

            // 賭け確定の既存シーケンスは維持し、暗転解除の瞬間だけ指揮者へ追加する。
            // isDarkened 中は盤面が戻らないため、この追加演出も予約しない。
            if (!isDarkened) Effects.PhaseTransitionDirector.PrepareBattleStartRelease();

            // トランジション（対局開始演出）が開始された瞬間に敵のHPなどのUIを非表示にする
            // if (targetPlayerInfoUI != null)
            // {
            //     targetPlayerInfoUI.gameObject.SetActive(false);
            // }

            if (!isDarkened)
            {
                Debug.Log("PhaseTransition: Step 1 - Line In");
                // === 1. 一本線が入る + 「対局開始」 ===
                PlayTransitionStinger("br_band_open");
                yield return OpenBand();
                yield return RevealBandText("対局開始");
                yield return new WaitForSeconds(textWaitDuration);
                float t;

                Debug.Log("PhaseTransition: Step 2 - Line Expand and Checker Fade In");
                // === 2. 線を中心に、市松模様が上下に広がり画面を埋める ===

                PlayTransitionStinger("br_fall");

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
                    if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", PresentationTiming.Sample(t) / checkerFadeDuration);
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
            // === 3. 「対局開始」が消えて「第N局目」になる ===
            // パッと差し替えず、薄くして消してから1字ずつ出す（2026-10-25 のユーザー指示
            // 「一局目などの遷移の演出を作っていたのですが、それが適応されていなかった」）
            if (centerText != null && !(centerText.gameObject.activeSelf && centerText.text == roundName))
            {
                yield return HideBandText();
                yield return RevealBandText(roundName);
            }
            
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

                // **英語の仮の文言をやめて日本語にする（2026-10-25）。** 見出しは小さく、数字を大きく。
                // 「Enemy HP: 8480」は欄の幅 200 に収まらず、数字だけが次の行へ落ちていた。
                if (enemyBetObj != null) enemyBetObj.text = BetLine("相手の賭け金", bet.EnemyBet);
                if (playerBetObj != null) playerBetObj.text = BetLine("あなたの賭け金", bet.LocalBet);
                if (enemyHpObj != null) enemyHpObj.text = RestLine(startEnemyHp);
                if (playerHpObj != null) playerHpObj.text = RestLine(startPlayerHp);

                // 行ごとに時間差で出す。賭け金の行 → VS → 残りの行の順に目線を下ろす。
                // 各行は動かさず、その場でぱっと出す（2026-10-26 のユーザー指示。以前は下から浮かせていた）
                var vsTransform = hpBetContainer.transform.Find("VS");
                var vsText = vsTransform != null ? vsTransform.GetComponent<TMPro.TextMeshProUGUI>() : null;
                var rows = new[] { enemyBetObj, playerBetObj, vsText, enemyHpObj, playerHpObj };
                foreach (var row in rows) if (row != null) row.alpha = 0f;
                yield return null; // 有効にした次のフレームで形を取る
                for (int i = 0; i < rows.Length; i++)
                {
                    if (rows[i] == null) continue;
                    rows[i].alpha = 1f;
                    if (i == 1 || i == 2) yield return new WaitForSeconds(HpRowStagger);
                }

                // 残りの数字は、減速しながら目標まで数える（ローリングカウンター）。
                // 一定の速さだと最後の桁が止まる瞬間が分からない
                float tHp = 0;
                while (tHp < hpDeductionDuration)
                {
                    float e = EaseOutCubic(StepTextTime(tHp) / hpDeductionDuration);
                    if (enemyHpObj != null) enemyHpObj.text = RestLine(Mathf.RoundToInt(Mathf.Lerp(startEnemyHp, targetEnemyHp, e)));
                    if (playerHpObj != null) playerHpObj.text = RestLine(Mathf.RoundToInt(Mathf.Lerp(startPlayerHp, targetPlayerHp, e)));
                    tHp += Time.deltaTime;
                    yield return null;
                }
                if (enemyHpObj != null) enemyHpObj.text = RestLine(targetEnemyHp);
                if (playerHpObj != null) playerHpObj.text = RestLine(targetPlayerHp);

                yield return new WaitForSeconds(1.0f);
                // パッと消さず、薄くして消す
                for (float tf = 0f; tf < BandTextFadeOut; tf += Time.deltaTime)
                {
                    foreach (var row in rows) if (row != null) row.alpha = 1f - StepTextTime(tf) / BandTextFadeOut;
                    yield return null;
                }
                hpBetContainer.SetActive(false);
                foreach (var row in rows) if (row != null) row.alpha = 1f;
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            Debug.Log("PhaseTransition: Step 5 - Checker Fade Out (Skipped if Darkened)");
            // === 5. テキスト消滅、市松模様フェードアウト ===
            yield return HideBandText();

            if (!isDarkened)
            {
                // 既存の市松模様を明ける直前に、タメと開戦の着弾を足す。
                // onMidpoint と onComplete の進行処理には触れない。
                yield return Effects.PhaseTransitionDirector.PlayBattleStartRelease();

                PlayTransitionStinger("br_riser");
                float tFade = 0;
                // **揺れは市松模様が半分以上晴れてから。**
                // 晴れる前に揺らすと画面が真っ暗で、揺れがまったく見えない（2026-09-20 のユーザー指摘）
                bool impactDone = false;
                while (tFade < checkerFadeDuration)
                {
                    if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (tFade / checkerFadeDuration));
                    if (!impactDone && tFade >= checkerFadeDuration * BattleStartImpactAtFadeRatio)
                    {
                        impactDone = true;
                        Effects.PhaseTransitionDirector.PlayBattleStartImpact();
                    }
                    tFade += Time.deltaTime;
                    yield return null;
                }
                if (!impactDone) Effects.PhaseTransitionDirector.PlayBattleStartImpact();
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
                if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);
            }

            Debug.Log("PhaseTransition: Step 6 - Turn Indicator");

            // === 6. 線が入り「先行/後攻」 ===
            PlayTransitionStinger("br_band_open");
            yield return OpenBand();

            bool isFirst = displayIsLocalTurn ?? KillingMahjong.Managers.BoardStateManager.Instance.IsLocalTurn;
            yield return RevealBandText(isFirst ? "先攻" : "後攻");

            yield return new WaitForSeconds(textWaitDuration);

            Debug.Log("PhaseTransition: Step 7 - Finish");
            // === 7. 線アウト + 完了 ===
            yield return HideBandText();
            PlayTransitionStinger("br_band_close");
            yield return CloseBand();

            Debug.Log("PhaseTransition: Complete Callback invoked");
            onComplete?.Invoke();
        }
    }
}
