using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// 打牌関連: "discard_completed"（打牌確定・手番交代）,
    /// "discard_accepted"（自分の打牌受理。ロン成立時は清算まで行う）。
    /// </summary>
    public class DiscardMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "discard_completed", "discard_accepted" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            var board = Managers.BoardStateManager.Instance;

            switch (messageType)
            {
                case "discard_completed":
                    DiscardCompletedMessage discardMsg = JsonUtility.FromJson<DiscardCompletedMessage>(jsonString);
                    if (discardMsg != null && discardMsg.data != null)
                    {
                        bool isLocal = (discardMsg.data.player_id == network.LocalPlayerId);
                        if (isLocal)
                        {
                            board.SetLocalTurn(false);
                        }
                        else
                        {
                            board.SetLocalTurn(true);
                        }
                        network.RaiseTileDiscarded(discardMsg.data.tile, isLocal);
                    }
                    break;

                case "discard_accepted":
                    DiscardAcceptedMessage daMsg = JsonUtility.FromJson<DiscardAcceptedMessage>(jsonString);
                    if (daMsg != null && daMsg.data != null)
                    {
                        if (daMsg.data.is_win && !network.AgariProcessed)
                        {
                            // ロン判定時のみ、フェーズがAgariに変わる前に打牌を反映させる
                            if (daMsg.data.tile > 0)
                            {
                                network.RaiseTileDiscarded(daMsg.data.tile, true);
                            }
                            network.AgariProcessed = true;
                            Debug.Log($"[Network] discard_accepted: ロン成立 (is_win=true)");
                            // discard_accepted にも liquidation が含まれている場合はここで処理
                            LiquidationData daLiq = daMsg.data.liquidation;
                            if (daLiq == null || string.IsNullOrEmpty(daLiq.winner_id))
                            {
                                daLiq = ServerJsonParser.ParseLiquidationFromJson(jsonString);
                            }
                            if (daLiq != null && !string.IsNullOrEmpty(daLiq.winner_id))
                            {
                                Debug.Log($"[Network] discard_accepted ロン: winner={daLiq.winner_id}");
                                bool isLocalWinDa = (daLiq.winner_id == network.LocalPlayerId);
                                board.LastIsLocalWin = isLocalWinDa;
                                board.LastLiquidationData = daLiq;

                                int newLocalHpDa = isLocalWinDa ? daLiq.winner_health : daLiq.loser_health;
                                int newEnemyHpDa = isLocalWinDa ? daLiq.loser_health : daLiq.winner_health;
                                board.UpdateHp(newLocalHpDa, newEnemyHpDa);

                                network.RaisePhaseStatusChanged(RoundStatus.Agari);
                                network.RaiseAgari(isLocalWinDa);
                            }
                        }
                    }
                    break;
            }
        }
    }
}
