using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// マッチングと対戦開始/中断: "matching_waiting", "match_cancelled", "game_started"。
    /// </summary>
    public class MatchLifecycleMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "matching_waiting", "match_cancelled", "game_started" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            switch (messageType)
            {
                case "matching_waiting":
                    MatchingWaitingMessage waitMsg = JsonUtility.FromJson<MatchingWaitingMessage>(jsonString);
                    MatchingWaitingData waitData = waitMsg != null ? waitMsg.data : null;
                    Debug.Log("[MatchLifecycleMessageHandler] matching_waiting received. " +
                              $"mode={waitData?.mode} queue={waitData?.queue_position}/{waitData?.queue_size}");
                    network.RaiseMatchmakingWaiting(waitData);
                    break;

                case "match_cancelled":
                    MatchCancelledMessage cancelMsg = JsonUtility.FromJson<MatchCancelledMessage>(jsonString);
                    string reasonMsg = "通信が切断されました。マッチング待機中です...";
                    if (cancelMsg != null && cancelMsg.data != null && cancelMsg.data.reason == "player_disconnected")
                    {
                        reasonMsg = "対戦相手が切断しました。マッチング待機中です...";
                    }
                    network.RaiseMatchCancelled(reasonMsg);
                    break;

                case "game_started":
                    network.RaiseGameStarted();
                    break;
            }
        }
    }
}
