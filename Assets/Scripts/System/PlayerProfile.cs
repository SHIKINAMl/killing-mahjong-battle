using UnityEngine;

namespace KillingMahjong.Core
{
    /// <summary>
    /// プレイヤーの名前（2026-10-20）。チュートリアル冒頭の契約書の「私＿＿＿」で書いてもらう。
    ///
    /// PlayerPrefs だけで完結し、サーバーには送らない（<see cref="PlayerStatsManager"/> と同じ考え方）。
    /// 展示用のランキング（名前・累計獲得点数など）で使う予定がある。
    /// </summary>
    public static class PlayerProfile
    {
        private const string KeyName = "PlayerName";

        /// <summary>名前の最大文字数。契約書の下線に収まる長さ。</summary>
        public const int MaxNameLength = 10;

        /// <summary>保存してある名前。まだ書いていなければ空文字。</summary>
        public static string Name => PlayerPrefs.GetString(KeyName, "");

        public static bool HasName => !string.IsNullOrEmpty(Name);

        /// <summary>前後の空白を除いて保存する。空なら保存しない（false を返す）。</summary>
        public static bool SetName(string name)
        {
            string trimmed = Normalize(name);
            if (trimmed.Length == 0) return false;
            PlayerPrefs.SetString(KeyName, trimmed);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>前後の空白（全角の空白も）を除き、最大文字数で切る。</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string trimmed = name.Trim().Trim('　').Trim();
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength) : trimmed;
        }
    }
}
