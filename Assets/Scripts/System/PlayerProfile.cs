using UnityEngine;

namespace KillingMahjong.Core
{
    /// <summary>
    /// プレイヤーの名前（2026-10-20）。チュートリアル冒頭の契約書の「私＿＿＿」で書いてもらう。
    ///
    /// 端末には PlayerPrefs で残す。**サーバーへは、対局のために繋いで名乗るたびに送る**
    /// （2026-10-20、名前・ランキングのデータベース対応。<see cref="KillingMahjong.Network.PlayerIdentity"/>）。
    /// サーバーのランキングは、名前を登録した後の対人戦だけを数える。
    /// </summary>
    public static class PlayerProfile
    {
        private const string KeyName = "PlayerName";

        /// <summary>名前の最大文字数。契約書の下線に収まる長さ。</summary>
        public const int MaxNameLength = 10;

        /// <summary>保存してある名前。まだ書いていなければ空文字。</summary>
        public static string Name => PlayerPrefs.GetString(KeyName, "");

        public static bool HasName => !string.IsNullOrEmpty(Name);

        /// <summary>名前を保存したとき（整えた後の名前）。繋いでいればサーバーへ送り直すのに使う。</summary>
        public static event System.Action<string> NameChanged;

        /// <summary>前後の空白を除いて保存する。空なら保存しない（false を返す）。</summary>
        public static bool SetName(string name)
        {
            string trimmed = Normalize(name);
            if (trimmed.Length == 0) return false;
            bool changed = trimmed != Name;
            PlayerPrefs.SetString(KeyName, trimmed);
            PlayerPrefs.Save();
            if (changed) NameChanged?.Invoke(trimmed);
            return true;
        }

        /// <summary>
        /// 制御文字（改行など）を消し、前後の空白（全角の空白も）を除き、最大文字数で切る。
        /// サーバーも同じように整える（`mahjong_engine/records/README.md` の「名前の決まり」）。
        /// </summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var kept = new System.Text.StringBuilder(name.Length);
            foreach (char c in name) if (!char.IsControl(c)) kept.Append(c);
            string trimmed = kept.ToString().Trim().Trim('　').Trim();
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength) : trimmed;
        }
    }
}
