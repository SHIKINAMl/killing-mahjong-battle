using System.Collections.Generic;

namespace KillingMahjong.Managers.Reactions
{
    /// <summary>
    /// 変数の名前。**文字列を直書きしないためだけの定数置き場。**
    /// 打ち間違えると条件が黙って成立しなくなるので、必ずここを経由する。
    /// </summary>
    public static class ReactionVars
    {
        // 共通
        public const string MyHp = "my_hp";
        public const string EnemyHp = "enemy_hp";
        public const string HpDiff = "hp_diff";
        public const string RoundNumber = "round";
        public const string TotalWins = "total_wins";
        public const string TotalLosses = "total_losses";
        public const string DaysSinceLastPlay = "days_since_last_play";
        public const string HourOfDay = "hour";

        // 局開始
        public const string PrevWasDraw = "prev_was_draw";
        public const string PrevWasLoss = "prev_was_loss";

        // 賭け金
        public const string BetAmount = "bet_amount";
        public const string BetMax = "bet_max";
        public const string IsMaxBet = "is_max_bet";
        public const string IsMinBet = "is_min_bet";
        public const string IsTenpai = "is_tenpai";
        public const string BetChangeCount = "bet_change_count";
        public const string BetDecideSeconds = "bet_decide_seconds";
        public const string IsMyBet = "is_my_bet";

        // 手牌
        public const string HandDecideSeconds = "hand_decide_seconds";

        // 打牌
        public const string IsMyDiscard = "is_my_discard";
        public const string TileSuit = "tile_suit";
        public const string TileNumber = "tile_number";
        public const string IsRedDora = "is_red_dora";
        public const string IsYakuhai = "is_yakuhai";
        public const string IsOtakaze = "is_otakaze";
        public const string IsCenterTile = "is_center_tile";
        public const string IsSameAsPrev = "is_same_as_prev";
        public const string IsSuji = "is_suji";
        public const string HonorStreak = "honor_streak";
        public const string TurnElapsedSeconds = "turn_elapsed_seconds";

        // 決着
        public const string IsMyWin = "is_my_win";
        public const string IsYakuman = "is_yakuman";
        public const string IsDoraBomb = "is_dora_bomb";
        public const string IsCheapHand = "is_cheap_hand";
        public const string DrawCount = "draw_count";

        // スキル
        public const string SkillType = "skill_type";
        public const string IsMySkill = "is_my_skill";
        public const string SkillCost = "skill_cost";
        public const string HpAfterSkill = "hp_after_skill";

        // クリック
        public const string ClickArea = "click_area";
        public const string ClickStreak = "click_streak";

        // 操作
        public const string ActivityKind = "activity_kind";
        public const string ActivitySeconds = "activity_seconds";
        public const string ActivityCount = "activity_count";
    }

    /// <summary>変数1つの説明。エディタのドロップダウンと入力欄はこれを見て作られる</summary>
    public class ReactionVarInfo
    {
        public string key;
        public string label;
        public VarKind kind;
        public string help;
        /// <summary>Text のときの選択肢。null なら自由入力</summary>
        public string[] choices;

        public ReactionVarInfo(string key, string label, VarKind kind, string help, string[] choices = null)
        {
            this.key = key;
            this.label = label;
            this.kind = kind;
            this.help = help;
            this.choices = choices;
        }
    }

    /// <summary>
    /// どのイベントでどの変数が使えるかの一覧。
    ///
    /// **これが実行時とエディタの唯一の共有点。** エディタは「あるはずの変数」を
    /// ここからしか引かないので、コード側で渡し忘れた変数が候補に出ることはあっても、
    /// 存在しない変数名を選ばせてしまうことは無い。
    ///
    /// **変数を足したら、渡す側（`ReactionController` など）にも `Set` を書くこと。**
    /// 渡していない変数を条件に使うと、そのルールは「値が無い」として不成立になる。
    /// </summary>
    public static class ReactionVariableCatalog
    {
        private static readonly ReactionVarInfo[] Common =
        {
            new ReactionVarInfo(ReactionVars.RoundNumber, "何局目か", VarKind.Number, "1 から数える"),
            new ReactionVarInfo(ReactionVars.MyHp, "自分の血", VarKind.Number, "対局開始時は 20000"),
            new ReactionVarInfo(ReactionVars.EnemyHp, "相手の血", VarKind.Number, "対局開始時は 20000"),
            new ReactionVarInfo(ReactionVars.HpDiff, "血の差（自分 − 相手）", VarKind.Number, "自分が有利なら正の数"),
            new ReactionVarInfo(ReactionVars.TotalWins, "通算の勝ち数", VarKind.Number, "端末に保存された累計。対局をまたいで増える"),
            new ReactionVarInfo(ReactionVars.TotalLosses, "通算の負け数", VarKind.Number, "同上"),
            new ReactionVarInfo(ReactionVars.DaysSinceLastPlay, "前回プレイからの日数", VarKind.Number, "初回起動は 0。「久しぶりね」に使える"),
            new ReactionVarInfo(ReactionVars.HourOfDay, "いまの時刻（時）", VarKind.Number, "0〜23。「こんな時間まで」に使える"),
        };

