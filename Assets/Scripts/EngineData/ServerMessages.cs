using System;
using System.Collections.Generic;

namespace KillingMahjong.EngineData
{
    // --- 汎用ベースメッセージ ---
    [Serializable]
    public class ServerMessageBase
    {
        public string type;
    }

    [Serializable]
    public class ErrorMessage
    {
        public string type;
        public string message;
    }

    // --- 共通データ構造 ---
    [Serializable]
    public class PlayerRef { public string client_id; }

    [Serializable]
    public class LiquidationData
    {
        public string winner_id;
        public string loser_id;
        public int han;
        public float multiplier;
        public int winner_bet;
        public int loser_bet;
        public int winner_gain;
        public int loser_loss;
        public int winner_health;
        public int loser_health;
        public string[] yaku;
    }

    // JsonUtilityはネストした配列をデシリアライズできないためラッパークラスを使用する
    // ただし、Unityのリモートデシリアライズで List<int> は可能ですが、List<List<int>> 等はラッパーが必要です
    [Serializable]
    public class IntArray
    {
        public int[] items;
    }

    // --- 各種イベントデータ構造 ---

    [Serializable]
    public class ConnectedMessage
    {
        public string type;
        public ConnectedData data;
    }
    [Serializable]
    public class ConnectedData
    {
        public string client_id;
    }

    [Serializable]
    public class MatchCancelledMessage
    {
        public string type;
        public MatchCancelledData data;
    }
    [Serializable]
    public class MatchCancelledData
    {
        public string match_id;
        public string reason;
    }

    [Serializable]
    public class GameStartedMessage
    {
        public string type;
        public GameStartedData data;
    }
    [Serializable]
    public class GameStartedData
    {
        public string match_id;
        public PlayerRef[] players;
    }

    [Serializable]
    public class RoundStartMessage
    {
        public string type;
        public int round;
    }

    [Serializable]
    public class PhaseChangeMessage
    {
        public string type;
        public string new_status;
    }

    [Serializable]
    public class DealingCompletedMessage
    {
        public string type;
        public int dora_id;
        public DealingCompletedHand[] hands;
    }
    [Serializable]
    public class DealingCompletedHand
    {
        public string client_id;
        public int[] wall;
        public int[] tenpai_examples;
    }

    [Serializable]
    public class HandSelectionCompletedMessage
    {
        public string type;
        public HandSelectionCompletedData data;
    }
    [Serializable]
    public class HandSelectionCompletedData
    {
        public HandData[] hands;
    }
    [Serializable]
    public class HandData
    {
        public string client_id;
        public int[] hand;
        public int[] waits;
        public int[] wall;
    }

    [Serializable]
    public class HandSelectionConfirmationMessage
    {
        public string type;
        public HandSelectionConfirmationData data;
    }
    
    [Serializable]
    public class HandSelectionConfirmationData
    {
        public string reason;
        public string message;
        public List<int> hand_indexes;
        public WaitData[] waits;
    }

    [Serializable]
    public class BetCompletedMessage
    {
        public string type;
        public BetCompletedData data;
    }
    [Serializable]
    public class BetCompletedData
    {
        public PlayerBetData[] bets;
    }
    [Serializable]
    public class PlayerBetData
    {
        public string client_id;
        public int bet;
    }

    [Serializable]
    public class DiscardPhaseStartedMessage
    {
        public string type;
        public DiscardPhaseStartedData data;
    }
    [Serializable]
    public class DiscardPhaseStartedData
    {
        public string first_player;
    }

    [Serializable]
    public class DiscardCompletedMessage
    {
        public string type;
        public DiscardCompletedData data;
    }
    [Serializable]
    public class DiscardCompletedData
    {
        public string player_id;
        public int tile;
    }

    [Serializable]
    public class RoundEndMessage
    {
        public string type;
        public RoundEndData data;
    }
    [Serializable]
    public class RoundEndData
    {
        public bool is_draw;
        public LiquidationData liquidation;
    }

    // --- アクション応答系 ---

    [Serializable]
    public class IsTenpaiMessage
    {
        public string type;
        public IsTenpaiData data;
    }
    [Serializable]
    public class IsTenpaiData
    {
        public WaitData[] waits;
    }
    [Serializable]
    public class WaitData
    {
        public int tile;
        public bool mangan_or_more;
        public string[] yaku;
    }

    [Serializable]
    public class NotTenpaiMessage
    {
        public string type;
        public string message;
    }

    [Serializable]
    public class HandSelectionAcceptedMessage
    {
        public string type;
        public HandData data; // サーバーの仕様では単一HandDataが返る
    }

    [Serializable]
    public class BetAcceptedMessage
    {
        public string type;
        public BetAcceptedData data;
    }
    [Serializable]
    public class BetAcceptedData
    {
        public int bet_amount;
        public int max_bet;
        public int bet_unit;
    }

    [Serializable]
    public class DiscardAcceptedMessage
    {
        public string type;
        public DiscardAcceptedData data;
    }
    [Serializable]
    public class DiscardAcceptedData
    {
        public int wall_index;
        public int tile;
        public bool is_win;
        public LiquidationData liquidation;
    }

    [Serializable]
    public class SpecialVictoryMessage
    {
        public string type;
        public SpecialVictoryData data;
    }
    [Serializable]
    public class SpecialVictoryData
    {
        public string player_id;
    }

    [Serializable]
    public class GameEndMessage
    {
        public string type;
        // Parsing of final_scores dictionary is handled manually via string operations
    }
}
