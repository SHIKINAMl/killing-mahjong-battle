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

        private bool _isCanceling = false;

        public void CancelHandSelection()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;
            if (uiManager.IsTransitioning) return; 

            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);

            // 裏技: Python側のキャンセルアクションがないため、以前は1枚の牌を送って解除していたが
            // それだと選び直しの際に手牌が1枚になってしまうため、仮の処置として
            // 前回選択していた13枚をそのまま送りなおして確定状態を解除させる。
            // ※今後Python側に専用のキャンセルコマンドが実装される予定
            _isCanceling = true;
            
            var payload = new KillingMahjong.Network.ActionPayload();
            if (_pendingHandIndexes != null && _pendingHandIndexes.Count > 0)
            {
                payload.hand_indexes = _pendingHandIndexes;
                payload.hand = _pendingHandTiles;
            }
            else
            {
                payload.hand_indexes = new System.Collections.Generic.List<int> { 0 };
            }
            
            uiManager.SendActionToServer("select", payload);
        }

        public void HandleIsTenpaiReceived(IsTenpaiData data)
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;

            string message = "【予想役・点数】\n";
            int[] waitTileIds = new int[0];
            
            if (data.waits != null && data.waits.Length > 0)
            {
                // ConfirmationDialogUI側で待ち牌を表示するため、ここではテキストのみ構築
                message += "待ち牌:\n\n\n"; 
                
                System.Collections.Generic.List<int> ids = new System.Collections.Generic.List<int>();
                foreach (var wait in data.waits)
                {
                    ids.Add(wait.tile);
                    string yakuText = (wait.yaku != null && wait.yaku.Length > 0) ? string.Join(" / ", wait.yaku) : "役なし";
                    bool isMangan = wait.mangan_or_more;
                    string manganText = isMangan ? "満貫以上" : "満貫未満";
                    message += $"-> {yakuText} ({manganText})\n";
                }
                waitTileIds = ids.ToArray();
            }
            message += "\nこの手牌で決定しますか？";

            // WaitUIを移動させる処理を廃止 (ConfirmationDialogUI内部で表示する)
            // if (uiManager.WaitUI != null) uiManager.WaitUI.MoveToCenter();

            if (uiManager.ConfirmationDialogUI != null)
            {
                uiManager.ConfirmationDialogUI.ShowDialogWithWaits(
                    message,
                    waitTileIds,
                    () => {
                        if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                        _autoConfirmNextHandSelection = true;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
                        // if (uiManager.WaitUI != null) uiManager.WaitUI.MoveToOriginalPosition();

                        if (uiManager.PhaseTransitionUI != null)
                        {
                            uiManager.SetIsTransitioning(true);
                            uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                            {
                                uiManager.SetIsTransitioning(false);
                                
                                // 手牌決定演出が終わったタイミングで、左下のプレイヤー情報UIに待ち牌を表示する
                                BoardStateManager.Instance.SetLocalState(null, null, new System.Collections.Generic.List<int>(waitTileIds));
                                BoardStateManager.Instance.FireRebuildEvent();

                                uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                            });
                        }
                        else
                        {
                            BoardStateManager.Instance.SetLocalState(null, null, new System.Collections.Generic.List<int>(waitTileIds));
                            BoardStateManager.Instance.FireRebuildEvent();

                            uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                        }
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
                        BoardStateManager.Instance.ClearWaitTiles();
                        // if (uiManager.WaitUI != null) 
                        // {
                        //     uiManager.WaitUI.MoveToOriginalPosition();
                        //     uiManager.WaitUI.Hide();
                        // }
                        if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
                    }
                );
            }
            else
            {
                // if (uiManager.WaitUI != null) uiManager.WaitUI.MoveToOriginalPosition();
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
            if (_isCanceling)
            {
                _isCanceling = false;
                return;
            }

            _autoConfirmNextHandSelection = false;
            
            // 満貫未満の警告ダイアログを表示せず、自動で確定を送信する
            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
            uiManager.SendActionToServer("select_confirm", new KillingMahjong.Network.ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
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
