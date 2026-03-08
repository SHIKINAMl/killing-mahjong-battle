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

    // --- 各種イベントデータ構造 ---
    // (Pythonの `{"type": "connected", "data": {"client_id": "C0001"}}` など)
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

    // `ping` や `matching_waiting` は data不要/汎用ベースでパース可能

    [Serializable]
    public class WallDealtMessage
    {
        public string type;
        public int dora_id;
        public WallDealtHand[] hands;
    }
    [Serializable]
    public class WallDealtHand
    {
        public string client_id;
        public int[] hand;
        public int[] tenpai_examples;
    }

    [Serializable]
    public class RoundStartMessage
    {
        public string type;
        public int round;
    }

    [Serializable]
    public class HandSelectedMessage
    {
        public string type;
        public HandSelectedData[] hands;
    }
    
    [Serializable]
    public class HandSelectedData
    {
        public string client_id;
        public int[] hand;
        public int[] wait;
        public int[] wall;
    }
    
    [Serializable]
    public class TurnDecidedMessage
    {
        public string type;
        public int current_player; 
    }
    
    [Serializable]
    public class GameEndMessage
    {
        public string type;
        // Python: {"client_id1": score1, "client_id2": score2} という辞書なので専用または独自パース必要
        // TODO: Dictionary対応または string処理
    }
    
    // アクション送信用のJSON定義は GameUIManager 内部に定義されていますが、
    // 追加の `is_tenpai` 受信用等が必要ならここに追加します
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
    public class DiscardMessage
    {
        public string type;
        public string client_id;
        public int tile;
    }
}
