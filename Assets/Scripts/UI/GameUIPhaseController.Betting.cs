using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 賭け金フェイズ。UIの開始・確定の受け・確定後の演出。GameUIPhaseController から分離（partial）。
    public partial class GameUIPhaseController
    {

        private void StartBettingPhase(int currentHealth)
        {
            if (uiManager.BettingUI != null)
            {
                int svCount = Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount;
                uiManager.BettingUI.ShowBettingPhase(20000, currentHealth, svCount, OnBetConfirmed);
                if (ReactionController.Instance != null)
                {
                    ReactionController.Instance.StartBetPhaseTimer();
                }
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.StartTurnTimer(10f); // 10秒
            }
        }

        private void OnBetConfirmed(int betAmount)
        {
            uiManager.BettingUI.HideBettingPhase();
            
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.StopTurnTimer();
                if (uiManager.PlayerInfoUI.gameObject.activeInHierarchy)
                {
                    uiManager.PlayerInfoUI.StartCoroutine(
                        uiManager.PlayerInfoUI.ResetZoomRoutine(BetZoomOutDuration));
                }
            }

            // スマホが縮んでから「準備完了」を出す。相手が賭けるまではここで待つことになる
            StartCoroutine(ShowReadyBadgesAfterZoomOut());

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(betAmount, Managers.BoardStateManager.Instance.LocalPlayerHp, true);
            }
            uiManager.SendActionToServer("bet", new ActionPayload { bet_amount = betAmount, amount = betAmount });
        }

        public void OnBettingCompleteFromServer(KillingMahjong.EngineData.BettingCompletedInfo info)
        {
            int playerBet = info.LocalBet;
            int enemyBet = info.EnemyBet;

            // 流局では決着せず次の局でも同額が賭けられるので、場の表示は積み増していく。
            // 場の血が動くのは決着したときだけなので、クリアはロン演出の完了時に行う。
            if (uiManager.BetPotUI != null) uiManager.BetPotUI.AddStakes(playerBet, enemyBet);
            // 賭けている額はゲージの下にも出す。決着でゲージへ吸い込まれる
            if (!uiManager.IsTutorialMode) uiManager.ScoreGauge.AddStakes(playerBet, enemyBet);

            string roundTitle = $"第{_currentRoundIndex}局目";
            if (_isCarryOverNextRound) 
            {
                roundTitle += "\n自動ベット";
            }

            // セリフの条件は「いま何を持っているか」なので、賭けたあとの血を使う
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(enemyBet, info.EnemyHpAfter, false);
                ReactionController.Instance.SetPlayerHp(info.LocalHpAfter);
                ReactionController.Instance.SetEnemyHp(info.EnemyHpAfter);
            }

            TriggerBettingAnimationPhase(roundTitle, info);
            _isCarryOverNextRound = false;
        }

        /// <summary>
        /// ベット確定の演出。**演出は `info` の「賭ける前」から「賭けたあと」へ数字を動かす。**
        /// 演出が後回し（`DeferUntilIdle`）になっても値がずれないよう、
        /// そのとき盤面を見に行くのではなく、届いた時点の値をそのまま持ち回す。
        /// </summary>
        public void TriggerBettingAnimationPhase(string roundString, KillingMahjong.EngineData.BettingCompletedInfo info)
        {
             // このメソッドは演出だけでなく進行の責務も持っている（onMidpoint で
             // UpdatePhaseStatus(Discard) を呼ぶ）。捨てると打牌フェイズへ進めず Betting で固まる。
             if (uiManager.IsBusyWithTransition)
             {
                 uiManager.DeferUntilIdle("bettingAnimation",
                     () => TriggerBettingAnimationPhase(roundString, info));
                 return;
             }

             if (uiManager.PhaseTransitionUI != null)
             {
                 uiManager.SetIsTransitioning(true);
                 
                 // スマホは消さないようにする（ユーザー要望）
                 // if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                 if (uiManager.PlayerInfoUI != null) 
                 {
                     uiManager.PlayerInfoUI.ResetZoomImmediate();
                     // uiManager.PlayerInfoUI.gameObject.SetActive(false);
                 }
                 if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                 if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(false);

                 uiManager.PhaseTransitionUI.PlayTransition(roundString, uiManager.PlayerInfoUI, info,
                    onMidpoint: () => {
                         uiManager.SetIsTransitioning(false);
                         
                         // UIのみクリア（BoardStateManagerのデータを消さないようにする）
                         if (uiManager.RiverUI != null) uiManager.RiverUI.Clear();
                         if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.Clear();
                         if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                         
                         // 牌を再構築する前にフェーズをDiscardへ進める
                         if (uiManager.CurrentPhaseStatus == RoundStatus.Betting)
                         {
                             UpdatePhaseStatus(RoundStatus.Discard);
                         }
                         
                         if (uiManager.VisualController != null) uiManager.VisualController.RebuildAllTilesFromState();
                         
                         if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.WallUI != null)
                         {
                             uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                         }
                         
                         SetMatchUIVisibility(true); 
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         
                         uiManager.SetIsTransitioning(true);
                         
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
                    },
                    onComplete: () => {
                         uiManager.SetIsTransitioning(false);
                         
                         // Catch any missed updates
                         if (uiManager.VisualController != null) uiManager.VisualController.RebuildAllTilesFromState();
                         
                         if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.WallUI != null)
                         {
                             uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateDiscardTurnIndicator(BoardStateManager.Instance.IsLocalTurn, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                         }
                         
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(true);
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    }
                 );
             }
        }
    }
}
