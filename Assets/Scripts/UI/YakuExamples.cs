using System.Collections.Generic;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 役の「並べる順番」と「牌で組んだ例」（2026-10-09）。
    ///
    /// コレクションの「役」タブ（<see cref="CollectionUI"/>）と、役強化で強める役を選ぶ画面
    /// （<see cref="YakuSelectionUI"/>）の両方が使う。最初はコレクションの中に持っていたが、
    /// 選ぶ画面を同じ形に作り直すときに、ここへ出した。
    ///
    /// **役の名前と翻数は、ここでは持たない。**
    ///   翻数 … <see cref="GameRules.GetBaseHan"/>（サーバーの `mahjong_engine/engine/yaku.py` と同じ27役）
    ///   説明 … <see cref="YakuInfo.GetDescription"/>（対局中の役一覧と同じ文）
    /// 役が増えたら <see cref="Entries"/> に1行足す。
    ///
    /// **例の牌は、このゲームの牌だけで組む。** 字牌は東と西の2種しか無い（發・中・白などは無い）。
    /// 手牌13枚＋ロンする1枚。鳴きは無いので、どの例も門前の形。
    /// </summary>
    internal static class YakuExamples
    {
        /// <summary>一覧に並べる1役。</summary>
        public readonly struct Entry
        {
            public readonly string Name;

            /// <summary>
            /// 例の牌。「m123 p456 s789 E3 m5 + m5」のように書く
            /// （m=萬子 p=筒子 s=索子、後ろの数字が1枚ずつ。E=東 W=西 は後ろの数字が枚数。
            /// r を頭に付けると赤ドラ。「+」より後ろがロンする牌）。空なら例を出さない。
            /// </summary>
            public readonly string Example;

            /// <summary>例の下に出すひとこと。空なら既定の文。</summary>
            public readonly string Note;

            public Entry(string name, string example, string note = "")
            {
                Name = name;
                Example = example;
                Note = note;
            }
        }

        /// <summary>
        /// 並べる順番と、例の牌。翻数の小さい順（サーバーの yaku.py と同じ並び）。
        /// </summary>
        public static readonly Entry[] Entries =
        {
            // ---- 1翻 ----
            new Entry("立直",     "", "形は問わない"),
            new Entry("断么九",   "m234 m567 p345 s456 s8 + s8"),
            new Entry("平和",     "m123 p567 s234 s67 p88 + s8", "順子4組。待ちは両側（5索か8索）"),
            new Entry("一盃口",   "m223344 p567 s789 E1 + E1"),
            new Entry("東",       "E3 m234 p567 s345 s9 + s9"),
            new Entry("西",       "W3 m345 p234 s678 p9 + p9"),
            new Entry("ドラ",     "", "形は問わない"),
            new Entry("赤ドラ",   "rm5 rp5 rs5", "赤い5はこの3種類"),
            new Entry("一発",     "", "形は問わない"),
            new Entry("河底撈魚", "", "形は問わない"),

            // ---- 2翻 ----
            new Entry("三色同順", "m345 m789 p345 s345 p1 + p1"),
            new Entry("三色同刻", "m222 p222 s222 m678 W1 + W1"),
            new Entry("三暗刻",   "m111 p444 s777 m456 p8 + p8"),
            new Entry("対々和",   "m222 p555 s888 E2 W2 + E1"),
            new Entry("混老頭",   "m111 m999 p111 s999 E1 + E1"),
            new Entry("混全帯么九", "m123 m789 p123 E3 s9 + s9"),
            new Entry("七対子",   "m11 m44 p22 p77 s33 s66 W1 + W1"),
            new Entry("一気通貫", "m123 m456 m789 p234 s5 + s5"),

            // ---- 3翻 ----
            new Entry("二盃口",   "m223344 p556677 s9 + s9"),
            new Entry("混一色",   "m111 m345 m678 E3 m9 + m9"),
            new Entry("純全帯么九", "m123 m789 p123 s789 s1 + s1"),

            // ---- 6翻 ----
            new Entry("清一色",   "m123 m345 m567 m789 m9 + m9"),

            // ---- 役満 ----
            new Entry("九蓮宝燈", "m1112245678999 + m3"),
            new Entry("緑一色",   "s234 s234 s666 s888 s4 + s4", "2・3・4・6・8索だけ。このゲームに發は無い"),
            new Entry("清老頭",   "m111 m999 p111 s11 s99 + s9"),
            new Entry("四暗刻",   "m222 p444 p777 s333 E1 + E1"),

            // ---- ダブル役満 ----
            new Entry("純正九蓮宝燈", "m1112345678999 + m5", "1から9のどれが来てもアガれる形"),
        };

        /// <summary>名前から探す。無ければ、例もひとことも空の1件を返す。</summary>
        public static Entry Find(string name)
        {
            foreach (Entry entry in Entries)
            {
                if (entry.Name == name) return entry;
            }
            return new Entry(name, "");
        }

        /// <summary>
        /// 画面に出す役名。**サーバーの表記のまま出してはいけない。**
        /// 「断么九」などの「么」はフォントに無く、□ になる（最初に作ったとき実際に □ が出た）。
        /// 対局中の画面と同じ関数を通して、収録済みの「幺」に置き換える。
        /// </summary>
        public static string DisplayName(string name)
        {
            return YakuNameUtil.ToDisplayText(new YakuNameUtil.Entry { BaseName = name, Boost = 0, Count = 1 });
        }

        /// <summary>一覧の区切りに出す見出し。</summary>
        public static string SectionName(int han)
        {
            if (han >= 26) return "ダブル役満";
            if (han >= 13) return "役満";
            return han + "翻";
        }

        public static string HanText(int han)
        {
            if (han >= 26) return "ダブル役満（26翻）";
            if (han >= 13) return "役満（13翻）";
            return han + "翻";
        }

        /// <summary>例の下に出すひとこと。ロンする牌がある例には「右に離した1枚でロン」を頭に付ける。</summary>
        public static string NoteOf(Entry entry)
        {
            bool wins = entry.Example.Contains("+");
            if (!wins) return entry.Note;
            return string.IsNullOrEmpty(entry.Note) ? "右に離した1枚でロン" : "右に離した1枚でロン。" + entry.Note;
        }

        /// <summary>
        /// 例の書式（<see cref="Entry.Example"/>）を牌の番号の並びにする。
        /// </summary>
        /// <param name="winFrom">ロンする牌が始まる位置。無ければ -1</param>
        public static List<int> Parse(string example, out int winFrom)
        {
            var ids = new List<int>();
            winFrom = -1;
            if (string.IsNullOrEmpty(example)) return ids;

            // 「+」の前が手牌、後ろがロンする牌
            foreach (string token in example.Split(' '))
            {
                if (token.Length == 0) continue;
                if (token == "+") { winFrom = ids.Count; continue; }
                AddTiles(token, ids);
            }
            return ids;
        }

        /// <summary>「m123」「E3」「rp5」のような1かたまりを牌の番号にして足す。</summary>
        private static void AddTiles(string token, List<int> ids)
        {
            bool red = token[0] == 'r';
            if (red) token = token.Substring(1);
            if (token.Length < 2) return;

            char kind = token[0];
            string digits = token.Substring(1);

            if (kind == 'E' || kind == 'W')
            {
                int count = digits[0] - '0';
                int honor = kind == 'E' ? TutorialTiles.Ton : TutorialTiles.Sha;
                for (int i = 0; i < count; i++) ids.Add(TutorialTiles.Encode(honor));
                return;
            }

            foreach (char d in digits)
            {
                int number = d - '0';
                if (number < 1 || number > 9) continue;
                int baseId = kind == 'm' ? TutorialTiles.Man(number)
                           : kind == 'p' ? TutorialTiles.Pin(number)
                           : TutorialTiles.Sou(number);
                ids.Add(TutorialTiles.Encode(baseId, false, red));
            }
        }
    }
}
