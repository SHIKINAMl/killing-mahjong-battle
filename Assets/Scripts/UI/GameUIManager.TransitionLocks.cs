namespace KillingMahjong.UI
{
    public partial class GameUIManager
    {
        private TransitionLockSet transitionLocks;
        private TransitionLockSet.Lease compatibilityTransition;
        private TransitionLockSet Locks => transitionLocks ?? (transitionLocks = new TransitionLockSet(ApplyTransitionLockState));

        public TransitionLockSet.Lease BeginTransition(string owner) => Locks.Acquire(owner);

        // 既存のInspector呼び出し口を保持。falseは互換入口自身のロックだけを解除する。
        public void SetIsTransitioning(bool value)
        {
            if (value)
            {
                if (compatibilityTransition == null || !compatibilityTransition.IsActive)
                    compatibilityTransition = BeginTransition("compatibility");
            }
            else
            {
                compatibilityTransition?.Dispose();
                compatibilityTransition = null;
            }
        }

        private void ApplyTransitionLockState()
        {
            isTransitioning = transitionLocks != null && transitionLocks.IsLocked;
            UpdateTurnIndicatorVisibility();
            if (abilityUI != null) abilityUI.SetSuppressedForTransition(isTransitioning);
            if (!isTransitioning) PhaseController?.SynchronizeDiscardTurnTimer();
        }

        internal void ResetTransitionLocks()
        {
            compatibilityTransition = null;
            transitionLocks?.Reset();
        }

        internal void RefreshBoardAfterTransition()
        {
            if (IsBusyWithTransition)
            {
                DeferUntilIdle("transitionBoardRefresh", RefreshBoardAfterTransition);
                return;
            }
            VisualController?.RebuildAllTilesFromState();
        }

        private void OnDisable()
        {
            PhaseController?.CancelPhasePresentations();
            HandSelectionController?.CancelPendingConfirmation();
            SkillController?.CancelActiveTransitions();
            ResetTransitionLocks();
        }
    }
}
