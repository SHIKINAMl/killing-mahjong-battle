using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public partial class GameUIHandSelectionController
    {
        private int submissionVersion;
        private TransitionLockSet.Lease confirmationTransition;
        private Coroutine confirmationRoutine;
        private bool _confirming, _confirmationResolved, _confirmationTextDone, _selectionSubmitted;
        private bool confirmationPhaseWasActive;
        private IsTenpaiData _confirmationData;
        public bool IsConfirming => _confirming;

        internal void CancelPendingConfirmation()
        {
            submissionVersion++;
            bool wasConfirming = _confirming;
            _confirming = false;
            _selectionSubmitted = false;
            if (confirmationRoutine != null) StopCoroutine(confirmationRoutine);
            confirmationRoutine = null;
            confirmationTransition?.Dispose();
            confirmationTransition = null;
            if (wasConfirming)
            {
                uiManager?.PhaseTransitionUI?.CancelTransitions();
                RestoreConfirmationPhaseVisibility();
            }
            if (uiManager != null && uiManager.WaitUI != null && uiManager.WaitUI.IsArrivalInProgress) uiManager.WaitUI.Hide();
            _waitReveal?.Hide();
            _hasRankResult = false; _rankWaits = null;
        }

        private void OnDisable() { CancelPendingConfirmation(); }

        private void BeginImmediateConfirmation()
        {
            _confirming = true; _selectionSubmitted = false;
            _confirmationResolved = false; _confirmationData = null;
            _confirmationTextDone = uiManager.PhaseTransitionUI == null;
            _autoConfirmNextHandSelection = true;
            uiManager.HandUI?.SetSubmittedState(true);
            ReactionController.Instance?.StopHandSelectionTimer(true);
            confirmationTransition = uiManager.BeginTransition("hand-confirmation");
            confirmationPhaseWasActive = uiManager.PhaseTransitionUI != null && uiManager.PhaseTransitionUI.gameObject.activeSelf;
            if (uiManager.PhaseTransitionUI != null) uiManager.PhaseTransitionUI.gameObject.SetActive(true);
            int stamp = submissionVersion;
            int generation = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            // 決定の入力と同じフレームで既存の演出へ。追加の確認画面は開かない。
            if (uiManager.PhaseTransitionUI != null)
                uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2f, () => {
                    if (stamp == submissionVersion) _confirmationTextDone = true;
                });
            confirmationRoutine = StartCoroutine(CompleteConfirmation(stamp, generation));
        }

        private void ResolveImmediateConfirmation(IsTenpaiData data)
        {
            if (!_confirming || _confirmationResolved) return;
            _confirmationData = data ?? new IsTenpaiData { waits = System.Array.Empty<WaitData>() };
            _confirmationResolved = true;
            StoreWaitDetails(_confirmationData.waits);
        }

        private bool ConfirmationIsCurrent(int stamp, int generation) =>
            _confirming && stamp == submissionVersion && uiManager != null
            && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection
            && (uiManager.PhaseController == null || generation == uiManager.PhaseController.PresentationGeneration);

        private IEnumerator CompleteConfirmation(int stamp, int generation)
        {
            bool submitted = false;
            try
            {
                float deadline = Time.unscaledTime + 15f;
                while (!_confirmationResolved && ConfirmationIsCurrent(stamp, generation))
                {
                    if (Time.unscaledTime >= deadline)
                    {
                        Debug.LogWarning("[HandSelection] 聴牌の応答がありません。選択を確定せずに戻します。");
                        uiManager.PhaseTransitionUI?.CancelTransitions();
                        uiManager.DialogueUI?.ShowText("応答を確認できませんでした。もう一度決定してください。");
                        yield break;
                    }
                    yield return null;
                }
                if (!ConfirmationIsCurrent(stamp, generation)) yield break;
                var data = _confirmationData.waits ?? System.Array.Empty<WaitData>();
                if (data.Length > 0 && _waitReveal != null && uiManager.WaitUI != null)
                {
                    if (!_waitReveal.HasCandidates(data)) _waitReveal.Show(data, uiManager.TileResourceManager, false);
                    yield return _waitReveal.Transfer(uiManager.WaitUI);
                }
                else _waitReveal?.Hide();
                while (!_confirmationTextDone && ConfirmationIsCurrent(stamp, generation)) yield return null;
                if (!ConfirmationIsCurrent(stamp, generation)) yield break;
                var ids = new List<int>();
                foreach (var w in data) if (w != null) ids.Add(w.tile);
                var board = BoardStateManager.Instance;
                if (board != null) board.SetLocalState(null, null, ids);
                // ロックを外したあとに再構築と送信。受理・次フェイズは従来どおりサーバー通知が正。
                confirmationTransition?.Dispose(); confirmationTransition = null;
                _confirming = false; _selectionSubmitted = true; submitted = true;
                board?.FireRebuildEvent();
                if (uiManager.IsTutorialMode && uiManager.TutorialManager != null)
                    uiManager.TutorialManager.ConfirmHandSelectionComplete();
                else
                    uiManager.SendActionToServer("select", new Network.ActionPayload {
                        hand_indexes = new List<int>(_pendingHandIndexes), hand = new List<int>(_pendingHandTiles) });
            }
            finally
            {
                if (stamp == submissionVersion)
                {
                    confirmationRoutine = null;
                    confirmationTransition?.Dispose(); confirmationTransition = null;
                    _confirming = false;
                    RestoreConfirmationPhaseVisibility();
                    if (!submitted)
                    {
                        _waitReveal?.Hide();
                        if (uiManager.WaitUI != null && uiManager.WaitUI.IsArrivalInProgress) uiManager.WaitUI.Hide();
                        _autoConfirmNextHandSelection = false;
                        uiManager.HandUI?.SetSubmittedState(false);
                    }
                }
            }
        }

        private void RestoreConfirmationPhaseVisibility()
        {
            if (!confirmationPhaseWasActive && uiManager != null && uiManager.PhaseTransitionUI != null)
                uiManager.PhaseTransitionUI.gameObject.SetActive(false);
        }
    }
}
