using System.Collections.Generic;
using UnityEngine;

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

        // --- スキルコスト（サーバーが正） -------------------------------------
        //
        // 額を決めているのは Python の `mahjong_engine/engine/game_state.py` の
        // `HP_COST_TABLE`。**この表はそちらの複製であって、正ではない。**
        //
        // ところがサーバーは、コストを「発動した後」にしか送ってこない
        // （`skill_accepted` / `skill_casted` の `cost`）。能力一覧は発動する前に
        // 「-1200」と出す必要があるため、それまでの繋ぎとしてこの表が要る。
        //
        // 実際に 2026-08-07 の時点で3箇所ズレていた（特殊勝利2回以降の段で
        // 透視 5000/4500・特殊勝利 30000/45000・賭け金上限 30000/50000）。
        // 複製がある限り同じことが起きるので、下記の方針で運用する:
        //
        //   1. サーバーから本物の額が届いたら `SetServerSkillCost` で覚え、以降そちらを使う
        //   2. 届いていないぶんだけ、この表を使う
        //   3. サーバーが発動前にコスト表を送るようになったら（SERVER_REQUESTS の A-7）、
        //      `FallbackCostTable` ごと削除する。それがこのファイルの最終形
        //
        // 賭け金の上限・単位も同じ扱い。こちらは `BoardStateManager.SetServerBetRules`
        // （`bet_accepted` で届く）が先に同じ仕組みを持っている。

        /// <summary>
        /// サーバーから実際に届いたスキルコスト。キーは「skillType の後ろに特殊勝利の回数」。
        /// 届いたぶんだけ入るので、入っていないものは <see cref="FallbackCostTable"/> に落とす。
        /// </summary>
        private static readonly Dictionary<string, int> ServerSkillCosts = new Dictionary<string, int>();

        private static string ServerCostKey(string skillType, int specialVictoryCount)
        {
            int index = ClampCostIndex(specialVictoryCount);
            return skillType + "@" + index;
        }

        /// <summary>
        /// サーバーが送ってきた本物のスキルコストを控える。
        /// `skill_casted` / `skill_accepted` を受けたときに、**自分が発動したぶんだけ**呼ぶこと。
        /// 相手の発動を自分の特殊勝利回数で覚えると、段が違う額を混ぜてしまう。
        /// </summary>
        public static void SetServerSkillCost(string skillType, int specialVictoryCount, int cost)
        {
            if (string.IsNullOrEmpty(skillType) || cost <= 0) return;

            // 複製とズレていたら気づけるようにしておく。黙って動くと、
            // 一覧の表示額と実際に引かれる額が静かに食い違う
            int fallback = GetFallbackSkillCost(skillType, specialVictoryCount);
            if (fallback != cost)
            {
                Debug.LogWarning(
                    $"[GameRules] スキルコストがサーバーと違います。" +
                    $"サーバー {skillType}={cost} / 複製={fallback}" +
                    $"（特殊勝利 {specialVictoryCount} 回）。GameRules 側を直してください");
            }

            ServerSkillCosts[ServerCostKey(skillType, specialVictoryCount)] = cost;
        }

        /// <summary>対局をまたいで持ち越さないよう、開始時に捨てる。</summary>
        public static void ClearServerSkillCosts()
        {
            ServerSkillCosts.Clear();
        }

        private static readonly RuleSet[] FallbackCostTable = new RuleSet[]
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
                    { "assault", 3000 },
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
                    { "assault", 3000 },
                    { "special_victory", 30000 }
                }
            },
            // Count 2+
            new RuleSet
            {
                BetMax = 50000,
                BetUnit = 3000,
                SkillCosts = new Dictionary<string, int>
                {
                    { "mulligan", 800 },
                    { "perspective", 4500 },
                    { "boost_hand", 8000 },
                    { "assault", 3000 }, // 強襲だけは段が上がっても額が変わらない
                    { "special_victory", 45000 }
                }
            }
        };

        private static int ClampCostIndex(int specialVictoryCount)
        {
            int index = specialVictoryCount;
            if (index < 0) index = 0;
            if (index >= FallbackCostTable.Length) index = FallbackCostTable.Length - 1;
            return index;
        }

        public static RuleSet GetRuleSet(int specialVictoryCount)
        {
            return FallbackCostTable[ClampCostIndex(specialVictoryCount)];
        }

        /// <summary>
        /// スキルの血のコスト。**サーバーから届いた実額があればそれを返す。**
        /// まだ一度も発動していないスキルだけ、複製の表に落ちる。
        /// </summary>
        public static int GetSkillCost(string skillType, int specialVictoryCount)
        {
            if (!string.IsNullOrEmpty(skillType) &&
                ServerSkillCosts.TryGetValue(ServerCostKey(skillType, specialVictoryCount), out int serverCost))
            {
                return serverCost;
            }

            return GetFallbackSkillCost(skillType, specialVictoryCount);
        }

        private static int GetFallbackSkillCost(string skillType, int specialVictoryCount)
        {
            var rules = GetRuleSet(specialVictoryCount);
            if (skillType != null && rules.SkillCosts.TryGetValue(skillType, out int cost))
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
