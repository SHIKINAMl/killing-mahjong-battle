using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    // 決着（勝敗判定・結果画面）まわり。GameUIManager から分離（partial）。
    // クラス・namespace・[SerializeField] は変えていないのでシーン参照には影響しない。
    public partial class GameUIManager
    {
        // --- 戦況グラフ用HP履歴 ---
        private List<int> playerHpHistory = new List<int>();
        private List<int> enemyHpHistory = new List<int>();

        public void RecordHpHistory(int localHp, int enemyHp)
        {
            // 同じHPが連続する場合はスキップする（変化があった時のみ記録）
            if (playerHpHistory.Count > 0 && enemyHpHistory.Count > 0)
            {
                if (playerHpHistory[playerHpHistory.Count - 1] == localHp &&
                    enemyHpHistory[enemyHpHistory.Count - 1] == enemyHp)
                {
                    return;
                }
            }
            playerHpHistory.Add(localHp);
            enemyHpHistory.Add(enemyHp);
        }

        /// <summary>直近の game_end。勝敗を決めるときに決着理由を見るため保持する。</summary>
        private GameEndInfo lastGameEndInfo;

        private void HandleGameEnded(GameEndInfo info)
        {
            IsGameOver = true;
            lastGameEndInfo = info;

            var board = BoardStateManager.Instance;

            // final_scores に自分（相手）の ID が無かった場合、スコアは 0 のまま届く。
            // **その 0 をそのまま勝敗に使うと、勝った側にも敗北画面が出る。**
            // 最後に status で届いた HP の方がまだ実態に近いので、そちらで補う。
            int localScore = info.LocalScoreFound ? info.LocalScore : (board != null ? board.LocalPlayerHp : 0);
            int enemyScore = info.EnemyScoreFound ? info.EnemyScore : (board != null ? board.EnemyPlayerHp : 0);

            LocalFinalScore = localScore;
            EnemyFinalScore = enemyScore;

            // 決着時の最終HPも記録しておく
            RecordHpHistory(localScore, enemyScore);
        }

        /// <summary>
        /// 自分が勝ったのかを決める。**サーバーは勝者 ID を送ってこない**ので、
        /// 決着理由（`victory_method`）と手元の値から組み立てるしかない（2026-08-23）。
        ///
        /// 以前は `LocalFinalScore > 0 && EnemyFinalScore <= 0` だった。
        /// **HP は 0 でクランプされ、しかも決着の大半は「最低賭け金を払えない」（HP は 0 より上）**
        /// なので、この条件はほぼ成立せず、**勝者の画面にも「敗北」が出ていた**。
        /// 起票済みの A-9（`winner_id` を送ってほしい）が入れば、ここは読み替えるだけで済む。
        /// </summary>
        private bool DetermineLocalWin()
        {
            var board = BoardStateManager.Instance;
            string method = lastGameEndInfo != null ? lastGameEndInfo.VictoryMethod : "";

            // 累計30000到達の決着だけは、HP を見ても勝者が分からない（勝者の HP が低いこともある）。
            if (method == "cumulative_earned_points" && board != null)
            {
                bool localReached = board.LocalCumulativeEarnedPoints >= BoardStateManager.CumulativeVictoryPoints;
                bool enemyReached = board.EnemyCumulativeEarnedPoints >= BoardStateManager.CumulativeVictoryPoints;

                if (localReached != enemyReached) return localReached;

                // 最後の status が届く前に game_end が来ると、どちらも未到達に見える。
                // その場合でも直前の局で稼いだ側の方が多いはずなので、大小で決める
                if (board.LocalCumulativeEarnedPoints != board.EnemyCumulativeEarnedPoints)
                {
                    return board.LocalCumulativeEarnedPoints > board.EnemyCumulativeEarnedPoints;
                }
            }

            // hp_zero / max_rounds / unknown（最低賭け金を払えない）は、**残った血が多い方が勝ち**。
            // unknown が一番多い経路であることに注意（サーバーは払えなくなった側を負けにしている）
            if (LocalFinalScore != EnemyFinalScore) return LocalFinalScore > EnemyFinalScore;

            // HP が同点。累計獲得で割る
            if (board != null && board.LocalCumulativeEarnedPoints != board.EnemyCumulativeEarnedPoints)
            {
                return board.LocalCumulativeEarnedPoints > board.EnemyCumulativeEarnedPoints;
            }

            // 完全に同点。**引き分けの結果画面が無い**ので敗北として出す（実戦ではまず起きない）
            Debug.LogWarning($"[GameUIManager] 勝敗を決められませんでした（HP {LocalFinalScore} 対 {EnemyFinalScore} / 決着理由 '{method}'）。敗北として表示します");
            return false;
        }

        private bool gameResultShown = false;

        public void ShowGameResult()
        {
            // 呼び出し経路が2つ（ダイアログのOKと即時分岐）あるため、二重表示を防ぐ
            if (gameResultShown) return;
            gameResultShown = true;

            StartCoroutine(ShowGameResultRoutine());
        }

        private System.Collections.IEnumerator ShowGameResultRoutine()
        {
            // 決着したら瀕死ビネットを消す（結果画面より手前に描画されるため）
            if (playerInfoUI != null) playerInfoUI.StopHeartbeatEffect();

            // 戦況グラフの表示（履歴が2件以上あれば表示）
            if (matchMomentumUI != null && playerHpHistory.Count >= 2)
            {
                matchMomentumUI.ShowMomentum(playerHpHistory, enemyHpHistory);
                // グラフ演出が終わるまで待つ（表示時間2秒 + 前後フェード1秒 = 約3秒。MatchMomentumUI側の設定に合わせる）
                yield return new WaitForSeconds(3.0f);
            }

            bool isWin = DetermineLocalWin();
            Debug.Log($"[GameUIManager] 決着: {(isWin ? "勝ち" : "負け")} / HP 自分 {LocalFinalScore} 相手 {EnemyFinalScore} / 理由 '{(lastGameEndInfo != null ? lastGameEndInfo.VictoryMethod : "")}'");

            if (victoryUI != null)
            {
                victoryUI.PlayAnimation(
                    isWin ? VictoryType.NormalVictory : VictoryType.NormalDefeat,
                    LocalFinalScore, EnemyFinalScore);
            }
        }
    }
}
