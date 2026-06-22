using System.Collections.Generic;

namespace KillingMahjong
{
    public static class GameRules
    {
        public struct RuleSet
        {
            public int BetMax;
            public int BetUnit;
            public Dictionary<string, int> SkillCosts;
        }

        private static readonly RuleSet[] CostTable = new RuleSet[]
        {
            // Count 0
            new RuleSet
            {
                BetMax = 5000,
                BetUnit = 200,
                SkillCosts = new Dictionary<string, int>
                {
                    { "mulligan", 1200 },
                    { "perspective", 1500 },
                    { "boost_hand", 10000 },
                    { "special_victory", 30000 }
                }
            },
            // Count 1
            new RuleSet
            {
                BetMax = 10000,
                BetUnit = 1000,
                SkillCosts = new Dictionary<string, int>
                {
                    { "mulligan", 1000 },
                    { "perspective", 3000 },
                    { "boost_hand", 9000 },
                    { "special_victory", 30000 }
                }
            },
            // Count 2+
            new RuleSet
            {
                BetMax = 30000,
                BetUnit = 3000,
                SkillCosts = new Dictionary<string, int>
                {
                    { "mulligan", 800 },
                    { "perspective", 5000 },
                    { "boost_hand", 8000 },
                    { "special_victory", 30000 }
                }
            }
        };

        public static RuleSet GetRuleSet(int specialVictoryCount)
        {
            int index = specialVictoryCount;
            if (index < 0) index = 0;
            if (index >= CostTable.Length) index = CostTable.Length - 1;
            return CostTable[index];
        }

        public static int GetSkillCost(string skillType, int specialVictoryCount)
        {
            var rules = GetRuleSet(specialVictoryCount);
            if (rules.SkillCosts.TryGetValue(skillType, out int cost))
            {
                return cost;
            }
            return 99999; // Default fallback cost
        }

        public static int GetBaseHan(string yakuName)
        {
            if (yakuName == "河底") yakuName = "河底撈魚";
            
            switch (yakuName)
            {
                case "立直":
                case "断么九":
                case "平和":
                case "一盃口":
                case "東":
                case "西":
                case "ドラ":
                case "赤ドラ":
                case "一発":
                case "河底撈魚":
                    return 1;

                case "三色同順":
                case "三色同刻":
                case "三暗刻":
                case "対々和":
                case "混老頭":
                case "混全帯么九":
                case "七対子":
                    return 2;

                case "二盃口":
                case "混一色":
                case "純全帯么九":
                    return 3;

                case "清一色":
                    return 6;

                case "九蓮宝燈":
                case "緑一色":
                case "清老頭":
                case "四暗刻":
                    return 13;

                case "純正九蓮宝燈":
                    return 26;

                default:
                    return -1;
            }
        }

        public static int CalculateTotalHan(string[] baseYakuList, Dictionary<string, int> boosts)
        {
            int totalHan = 0;
            if (baseYakuList != null)
            {
                foreach (var yaku in baseYakuList)
                {
                    int han = GetBaseHan(yaku);
                    if (han > 0) totalHan += han;
                }
            }

            if (boosts != null)
            {
                foreach (var kvp in boosts)
                {
                    totalHan += kvp.Value;
                }
            }

            return totalHan;
        }

        public static float GetMultiplier(int han)
        {
            if (han >= 26) return 8.0f;
            if (han >= 13) return 4.0f;
            if (han >= 11) return 3.0f;
            if (han >= 8)  return 2.0f;
            if (han >= 6)  return 1.5f;
            return 1.0f;
        }
    }
}
