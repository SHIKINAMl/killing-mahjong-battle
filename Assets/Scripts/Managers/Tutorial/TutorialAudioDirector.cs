using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers.Tutorial
{
    /// <summary>
    /// チュートリアルの音を台詞に合わせて当てる（2026-09-11）。
    ///
    /// 設計の出どころは `km-docs/tutorial/04_演出と音_統合.md`。
    /// Claude / Codex / Antigravity の3稿を突き合わせて決めたもので、
    /// **音の切り替えは局（RoundStatus）ではなく台詞で起きる。**
    /// 台本の局の切れ目と、空気の切れ目が一致しないため。
    ///
    /// 表に無い台詞では何も起きない（直前の音が続く）。
    /// **無言になることはない**ので、途中まで埋めた表でも安全。
    ///
    /// 対になっている音（drop×2 / crack↔crack_long / choice↔choice_dark /
    /// ability↔collapse）は、鳴る場所が離れていて初めて意味を持つ。
    /// **片方だけ差し替えないこと。**
    /// </summary>
    public static class TutorialAudioDirector
    {
        public struct Cue
        {
            /// <summary>鳴らすBGM。空なら変えない。</summary>
            public string Bgm;
            /// <summary>true なら1フレームで音楽を断つ。</summary>
            public bool Cut;
            /// <summary>重ねる一発物。空なら鳴らさない。</summary>
            public string Se;

            public Cue(string bgm, bool cut, string se) { Bgm = bgm; Cut = cut; Se = se; }
            public static Cue Music(string bgm) { return new Cue(bgm, false, null); }
            public static Cue Sound(string se) { return new Cue(null, false, se); }
            public static Cue Silence() { return new Cue(null, true, null); }
            public static Cue SilenceWith(string se) { return new Cue(null, true, se); }
            public static Cue Both(string bgm, string se) { return new Cue(bgm, false, se); }
        }

        private static readonly Dictionary<string, Cue> Cues = new Dictionary<string, Cue>
        {
            // ---------- 第1局: 契約と、満貫の残酷な算数 ----------
            { "r0.introLines[0]",      Cue.Music("tut_lesson") },
            { "r0.introLines[2]",      Cue.Sound("se_drop") },        // 「あなたの色よ」
            // 絶叫。**ここだけ曲の外側**。前後の音楽を変えず、終わったら何事もなく戻す
            { "r0.introLines[3]",      Cue.SilenceWith("se_crack") },
            { "r0.introLines[4]",      Cue.Music("tut_lesson") },
            { "r0.beforeBetLines[1]",  Cue.Sound("se_tube_slow") },
            { "r0.beforeBetLines[2]",  Cue.Sound("se_tube_fast") },
            { "r0.beforeBetLines[6]",  Cue.Sound("se_choice") },      // 「自分で決めていいのよ」
            { "r0.outroLines[3]",      Cue.Silence() },               // 「満貫は、1倍なの」
            { "r0.outroLines[4]",      Cue.Music("tut_lesson") },

            // ---------- 第2局: 流局 ----------
            { "r1.introLines[0]",      Cue.Music("tut_slack") },
            { "r1.outroLines[3]",      Cue.Sound("se_stack") },
            // 空気の切れ目2。弛緩から重圧の自覚へ
            { "r1.outroLines[5]",      Cue.Music("tut_lesson") },

            // ---------- 第3局: 嘘 ----------
            { "r2.inheritedBetLines[1]", Cue.Sound("se_stack") },
            // **ここから5行だけ温かい。** 長調にはしない。温かさは音色と残響で作ってある
            { "r2.onBattleStartLines[0]", Cue.Music("tut_lie") },
            // 空気の切れ目3。**フェードでも小節待ちでもなく、1フレームで断つ**
            { "r2.outroLines[0]",      Cue.Silence() },               // 「ロン。」
            { "r2.outroLines[2]",      Cue.Music("tut_cruel") },
            { "r2.outroLines[3]",      Cue.Sound("se_choice_dark") }, // 「決めさせられていたの」
            { "r2.outroLines[8]",      Cue.Sound("se_drop") },        // 「契約書に垂れちゃった」

            // ---------- 第4局: 能力、そして反転 ----------
            { "r3.introLines[0]",      Cue.Music("tut_lesson") },
            { "r3.abilityIntroLines[2]", Cue.Music("tut_ability") },
            { "r3.abilityShowcases[0].beforeLines[0]", Cue.Sound("se_ability_1") },
            { "r3.abilityShowcases[1].beforeLines[0]", Cue.Sound("se_ability_2") },
            { "r3.abilityShowcases[2].beforeLines[0]", Cue.Sound("se_ability_3") },
            // 空気の切れ目4。積み上げたものが崩れる。加害者から当事者へ
            { "r3.abilityExplainLines[2]", Cue.Both("tut_reversal", "se_collapse") },
            { "r3.outroLines[4]",      Cue.Silence() },               // 「私のゲージ、あなたより短いのよ」
            { "r3.outroLines[5]",      Cue.Music("tut_final") },      // 「次で最後にしましょう」

            // ---------- 第5局: 決着、そして問い ----------
            { "r4.introLines[0]",      Cue.Music("tut_final") },
            // 空気の切れ目5。駆け引きが消え、命のやり取りへ
            { "r4.onBattleStartLines[5]", Cue.Sound("se_two_pulses") },// 「私も、あなたと同じだけ抜かれている」
            { "r4.outroLines[0]",      Cue.Silence() },               // 「……九蓮宝燈。」
            // 導入の絶叫と**同じ音源**。あちらは仮面の裂け目、こちらは崩壊
            { "r4.outroLines[2]",      Cue.Sound("se_crack_long") },
            { "r4.outroLines[3]",      Cue.Music("tut_farewell") },

            // ---------- 幕 ----------
            { "ending[0]",             Cue.Silence() },
        };

        /// <summary>台詞が表示される直前に呼ぶ。表に無い ID なら何もしない。</summary>
        public static void OnLine(TutorialLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.id)) return;

            Cue cue;
            if (!Cues.TryGetValue(line.id, out cue)) return;

            var audio = AudioManager.Instance;
            if (audio == null) return;

            if (cue.Cut) audio.CutBgmImmediately();
            if (!string.IsNullOrEmpty(cue.Bgm)) audio.PlayTutorialBgm(cue.Bgm);
            if (!string.IsNullOrEmpty(cue.Se)) audio.PlayStinger(cue.Se);
        }

        /// <summary>この表が参照している音源をすべて数え上げる。検証用。</summary>
        public static void CollectReferenced(List<string> bgm, List<string> se)
        {
            foreach (var kv in Cues)
            {
                if (!string.IsNullOrEmpty(kv.Value.Bgm) && !bgm.Contains(kv.Value.Bgm)) bgm.Add(kv.Value.Bgm);
                if (!string.IsNullOrEmpty(kv.Value.Se) && !se.Contains(kv.Value.Se)) se.Add(kv.Value.Se);
            }
        }

        /// <summary>表の ID が台本に実在するか確かめる。存在しない ID は無言の原因になる。</summary>
        public static List<string> FindUnknownIds(TutorialScenario scenario)
        {
            var known = new HashSet<string>();
            foreach (var row in TutorialLineTable.Dump(scenario)) known.Add(row.id);

            var unknown = new List<string>();
            foreach (var kv in Cues)
                if (!known.Contains(kv.Key)) unknown.Add(kv.Key);
            return unknown;
        }
    }
}
