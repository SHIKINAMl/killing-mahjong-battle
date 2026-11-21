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

            // 合言葉を見せるときだけ、文字の後ろに暗い板を敷いて吹き出しより手前に出す（MatchmakingUI）
            bool passcode = data != null && data.mode == "private_host" && !string.IsNullOrEmpty(data.password);
            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting(BuildWaitingMessage(data), passcode);
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
                return BuildPasswordMessage(data.password);
            }

            return DefaultMessage;
        }

        /// <summary>
        /// 部屋を作った側に出す合言葉（2026-10-23 ユーザー指示「部屋を作った時の合言葉がわかりにくいのでわかりやすくして」）。
        ///
        /// 以前は 80pt の白い字を卓と立ち絵の上にそのまま重ねていて、見出しは上の点数の帯に隠れ、
        /// 字体のせいで英大文字が小文字のようにも見え、8 と B、0 と O のような数字と英字も見分けにくかった。
        ///   ・文字の後ろに暗い板を敷き、相手の吹き出しより手前に出す（<see cref="MatchmakingUI"/>）。
        ///     文字の飾りの下地（TMP の mark）も試したが、この字体では薄くしか出ず後ろの絵が透けた
        ///   ・合言葉は1文字ずつ間を空けて同じ幅で並べ、数字だけ金色にする（合言葉は英大文字と数字だけ。サーバーの決まり）
        ///   ・相手がどこで打つか（「あいことばで入る」）も書く
        /// </summary>
        private static string BuildPasswordMessage(string password)
        {
            const string Digit = "#F2C14E";
            var code = new System.Text.StringBuilder();
            bool hasDigit = false;
            foreach (char c in password.ToUpperInvariant())
            {
                if (code.Length > 0) code.Append(' ');
                if (char.IsDigit(c)) { hasDigit = true; code.Append("<color=" + Digit + ">").Append(c).Append("</color>"); }
                else code.Append(c);
            }
            var text = new System.Text.StringBuilder();
            text.Append("<size=40%>あいことば</size>\n");
            text.Append("<mspace=0.6em>").Append(code).Append("</mspace>\n");
            text.Append("<size=30%>");
            if (hasDigit) text.Append("<color=" + Digit + ">金色</color>の文字は数字です\n");
            text.Append("この5文字を相手に伝えてください\n");
            text.Append("相手は「あいことばで入る」から打ちます");
            text.Append("</size>");
            // 下で牌をかき混ぜている（TileClatterEffect）ので、空の行を足して文字の塊を上へ寄せる（字は上下の真ん中に揃う）
            text.Append("\n\n");
            return text.ToString();
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