        private static readonly Dictionary<ReactionEvent, ReactionVarInfo[]> PerEvent =
            new Dictionary<ReactionEvent, ReactionVarInfo[]>
        {
            { ReactionEvent.RoundStart, new[] {
                new ReactionVarInfo(ReactionVars.PrevWasDraw, "前の局は流局だった", VarKind.Bool, "第1局では「いいえ」"),
                new ReactionVarInfo(ReactionVars.PrevWasLoss, "前の局は負けた", VarKind.Bool, "第1局では「いいえ」"),
            } },

            { ReactionEvent.BetConfirmed, new[] {
                new ReactionVarInfo(ReactionVars.IsMyBet, "自分の賭けである", VarKind.Bool, "「いいえ」なら相手（女の子）が賭けた額"),
                new ReactionVarInfo(ReactionVars.BetAmount, "賭けた額", VarKind.Number, ""),
                new ReactionVarInfo(ReactionVars.BetMax, "賭けられる上限", VarKind.Number, "サーバーが決める。既定は 5000"),
                new ReactionVarInfo(ReactionVars.IsMaxBet, "上限いっぱい賭けた", VarKind.Bool, ""),
                new ReactionVarInfo(ReactionVars.IsMinBet, "最小額しか賭けなかった", VarKind.Bool, ""),
                new ReactionVarInfo(ReactionVars.IsTenpai, "自分はテンパイしている", VarKind.Bool, "サーバーの is_tenpai が基準"),
                new ReactionVarInfo(ReactionVars.BetChangeCount, "賭け金をいじった回数", VarKind.Number, "＋−とオール押しの合計。迷いの量"),
                new ReactionVarInfo(ReactionVars.BetDecideSeconds, "決めるまでの秒数", VarKind.Number, ""),
            } },

            { ReactionEvent.HandConfirmed, new[] {
                new ReactionVarInfo(ReactionVars.HandDecideSeconds, "手牌を決めるまでの秒数", VarKind.Number, ""),
            } },

            { ReactionEvent.Discard, new[] {
                new ReactionVarInfo(ReactionVars.IsMyDiscard, "自分が切った", VarKind.Bool, "「いいえ」なら相手の打牌"),
                new ReactionVarInfo(ReactionVars.TileSuit, "牌の種類", VarKind.Text, "", new[] { "萬子", "筒子", "索子", "字牌" }),
                new ReactionVarInfo(ReactionVars.TileNumber, "牌の数字", VarKind.Number, "字牌は 1=東 … 7=中"),
                new ReactionVarInfo(ReactionVars.IsRedDora, "赤ドラだった", VarKind.Bool, ""),
                new ReactionVarInfo(ReactionVars.IsYakuhai, "役牌だった", VarKind.Bool, "白發中"),
                new ReactionVarInfo(ReactionVars.IsOtakaze, "オタ風だった", VarKind.Bool, "東南西北"),
                new ReactionVarInfo(ReactionVars.IsCenterTile, "中張牌だった", VarKind.Bool, "数牌の 4・5・6"),
                new ReactionVarInfo(ReactionVars.IsSameAsPrev, "直前と同じ牌だった", VarKind.Bool, ""),
                new ReactionVarInfo(ReactionVars.IsSuji, "スジ牌だった", VarKind.Bool, "自分が切った牌の ±3"),
                new ReactionVarInfo(ReactionVars.HonorStreak, "字牌を続けて切った数", VarKind.Number, "字牌以外を切ると 0 に戻る"),
                new ReactionVarInfo(ReactionVars.TurnElapsedSeconds, "手番が来てからの秒数", VarKind.Number, "自分の打牌のときだけ意味がある"),
            } },

            { ReactionEvent.Agari, new[] {
                new ReactionVarInfo(ReactionVars.IsMyWin, "自分の和了である", VarKind.Bool, "「いいえ」なら相手の和了"),
                new ReactionVarInfo(ReactionVars.IsYakuman, "役満だった", VarKind.Bool, ""),
                new ReactionVarInfo(ReactionVars.IsDoraBomb, "ドラ爆だった", VarKind.Bool, ""),
                new ReactionVarInfo(ReactionVars.IsCheapHand, "安手だった", VarKind.Bool, "1飜か2飜"),
            } },

            { ReactionEvent.Draw, new[] {
                new ReactionVarInfo(ReactionVars.DrawCount, "この対局で何回目の流局か", VarKind.Number, "1 から数える"),
            } },

            { ReactionEvent.MatchEnd, new[] {
                new ReactionVarInfo(ReactionVars.IsMyWin, "自分が勝った", VarKind.Bool, ""),
            } },

            { ReactionEvent.SkillCast, new[] {
                new ReactionVarInfo(ReactionVars.IsMySkill, "自分が発動した", VarKind.Bool, "「いいえ」なら女の子が発動"),
                new ReactionVarInfo(ReactionVars.SkillType, "スキルの種類", VarKind.Text, "",
                    new[] { "mulligan", "perspective", "boost_hand", "assault", "special_victory" }),
                new ReactionVarInfo(ReactionVars.SkillCost, "払った血", VarKind.Number, "サーバーの値から計算"),
                new ReactionVarInfo(ReactionVars.HpAfterSkill, "発動した側の残り血", VarKind.Number, ""),
            } },

            { ReactionEvent.CharacterClick, new[] {
                new ReactionVarInfo(ReactionVars.ClickArea, "触った部位", VarKind.Text,
                    "シーンの ClickableCharacter に登録されている枠の名前", new[] { "Head", "Chest", "Nipple", "" }),
                new ReactionVarInfo(ReactionVars.ClickStreak, "短時間に押した回数", VarKind.Number, "5秒以内の連打を数える"),
            } },

            { ReactionEvent.PlayerActivity, new[] {
                new ReactionVarInfo(ReactionVars.ActivityKind, "操作の種類", VarKind.Text, "",
                    new[] { "放置", "ミュート", "ウィンドウ復帰", "画面連打", "牌連打", "牌つつき",
                            "牌の上で迷う", "即切り", "長考", "手牌を覗く" }),
                new ReactionVarInfo(ReactionVars.ActivitySeconds, "秒数", VarKind.Number, "放置・復帰・長考のときだけ入る"),
                new ReactionVarInfo(ReactionVars.ActivityCount, "回数", VarKind.Number, "連打のときだけ入る"),
            } },
        };

