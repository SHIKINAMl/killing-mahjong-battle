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

        /// <summary>
        /// 役の倍率。満貫=1倍 / 跳満=1.5倍 / 倍満=2倍 / 三倍満=3倍 / 役満=4倍 / ダブル役満=8倍。
        /// </summary>
        public static float GetMultiplier(int han)
        {
            if (han >= 26) return 8.0f;
            if (han >= 13) return 4.0f;
            if (han >= 11) return 3.0f;
            if (han >= 8)  return 2.0f;
            if (han >= 6)  return 1.5f;
            return 1.0f;
        }

        // --- 獲得金・失う金 ---
        //
        // 賭け金は各プレイヤーが任意の額を賭け合う。決着したときの血の増減は下の式だけで決まる。
        //
        //   勝者が得る額 = 「勝者自身の」賭け金 × 勝者の役の倍率
        //   敗者が失う額 = 「敗者自身の」賭け金 × 勝者の役の倍率（勝者が単騎で上がっていれば2倍）
        //
        // 勝者の得と敗者の損は別々に計算されるので、両者の賭け金が違えば額も一致しない
        // （＝血の総量は保存されない）。場に積まれた賭け金の表示（BetPotUI）は情報表示であり、
        // 表示額そのものが移動するわけではない。

        /// <summary>単騎で上がられた側の損失倍率。</summary>
        public const float TankiLossMultiplier = 2.0f;

        /// <summary>勝者が得る額。自分が賭けた額 × 自分の役の倍率。</summary>
        public static int CalculateWinnerGain(int winnerBet, int winnerHan)
        {
            if (winnerBet <= 0) return 0;
            return (int)System.Math.Round(winnerBet * (double)GetMultiplier(winnerHan));
        }

        /// <summary>
        /// 敗者が失う額。自分が賭けた額 × 相手の役の倍率。相手が単騎で上がっていれば2倍。
        /// </summary>
        public static int CalculateLoserLoss(int loserBet, int winnerHan, bool winnerWonWithTanki)
        {
            if (loserBet <= 0) return 0;
            double loss = loserBet * (double)GetMultiplier(winnerHan);
            if (winnerWonWithTanki) loss *= TankiLossMultiplier;
            return (int)System.Math.Round(loss);
        }
    }
}
