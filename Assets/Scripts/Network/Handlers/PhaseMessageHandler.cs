using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// フェーズ遷移: "phase_change", "discard_phase_started"。
    /// </summary>
    public class PhaseMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "phase_change", "discard_phase_started" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            switch (messageType)
            {
                case "phase_change":
                    PhaseChangeMessage pMsg = JsonUtility.FromJson<PhaseChangeMessage>(jsonString);
                    if (pMsg != null)
                    {
                        if (pMsg.new_status == "dealing")
                        {
                            network.AgariProcessed = false; // 新ラウンドでリセット
                            network.RaisePhaseStatusChanged(RoundStatus.Dealing);
                            network.RaiseDealingStarted();
                        }
                        else if (pMsg.new_status == "hand_selection") network.RaisePhaseStatusChanged(RoundStatus.HandSelection);
                        else if (pMsg.new_status == "betting") network.RaisePhaseStatusChanged(RoundStatus.Betting);
                        else if (pMsg.new_status == "discard") network.RaisePhaseStatusChanged(RoundStatus.Discard);
                    }
                    break;

                case "discard_phase_started":
                    DiscardPhaseStartedMessage dpsMsg = JsonUtility.FromJson<DiscardPhaseStartedMessage>(jsonString);
                    bool isLocalTurn = (dpsMsg != null && dpsMsg.data != null && dpsMsg.data.first_player == network.LocalPlayerId);
                    Managers.BoardStateManager.Instance.SetLocalPlayerFirstRound(isLocalTurn);
                    Managers.BoardStateManager.Instance.SetLocalTurn(isLocalTurn);
                    network.RaisePhaseStatusChanged(RoundStatus.Discard);
                    break;
            }
        }
    }
}
