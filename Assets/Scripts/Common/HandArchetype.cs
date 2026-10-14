using System.Collections.Generic;

namespace KillingMahjong.Common
{
    /// <summary>
    /// 手の「型」（2026-10-15 のユーザー指示）。
    ///
    /// 「中盤ぐらいで相手は〇〇だという特徴が分かるようになってほしい。
    /// シャドバだと4〜6ターン目にはアグロかコントロールか分かる」。
    ///
    /// 和了は満貫（4翻）以上が条件で、立直は必ず付く（サーバー hand_analyzer.py）。
    /// 残り3翻を作る道は数種類しかないので、それに名前を付けて見せる。
    /// **名前は仮。** 企画が決めたら <see cref="All"/> の Name だけ差し替えればよい。
    ///
    /// **役を判定し直すわけではない。** サーバーが返した役名を、表示用に振り分けるだけ。
    /// 1つの手が複数の型に当てはまるときは、<see cref="All"/> の上にあるものを取る
    /// （捨て牌に特徴が強く出る順に並べてある）。
    /// </summary>
    public static class HandArchetype
    {
        public sealed class Info
        {
            public string Name;
            /// <summary>中心になる役。役一覧で見出しの下に並べる順でもある。</summary>
            public string[] Yaku;
            /// <summary>どんな手か（1行）。</summary>
            public string Summary;
            /// <summary>相手がこの型のとき、捨て牌に出る特徴（1行）。</summary>
            public string Tell;
        }

        public static readonly Info Yakuman = new Info
        {
            Name = "役満",
            Yaku = new[] { "四暗刻", "緑一色", "清老頭", "九蓮宝燈", "純正九蓮宝燈" },
            Summary = "それだけで役満になる手",
            Tell = "どの役満かで、捨て牌の特徴が変わる",
        };

        public static readonly Info Some = new Info
        {
            Name = "染め",
            Yaku = new[] { "混一色", "清一色" },
            Summary = "1つの柄（と東・西）に寄せる",
            Tell = "1つの柄だけ、まったく捨てられない",
        };

        public static readonly Info Chiitoi = new Info
        {
            Name = "七対子",
            Yaku = new[] { "七対子" },
            Summary = "同じ牌2枚の組を7つ",
            Tell = "同じ牌を2枚捨てない。捨て牌はばらばら",
        };

        public static readonly Info Koutsu = new Info
        {
            Name = "刻子",
            Yaku = new[] { "対々和", "三暗刻", "三色同刻" },
            Summary = "同じ牌3枚の組を重ねる",
            Tell = "真ん中の続いた牌も平気で捨てる",
        };

        public static readonly Info Hashi = new Info
        {
            Name = "端",
            Yaku = new[] { "純全帯么九", "混全帯么九", "混老頭" },
            Summary = "どの組にも1・9（か東・西）を入れる",
            Tell = "3から7の真ん中の牌がよく捨てられる",
        };

        public static readonly Info Narabi = new Info
        {
            Name = "並び",
            Yaku = new[] { "一気通貫", "三色同順", "二盃口", "一盃口", "平和", "断么九" },
            Summary = "順子（123のような並び）で役を重ねる",
            Tell = "1・9や東・西が早く捨てられる",
        };

        public static readonly Info Dora = new Info
        {
            Name = "ドラ頼み",
            Yaku = new[] { "ドラ", "赤ドラ" },
            Summary = "ドラの枚数で満貫に届かせる",
            Tell = "形の特徴が薄い。ドラは捨てられない",
        };

        /// <summary>優先順。役一覧の並びもこの順。</summary>
        public static readonly Info[] All = { Yakuman, Some, Chiitoi, Koutsu, Hashi, Narabi, Dora };

        /// <summary>
        /// 役の一覧から型を決める。役名は清算パネルの表示名（`ドラ×3` のように枚数が付いたもの）でもよい。
        /// 決められなければ null。
        /// </summary>
        public static Info Classify(IEnumerable<string> yakuNames)
        {
            if (yakuNames == null) return null;

            var names = new HashSet<string>();
            bool hasDora = false;
            foreach (var raw in yakuNames)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string n = raw.Trim();
                if (n.StartsWith("ドラ") || n.StartsWith("赤ドラ")) hasDora = true;
                names.Add(n);
            }
            if (names.Count == 0) return null;

            // ドラと「並び」の弱い役（平和・断么九・一盃口）だけの手は、ドラが無ければ届かない。
            // 先に形の強い型を見て、どれにも当たらなければドラの有無で分ける
            foreach (var info in new[] { Yakuman, Some, Chiitoi, Koutsu, Hashi })
            {
                foreach (var y in info.Yaku)
                    if (names.Contains(y)) return info;
            }

            foreach (var y in new[] { "一気通貫", "三色同順", "二盃口" })
                if (names.Contains(y)) return Narabi;

            if (hasDora) return Dora;

            foreach (var y in Narabi.Yaku)
                if (names.Contains(y)) return Narabi;

            return null;
        }

        /// <summary>役名が属する型。役一覧で見出しを付けるのに使う。どこにも属さなければ null。</summary>
        public static Info Of(string yakuName)
        {
            if (string.IsNullOrEmpty(yakuName)) return null;
            foreach (var info in All)
                foreach (var y in info.Yaku)
                    if (y == yakuName) return info;
            return null;
        }
    }
}
