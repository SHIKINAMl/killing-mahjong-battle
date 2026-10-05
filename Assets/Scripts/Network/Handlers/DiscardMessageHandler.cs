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
                            LiquidationMessageApplier.TryApply(daMsg.data.liquidation, jsonString, network,
                                beforeApply: () => {
                                    // 一萬のID=0も有効。清算によるフェイズ変更より先に打牌を反映する。
                                    if (daMsg.data.tile >= 0) network.RaiseTileDiscarded(daMsg.data.tile, true);
                                });
                        }
                    }
                    break;
            }
        }
    }
}
