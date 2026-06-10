using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUIHandSelectionController : MonoBehaviour
    {
        private GameUIManager uiManager;

        private bool _autoConfirmNextHandSelection = false;
        public bool AutoConfirmNextHandSelection => _autoConfirmNextHandSelection;

        private List<int> _pendingHandIndexes;
        private List<int> _pendingHandTiles;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }

        public void CompleteHandSelection()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;
            if (uiManager.DialogueUI != null && uiManager.DialogueUI.IsLogOpen) return;
            
            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

            if (BoardStateManager.Instance.TargetHandIndexes != null && BoardStateManager.Instance.TargetHandIndexes.Count == 13)
            {
                _pendingHandIndexes = new List<int>(BoardStateManager.Instance.TargetHandIndexes);
            }
            else
            {
                _pendingHandIndexes = new List<int>();
                HashSet<int> usedIndexes = new HashSet<int>();
                foreach(int tileId in BoardStateManager.Instance.CurrentHandTiles) {
                     var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
                     int idx = -1;
                     for (int i = 0; i < wallTiles.Count; i++)
                     {
                         if (wallTiles[i] == tileId && !usedIndexes.Contains(i))
                         {
                             idx = i;
                             break;
                         }
                     }
                     if (idx >= 0) {
                         _pendingHandIndexes.Add(idx);
                         usedIndexes.Add(idx);
                     }
                }
            }
            _pendingHandTiles = new List<int>(BoardStateManager.Instance.CurrentHandTiles);

            uiManager.SendActionToServer("is_tenpai", new KillingMahjong.Network.ActionPayload { wall_indexes = _pendingHandIndexes });
        }

        public void CancelHandSelection()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;
            if (uiManager.IsTransitioning) return; 

            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
        }

        public void HandleIsTenpaiReceived(IsTenpaiData data)
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;

            string message = "【予想役・点数】\n";
            
            if (data.waits != null && data.waits.Length > 0)
            {
                message += "待ち牌:\n\n\n\n\n\n"; // 麻雀牌と被らないように改行を増加
                foreach (var wait in data.waits)
                {
                    string yakuText = (wait.yaku != null && wait.yaku.Length > 0) ? string.Join(" / ", wait.yaku) : "役なし";
                    bool isMangan = wait.mangan_or_more;
                    string manganText = isMangan ? "満貫以上" : "満貫未満";
                    message += $"-> {yakuText} ({manganText})\n";
                }
            }
            message += "\nこの手牌で決定しますか？";

            if (uiManager.WaitUI != null) uiManager.WaitUI.MoveToCenter();

            if (uiManager.ConfirmationDialogUI != null)
            {
                uiManager.ConfirmationDialogUI.ShowDialog(
                    message,
                    () => {
                        if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                        _autoConfirmNextHandSelection = true;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
                        if (uiManager.WaitUI != null) uiManager.WaitUI.MoveToOriginalPosition();

                        if (uiManager.PhaseTransitionUI != null)
                        {
                            uiManager.SetIsTransitioning(true);
                            uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                            {
                                uiManager.SetIsTransitioning(false);
                                uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                            });
                        }
                        else
                        {
                            uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                        }
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
                        BoardStateManager.Instance.ClearWaitTiles();
                        if (uiManager.WaitUI != null) 
                        {
                            uiManager.WaitUI.MoveToOriginalPosition();
                            uiManager.WaitUI.Hide();
                        }
                        if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
                    }
                );
            }
            else
            {
                if (uiManager.WaitUI != null) uiManager.WaitUI.MoveToOriginalPosition();
                if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                _autoConfirmNextHandSelection = true;
                if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                if (uiManager.PhaseTransitionUI != null)
                {
                    uiManager.SetIsTransitioning(true);
                    uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                    {
                        uiManager.SetIsTransitioning(false);
                        uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                    });
                }
                else
                {
                    uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                }
            }
        }

        public void HandleNotTenpaiReceived(string reason)
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;

            string message = $"ノーテン（聴牌していません）\n\nこのまま決定しますか？";
            if (uiManager.ConfirmationDialogUI != null)
            {
                uiManager.ConfirmationDialogUI.ShowDialog(
                    message,
                    () => {
                        if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                        _autoConfirmNextHandSelection = true;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                        if (uiManager.PhaseTransitionUI != null)
                        {
                            uiManager.SetIsTransitioning(true);
                            uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                            {
                                uiManager.SetIsTransitioning(false);
                                uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                            });
                        }
                        else
                        {
                            uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                        }
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
                        if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
                    }
                );
            }
            else
            {
                if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                _autoConfirmNextHandSelection = true;
                if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                if (uiManager.PhaseTransitionUI != null)
                {
                    uiManager.SetIsTransitioning(true);
                    uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                    {
                        uiManager.SetIsTransitioning(false);
                        uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                    });
                }
                else
                {
                    uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                }
            }
        }

        public void HandleHandSelectionConfirmation(HandSelectionConfirmationData data)
        {
            if (_autoConfirmNextHandSelection)
            {
                _autoConfirmNextHandSelection = false;
                if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
                uiManager.SendActionToServer("select_confirm", new KillingMahjong.Network.ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
                return;
            }

            if (uiManager.ConfirmationDialogUI != null)
            {
                uiManager.ConfirmationDialogUI.ShowDialog(
                    data.message,
                    () => {
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
                        uiManager.SendActionToServer("select_confirm", new KillingMahjong.Network.ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
                    },
                    () => {
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
                        BoardStateManager.Instance.ClearWaitTiles();
                        if (uiManager.WaitUI != null) uiManager.WaitUI.Hide();
                        if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
                    }
                );
            }
            else
            {
                if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
                uiManager.SendActionToServer("select_confirm", new KillingMahjong.Network.ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
            }
        }

        public void OnHandSelectionAccepted()
        {
            _autoConfirmNextHandSelection = false;
            if (uiManager.HandUI != null && uiManager.HandUI.IsSubmitted) 
            {
                if (uiManager.DialogueUI != null) 
                {
                    string text = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.HandSelection) : null;
                    if (string.IsNullOrEmpty(text)) text = "相手の手牌選択を待っています...";
                    uiManager.DialogueUI.ShowText(text);
                }
            }
            if (uiManager.PhaseController != null) uiManager.PhaseController.HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
            if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
        }
    }
}
