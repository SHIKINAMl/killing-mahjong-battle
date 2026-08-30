using UnityEngine;

namespace KillingMahjong.Core
{
    /// <summary>
    /// 対局をまたいで残る通算戦績。PlayerPrefs だけで完結し、サーバーには一切依存しない。
    ///
    /// SettingsManager と違って MonoBehaviour にしていないのは、
    /// シーン上のオブジェクト配置なしでどこからでも記録できるようにするため。
    /// </summary>
    public static class PlayerStatsManager
    {
        private const string KeyWins = "Stats_Wins";
        private const string KeyLosses = "Stats_Losses";
        private const string KeySpecialWins = "Stats_SpecialWins";
        private const string KeyBestScore = "Stats_BestScore";

        public static int Wins => PlayerPrefs.GetInt(KeyWins, 0);
        public static int Losses => PlayerPrefs.GetInt(KeyLosses, 0);
        public static int SpecialWins => PlayerPrefs.GetInt(KeySpecialWins, 0);
        /// <summary>これまでの1回の和了で叩き出した最高打点。</summary>
        public static int BestScore => PlayerPrefs.GetInt(KeyBestScore, 0);

        public static int TotalMatches => Wins + Losses;

        /// <summary>勝率（0〜100）。1戦もしていなければ 0。</summary>
        public static int WinRatePercent
        {
            get
            {
                int total = TotalMatches;
                if (total <= 0) return 0;
                return Mathf.RoundToInt(Wins * 100f / total);
            }
        }

        public static void RecordMatchResult(bool isWin, bool isSpecialVictory = false)
        {
            if (isWin)
            {
                PlayerPrefs.SetInt(KeyWins, Wins + 1);
                if (isSpecialVictory) PlayerPrefs.SetInt(KeySpecialWins, SpecialWins + 1);
            }
            else
            {
                PlayerPrefs.SetInt(KeyLosses, Losses + 1);
            }
            PlayerPrefs.Save();
        }

        /// <summary>和了打点を記録する。自己ベストを更新した場合のみ true を返す。</summary>
        public static bool RecordScore(int score)
        {
            if (score <= BestScore) return false;
            PlayerPrefs.SetInt(KeyBestScore, score);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>「3勝2敗（勝率60%）」のような1行サマリ。</summary>
        public static string BuildSummaryLine()
        {
            if (TotalMatches <= 0) return "通算成績: 初戦";
            return $"通算成績: {Wins}勝{Losses}敗（勝率{WinRatePercent}%）";
        }
    }
}
