using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "status": 自分・相手の盤面状態（山・手牌・待ち・公開牌・ブースト）の同期。
    /// </summary>
    public class StatusMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "status" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            StatusMessage statusMsg = JsonUtility.FromJson<StatusMessage>(jsonString);
            if (statusMsg == null || statusMsg.data == null) return;

            bool shouldRebuild = false;
            var board = Managers.BoardStateManager.Instance;

            // 文字列抽出による boost_hand_bonus の簡単な取得 (JsonUtility制約回避用)
            if (statusMsg.data.player_state != null)
            {
                board.LocalPlayerSpecialVictoryCount = statusMsg.data.player_state.special_victory_count;
                var bonusDict = ServerJsonParser.ParseBoostHandBonusFromStateKey(jsonString, "player_state");
                if (bonusDict != null)
                {
                    board.LocalBoostHandBonus = bonusDict;
                }

                if (statusMsg.data.player_state.wall != null && statusMsg.data.player_state.hand != null)
                {
                    var localWall = new List<int>(statusMsg.data.player_state.wall);
                    var localHand = new List<int>();

                    if (statusMsg.data.game_state != null && statusMsg.data.game_state.status == "hand_selection")
                    {
                        var currentHand = board.CurrentHandTiles;
                        if (currentHand != null && currentHand.Count > 0)
                        {
                            localHand.AddRange(currentHand);
                        }
                        else
                        {
                            var targetIndexes = board.TargetHandIndexes;
                            if (targetIndexes != null)
                            {
                                foreach (var idx in targetIndexes)
                                {
                                    if (idx >= 0 && idx < localWall.Count)
                                    {
                                        localHand.Add(localWall[idx]);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        var localHandIndices = statusMsg.data.player_state.hand;
                        if (localHandIndices != null)
                        {
                            foreach (var idx in localHandIndices)
                            {
                                if (idx >= 0 && idx < localWall.Count)
                                {
                                    localHand.Add(localWall[idx]);
                                }
                            }
                        }
                    }

                    var waitTiles = statusMsg.data.player_state.waits ?? statusMsg.data.player_state.wait;
                    if (waitTiles != null && waitTiles.Length == 0) waitTiles = null;

                    board.SetLocalState(
                        localWall,
                        localHand,
                        waitTiles != null ? new List<int>(waitTiles) : null
                    );

                    board.ExposedLocalHandWallIndexes.Clear();
                    if (statusMsg.data.player_state.exposed_hand_indexes != null)
                    {
                        foreach (var idx in statusMsg.data.player_state.exposed_hand_indexes)
                        {
                            board.ExposedLocalHandWallIndexes.Add(idx);
                        }
                    }

                    shouldRebuild = true;
                }
            }

            if (statusMsg.data.opponent_player_state != null)
            {
                var bonusDict = ServerJsonParser.ParseBoostHandBonusFromStateKey(jsonString, "opponent_player_state");
                if (bonusDict != null)
                {
                    board.EnemyBoostHandBonus = bonusDict;
                }

                if (statusMsg.data.opponent_player_state.wall != null && statusMsg.data.opponent_player_state.hand != null)
                {
                    var enemyWaitTiles = statusMsg.data.opponent_player_state.waits ?? statusMsg.data.opponent_player_state.wait;
                    if (enemyWaitTiles != null && enemyWaitTiles.Length == 0) enemyWaitTiles = null;

                    var enemyWall = new List<int>(statusMsg.data.opponent_player_state.wall);
                    var enemyHandIndices = statusMsg.data.opponent_player_state.hand;
                    var enemyHand = new List<int>();

                    if (statusMsg.data.game_state == null || statusMsg.data.game_state.status != "hand_selection")
                    {
                        if (enemyHandIndices != null)
                        {
                            foreach (var idx in enemyHandIndices)
                            {
                                if (idx >= 0 && idx < enemyWall.Count) enemyHand.Add(enemyWall[idx]);
                            }
                        }
                    }

                    board.SetEnemyState(
                        enemyWall,
                        enemyHand,
                        enemyWaitTiles != null ? new List<int>(enemyWaitTiles) : null
                    );

                    board.ExposedEnemyHandWallIndexes.Clear();
                    if (statusMsg.data.opponent_player_state.exposed_hand_indexes != null)
                    {
                        foreach (var idx in statusMsg.data.opponent_player_state.exposed_hand_indexes)
                        {
                            board.ExposedEnemyHandWallIndexes.Add(idx);
                        }
                    }

                    shouldRebuild = true;
                }
            }

            if (shouldRebuild)
            {
                board.FireRebuildEvent();
            }

            // 恒常強化された役のパース等、後でUI表示用に利用
            network.RaiseStatusReceived(statusMsg.data);
        }
    }
}
