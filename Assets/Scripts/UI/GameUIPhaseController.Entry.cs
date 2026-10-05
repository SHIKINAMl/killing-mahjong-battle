using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public partial class GameUIPhaseController
    {
        // 入場チケットは再描画と別のキーで保持する。後の再描画で開始処理を上書きしない。
        private sealed class PhaseEntry
        {
            public RoundStatus Status;
            public int Generation;
            public int Sequence;
            public bool Applied;
        }

        private int phasePresentationGeneration;
        private int phaseEntrySequence;
        private bool discardTurnTimerPending;
        internal int PresentationGeneration => phasePresentationGeneration;

        private void ResetPhasePresentation()
        {
            phasePresentationGeneration++;
            discardTurnTimerPending = false;
        }

        private void EnterDealingPhase()
        {
            _hasShownHandSelectionPrompt = false;
            _hasExecutedRonAnimation = false;
            if (uiManager.IsWaitDeductionUIEnabled) uiManager.WaitDeduction.ResetForNewRound();
            uiManager.EnemyInfoUI?.ShowReadyBox(false);
            uiManager.PlayerInfoUI?.ShowReadyBox(false);
            ResetPhaseReadyMarks();
            SetReadyBadgesSuppressed(false);
            bool afterDraw = _pendingDrawTransition;
            _pendingDrawTransition = false;
            StartRoundStartTransition($"第{_currentRoundIndex}局...", afterDraw);
        }

        private void EnterPhase(RoundStatus status)
        {
            ApplyPhaseEntry(new PhaseEntry
            {
                Status = status, Generation = phasePresentationGeneration, Sequence = ++phaseEntrySequence
            });
        }

        private void ApplyPhaseEntry(PhaseEntry entry)
        {
            if (entry.Applied || entry.Generation != phasePresentationGeneration) return;
            if (uiManager.IsBusyWithTransition && !uiManager.IsUpdatingCoveredBoard)
            {
                uiManager.DeferUntilIdle($"phaseEntry:{entry.Sequence}", () => ApplyPhaseEntry(entry));
                return;
            }
            entry.Applied = true;

            // 古い表示は出さない。ただし Dealing の局頭準備は、後続通知より遅れても必須。
            if (entry.Status != uiManager.CurrentPhaseStatus)
            {
                if (entry.Status == RoundStatus.Dealing) EnterDealingPhase();
                return;
            }

            bool settlement = entry.Status == RoundStatus.Agari || entry.Status == RoundStatus.Ron
                || entry.Status == RoundStatus.Result || entry.Status == RoundStatus.Draw;
            if (!settlement) Effects.ScreenFlash.Play(playSound: false);
            RefreshPhaseView(entry.Status);

            switch (entry.Status)
            {
                case RoundStatus.Dealing:
                    EnterDealingPhase();
                    break;
                case RoundStatus.Betting:
                    // チュートリアルは台本のセリフ後に賭け金UIを開く。
                    if (!uiManager.IsTutorialMode)
                    {
                        StartBettingPhase(BoardStateManager.Instance.LocalPlayerHp);
                        SetReadyBadgesSuppressed(true);
                        ApplyPhaseReadyMarks(entry.Status);
                    }
                    break;
                case RoundStatus.HandSelection:
                    if (!uiManager.IsTutorialMode)
                    {
                        uiManager.PlayerInfoUI?.StartTurnTimer(15f);
                        ReactionController.Instance?.StartHandSelectionTimer();
                        if (uiManager.PhaseTransitionUI != null && !_hasShownHandSelectionPrompt)
                        {
                            uiManager.PhaseTransitionUI.PlayPromptText("手牌を選んでください", 1.5f);
                            _hasShownHandSelectionPrompt = true;
                        }
                    }
                    break;
                case RoundStatus.Discard:
                    discardTurnTimerPending = true;
                    SynchronizeDiscardTurnTimer();
                    break;
                case RoundStatus.Agari:
                case RoundStatus.Ron:
                case RoundStatus.Result:
                    uiManager.PlayerInfoUI?.StopTurnTimer();
                    // チュートリアルのロンボタンと演出順は台本に任せる。
                    if (!uiManager.IsTutorialMode && uiManager.RonAnimationUI != null
                        && BoardStateManager.Instance.LastIsLocalWin) uiManager.ExecuteRonAction();
                    break;
                case RoundStatus.Draw:
                    uiManager.PlayerInfoUI?.StopTurnTimer();
                    uiManager.DialogueUI?.ShowText("流局…次の対局へ");
                    break;
                default:
                    uiManager.PlayerInfoUI?.StopTurnTimer();
                    break;
            }
        }

        public void HandleDiscardTurnChanged()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.Discard) return;
            discardTurnTimerPending = true;
            SynchronizeDiscardTurnTimer();
        }

        public void SynchronizeDiscardTurnTimer()
        {
            if (!discardTurnTimerPending || uiManager.IsBusyWithTransition
                || uiManager.CurrentPhaseStatus != RoundStatus.Discard || uiManager.IsTutorialMode
                || uiManager.PlayerInfoUI == null) return;
            discardTurnTimerPending = false;
            if (BoardStateManager.Instance.IsLocalTurn) uiManager.PlayerInfoUI.StartTurnTimer(10f);
            else uiManager.PlayerInfoUI.StopTurnTimer();
        }
    }
}
