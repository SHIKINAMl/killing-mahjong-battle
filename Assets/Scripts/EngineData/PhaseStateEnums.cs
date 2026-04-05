using System;

namespace KillingMahjong.EngineData
{
    public enum GameStatus
    {
        Waiting,
        Playing,
        GameEnd
    }

    public enum RoundStatus
    {
        None,
        Dealing,
        HandSelection,
        Betting,
        TurnDecision,
        Discard,
        Liquidation,
        Agari,
        Ron,
        Result
    }

    public enum PlayerStatus
    {
        Waiting,
        Active,
        Inactive
    }

    public static class PhaseStateExtensions
    {
        public static RoundStatus ParseRoundStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return RoundStatus.None;
            
            switch (status.ToLower())
            {
                case "dealing": return RoundStatus.Dealing;
                case "hand_selection": return RoundStatus.HandSelection;
                case "betting": return RoundStatus.Betting;
                case "turn_decision": return RoundStatus.TurnDecision;
                case "discard": return RoundStatus.Discard;
                case "liquidation": return RoundStatus.Liquidation;
                case "agari": return RoundStatus.Agari;
                case "ron": return RoundStatus.Ron;
                case "result": return RoundStatus.Result;
                default: return RoundStatus.None;
            }
        }

        public static string ToStatusString(this RoundStatus status)
        {
            switch (status)
            {
                case RoundStatus.Dealing: return "dealing";
                case RoundStatus.HandSelection: return "hand_selection";
                case RoundStatus.Betting: return "betting";
                case RoundStatus.TurnDecision: return "turn_decision";
                case RoundStatus.Discard: return "discard";
                case RoundStatus.Liquidation: return "liquidation";
                case RoundStatus.Agari: return "agari";
                case RoundStatus.Ron: return "ron";
                case RoundStatus.Result: return "result";
                default: return "none";
            }
        }
    }
}
