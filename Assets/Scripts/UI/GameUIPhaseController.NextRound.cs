using System;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    public partial class GameUIPhaseController
    {
        private readonly RoundEndCoordinator roundEnd = new RoundEndCoordinator();

        internal void CancelPhasePresentations()
        {
            StopAllCoroutines();
            uiManager?.PhaseTransitionUI?.CancelTransitions();
            uiManager?.RonAnimationUI?.StopAllCoroutines();
            ResetPhasePresentation();
            roundEnd.Reset();
            roundStart.Reset();
            CancelRoundStartTransition();
            ResetBettingTransition();
            ronTransition?.Dispose();
            ronTransition = null;
        }

        private void OnDisable() { CancelPhasePresentations(); }

        private void RunRoundEndWhenIdle(RoundEndCoordinator.Ticket ticket, Action action)
        {
            if (!roundEnd.IsCurrent(ticket)) return;
            if (uiManager.IsBusyWithTransition)
            {
                uiManager.DeferUntilIdle("roundEnd", () => RunRoundEndWhenIdle(ticket, action));
                return;
            }
            action();
        }

        // 共通の牌公開。待ち牌の公開や持ち越しは流局側、清算・効果表示はロン側に残す。
        private void RevealRoundEndHands()
        {
            var board = BoardStateManager.Instance;
            board.SortTileIds(board.CurrentHandTiles);
            board.SortTileIds(board.CurrentEnemyHandTiles);
            uiManager.HandUI?.SortHandSlots();
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.SortHandSlots();
                uiManager.EnemyHandUI.RevealAllHands(uiManager.TileResourceManager);
            }
        }

        private void ShowNextRoundWait(RoundEndCoordinator.Ticket ticket)
        {
            if (!roundEnd.FinishPresentation(ticket)) return;
            uiManager.PlayerInfoUI?.ShowReadyBox(true);
            uiManager.EnemyInfoUI?.ShowReadyBox(true);
            ApplyNextRoundReadyMarks();
            if (uiManager.DialogueUI != null)
                uiManager.DialogueUI.ShowNextRoundButton(() => RunRoundEndWhenIdle(ticket, () => ConfirmNextRound(ticket)));
            else
                RunRoundEndWhenIdle(ticket, () => ConfirmNextRound(ticket));
        }

        private void ApplyNextRoundReadyMarks()
        {
            uiManager.PlayerInfoUI?.SetReadyCheck(roundEnd.LocalReady);
            uiManager.EnemyInfoUI?.SetReadyCheck(roundEnd.EnemyReady);
        }

        private void ConfirmNextRound(RoundEndCoordinator.Ticket ticket)
        {
            // OKの遅いコールバックを現在のプレイ中フェイズへ持ち込まない。
            var phase = uiManager.CurrentPhaseStatus;
            if (phase != RoundStatus.Agari && phase != RoundStatus.Ron && phase != RoundStatus.Result
                && phase != RoundStatus.Draw) return;
            if (!roundEnd.Confirm(ticket)) return;
            ApplyNextRoundReadyMarks();
            if (uiManager.IsGameOver)
            {
                uiManager.ShowGameResult();
                return;
            }
            if (ticket.Result == RoundEndCoordinator.Outcome.Draw)
            {
                uiManager.WaitUI?.gameObject.SetActive(false);
                uiManager.EnemyWaitUI?.gameObject.SetActive(false);
                _pendingDrawTransition = true;
            }
            SendNextRoundAction();
        }
    }
}
