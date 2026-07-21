using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// ラウンド終了とゲーム終了:
    /// "agari_pending"（ロン猶予）, "round_end"（流局/和了・清算）,
    /// "next_round_waiting", "next_round_accepted", "game_end"。
    /// </summary>
    public class RoundLifecycleMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types =
        {
            "agari_pending",
            "round_end",
            "next_round_waiting",
            "next_round_accepted",
            "game_end",
            "round_start"
        };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            var board = Managers.BoardStateManager.Instance;

            switch (messageType)
            {
                case "round_start":
                    network.RaiseGameStarted();
                    break;
                    
                case "agari_pending":
                    AgariPendingMessage agariPendingMsg = JsonUtility.FromJson<AgariPendingMessage>(jsonString);
                    if (agariPendingMsg != null && agariPendingMsg.data != null)
                    {
                        network.RaiseAgariPendingReceived(agariPendingMsg.data);
                    }
                    break;

                case "round_end":
                    Debug.Log($"[Network] round_end 受信: {jsonString}");
                    RoundEndMessage reMsg = JsonUtility.FromJson<RoundEndMessage>(jsonString);
                    if (reMsg != null && reMsg.data != null)
                    {
                        Debug.Log($"[Network] round_end パース結果: is_draw={reMsg.data.is_draw}, liquidation={reMsg.data.liquidation != null}");
                        if (reMsg.data.is_draw)
                        {
                            // 流局
                            Debug.Log("[Network] 流局が発生しました");
                            network.RaisePhaseStatusChanged(RoundStatus.Draw);
                            network.RaiseDraw(reMsg.data.draw_data);
                        }
                        else if (!network.AgariProcessed)
                        {
                            network.AgariProcessed = true;
                            // ロン判定 - JsonUtility がネストした liquidation をパースできない場合に手動パースする
                            LiquidationData liq = reMsg.data.liquidation;
                            if (liq == null || string.IsNullOrEmpty(liq.winner_id))
                            {
                                liq = ServerJsonParser.ParseLiquidationFromJson(jsonString);
                            }

                            if (liq != null && !string.IsNullOrEmpty(liq.winner_id))
                            {
                                Debug.Log($"[Network] ロン成立: winner={liq.winner_id}, loser={liq.loser_id}, winner_health={liq.winner_health}, loser_health={liq.loser_health}");
                                bool isLocalWin = (liq.winner_id == network.LocalPlayerId);
                                board.LastIsLocalWin = isLocalWin;
                                board.LastLiquidationData = liq;

                                int newLocalHp = isLocalWin ? liq.winner_health : liq.loser_health;
                                int newEnemyHp = isLocalWin ? liq.loser_health : liq.winner_health;
                                board.UpdateHp(newLocalHp, newEnemyHp);

                                network.RaisePhaseStatusChanged(RoundStatus.Agari);
                                network.RaiseAgari(isLocalWin);
                            }
                            else
                            {
                                Debug.LogWarning("[Network] round_end: is_draw=false だが liquidation データが取得できませんでした");
                            }
                        }
                    }
                    break;

                case "next_round_waiting":
                    var nrwMsg = JsonUtility.FromJson<NextRoundWaitingMessage>(jsonString);
                    if (nrwMsg != null && nrwMsg.data != null)
                    {
                        if (nrwMsg.data.ready_count > 0)
                        {
                            Debug.Log("[Network] 次局進行待ち (ready_count > 0) - 相手が演出を進行させた合図として処理します");
                            network.RaiseNextRoundWaitingReceived(nrwMsg.data);
                        }
                        else
                        {
                            Debug.Log("[Network] 次局進行待ち (ready_count == 0) - ラウンド終了直後の通知のため無視します");
                        }
                    }
                    else
                    {
                        // パース失敗時等のフォールバック
                        network.RaiseNextRoundWaitingReceived(new NextRoundWaitingData() { ready_players = new List<string>() });
                    }
                    break;

                case "next_round_accepted":
                    Debug.Log("[Network] 次局進行承認済み");
                    break;

                case "game_end":
                    if (ServerJsonParser.TryParseFinalScores(jsonString, network.LocalPlayerId, out int localScore, out int enemyScore))
                    {
                        network.RaiseGameEnded(localScore, enemyScore);
                    }
                    break;
            }
        }
    }
}
