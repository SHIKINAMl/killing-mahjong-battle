namespace KillingMahjong.UI
{
    public partial class GameUIManager
    {
        private TransitionLockSet transitionLocks;
        private TransitionLockSet.Lease compatibilityTransition;
        private TransitionLockSet Locks => transitionLocks ?? (transitionLocks = new TransitionLockSet(ApplyTransitionLockState));

        public TransitionLockSet.Lease BeginTransition(string owner)
        {
            // 演出が始まる。いま出ている「カットしてよい演出」（役名表示・手牌選択の案内）は、ここで切る
            if (!IsQuietTransitionOwner(owner)) Effects.EffectQueue.CutShowingIfAny(owner);
            return Locks.Acquire(owner);
        }

        /// <summary>
        /// 演出として数えないロックか。
        ///   tile-move         … 牌を1枚動かす短いアニメ。役名表示は、まさにこの直後に出る
        ///   mulligan-request  … 牌交換の返事待ち。返事（スキルの演出）が来るまで持ち続けるので、
        ///                        これを待つと、そのスキルの演出が永久に始まらない
        /// </summary>
        private static bool IsQuietTransitionOwner(string owner)
        {
            return owner == "tile-move" || owner == "mulligan-request";
        }

        /// <summary>
        /// 演出の順番待ち（Effects.EffectQueue）が、次の演出を流すのを待つべきか。
        /// 順番待ちを通らない演出（配牌・対局開始・ロン・手牌決定の文字など）が流れているあいだは true。
        /// **順番待ちが見るのは、自分の演出を流していないときだけ**なので、スキルのロックは混ざらない。
        /// </summary>
        internal bool IsBusyForQueuedEffect =>
            Locks.IsLockedByOthers(IsQuietTransitionOwner)
            || (phaseTransitionUI != null && phaseTransitionUI.IsDarkenTransitioning);

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
            if (!isTransitioning && PhaseController != null) PhaseController.SynchronizeDiscardTurnTimer();
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
            // 保留通知は維持する。非表示中の時間をタイムアウトに数えない。
            ResetDeferredWait();
            if (PhaseController != null) PhaseController.CancelPhasePresentations();
            if (HandSelectionController != null) HandSelectionController.CancelPendingConfirmation();
            if (SkillController != null) SkillController.CancelActiveTransitions();
            // 順番を待っている演出は、もう流す先が無い
            Effects.EffectQueue.ClearIfAny();
            ResetTransitionLocks();
        }
    }
}
