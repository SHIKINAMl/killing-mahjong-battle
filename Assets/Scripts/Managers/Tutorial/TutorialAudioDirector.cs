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
        /// <summary>画面にかける色。`None` は「変えない」で、`Clear` は「抜く」。</summary>
        public enum Tint
        {
            Keep = 0,
            Clear,
            /// <summary>血の赤。契約書・裏切り</summary>
            Blood,
            /// <summary>温かい灯り。第3局の嘘、第5局の告白</summary>
            Warm,
            /// <summary>冷たい青。裏切りの瞬間</summary>
            Cold,
            /// <summary>沈む灰。幕切れ</summary>
            Ash,
        }

        public struct Cue
        {
            /// <summary>鳴らすBGM。空なら変えない。</summary>
            public string Bgm;
            /// <summary>true なら1フレームで音楽を断つ。</summary>
            public bool Cut;
            /// <summary>重ねる一発物。空なら鳴らさない。</summary>
            public string Se;
            /// <summary>画面にかける色。**音と同じ表に置く。**</summary>
            public Tint Tint;
            /// <summary>0 より大きいと、その秒数だけ画面を明滅させる。</summary>
            public float FlickerSeconds;

            public Cue(string bgm, bool cut, string se) { Bgm = bgm; Cut = cut; Se = se; Tint = Tint.Keep; FlickerSeconds = 0f; }
            public static Cue Music(string bgm) { return new Cue(bgm, false, null); }
            public static Cue Sound(string se) { return new Cue(null, false, se); }
            public static Cue Silence() { return new Cue(null, true, null); }
            public static Cue SilenceWith(string se) { return new Cue(null, true, se); }
            public static Cue Both(string bgm, string se) { return new Cue(bgm, false, se); }

            /// <summary>
            /// 音は変えず、色だけ当てる。
            /// **曲を止めていない場面で色を戻すのに要る。**
            /// `Cue.Music(...)` で戻そうとすると曲が頭から鳴り直してしまう。
            /// </summary>
            public static Cue Screen(Tint tint) { return new Cue(null, false, null).With(tint); }

            /// <summary>色を足す。`Cue.Music("x").With(Tint.Warm)` のように繋げて使う。</summary>
            public Cue With(Tint tint) { var c = this; c.Tint = tint; return c; }

            /// <summary>明滅を足す。色は `tint` の色を使う。</summary>
            public Cue Flick(Tint tint, float seconds) { var c = this; c.Tint = tint; c.FlickerSeconds = seconds; return c; }
        }

        private static readonly Dictionary<string, Cue> Cues = new Dictionary<string, Cue>
        {
            // ---------- 第1局: 契約と、満貫の残酷な算数 ----------
            //
            // **2026-09-13 に貼り直した。** 導入の台詞が5行から15行へ書き換わり、
            // 合図だけが古い行番号に残っていた。
            // 直す前は [3] の「……ってアタシのこと好きすぎかよー！！」で
            // 悲鳴のSEと赤い明滅が鳴っていた。
            { "r0.introLines[0]",      Cue.Music("tut_lesson") },
            // 空気の切れ目1。軽口から「命がけ」の告知へ
            { "r0.introLines[6]",      Cue.Sound("se_crack").Flick(Tint.Blood, 0.9f) },
            // **契約書と血の合図は、契約書の話が出るここへ移した。**
            { "r0.introLines[8]",      Cue.Sound("se_drop").With(Tint.Blood) },        // 「アタシはそんなの書かなかったけど」
            // 冗談で済まない気配。色だけ冷たくする
            { "r0.introLines[9]",      Cue.Screen(Tint.Cold) },      // 「そっちのほうが怖いな」
            // ルール説明へ戻るので色を抜く。**曲は止めていないので鳴らし直さない**
            { "r0.introLines[10]",     Cue.Screen(Tint.Clear) },
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
            { "r2.onBattleStartLines[0]", Cue.Music("tut_lie").With(Tint.Warm) },
            // 空気の切れ目3。**フェードでも小節待ちでもなく、1フレームで断つ**
            { "r2.outroLines[0]",      Cue.Silence().With(Tint.Cold) },               // 「ロン。」
            { "r2.outroLines[2]",      Cue.Music("tut_cruel").With(Tint.Clear) },
            { "r2.outroLines[3]",      Cue.Sound("se_choice_dark") }, // 「決めさせられていたの」
            { "r2.outroLines[8]",      Cue.Sound("se_drop").With(Tint.Blood) },        // 「契約書に垂れちゃった」

            // ---------- 第4局: 能力、そして反転 ----------
            { "r3.introLines[0]",      Cue.Music("tut_lesson").With(Tint.Clear) },
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
            { "r4.onBattleStartLines[5]", Cue.Sound("se_two_pulses").With(Tint.Warm) },// 「私も、あなたと同じだけ抜かれている」
            { "r4.outroLines[0]",      Cue.Silence() },               // 「……九蓮宝燈。」
            // 導入の絶叫と**同じ音源**。あちらは仮面の裂け目、こちらは崩壊
            { "r4.outroLines[2]",      Cue.Sound("se_crack_long").Flick(Tint.Blood, 1.6f) },
            { "r4.outroLines[3]",      Cue.Music("tut_farewell").With(Tint.Ash) },

            // ---------- 幕 ----------
            { "ending[0]",             Cue.Silence().With(Tint.Ash) },
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

            ApplyTint(cue);
        }

        /// <summary>
        /// 画面の色を当てる。**薄くかけること。**
        /// 濃くすると盤面の牌が読みにくくなり、演出のために遊びを壊すことになる。
        /// </summary>
        private static void ApplyTint(Cue cue)
        {
            if (cue.Tint == Tint.Keep) return;

            if (cue.Tint == Tint.Clear)
            {
                KillingMahjong.UI.Effects.ScreenTint.Clear(0.8f);
                return;
            }

            Color c; float alpha; float fade;
            switch (cue.Tint)
            {
                // 血の赤。契約書が何で書かれているかを、色で分からせる
                case Tint.Blood: c = new Color(0.78f, 0.09f, 0.14f); alpha = 0.16f; fade = 0.5f; break;
                // 温かい灯り。**第3局の嘘と、第5局の告白で同じ色を使う。**
                // 音は別の楽器に替えて区別を付けているので、色は同じでよい
                case Tint.Warm:  c = new Color(1.00f, 0.72f, 0.42f); alpha = 0.13f; fade = 1.2f; break;
                // 冷たい青。温かさを断ち切るための色
                case Tint.Cold:  c = new Color(0.46f, 0.66f, 1.00f); alpha = 0.18f; fade = 0.05f; break;
                // 沈む灰。幕切れ
                case Tint.Ash:   c = new Color(0.30f, 0.30f, 0.34f); alpha = 0.22f; fade = 2.5f; break;
                default: return;
            }

            if (cue.FlickerSeconds > 0f)
            {
                // 明滅のあとは抜く。掛けっぱなしにすると次の場面へ引きずる
                KillingMahjong.UI.Effects.ScreenTint.Flicker(c, cue.FlickerSeconds, alpha * 2.6f, 0f);
                return;
            }
            KillingMahjong.UI.Effects.ScreenTint.Set(c, alpha, fade);
        }

        /// <summary>チュートリアルを抜けるときに呼ぶ。**掛けっぱなしを残さない。**</summary>
        public static void ResetVisuals()
        {
            KillingMahjong.UI.Effects.ScreenTint.Clear(0.3f);
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
