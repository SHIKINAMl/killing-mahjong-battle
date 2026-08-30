using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 流局。GameUIPhaseController から分離（partial）。
    public partial class GameUIPhaseController
    {

        private bool _pendingDrawTransition = false;

        public void HandleDraw(KillingMahjong.EngineData.DrawPlayerData[] drawData = null)
        {
            // 流局は「最後の打牌の直後」に届くので打牌アニメや能力演出と重なりやすい。
            // ここで捨てると _currentRoundIndex++・流局ダイアログ・next_round 送信が全部飛び、
            // サーバーが承認を待ち続けて対局が止まる。捨てずに演出明けまで保留する。
            if (uiManager.IsBusyWithTransition)
            {
                uiManager.DeferUntilIdle("draw", () => HandleDraw(drawData));
                return;
            }

            // 待機中などの表示は消す
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
            
            Debug.Log("[GameUIManager] 流局処理開始");
            _isCarryOverNextRound = true;
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.CheckAndPlayDrawReaction();
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

            // 自分の待ち牌表示
            if (uiManager.WaitUI != null && Managers.BoardStateManager.Instance.CurrentWaitTiles != null && Managers.BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                uiManager.WaitUI.gameObject.SetActive(true);
                uiManager.WaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentWaitTiles);
            }

            // 相手の待ち牌表示
            if (uiManager.EnemyWaitUI != null && Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles != null && Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles.Count > 0)
            {
                uiManager.EnemyWaitUI.gameObject.SetActive(true);
                uiManager.EnemyWaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles);
            }

            // 既存の手牌データソート
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentHandTiles);
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentEnemyHandTiles);
            
            if (uiManager.HandUI != null) uiManager.HandUI.SortHandSlots();
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.SortHandSlots();

            // 相手の手牌強制公開
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.RevealAllHands(uiManager.TileResourceManager);
            }

            // このOKが出ている時点で、準備完了という文字を出してほしい
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            // ダイアログを出してOKボタンを待つ
            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText("流局しました。\nお互いの手牌と待ちを確認してください。");
                uiManager.DialogueUI.ShowNextRoundButton(() => {
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.EnemyWaitUI != null) uiManager.EnemyWaitUI.gameObject.SetActive(false);
                    
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyCheck(true);
                    _pendingDrawTransition = true;
                    SendNextRoundAction();
                });
            }
            else
            {
                _pendingDrawTransition = true;
                SendNextRoundAction();
            }
        }

        private void ExecuteDrawTransitionForDealing()
        {
            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.SetIsTransitioning(true);
                uiManager.PhaseTransitionUI.PlayDrawTransition(
                    onMidpoint: () => {
                        BoardStateManager.Instance.ClearAllBoardData();
                        uiManager.ClearAllTiles();
                        SetMatchUIVisibility(false);
                        
                        if (ReactionController.Instance != null)
                        {
                            ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
                        }
                    },
                    onComplete: () => {
                        uiManager.SetIsTransitioning(false);
                        StartCoroutine(DealingRoutine());
                    }
                );
            }
            else
            {
                StartCoroutine(DrawSequence());
            }
        }

        private IEnumerator DrawSequence()
        {
            yield return new WaitForSeconds(3.0f);
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
            }

            if (uiManager.PlayerInfoUI != null) 
            {
                uiManager.PlayerInfoUI.ShowReadyBox(true);
                uiManager.PlayerInfoUI.SetReadyCheck(true);
            }
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            Debug.Log("[GameUIManager] 流局演出完了 - 次ラウンド待ち承認自動送信");
            SendNextRoundAction();
        }
    }
}
