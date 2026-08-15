using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers.Reactions
{
    /// <summary>
    /// ルールを上から順に見て、最初に条件を満たしたものを返すだけの小さな判定器。
    ///
    /// **点数付けで「いちばん条件に合うもの」を選ぶ方式は採らない。**
    /// プランナーが「なぜこれが出たのか」を追えなくなるため。
    /// 順番がそのまま優先順位で、並べ替えは一覧の ↑↓ でできる。
    /// </summary>
    public static class ReactionRuleEngine
    {
        /// <summary>
        /// このイベントで出すべきルールを1つ選ぶ。無ければ null。
        ///
        /// **発火の記録（クールダウン・回数制限）は呼び出し側が確定してから付ける。**
        /// ここで付けてしまうと、セリフが空で結局出せなかった場合にも
        /// 「出した」ことになってしまう。
        /// </summary>
        public static ReactionRule Match(ReactionRuleSet set, ReactionEvent ev, ReactionContext ctx)
        {
            if (set == null || set.rules == null || ctx == null) return null;

            float now = Time.unscaledTime;

            for (int i = 0; i < set.rules.Count; i++)
            {
                var rule = set.rules[i];
                if (rule == null || !rule.enabled) continue;
                if (rule.trigger != ev) continue;

                // **枠だけあって中身が空のセリフは「無い」として扱う。**
                // 件数だけ見ていると、書きかけのルールが当たってしまい、
                // 何も喋らないまま下のルールを塞ぐ（＝書いたのに出ない、の典型）
                if (!HasUsableLine(rule)) continue;

                if (rule.cooldownSeconds > 0f && now - rule.lastFiredAt < rule.cooldownSeconds) continue;
                if (rule.limit == FireLimit.OncePerRound && rule.firedInRound > 0) continue;
                if (rule.limit == FireLimit.OncePerMatch && rule.firedInMatch > 0) continue;

                if (!Matches(rule, ctx)) continue;

                return rule;
            }

            return null;
        }

        /// <summary>実際に喋れる行が1本でもあるか（テキストが空でない行）</summary>
        public static bool HasUsableLine(ReactionRule rule)
        {
            if (rule == null || rule.lines == null) return false;
            foreach (var l in rule.lines)
                if (l != null && !string.IsNullOrEmpty(l.text)) return true;
            return false;
        }

        /// <summary>すべての条件を満たすか。条件が空なら「そのイベントなら必ず」</summary>
        public static bool Matches(ReactionRule rule, ReactionContext ctx)
        {
            if (rule.conditions == null) return true;

            for (int i = 0; i < rule.conditions.Count; i++)
            {
                var c = rule.conditions[i];
                if (c == null || string.IsNullOrEmpty(c.key)) continue;   // 書きかけの行は無視する
                if (!Evaluate(c, ctx)) return false;
            }
            return true;
        }

        private static bool Evaluate(ReactionCondition c, ReactionContext ctx)
        {
            string textValue;
            if (ctx.TryGetText(c.key, out textValue))
            {
                bool same = string.Equals(textValue, c.text ?? "");
                // 文字の比較で「以上」などを選ばれても意味がないので、等値だけ見る
                return c.op == CompareOp.NotEqual ? !same : same;
            }

            float value;
            if (!ctx.TryGetNumber(c.key, out value))
            {
                // **渡されていない変数を条件にしたら、そのルールは成立しない。**
                // 「値が無い＝条件を満たさない」に倒す。逆にすると、
                // 変数名を打ち間違えたルールが常に成立してしまう
                return false;
            }

            switch (c.op)
            {
                case CompareOp.Equal:          return Mathf.Approximately(value, c.number);
                case CompareOp.NotEqual:       return !Mathf.Approximately(value, c.number);
                case CompareOp.GreaterOrEqual: return value >= c.number;
                case CompareOp.LessOrEqual:    return value <= c.number;
                case CompareOp.Greater:        return value > c.number;
                case CompareOp.Less:           return value < c.number;
                default:                       return false;
            }
        }

        /// <summary>ルールが出せたときに呼ぶ。クールダウンと回数制限の記録</summary>
        public static void MarkFired(ReactionRule rule)
        {
            if (rule == null) return;
            rule.lastFiredAt = Time.unscaledTime;
            rule.firedInRound++;
            rule.firedInMatch++;
        }

        public static void ResetRound(ReactionRuleSet set)
        {
            if (set == null || set.rules == null) return;
            foreach (var r in set.rules) if (r != null) r.firedInRound = 0;
        }

        public static void ResetMatch(ReactionRuleSet set)
        {
            if (set == null || set.rules == null) return;
            foreach (var r in set.rules)
            {
                if (r == null) continue;
                r.firedInRound = 0;
                r.firedInMatch = 0;
                r.lastFiredAt = -9999f;
            }
        }

        /// <summary>
        /// そのルールより上に「同じイベントで条件が空のルール」があるか。
        /// あると絶対に到達しない。エディタが赤く出すために使う。
        /// </summary>
        public static bool IsUnreachable(ReactionRuleSet set, int index)
        {
            if (set == null || set.rules == null) return false;
            if (index < 0 || index >= set.rules.Count) return false;

            var target = set.rules[index];
            if (target == null) return false;

            for (int i = 0; i < index; i++)
            {
                var above = set.rules[i];
                if (above == null || !above.enabled) continue;
                if (above.trigger != target.trigger) continue;
                if (!HasUsableLine(above)) continue;
                if (above.cooldownSeconds > 0f || above.limit != FireLimit.None) continue;

                bool noConditions = above.conditions == null || above.conditions.Count == 0;
                if (noConditions) return true;
            }
            return false;
        }

        /// <summary>ルール1本から実際に喋る行を選ぶ。複数あればランダム</summary>
        public static ReactionRuleLine PickLine(ReactionRule rule)
        {
            if (rule == null || rule.lines == null || rule.lines.Count == 0) return null;

            // セリフが空の行は選ばない。書きかけを混ぜても無言にならないように
            var usable = new List<ReactionRuleLine>();
            foreach (var l in rule.lines)
                if (l != null && !string.IsNullOrEmpty(l.text)) usable.Add(l);

            if (usable.Count == 0) return null;
            return usable[Random.Range(0, usable.Count)];
        }
    }
}
