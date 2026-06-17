using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class StatusDebugUI : MonoBehaviour
    {
        private void FetchAndLog(System.Action<PlayerStateData> onLog)
        {
            if (KillingMahjong.Network.NetworkMessageHandler.Instance == null)
            {
                Debug.LogWarning("[StatusDebug] NetworkMessageHandler.Instance is null");
                return;
            }

            System.Action<StatusData> handler = null;
            handler = (data) => {
                KillingMahjong.Network.NetworkMessageHandler.Instance.OnStatusReceived -= handler;
                if (data != null && data.player_state != null)
                {
                    onLog?.Invoke(data.player_state);
                }
            };
            KillingMahjong.Network.NetworkMessageHandler.Instance.OnStatusReceived += handler;
            KillingMahjong.Network.NetworkMessageHandler.Instance.SendActionToServer("status", null);
        }

        public void LogPlayerId() => FetchAndLog(p => Debug.Log($"[Status] player_id: {p.player_id ?? p.id}"));
        public void LogHand() => FetchAndLog(p => Debug.Log($"[Status] hand: {(p.hand != null ? string.Join(", ", p.hand) : "null")}"));
        public void LogWall() => FetchAndLog(p => Debug.Log($"[Status] wall: {(p.wall != null ? string.Join(", ", p.wall) : "null")}"));
        public void LogWaits() => FetchAndLog(p => Debug.Log($"[Status] waits: {(p.waits != null ? string.Join(", ", p.waits) : (p.wait != null ? string.Join(", ", p.wait) : "null"))}"));
        public void LogDiscards() => FetchAndLog(p => Debug.Log($"[Status] discards: {(p.discards != null ? string.Join(", ", p.discards) : "null")}"));
        public void LogDiscardedWallIndexes() => FetchAndLog(p => Debug.Log($"[Status] discarded_wall_indexes: {(p.discarded_wall_indexes != null ? string.Join(", ", p.discarded_wall_indexes) : "null")}"));
        public void LogHealth() => FetchAndLog(p => Debug.Log($"[Status] health: {p.health}"));
        public void LogBet() => FetchAndLog(p => Debug.Log($"[Status] bet: {p.bet}"));
        public void LogSpecialVictoryCount() => FetchAndLog(p => Debug.Log($"[Status] special_victory_count: {p.special_victory_count}"));
        public void LogBoostHandBonus() => FetchAndLog(p => Debug.Log($"[Status] boost_hand_bonus: (JsonUtilityではDictionaryのパースができないため省略されます)"));
        public void LogExposedHandIndexes() => FetchAndLog(p => Debug.Log($"[Status] exposed_hand_indexes: {(p.exposed_hand_indexes != null ? string.Join(", ", p.exposed_hand_indexes) : "null")}"));
    }
}
