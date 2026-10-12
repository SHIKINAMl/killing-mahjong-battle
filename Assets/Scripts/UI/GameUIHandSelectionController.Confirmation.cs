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
        private bool _confirmationPresentationStarted, _awaitingWeakHandConfirmation, _weakHandApproved, _selectConfirmSent;
        private bool _serverConfirmationPending;
        private string _pendingSignature, _approvedSignature;
        private int _confirmationGeneration, _serverConfirmationGeneration;
        private bool confirmationPhaseWasActive;
        private IsTenpaiData _confirmationData;
        public bool IsConfirming => _confirming;
        public bool IsAwaitingWeakHandConfirmation => _awaitingWeakHandConfirmation || _serverConfirmationPending;

        internal void CancelPendingConfirmation()
        {
            submissionVersion++;
            bool wasConfirming = _confirming;
            _confirming = false;
            _selectionSubmitted = false;
            _awaitingWeakHandConfirmation = _serverConfirmationPending = _weakHandApproved = _selectConfirmSent = false;
            _weakConfirmation?.Hide();
            _waitReveal?.SetForcedVisible(false);
            _autoConfirmNextHandSelection = false;
            if (confirmationRoutine != null) StopCoroutine(confirmationRoutine);
            confirmationRoutine = null;
            confirmationTransition?.Dispose();
            confirmationTransition = null;
            if (wasConfirming)
            {
                if (_confirmationPresentationStarted) uiManager?.PhaseTransitionUI?.CancelTransitions();
                RestoreConfirmationPhaseVisibility();
            }
            if (uiManager != null && uiManager.WaitUI != null && uiManager.WaitUI.IsArrivalInProgress) uiManager.WaitUI.Hide();
            _waitReveal?.Hide();
            _hasRankResult = false; _rankWaits = null;
            if (wasConfirming && uiManager != null && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection)
                uiManager.HandUI?.SetSubmittedState(false);
            _confirmationPresentationStarted = false;
            _pendingSignature = _approvedSignature = null;
        }

        private void OnDisable() { CancelPendingConfirmation(); }

        private void BeginImmediateConfirmation()
        {
            _confirming = true; _selectionSubmitted = false;
            _confirmationResolved = false; _confirmationData = null;
            _confirmationPresentationStarted = _awaitingWeakHandConfirmation = _weakHandApproved = _selectConfirmSent = false;
            _serverConfirmationPending = false; _weakConfirmation?.Hide();
            _pendingSignature = CurrentSelectionSignature(); _approvedSignature = null;
            _confirmationTextDone = uiManager.PhaseTransitionUI == null;
            _autoConfirmNextHandSelection = false;
            confirmationTransition = uiManager.BeginTransition("hand-confirmation");
            confirmationPhaseWasActive = uiManager.PhaseTransitionUI != null && uiManager.PhaseTransitionUI.gameObject.activeSelf;
            if (uiManager.PhaseTransitionUI != null) uiManager.PhaseTransitionUI.gameObject.SetActive(true);
            int stamp = submissionVersion;
            int generation = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            _confirmationGeneration = generation;
            // サーバー結果が分かるまでは決定の文字・送信を始めない。
            confirmationRoutine = StartCoroutine(CompleteConfirmation(stamp, generation));
        }

        private void ResolveImmediateConfirmation(IsTenpaiData data)
        {
            if (!_confirming || _confirmationResolved) return;
            _confirmationData = data ?? new IsTenpaiData { waits = System.Array.Empty<WaitData>() };
            _confirmationResolved = true;
            StoreWaitDetails(_confirmationData.waits);
            var waits = _confirmationData.waits ?? System.Array.Empty<WaitData>();
            if (_waitReveal != null && !_waitReveal.HasCandidates(waits)) _waitReveal.Show(waits, uiManager.TileResourceManager, false);
            if (HasManganWait(waits)) StartConfirmationPresentation();
            else
            {
                _awaitingWeakHandConfirmation = true;
                _waitReveal?.SetForcedVisible(true);
                int stamp = submissionVersion, generation = _confirmationGeneration;
                _weakConfirmation.Show(waits.Length == 0,
                    () => ApproveWeakHand(stamp, generation), () => ReturnToHandSelection(stamp, generation));
            }
        }

        private static bool HasManganWait(WaitData[] waits)
        {
            if (waits != null) foreach (var wait in waits) if (wait != null && wait.mangan_or_more) return true;
            return false;
        }
        private void ApproveWeakHand(int stamp, int generation)
        {
            if (!_awaitingWeakHandConfirmation || !ConfirmationIsCurrent(stamp, generation)) return;
            _awaitingWeakHandConfirmation = false; _weakHandApproved = true; _approvedSignature = _pendingSignature;
            StartConfirmationPresentation();
        }
        private void ReturnToHandSelection(int stamp, int generation)
        {
            if (!ConfirmationIsCurrent(stamp, generation)) return;
            var waits = _confirmationData?.waits ?? System.Array.Empty<WaitData>();
            var indexes = new List<int>(_pendingHandIndexes); var signature = _pendingSignature;
            CancelPendingConfirmation();
            // まだselectを送っていないのでselect_cancelは不要。13枚も表示結果も保つ。
            _hasRankResult = true; _rankWaits = waits; _rankResultIndexes = indexes; _rankSignature = signature;
            _waitReveal?.Show(waits, uiManager.TileResourceManager, false);
        }
        private void StartConfirmationPresentation()
        {
            if (_confirmationPresentationStarted || !_confirming) return;
            _confirmationPresentationStarted = true; _autoConfirmNextHandSelection = true;
            _weakConfirmation?.Hide(); _waitReveal?.SetForcedVisible(true);
            uiManager.HandUI?.SetSubmittedState(true);
            ReactionController.Instance?.StopHandSelectionTimer(true);
            int stamp = submissionVersion;
            _confirmationTextDone = uiManager.PhaseTransitionUI == null;
            uiManager.PhaseTransitionUI?.PlayCenterTextAnim("手牌決定！", 2f, () => {
                if (stamp == submissionVersion) _confirmationTextDone = true;
            });
        }

        private bool ConfirmationIsCurrent(int stamp, int generation) =>
            _confirming && stamp == submissionVersion && uiManager != null
            && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection
            && (uiManager.PhaseController == null || generation == uiManager.PhaseController.PresentationGeneration)
            && (_pendingSignature == null || _pendingSignature == CurrentSelectionSignature())
            && (_pendingHandIndexes == null || (TryCaptureCurrentHandSelection(out List<int> now, out _) && SameHandIndexes(now, _pendingHandIndexes)));

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
                while (_awaitingWeakHandConfirmation && ConfirmationIsCurrent(stamp, generation))
                {
                    if (_weakConfirmation == null || !_weakConfirmation.IsOpen) yield break;
                    yield return null;
                }
                if (!_confirmationPresentationStarted || !ConfirmationIsCurrent(stamp, generation)) yield break;
                var data = _confirmationData.waits ?? System.Array.Empty<WaitData>();
                if (data.Length > 0 && _waitReveal != null && uiManager.WaitUI != null)
                {
                    if (!_waitReveal.HasCandidates(data)) _waitReveal.Show(data, uiManager.TileResourceManager, false);
                    _waitReveal.SetForcedVisible(true);
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
                    _awaitingWeakHandConfirmation = false; _weakConfirmation?.Hide();
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

        // サーバーが改めて確認を要求した場合も、承認していない手を自動確定しない。
        private void HandleServerWeakHandConfirmation(HandSelectionConfirmationData data)
        {
            if (data == null || !_selectionSubmitted || _selectConfirmSent || _serverConfirmationPending
                || uiManager.CurrentPhaseStatus != RoundStatus.HandSelection
                || uiManager.HandUI == null || !uiManager.HandUI.IsSubmitted
                || !SameHandIndexes(data.hand_indexes, _pendingHandIndexes)) return;
            _autoConfirmNextHandSelection = false;
            if (_weakHandApproved && _approvedSignature == CurrentSelectionSignature())
            { SendWeakHandConfirmation(data.hand_indexes); return; }
            _serverConfirmationPending = true;
            _serverConfirmationGeneration = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            int stamp = submissionVersion;
            string signature = CurrentSelectionSignature();
            _waitReveal?.SetForcedVisible(true);
            _weakConfirmation.Show(data.reason == "not_tenpai", () => {
                if (!_serverConfirmationPending || stamp != submissionVersion
                    || signature != CurrentSelectionSignature() || !ServerConfirmationIsCurrent()) return;
                _serverConfirmationPending = false; _weakHandApproved = true; _approvedSignature = signature;
                _waitReveal?.SetForcedVisible(false);
                SendWeakHandConfirmation(data.hand_indexes);
            }, () => {
                if (_serverConfirmationPending && stamp == submissionVersion && ServerConfirmationIsCurrent()) CancelHandSelection();
            });
        }
        private bool ServerConfirmationIsCurrent() => uiManager != null
            && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection
            && (uiManager.PhaseController == null || _serverConfirmationGeneration == uiManager.PhaseController.PresentationGeneration);
        private void SendWeakHandConfirmation(List<int> indexes)
        {
            if (_selectConfirmSent) return;
            _selectConfirmSent = true;
            uiManager.SendActionToServer("select_confirm", new Network.ActionPayload { hand_indexes = new List<int>(indexes), hand = new List<int>(_pendingHandTiles) });
        }
        private void Update()
        {
            if (_serverConfirmationPending && (!ServerConfirmationIsCurrent() || _pendingSignature != CurrentSelectionSignature()))
            { _serverConfirmationPending = false; _weakConfirmation?.Hide(); _waitReveal?.SetForcedVisible(false); }
        }
    }
}
