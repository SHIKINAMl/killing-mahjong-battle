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
                    HandleMatchCancelled(jsonString, network);
                    break;

                case "game_started":
                    network.RaiseGameStarted();
                    break;
            }
        }

        /// <summary>
        /// 対局が打ち切られたとき。**理由で扱いが変わる。**
        ///
        /// サーバーが待機列へ並べ直してくれるかどうかが、理由ごとに違う。
        ///
        ///   player_disconnected … **サーバーが並べ直す**（`_waiting_queue.append`）。
        ///                         こちらから `join` を送ると二重に並ぶので送らない。
        ///   idle_timeout        … **並べ直してくれない**（`_cancel_idle_matches` は
        ///                         `cleanup_match_locked` を呼ぶだけ）。繋ぎっぱなしのまま
        ///                         誰とも組まれないので、こちらから入り直す。
        ///
        /// `idle_timeout` は 2026-09-30 の `nami-engine8` で入った。無操作が
        /// `MATCH_IDLE_TIMEOUT_SEC`（既定 30分）続くとマッチが捨てられる。
        /// それまでは「通信が切断されました。マッチング待機中です...」と出ていたが、
        /// **切断もしていないし待機もしていない**ので、二重に嘘になっていた。
        /// </summary>
        private static void HandleMatchCancelled(string jsonString, NetworkMessageHandler network)
        {
            MatchCancelledMessage cancelMsg = JsonUtility.FromJson<MatchCancelledMessage>(jsonString);
            string reason = cancelMsg != null && cancelMsg.data != null ? cancelMsg.data.reason : null;

            string text;
            bool rejoinSelf;

            switch (reason)
            {
                case "player_disconnected":
                    text = "対戦相手が切断しました。マッチング待機中です...";
                    rejoinSelf = false;
                    break;

                case "idle_timeout":
                    text = "長い間操作がなかったため、対局を終了しました。相手を探し直しています...";
                    rejoinSelf = true;
                    break;

                default:
                    // 知らない理由。**送り直さない。** サーバーが並べ直す側だった場合に
                    // 二重に並ぶより、待たせて気づいてもらうほうが害が小さい
                    text = "通信が切断されました。マッチング待機中です...";
                    rejoinSelf = false;
                    Debug.LogWarning($"[MatchLifecycleMessageHandler] 知らない reason: {reason}");
                    break;
            }

            network.RaiseMatchCancelled(text);

            if (rejoinSelf && WebSocketGameClientSample.Instance != null)
            {
                WebSocketGameClientSample.Instance.RequestRejoin();
            }
        }
    }
}
