using System;

namespace KillingMahjong.UI
{
    /// <summary>局頭の順序だけを管理する。演出・通知の保持・描画は各担当が実行する。</summary>
    public sealed class RoundStartCoordinator
    {
        public enum BoardUpdateKind { DealingCompleted, Status }

        public enum Stage
        {
            Idle,
            WaitingForTransition,
            Darkening,
            WaitingForTransitionCompletion,
            WaitingForDeal,
            DealingApplied
        }

        public Stage CurrentStage { get; private set; }
        private int generation;

        public bool IsWaitingForReady => CurrentStage == Stage.WaitingForTransition
            || CurrentStage == Stage.Darkening || CurrentStage == Stage.WaitingForTransitionCompletion;

        public void Reset()
        {
            generation++;
            CurrentStage = Stage.Idle;
        }

        public bool BeginRound()
        {
            if (CurrentStage != Stage.Idle && CurrentStage != Stage.DealingApplied) return false;
            generation++;
            CurrentStage = Stage.WaitingForTransition;
            return true;
        }

        /// <param name="playTransition">画面を覆った時と演出が反映可能になった時を通知する。</param>
        public bool TryStartTransition(Action<Action, Action> playTransition, Action resetBoard, Action showWaitUI)
        {
            if (CurrentStage != Stage.WaitingForTransition) return false;
            int startedGeneration = generation;
            CurrentStage = Stage.Darkening;

            playTransition(
                () =>
                {
                    if (generation != startedGeneration || CurrentStage != Stage.Darkening) return;
                    resetBoard();
                    if (generation == startedGeneration)
                        CurrentStage = Stage.WaitingForTransitionCompletion;
                },
                () =>
                {
                    if (generation != startedGeneration || CurrentStage != Stage.WaitingForTransitionCompletion) return;
                    showWaitUI();
                    if (generation == startedGeneration) CurrentStage = Stage.WaitingForDeal;
                });
            return true;
        }

        public bool ShouldDeferBoardUpdate(BoardUpdateKind kind, bool isBusyWithTransition)
        {
            // 局頭の準備は両通知で待つ。通常の状態同期は他の演出中も値を受け取る。
            return IsWaitingForReady || (kind == BoardUpdateKind.DealingCompleted && isBusyWithTransition);
        }

        /// <summary>盤面への配牌反映が済んだ通知でだけ、黒幕を解除する。</summary>
        public bool CompleteDealing(Action revealBoard)
        {
            if (CurrentStage != Stage.WaitingForDeal && CurrentStage != Stage.Idle) return false;
            CurrentStage = Stage.DealingApplied;
            revealBoard();
            return true;
        }
    }
}
