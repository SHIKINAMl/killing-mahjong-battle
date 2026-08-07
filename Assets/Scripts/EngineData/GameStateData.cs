using System;

namespace KillingMahjong.EngineData
{
    [Serializable]
    public class GameStateData
    {
        public string status;
        public int round;
        public int honba;
        public int dora_id;
        public string current_player;
        public PlayerStateData[] players;
    }

    [Serializable]
    public class PlayerStateData
    {
        public string id;
        public string player_id;
        public int health;
        public int[] hand;
        public int[] wall;
        public int[] wait;
        public int[] waits;
        public int[] discards;
        public int[] discarded_wall_indexes;
        public int bet;

        /// <summary>
        /// 勝利で得た額だけを足した累計。**30000 に到達するとサーバーが対局を終わらせる**
        /// （`game_end` の `victory_method` が `cumulative_earned_points`）。
        /// 2026-08-04 に追加。ScoreGaugeUI はこれと別に自前で積んでいるので、
        /// 表示を正にしたいならこちらへ寄せる。
        /// </summary>
        public int cumulative_earned_points;

        public int special_victory_count;
        public int[] exposed_hand_indexes;
    }
}
