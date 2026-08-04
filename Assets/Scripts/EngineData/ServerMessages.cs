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

    /// <summary>
    /// round_end の清算内訳。点数内訳の表示に使う対応は以下（キー名に注意）:
    ///   素点     … winner_bet   （サーバー内部名は winner_effective_bet。賭け金 × 流局持ち越し回数）
    ///   翻数倍率 … multiplier   （満貫1 / 跳満1.5 / 倍満2 / 三倍満3 / 役満4 / 26飜8）
    ///   単騎倍率 … loser_loss_multiplier（単騎待ちなら2、それ以外1。敗者の支払いにだけ掛かる）
    /// 勝者の獲得 = winner_bet × multiplier
    /// 敗者の損失 = loser_bet × multiplier × loser_loss_multiplier
    /// </summary>
    [Serializable]
    public class LiquidationData
    {
        public string winner_id;
        public string loser_id;
        public int base_han;
        public int bonus_han;
        public int han;
        public float multiplier;
        public int winner_bet;   // = winner_effective_bet（持ち越し込みの素点）
        public int loser_bet;    // = loser_effective_bet
        public int winner_gain;
        public int loser_loss;
        public int loser_loss_multiplier; // 単騎倍率
        public bool is_tanki_wait;
        public int carry_over_draw_count;
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
    public class OpeningBoostAssignedMessage
    {
        public string type;
        public OpeningBoostData data;
    }

    [Serializable]
    public class OpeningBoostData
    {
        public OpeningBoostItem[] boosts;
    }

    [Serializable]
    public class OpeningBoostItem
    {
        public string client_id;
        public string yaku_name;
        public int bonus_han;
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


    /// <summary>
    /// "phase_completed_notice": 手牌選択・ベットを確定したプレイヤーの通知。
    /// 確定した本人ぶんだけが1通ずつ届く（両者ぶんがまとめて来るわけではない）。
    /// </summary>
    [Serializable]
    public class PhaseCompletedNoticeMessage
    {
        public string type;
        public PhaseCompletedNoticeData data;
    }
    [Serializable]
    public class PhaseCompletedNoticeData
    {
        /// <summary>"hand_selection" または "bet"</summary>
        public string phase;
        public string player_id;
        /// <summary>phase == "bet" のときのみ入る</summary>
        public int bet_amount;
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
        public DrawPlayerData[] draw_data;
    }
    
    [Serializable]
    public class DrawPlayerData
    {
        public string client_id;
        public int[] hand;
        public int[] waits;
    }

    // --- アクション応答系 ---

    [Serializable]
    public class AgariPendingMessage
    {
        public string type;
        public AgariPendingData data;
    }
    [Serializable]
    public class AgariPendingData
    {
        public string winner_id;
        public string loser_id;
        // サーバーは "tile" というキーで送ってくる（game_session.py の on_agari_pending）。
        // ここを tile_id にしていると JsonUtility が対応付けられず、常に 0 のままになる。
        // 0 は無効値ではなく一萬という正当な牌なので、エラーにならず静かに誤った牌が流れる。
        public int tile;
    }

    [Serializable]
    public class StatusMessage
    {
        public string type;
        public StatusData data;
    }
    [Serializable]
    public class StatusData
    {
        public GameStateData game_state;
        public RoundStateData round_state;
        public PlayerStateData player_state;
        public PlayerStateData opponent_player_state;
    }

    [Serializable]
    public class RoundStateData
    {
        public string status;
    }

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
        public int han; // ★ 追加: 翻数
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

    /// <summary>
    /// "bet_accepted": 自分のベットが受理された合図（送った本人にだけ届く）。
    /// **賭け金の上限と単位はサーバーが決めている。**同じ表が GameRules にもあるが、
    /// あちらはクライアント側の複製で、正はこちら。
    /// </summary>
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
