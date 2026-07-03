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
        public const string SpecialVictory = "special_victory";

        /// <summary>スキルtypeの表示名を返す。未知のtypeはそのまま返す。</summary>
        public static string GetDisplayName(string skillType)
        {
            switch (skillType)
            {
                case Mulligan: return "牌交換";
                case Perspective: return "透視";
                case BoostHand: return "役強化";
                case SpecialVictory: return "特殊勝利";
                default: return skillType;
            }
        }
    }
}
