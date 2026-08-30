using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // ロン演出の起動と、演出が終わったあとの盤面の戻し。GameUIPhaseController から分離（partial）。
    // 清算パネルの中身の組み立ては GameUIPhaseController.Settlement.cs。
    public partial class GameUIPhaseController
    {

        private bool _hasExecutedRonAnimation = false;

        public void ExecuteRonAction()
        {
            if (_hasExecutedRonAnimation) return;
            _hasExecutedRonAnimation = true;

            if (uiManager.RonWaitPanel != null) uiManager.RonWaitPanel.SetActive(false);

            bool isLocalWin = true;
            List<int> winningHand = new List<int>(BoardStateManager.Instance.CurrentHandTiles);
            var liq = BoardStateManager.Instance.LastLiquidationData;
            
            List<string> actualYaku = new List<string>();
            string actualFormula = "0飜";
            string actualRank = "満貫";
            
            if (liq != null)
            {
                if (liq.yaku != null) actualYaku = new List<string>(liq.yaku);
                else actualYaku.Add("不明な役");
                
                actualFormula = $"{liq.han}飜";
                
                actualRank = ResolveRankName(liq.multiplier);

                // 自分の和了なので、自己ベスト打点として通算戦績に記録する
                KillingMahjong.Core.PlayerStatsManager.RecordScore(liq.winner_gain);
            }

            int ronTile = BoardStateManager.Instance.LastDiscardedTileId >= 0
                ? BoardStateManager.Instance.LastDiscardedTileId
                : (winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0);

            // **ronTile を決めたあとに並べ替えること。** 上のフォールバックは
            // 「最後に引いた牌」を取る前提なので、先に並べ替えると別の牌になる。
            SortHandForRonAnimation(winningHand);

            StartCoroutine(PlayRonWithPreDialogue(isLocalWin, winningHand, ronTile, actualYaku, actualFormula, actualRank));
        }

        /// <summary>
        /// ロン演出に出す手牌を並べ替える。**渡す複製だけを並べ替え、盤面の実体には触らない。**
        ///
        /// 演出は受け取った配列をそのままの順で並べる（RonAnimationUI はソートしない）。
        /// 盤面側の並べ替えは OnScoreSettlementComplete で行うが、それが走るのは
        /// 演出が終わったあとなので、ここで揃えないと演出の中だけツモ順のまま出る。
        /// 自分の和了は CurrentHandTiles がたまたま整列しているだけなので、
        /// 両方の経路で明示的に揃えておく。
        /// </summary>
        private static void SortHandForRonAnimation(List<int> hand)
        {
            if (hand == null || hand.Count == 0) return;
            if (BoardStateManager.Instance == null) return;
            BoardStateManager.Instance.SortTileIds(hand);
        }

        public void HandleAgari(bool isLocalWin)
        {
            if (!isLocalWin)
            {
                Debug.Log("[GameUIPhaseController] 相手のロン成立。相手のロン演出を開始します。");

                List<int> winningHand = new List<int>(BoardStateManager.Instance.CurrentEnemyHandTiles);
                var liq = BoardStateManager.Instance.LastLiquidationData;
                
                List<string> actualYaku = new List<string>();
                string actualFormula = "0飜";
                string actualRank = "満貫";
                
                if (liq != null)
                {
                    if (liq.yaku != null) actualYaku = new List<string>(liq.yaku);
                    else actualYaku.Add("不明な役");
                    
                    actualFormula = $"{liq.han}飜";
                    
                    actualRank = ResolveRankName(liq.multiplier);
                }
                
                int ronTile = BoardStateManager.Instance.LastDiscardedTileId >= 0
                    ? BoardStateManager.Instance.LastDiscardedTileId
                    : (winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0);

                // 相手の手牌はツモ順のまま届く。ここで揃えないと演出中だけバラバラに見える
                SortHandForRonAnimation(winningHand);

                StartCoroutine(PlayRonWithPreDialogue(isLocalWin, winningHand, ronTile, actualYaku, actualFormula, actualRank));
            }
        }


        private IEnumerator PlayRonWithPreDialogue(bool isLocalWin, List<int> winningHand, int ronTile, List<string> yaku, string formula, string rank)
        {
            // ロンの一撃を予感させる合図。自分のロン（ExecuteRonAction）も
            // 相手のロン（HandleAgari）もここを通るので、1箇所で両方に効く。
            // **白フラッシュは止めた（2026-08-20 の演出削減バッチ1）。**
            // 同じフレームにカットインの黒幕(α0.7)が生成されるため、白は黒の上で合成されて
            // 灰色の一拍にしかなっておらず、「盤面が白く飛ぶ」体験になっていなかった。
            // フェイズ切替の ScreenFlash（このファイルの上方）はそのまま残す。
            // Effects.ScreenFlash.Play();

            // サーバーの役名は「役名＋強化回数」の連結で、ドラは1枚につき1要素で届く。
            // 表示にもリアクション判定にも、まとめた内訳の方を使う（YakuNameUtil のコメント参照）。
            var yakuSummary = KillingMahjong.Common.YakuNameUtil.Summarize(yaku);

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.ClearReactions();

                bool isYakuman = rank == "役満" || rank == "ダブル役満";

                // **以前は `y.Contains("ドラ") && y.Contains("3") || y.Contains("4") || ...` だった。**
                // `&&` が `||` より強いので「ドラを含み、かつ3を含む」または「4を含む」または「5を含む」
                // という判定になっており、しかもドラは `ドラ3` という形では届かないので、
                // **ドラ爆では一度も成立せず、役を4回か5回強化しただけの局で成立していた**
                // （2026-08-27 の調査）。枚数を数える形に直した。
                bool isDoraBaku = KillingMahjong.Common.YakuNameUtil.CountDora(yakuSummary) >= 3;

                bool isCheap = formula == "1飜" || formula == "2飜";

                ReactionController.Instance.HandleAgari(isLocalWin, isYakuman, isDoraBaku, isCheap);
                ReactionController.Instance.SetPlayerLostLastRound(!isLocalWin);
            }

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);

            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (uiManager.RonAnimationUI != null)
            {
                uiManager.RonAnimationUI.gameObject.SetActive(true);

                var liq = BoardStateManager.Instance.LastLiquidationData;
                int score = liq != null ? liq.winner_gain : 0;

                // **血はどちらもサーバー由来。掛け算も引き算もしない。**
                //   減った後 … liquidation の winner_health / loser_health を
                //              RoundLifecycleMessageHandler が既に反映済み
                //   減る前   … 反映する直前に控えたもの
                //
                // 以前は「後 − 獲得」「後 ＋ 損失」で前を逆算していたが、
                // 強襲のように獲得と損失が非対称になる仕様が入ると式が崩れる。
                var board = Managers.BoardStateManager.Instance;
                int newLocalHp = board.LocalPlayerHp;
                int newEnemyHp = board.EnemyPlayerHp;
                int prevLocalHp = board.HpBeforeLiquidationLocal;
                int prevEnemyHp = board.HpBeforeLiquidationEnemy;

                uiManager.RonAnimationUI.PlayRonSequence(winningHand, ronTile,
                    KillingMahjong.Common.YakuNameUtil.ToDisplayList(yakuSummary),
                    formula, rank, score, isLocalWin,
                    uiManager.PlayerInfoUI, uiManager.EnemyInfoUI, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp,
                    () => OnRonAnimationComplete(isLocalWin),
                    BuildScoreFormula(liq),
                    BuildSettlementInfo(liq, isLocalWin, yakuSummary, rank,
                        prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp));
            }
            yield break;
        }

        private void OnRonAnimationComplete(bool isLocalWin)
        {
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.HandleGameEnd(isLocalWin);
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

            // HPのアニメーションはRonAnimationUIで完了しているため、
            // そのまま結果エフェクトを表示して次局へ進む
            OnScoreSettlementComplete(isLocalWin);
        }

        private void OnScoreSettlementComplete(bool isLocalWin)
        {
            // 決着したので場の血は勝者へ移った。表示を空にする（流局のときはここを通らない）
            if (uiManager.BetPotUI != null) uiManager.BetPotUI.Clear();

            // 賭け金がゲージへ吸い込まれ、そのあとゲージが伸びる。
            // 勝敗の判定はサーバーの担当で、ここは表示のために積むだけ。
            // サーバーが「獲得で勝利」へ移行するまでは、この表示だけが先行する。
            // **`winner_gain > 0` を条件にしてはいけない。** 強襲を撃った局は獲得が 0 に潰れる
            // （得るはずだった額が相手への追加ダメージへ回る）ので、その局だけ吸い込みが走らず、
            // **場に出ている賭け金の表示が 0 に戻らないまま次の局へ積み増されていた**
            // （2026-08-27 の調査）。すぐ上の `BetPotUI.Clear()` は無条件に走るため、
            // 同じ決着で2つの賭け金表示が食い違う形になっていた。
            //
            // `AbsorbStakesIntoGauge` は gain が 0 でも正しく動く。賭け金を吸って表示を空にし、
            // ゲージには 0 を足す（＝伸びない）。判定はサーバーの担当で、ここは表示だけ。
            var liq = BoardStateManager.Instance.LastLiquidationData;
            if (liq != null && uiManager.ScoreGauge != null && !uiManager.IsTutorialMode)
            {
                uiManager.ScoreGauge.AbsorbStakesIntoGauge(isLocalWin, liq.winner_gain);
            }

            // 相手の手牌を並べ直して公開する。
            // これは流局（HandleDraw）にしか無く、ロンで決着したときは打牌フェイズのまま
            // ＝ツモ順でバラバラ・大半が伏せたままの状態が残っていた（要望9）。
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentHandTiles);
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentEnemyHandTiles);

            if (uiManager.HandUI != null) uiManager.HandUI.SortHandSlots();
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.SortHandSlots();
                uiManager.EnemyHandUI.RevealAllHands(uiManager.TileResourceManager);
            }

            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.gameObject.SetActive(true);
                uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
            }
            if (uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.SetPanelVisible(true);
                uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
            }

            if (isLocalWin)
            {
                if (uiManager.VictoryEffectPrefab != null && uiManager.PlayerInfoUI != null) 
                    Instantiate(uiManager.VictoryEffectPrefab, uiManager.PlayerInfoUI.transform.position, Quaternion.identity);
                if (uiManager.DamageEffectPrefab != null && uiManager.EnemyInfoUI != null) 
                    Instantiate(uiManager.DamageEffectPrefab, uiManager.EnemyInfoUI.transform.position, Quaternion.identity);
            }
            else
            {
                if (uiManager.VictoryEffectPrefab != null && uiManager.EnemyInfoUI != null) 
                    Instantiate(uiManager.VictoryEffectPrefab, uiManager.EnemyInfoUI.transform.position, Quaternion.identity);
                if (uiManager.DamageEffectPrefab != null && uiManager.PlayerInfoUI != null) 
                    Instantiate(uiManager.DamageEffectPrefab, uiManager.PlayerInfoUI.transform.position, Quaternion.identity);
            }

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.ShowNextRoundButton(() => {
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyCheck(true);

                    if (uiManager.IsGameOver)
                    {
                        uiManager.ShowGameResult();
                    }
                    else
                    {
                        SendNextRoundAction();
                    }
                });
            }
            else
            {
                if (uiManager.IsGameOver)
                {
                    uiManager.ShowGameResult();
                }
                else
                {
                    SendNextRoundAction();
                }
            }
        }
    }
}
