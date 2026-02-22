using System;

namespace KillingMahjong.EngineData
{
    [Serializable]
    public class GameStateData
    {
        public int status;
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
        public int health;
        public int[] hand;
        public int[] wall;
        public int[] wait;
        public int[] discards;
    }
}
