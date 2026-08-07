using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "bet_completed": ベット確定。双方のベット額をHPへ反映する。
    /// "bet_accepted": 自分のベットが受理された合図（送った本人にだけ届く）。
    /// </summary>
    public class BettingMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "bet_completed", "bet_accepted" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            if (messageType == "bet_accepted")
            {
                // 「準備完了」の表示と、賭け金の上限・単位の取り込みに使う。盤面は動かさない
                BetAcceptedMessage aMsg = JsonUtility.FromJson<BetAcceptedMessage>(jsonString);
                if (aMsg != null && aMsg.data != null)
                {
                    Managers.BoardStateManager.Instance.SetServerBetRules(aMsg.data.max_bet, aMsg.data.bet_unit);
                }
                network.RaiseLocalBetAccepted();
                return;
            }

            BetCompletedMessage bMsg = JsonUtility.FromJson<BetCompletedMessage>(jsonString);
            if (bMsg == null || bMsg.data == null || bMsg.data.bets == null) return;

            var board = Managers.BoardStateManager.Instance;

            int pBet = 0; int eBet = 0;
            foreach (var b in bMsg.data.bets)
            {
                if (b.client_id == network.LocalPlayerId) pBet = b.bet;
                else eBet = b.bet;
            }
            int currentLocalHp = board.LocalPlayerHp;
            int currentEnemyHp = board.EnemyPlayerHp;

            // **クライアントでは血を引かない（2026-08-04 決定）。**
            // 血はサーバーが送ってきた値だけで動かす。詳細は BoardStateManager.UseServerHealth。
            //
            // サーバーは今のところベットで health を減らさないので、賭けても血は動かない。
            // それを隠さずそのまま出す、というのが今の方針（SERVER_REQUESTS の A-8 で依頼中）。
            if (!Managers.BoardStateManager.UseServerHealth)
            {
                board.UpdateHp(currentLocalHp - pBet, currentEnemyHp - eBet);
            }

            network.RaiseBettingComplete(pBet, eBet, currentLocalHp, currentEnemyHp);

            // サーバー値を使うなら、決済後の血を取り寄せる。
            // bet_completed 自体には health が入っていないため（SERVER_REQUESTS の A-8 で依頼済み）
            if (Managers.BoardStateManager.UseServerHealth)
            {
                network.SendActionToServer("status", null);
            }
        }
    }
}
