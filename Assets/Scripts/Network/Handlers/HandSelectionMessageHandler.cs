using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// 手牌選択フェーズ関連:
    /// "hand_selection_completed", "hand_selection_accepted",
    /// "hand_selection_confirmation_required", "is_tenpai", "not_tenpai"。
    /// </summary>
    public class HandSelectionMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types =
        {
            "hand_selection_completed",
            "hand_selection_accepted",
            "hand_selection_confirmation_required",
            "is_tenpai",
            "not_tenpai"
        };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            var board = Managers.BoardStateManager.Instance;

            switch (messageType)
            {
                case "hand_selection_completed":
                    HandleHandSelectionCompleted(jsonString, network);
                    break;

                case "hand_selection_accepted":
                    HandSelectionAcceptedMessage hsaMsg = JsonUtility.FromJson<HandSelectionAcceptedMessage>(jsonString);
                    if (hsaMsg != null && hsaMsg.data != null && hsaMsg.data.waits != null && hsaMsg.data.waits.Length > 0)
                    {
                        var acceptedWaits = new List<int>(hsaMsg.data.waits);
                        board.SetLocalState(null, null, acceptedWaits);
                        board.FireRebuildEvent();
                    }
                    network.RaiseHandSelectionAccepted();
                    break;

                case "hand_selection_confirmation_required":
                    HandSelectionConfirmationMessage confirmMsg = JsonUtility.FromJson<HandSelectionConfirmationMessage>(jsonString);
                    if (confirmMsg != null && confirmMsg.data != null)
                    {
                        network.RaiseHandSelectionConfirmation(confirmMsg.data);
                    }
                    break;

                case "is_tenpai":
                    IsTenpaiMessage tenpaiMsg = JsonUtility.FromJson<IsTenpaiMessage>(jsonString);
                    if (tenpaiMsg != null && tenpaiMsg.data != null && tenpaiMsg.data.waits != null)
                    {
                        var waits = new List<int>();
                        var nonManganList = new List<int>();
                        var waitDataList = new List<WaitData>();

                        foreach (var w in tenpaiMsg.data.waits)
                        {
                            waits.Add(w.tile);
                            waitDataList.Add(w);
                            if (!w.mangan_or_more)
                            {
                                nonManganList.Add(w.tile);
                            }
                        }

                        // 保存
                        board.LocalWaitDataList.Clear();
                        board.LocalWaitDataList.AddRange(waitDataList);

                        board.SetNonManganWaits(nonManganList);
                        // テンパイ確認時(is_tenpai)の段階ではまだ手牌を「決定」していないため、
                        // 左下のプレイヤー情報にはまだ待ち牌を表示しない（hand_selection_acceptedで表示する）

                        network.RaiseIsTenpaiReceived(tenpaiMsg.data);
                    }
                    break;

                case "not_tenpai":
                    board.SetLocalState(null, null, new List<int>());
                    board.FireRebuildEvent();

                    NotTenpaiMessage notTenpaiMsg = JsonUtility.FromJson<NotTenpaiMessage>(jsonString);
                    network.RaiseNotTenpaiReceived(notTenpaiMsg?.message ?? "Hand is not in tenpai");
                    break;
            }
        }

        private void HandleHandSelectionCompleted(string jsonString, NetworkMessageHandler network)
        {
            HandSelectionCompletedMessage msg = JsonUtility.FromJson<HandSelectionCompletedMessage>(jsonString);
            if (msg.data == null || msg.data.hands == null) return;

            var board = Managers.BoardStateManager.Instance;
            string localPlayerId = network.LocalPlayerId;

            var handDict = ServerJsonParser.ParseIntArrays(jsonString, "hand");
            var wallDict = ServerJsonParser.ParseIntArrays(jsonString, "wall");
            var waitDict = ServerJsonParser.ParseIntArrays(jsonString, "wait"); // wait or waits, API doc depends
            if (waitDict.Count == 0) waitDict = ServerJsonParser.ParseIntArrays(jsonString, "waits");

            foreach (var h in msg.data.hands)
            {
                List<int> handTiles = new List<int>();
                List<int> wallTiles = new List<int>();
                List<int> waitTiles = null; // nullで初期化し、空リストで上書きしないようにする

                if (handDict.ContainsKey(h.client_id)) handTiles = handDict[h.client_id];
                if (wallDict.ContainsKey(h.client_id)) wallTiles = wallDict[h.client_id];
                if (waitDict.ContainsKey(h.client_id) && waitDict[h.client_id].Count > 0) waitTiles = waitDict[h.client_id];

                if (h.hand != null) handTiles = new List<int>(h.hand);
                if (h.wall != null) wallTiles = new List<int>(h.wall);
                if (h.waits != null && h.waits.Length > 0) waitTiles = new List<int>(h.waits);

                if (h.client_id == localPlayerId)
                {
                    board.SetLocalState(wallTiles, handTiles, waitTiles);
                }
                else
                {
                    board.SetEnemyState(wallTiles, handTiles);
                }
            }

            board.ClearSelection();
            board.FireRebuildEvent();
        }
    }
}