        /// <summary>そのイベントで使える変数（共通ぶんを含む）</summary>
        public static ReactionVarInfo[] For(ReactionEvent ev)
        {
            ReactionVarInfo[] own;
            if (!PerEvent.TryGetValue(ev, out own)) own = new ReactionVarInfo[0];

            var all = new List<ReactionVarInfo>(own);
            all.AddRange(Common);
            return all.ToArray();
        }

        public static ReactionVarInfo Find(ReactionEvent ev, string key)
        {
            foreach (var v in For(ev)) if (v.key == key) return v;
            return null;
        }

        /// <summary>画面に出す日本語のイベント名</summary>
        public static string EventLabel(ReactionEvent ev)
        {
            switch (ev)
            {
                case ReactionEvent.RoundStart: return "局が始まった";
                case ReactionEvent.BetConfirmed: return "賭け金が決まった";
                case ReactionEvent.HandConfirmed: return "手牌が決まった";
                case ReactionEvent.Discard: return "牌が切られた";
                case ReactionEvent.Agari: return "和了した";
                case ReactionEvent.Draw: return "流局した";
                case ReactionEvent.MatchEnd: return "対局が終わった";
                case ReactionEvent.SkillCast: return "スキルが発動した";
                case ReactionEvent.CharacterClick: return "女の子を触った";
                case ReactionEvent.PlayerActivity: return "盤面と関係ない操作";
                default: return ev.ToString();
            }
        }

        public static string OpLabel(CompareOp op)
        {
            switch (op)
            {
                case CompareOp.Equal: return "＝";
                case CompareOp.NotEqual: return "≠";
                case CompareOp.GreaterOrEqual: return "以上";
                case CompareOp.LessOrEqual: return "以下";
                case CompareOp.Greater: return "より大きい";
                case CompareOp.Less: return "より小さい";
                default: return op.ToString();
            }
        }
    }
}
