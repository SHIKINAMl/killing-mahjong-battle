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
    }
}
