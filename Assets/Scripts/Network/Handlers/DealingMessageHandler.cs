using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "dealing_completed": 配牌完了。各プレイヤーの山とテンパイお手本を盤面状態へ反映する。
    /// </summary>
    public class DealingMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "dealing_completed" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            // 既存実装と同じく、盤面反映より先に完了イベントを通知する
            network.RaiseDealingCompleted();

            DealingCompletedMessage msg = JsonUtility.FromJson<DealingCompletedMessage>(jsonString);
            if (msg.hands == null) return;

            var board = Managers.BoardStateManager.Instance;
            string localPlayerId = network.LocalPlayerId;

            Debug.Log($"[Network] サーバーからのJSON: {jsonString}");

            var tenpaiDict = ServerJsonParser.ParseTenpaiExamples(jsonString);
            var tenpaiExamples = new List<int[]>();

            if (tenpaiDict.ContainsKey(localPlayerId))
            {
                tenpaiExamples = tenpaiDict[localPlayerId];
            }

            Debug.Log($"[Network] 抽出されたお手本の数: {tenpaiExamples.Count}");
            for (int i = 0; i < tenpaiExamples.Count; i++)
            {
                Debug.Log($"[Network] お手本 {i}: [{string.Join(", ", tenpaiExamples[i])}]");
            }

            board.SetTenpaiExamples(tenpaiExamples);

            // ドラ表示牌を保存
            board.CurrentDoraId = msg.dora_id;

            // 手動独自パースで wall 配列を抽出 (JsonUtilityが int[] を上手くさばけない場合のフェールセーフ)
            var wallDict = ServerJsonParser.ParseIntArrays(jsonString, "wall");

            foreach (var h in msg.hands)
            {
                List<int> wallTiles = new List<int>();
                if (wallDict.ContainsKey(h.client_id)) wallTiles = wallDict[h.client_id];
                // JsonUtility が取得できていればそちらを優先
                if (h.wall != null && h.wall.Length > 0) wallTiles = new List<int>(h.wall);

                if (h.client_id == localPlayerId)
                {
                    board.SetLocalState(wallTiles, new List<int>());
                }
                else
                {
                    board.SetEnemyState(wallTiles, new List<int>());
                }
            }
            board.FireRebuildEvent();
        }
    }
}
