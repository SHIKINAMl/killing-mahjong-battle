using System;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public partial class GameUIHandSelectionController
    {
        private int submissionVersion;
        private TransitionLockSet.Lease confirmationTransition;

        internal void CancelPendingConfirmation()
        {
            submissionVersion++;
            confirmationTransition?.Dispose();
            confirmationTransition = null;
        }

        private void OnDisable() { CancelPendingConfirmation(); }

        // ダイアログ表示時点の局・選択を保持し、古い確認ボタンから新しい選択を確定しない。
        private Action GuardSelectionConfirmation(Action action)
        {
            int version = submissionVersion;
            int generation = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            return () => {
                if (version != submissionVersion || uiManager.CurrentPhaseStatus != RoundStatus.HandSelection
                    || (uiManager.PhaseController != null && generation != uiManager.PhaseController.PresentationGeneration)) return;
                action();
            };
        }

        private void ConfirmSelection(int[] waitsToPublish = null, bool notifyTutorial = false)
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection
                || _pendingHandIndexes == null || _pendingHandTiles == null) return;
            var indexes = new List<int>(_pendingHandIndexes);
            var tiles = new List<int>(_pendingHandTiles);
            var waits = waitsToPublish == null ? null : new List<int>(waitsToPublish);
            CancelPendingConfirmation();
            int version = submissionVersion;
            int generation = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            bool completed = false;
            TransitionLockSet.Lease ownedLock = null;

            ReactionController.Instance?.StopHandSelectionTimer(true);
            _autoConfirmNextHandSelection = true;
            uiManager.HandUI?.SetSubmittedState(true);
            Action submit = () => {
                ownedLock?.Dispose();
                if (completed || version != submissionVersion
                    || uiManager.CurrentPhaseStatus != RoundStatus.HandSelection
                    || (uiManager.PhaseController != null && generation != uiManager.PhaseController.PresentationGeneration)) return;
                completed = true;

                if (waits != null)
                {
                    BoardStateManager.Instance.SetLocalState(null, null, waits);
                    BoardStateManager.Instance.FireRebuildEvent();
                }
                if (notifyTutorial && uiManager.IsTutorialMode && uiManager.TutorialManager != null)
                    uiManager.TutorialManager.ConfirmHandSelectionComplete();
                else
                    uiManager.SendActionToServer("select", new Network.ActionPayload { hand_indexes = indexes, hand = tiles });
            };
            if (uiManager.PhaseTransitionUI == null) submit();
            else
            {
                ownedLock = uiManager.BeginTransition("hand-confirmation");
                confirmationTransition = ownedLock;
                uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, submit);
            }
        }

        private void CancelSelectionConfirmation(bool clearWaits)
        {
            CancelPendingConfirmation();
            _autoConfirmNextHandSelection = false;
            uiManager.HandUI?.SetSubmittedState(false);
            if (clearWaits) BoardStateManager.Instance.ClearWaitTiles();
            uiManager.PhaseController?.SetMatchUIVisibility(true);
        }
    }
}
