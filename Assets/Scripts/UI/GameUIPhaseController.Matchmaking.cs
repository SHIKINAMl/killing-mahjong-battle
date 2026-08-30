using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // マッチング待ちと、相手が来なかったときの後始末。GameUIPhaseController から分離（partial）。
    public partial class GameUIPhaseController
    {

        public void ShowMatchmakingWaiting(KillingMahjong.EngineData.MatchingWaitingData data = null)
        {
            Debug.Log("[GameUIPhaseController] ShowMatchmakingWaiting called.");
            if (uiManager != null && uiManager.IsTutorialMode) return; // チュートリアル中は非表示

            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting(BuildWaitingMessage(data));
            ShowMatchmakingWaitingBody();
        }

        /// <summary>
        /// 待機画面に出す文言を組む。
        ///
        /// **部屋を作った側（private_host）は合言葉を出さないと意味がない。**
        /// 相手はこの5文字を打ち込んで入ってくる。
        /// 合言葉で入った側にはそもそもこのメッセージが来ない（即マッチするため）。
        /// </summary>
        private static string BuildWaitingMessage(KillingMahjong.EngineData.MatchingWaitingData data)
        {
            const string DefaultMessage = "Waiting for Opponent\n対戦相手を待っています...";

            if (data == null) return DefaultMessage;

            if (data.mode == "private_host" && !string.IsNullOrEmpty(data.password))
            {
                return $"あいことば\n<size=150%>{data.password}</size>\n\nこの5文字を相手に伝えてください";
            }

            return DefaultMessage;
        }

        /// <summary>待機中に盤面を片付ける。文言だけ差し替えたいときと分けている。</summary>
        private void ShowMatchmakingWaitingBody()
        {
            SetMatchUIVisibility(false);
            
            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.gameObject.SetActive(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.gameObject.SetActive(false);
            if (uiManager.BettingUI != null) uiManager.BettingUI.HideBettingPhase(true);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }

        public void ShowMatchCancelled(string reason)
        {
            uiManager.ClearAllTiles();
            BoardStateManager.Instance.ClearAllBoardData();
            uiManager.SetCurrentPhaseStatus(RoundStatus.None);

            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting(reason);
            SetMatchUIVisibility(false);
            
            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.gameObject.SetActive(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            
            if (uiManager.DialogueUI != null) 
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText(reason);
            }
            
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.gameObject.SetActive(false);
            if (uiManager.BettingUI != null) uiManager.BettingUI.HideBettingPhase(true);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }
    }
}
