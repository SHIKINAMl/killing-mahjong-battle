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
            var ticket = roundEnd.Begin(RoundEndCoordinator.Outcome.Draw);
            if (ticket == null) return;
            RunRoundEndWhenIdle(ticket, () => PresentDraw(ticket, drawData));
        }

        private void PresentDraw(RoundEndCoordinator.Ticket ticket, DrawPlayerData[] drawData)
        {
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

            RevealRoundEndHands();

            // ダイアログを出してOKボタンを待つ
            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText("流局しました。\nお互いの手牌と待ちを確認してください。");
            }
            ShowNextRoundWait(ticket);
        }

    }
}
