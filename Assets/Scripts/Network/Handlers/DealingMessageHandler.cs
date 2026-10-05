using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.UI;
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
            // サーバーの配牌が速くなると、phase_change(dealing) の暗転が終わる前に
            // dealing_completed が届く。暗転の完了コールバックは前局の牌を消すため、
            // ここで先に盤面へ入れると **新しい配牌まで後から消されてしまう**。
            //
            // 局頭のリセットが済むまで payload 全体を保留し、完了後に状態・描画・
            // 暗転解除の順で適用する。サーバー側の到着順を前提にしない。
            var uiManager = Object.FindFirstObjectByType<GameUIManager>();
            if (uiManager != null && (uiManager.IsBusyWithTransition
                || (uiManager.PhaseTransitionUI != null && uiManager.PhaseTransitionUI.IsRoundStartResetPending)))
            {
                uiManager.DeferUntilIdle(
                    "dealingCompleted",
                    // 同じ保留の前に別のフェイズ遷移が始まることがあるため、
                    // 実行時にも改めて busy 状態を確認する。
                    () => Handle(messageType, jsonString, network));
                return;
            }

            ApplyDealingCompleted(jsonString, network);
        }

        private static void ApplyDealingCompleted(string jsonString, NetworkMessageHandler network)
        {

            DealingCompletedMessage msg = JsonUtility.FromJson<DealingCompletedMessage>(jsonString);
            if (msg == null || msg.hands == null) return;

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

            // PhaseTransitionUI はこのイベントで局頭の暗転を解除する。
            // 盤面反映より先に発火すると、表示がないまま暗転だけが明けるため、
            // FireRebuildEvent の後に通知する。
            network.RaiseDealingCompleted();
        }
    }
}
