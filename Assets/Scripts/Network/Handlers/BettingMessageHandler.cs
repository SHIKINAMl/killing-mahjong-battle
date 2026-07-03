using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "bet_completed": ベット確定。双方のベット額をHPへ反映する。
    /// </summary>
    public class BettingMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "bet_completed" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
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

            board.UpdateHp(currentLocalHp - pBet, currentEnemyHp - eBet);
            network.RaiseBettingComplete(pBet, eBet, currentLocalHp, currentEnemyHp);
        }
    }
}
