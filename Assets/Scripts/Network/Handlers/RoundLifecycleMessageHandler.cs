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
            "game_end"
        };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            var board = Managers.BoardStateManager.Instance;

            switch (messageType)
            {
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

                                // 演出は「減る前 → 減った後」を見せる。後はサーバーの確定値だが、
                                // 前は上書きしてしまうと分からなくなるので、ここで控えておく。
                                // 逆算（後 − 獲得）に頼ると、強襲のように獲得と損失が
                                // 非対称になる仕様が入ったときに静かにずれる
                                board.RememberHpBeforeLiquidation();

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
                    if (ServerJsonParser.TryParseGameEnd(jsonString, network.LocalPlayerId, out GameEndInfo endInfo))
                    {
                        if (!endInfo.LocalScoreFound || !endInfo.EnemyScoreFound)
                        {
                            // **ここが「勝ったのに敗北画面」の入口だった。**
                            // 自分の client_id が final_scores のキーと一致しないと両者 0 のまま
                            // 決着処理に流れ、HP の大小で勝敗を決められなくなる。
                            // 決着そのものは捨てず、後段が最後に届いた HP で補えるよう記録だけ残す。
                            Debug.LogWarning(
                                $"[Network] game_end の final_scores に ID が見つかりません。" +
                                $"local={network.LocalPlayerId} 自分={endInfo.LocalScoreFound} 相手={endInfo.EnemyScoreFound}: " + jsonString);
                        }
                        network.RaiseGameEnded(endInfo);
                    }
                    else
                    {
                        // **黙って捨てない。** final_scores を解釈できないと決着処理がまるごと落ち、
                        // 「HPが0でも敗北しない」ように見える（2026-08-19 の不具合報告 R-4 の候補）。
                        // サーバーが game_end を送っていないのか、形が変わって読めないだけなのかを
                        // 切り分けられるよう、受信した本文ごと必ず記録する。
                        Debug.LogWarning("[Network] game_end を受信したが final_scores を解釈できず、決着処理を行わなかった: " + jsonString);
                    }
                    break;
            }
        }
    }
}
