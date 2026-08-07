using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "phase_completed_notice": 手牌選択・ベットを確定したプレイヤーの通知。
    ///
    /// 「準備完了」の表示にだけ使う。盤面の状態は動かさない。
    /// 確定した本人ぶんが1通ずつ届くので、届いた player_id の側にだけ印を付ける。
    /// </summary>
    public class PhaseCompletedNoticeMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "phase_completed_notice" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            PhaseCompletedNoticeMessage msg = JsonUtility.FromJson<PhaseCompletedNoticeMessage>(jsonString);
            if (msg == null || msg.data == null) return;
            if (string.IsNullOrEmpty(msg.data.player_id) || string.IsNullOrEmpty(msg.data.phase))
            {
                Debug.LogWarning($"[PhaseCompletedNotice] phase / player_id が空です: {jsonString}");
                return;
            }

            // 届いていることを目視できるようにしておく。サーバー側の反映確認がこれだけで済む
            Debug.Log($"[PhaseCompletedNotice] phase={msg.data.phase} player={msg.data.player_id} bet={msg.data.bet_amount}");
            network.RaisePhaseCompletedNotice(msg.data);
        }
    }
}
