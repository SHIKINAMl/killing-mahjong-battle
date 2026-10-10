using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    // 配牌開始の入口・リセット・反映条件・暗転解除をここへ集約する。
    public partial class GameUIPhaseController
    {
        private readonly RoundStartCoordinator roundStart = new RoundStartCoordinator();
        private TransitionLockSet.Lease roundStartTransition;

        private void CancelRoundStartTransition()
        {
            roundStartTransition?.Dispose();
            roundStartTransition = null;
        }

        public bool ShouldDeferRoundStartBoardUpdate(RoundStartCoordinator.BoardUpdateKind kind, bool isBusyWithTransition)
        {
            return roundStart.ShouldDeferBoardUpdate(kind, isBusyWithTransition);
        }

        public void HandleDealingStarted()
        {
            // 同じ配牌フェイズの再通知で、反映済みの局をもう一度待たせない。
            if (uiManager.CurrentPhaseStatus == RoundStatus.Dealing) return;
            BeginRoundStart();
        }

        private void BeginRoundStart()
        {
            if (roundStart.BeginRound())
            {
                ResetPhasePresentation(); // 前の局の入場・確定コールバックを失効させる。
                ResetBettingTransition();
                CancelRoundStartTransition();
                roundEnd.Reset();
                ronTransition?.Dispose();
                ronTransition = null;
                uiManager.HandSelectionController?.CancelPendingConfirmation();
                uiManager.PhaseTransitionUI?.PrepareRoundStartWait();
            }
        }

        private void StartRoundStartTransition(string text, bool afterDraw)
        {
            var transition = uiManager.PhaseTransitionUI;
            TransitionLockSet.Lease ownedLock = null;
            roundStart.TryStartTransition(
                (onCovered, onReady) =>
                {
                    if (transition == null)
                    {
                        onCovered();
                        onReady();
                    }
                    else if (afterDraw)
                    {
                        ownedLock = roundStartTransition = uiManager.BeginTransition("round-start");
                        transition.PlayDrawTransition(onCovered, onReady);
                    }
                    else
                    {
                        ownedLock = roundStartTransition = uiManager.BeginTransition("round-start");
                        transition.PlayRoundStartDarken(text, onCovered, onReady);
                    }
                },
                () =>
                {
                    // ClearAllTiles が牌の返却・描画キャッシュ無効化・盤面データ消去を行う。
                    uiManager.ClearAllTiles();
                    if (afterDraw)
                    {
                        SetMatchUIVisibility(false);
                        if (Managers.ReactionController.Instance != null)
                            Managers.ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
                    }
                },
                () =>
                {
                    ShowDealingWaitUI();
                    ownedLock?.Dispose();
                    if (roundStartTransition == ownedLock) roundStartTransition = null;
                });
        }

        public void HandleDealingCompleted()
        {
            roundStart.CompleteDealing(() => uiManager.PhaseTransitionUI?.PlayRoundStartFadeOut());
        }
    }
}
