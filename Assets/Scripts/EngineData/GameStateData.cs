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
        public int special_victory_count;
        public int[] exposed_hand_indexes;
    }
}
