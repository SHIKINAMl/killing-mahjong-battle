namespace KillingMahjong.Common
{
    /// <summary>
    /// スキルtype文字列（サーバーと共通のID）と日本語表示名の対応表。
    /// </summary>
    public static class SkillNames
    {
        public const string Mulligan = "mulligan";
        public const string Perspective = "perspective";
        public const string BoostHand = "boost_hand";

        /// <summary>
        /// 2026-08-04 にサーバーへ追加（`b5bf57a`）。この局に限り、上がっても獲得を 0 にする代わりに、
        /// 得るはずだった額をそのまま相手への追加ダメージにする。**1局1回**。
        /// </summary>
        public const string Assault = "assault";

        public const string SpecialVictory = "special_victory";

        /// <summary>スキルtypeの表示名を返す。未知のtypeはそのまま返す。</summary>
        public static string GetDisplayName(string skillType)
        {
            switch (skillType)
            {
                case Mulligan: return "牌交換";
                case Perspective: return "透視";
                case BoostHand: return "役強化";
                case Assault: return "強襲"; // 仮の名前。決まったらここだけ変えればよい
                case SpecialVictory: return "特殊勝利";
                default: return skillType;
            }
        }
    }
}
