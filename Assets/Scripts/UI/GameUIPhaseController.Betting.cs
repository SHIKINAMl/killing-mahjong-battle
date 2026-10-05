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
            }

            // パネルが下へ戻ってから「準備完了」を出す。相手が賭けるまではここで待つことになる
            StartCoroutine(ShowReadyBadgesAfterBettingPanelSlideOut());

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(betAmount, Managers.BoardStateManager.Instance.LocalPlayerHp, true);
            }
            uiManager.SendActionToServer("bet", new ActionPayload { bet_amount = betAmount, amount = betAmount });
        }

        public void OnBettingCompleteFromServer(KillingMahjong.EngineData.BettingCompletedInfo info)
        {
            if (info == null || hasReceivedBettingResult) return;
            hasReceivedBettingResult = true;
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

            // BGMの濃さも同じ値から決める（2026-09-12）。
            // **ここは両方の体力が同時に確定する唯一の場所。** 片方ずつ更新される
            // 経路で呼ぶと、一瞬だけ嘘の段になる。
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.UpdateBgmIntensityFromHp(info.LocalHpAfter, info.EnemyHpAfter);
            }

            TriggerBettingAnimationPhase(roundTitle, info);
            _isCarryOverNextRound = false;
        }

        /// <summary>
        /// ベット確定の演出。**演出は `info` の「賭ける前」から「賭けたあと」へ数字を動かす。**
        /// 演出が後回し（`DeferUntilIdle`）になっても値がずれないよう、
        /// そのとき盤面を見に行くのではなく、届いた時点の値をそのまま持ち回す。
        /// </summary>
        private enum BettingStage { Waiting, Playing, Covered, Completed }
        private sealed class BettingTransition
        {
            public int Generation;
            public string Title;
            public BettingCompletedInfo Info;
            public BettingStage Stage;
            public TransitionLockSet.Lease Lock;
        }

        private BettingTransition activeBettingTransition;
        private bool hasReceivedBettingResult;

        private void ResetBettingTransition()
        {
            activeBettingTransition?.Lock?.Dispose();
            hasReceivedBettingResult = false;
            activeBettingTransition = null;
        }

        public void TriggerBettingAnimationPhase(string roundString, BettingCompletedInfo info)
        {
            if (info == null) return;
            if (activeBettingTransition != null && activeBettingTransition.Generation == PresentationGeneration) return;
            var transition = new BettingTransition
            {
                Generation = PresentationGeneration, Title = roundString,
                Info = new BettingCompletedInfo
                {
                    LocalBet = info.LocalBet, EnemyBet = info.EnemyBet,
                    LocalHpBefore = info.LocalHpBefore, EnemyHpBefore = info.EnemyHpBefore,
                    LocalHpAfter = info.LocalHpAfter, EnemyHpAfter = info.EnemyHpAfter,
                    HasServerHealth = info.HasServerHealth
                }
            };
            activeBettingTransition = transition;
            PlayBettingTransition(transition);
        }

        private bool IsCurrentBettingTransition(BettingTransition transition)
        {
            return activeBettingTransition == transition && transition.Generation == PresentationGeneration;
        }

        private void PlayBettingTransition(BettingTransition transition)
        {
            if (!IsCurrentBettingTransition(transition) || transition.Stage != BettingStage.Waiting) return;
            if (uiManager.IsBusyWithTransition)
            {
                uiManager.DeferUntilIdle("bettingAnimation", () => PlayBettingTransition(transition));
                return;
            }
            transition.Stage = BettingStage.Playing;
            transition.Lock = uiManager.BeginTransition("betting");
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(false);
            if (uiManager.PhaseTransitionUI != null)
                uiManager.PhaseTransitionUI.PlayTransition(transition.Title, uiManager.PlayerInfoUI, transition.Info,
                    () => ApplyBettingMidpoint(transition), () => CompleteBettingTransition(transition));
            else
            {
                ApplyBettingMidpoint(transition);
                CompleteBettingTransition(transition);
            }
        }

        private void ApplyBettingMidpoint(BettingTransition transition)
        {
            if (!IsCurrentBettingTransition(transition) || transition.Stage != BettingStage.Playing) return;
            transition.Stage = BettingStage.Covered;
            uiManager.RunCoveredBoardUpdate(() => {
                if (uiManager.RiverUI != null) uiManager.RiverUI.Clear();
                if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.Clear();
                if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                if (uiManager.CurrentPhaseStatus == RoundStatus.Betting) UpdatePhaseStatus(RoundStatus.Discard);
                RefreshBettingBoard(includeEnemyHand: true);
                SetMatchUIVisibility(true);
                RefreshPhaseView(uiManager.CurrentPhaseStatus);
                uiManager.PlayerInfoUI?.SetHP(BoardStateManager.Instance.LocalPlayerHp);
                uiManager.EnemyInfoUI?.SetHP(BoardStateManager.Instance.EnemyPlayerHp);
            });
        }

        private void CompleteBettingTransition(BettingTransition transition)
        {
            if (!IsCurrentBettingTransition(transition) || transition.Stage != BettingStage.Covered) return;
            transition.Stage = BettingStage.Completed;
            transition.Lock?.Dispose();
            RestoreBettingBoard(transition);
        }

        private void RestoreBettingBoard(BettingTransition transition)
        {
            if (!IsCurrentBettingTransition(transition)) return;
            if (uiManager.IsBusyWithTransition)
            {
                uiManager.DeferUntilIdle("bettingRestore", () => RestoreBettingBoard(transition));
                return;
            }
            // 演出中の状態同期を拾うため、完了時の再構築も残す。
            RefreshBettingBoard(includeEnemyHand: false);
            RefreshPhaseView(uiManager.CurrentPhaseStatus);
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(true);
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            uiManager.EnemyInfoUI?.SetPanelVisible(true);
        }

        private void RefreshBettingBoard(bool includeEnemyHand)
        {
            uiManager.VisualController?.RebuildAllTilesFromState();
            uiManager.HandUI?.UpdateLayout(uiManager.CurrentPhaseStatus);
            if (includeEnemyHand) uiManager.EnemyHandUI?.UpdateLayout(uiManager.CurrentPhaseStatus);
            if (uiManager.WallUI != null)
            {
                bool discard = uiManager.CurrentPhaseStatus == RoundStatus.Discard;
                uiManager.WallUI.UpdateContainerPosition(discard);
                uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, discard);
                uiManager.WallUI.UpdateDiscardTurnIndicator(BoardStateManager.Instance.IsLocalTurn, discard);
            }
        }
    }
}
