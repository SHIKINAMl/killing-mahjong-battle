using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // 能力の introduction（2026-09-28）。
        //
        // 出どころはフロー図（km-docs/tutorial/flow_20260927.drawio）の
        // 3枚目「流局＆能力について」。**前は題だけで中身が描かれていなかったが、
        // 2026-09-28 に 85 ノードまで描かれた。** ここはその後半、
        // 「ここに今までのチュートリアルがまとめてあるから困ったら見て」の続きから、
        // 「能力の使いすぎで負けないように注意！」までを作っている。
        //
        // 流れ（矢印はフロー図のとおり）
        //
        //   「それじゃあ第二局目だね」
        //     → フラッシュ →「むっ……後輩ちゃん けっこう点をもってるね」
        //     →「このまま手牌選択してもいいけど……せっかくなので」→「ほいっ……と！」
        //     → 縦揺れ＆フラッシュ → 上から能力ベルが落ちてくる →「じゃーん！！」
        //     →「これは呼び鈴」→「この鈴を鳴らすと便利な悪魔を召喚できるんだよね」
        //     → 横揺れ →「あっ知らなかった？ エクソシストやってんだよねぇアタシ」
        //     →「それで便利なことができるんだけど……まぁとりあえず鳴らしてみてよ！」
        //     → 能力ベルハイライト → プレイヤーが押す → 強襲を隠す → アニメ待ち
        //     →「コイツはベルル ギャンブルの悪魔なんだ」
        //     →「コイツに点を払うと いろんなイカサマ能力が使えるんだ」
        //     →「まっ 試しにやってみよっか」
        //     →★「じゃ好きなの適当に押してみてよ」→ 能力一覧ハイライト
        //     → 押した能力で分岐 → 発動ボタンハイライト → 押す → 効果発動
        //     → その能力の説明（1〜2行）
        //     →「まぁこんな感じかな まだ知りたい？」→ 分岐
        //          いいえ →「そっか 知りたくなったら資料を確認してね」──┐
        //          はい   → 残り持ち点が 8000 以上か                      │
        //                     以上 →「おっ勉強熱心だねー」→★へ戻る       │
        //                     未満 →「……もうさすがによくない？」        │
        //                            →「知りたくなったら資料を…」────┤
        //     →「そうそう重要なことだから言っとくけど」←────────┘
        //     → フラッシュ →「能力の発動には点を消費するからね」
        //     →「能力の使いすぎで負けないように注意！」

        private static readonly List<TutorialLine> AbilityOpeningLines = new List<TutorialLine>
        {
            new TutorialLine("それじゃあ第二局目だね"),
        };

        private static readonly List<TutorialLine> AbilityRichLines = new List<TutorialLine>
        {
            new TutorialLine("むっ……後輩ちゃん けっこう点をもってるね"),
            new TutorialLine("このまま手牌選択してもいいけど……せっかくなので"),
            new TutorialLine("ほいっ……と！"),
        };

        private static readonly List<TutorialLine> AbilityBellArrivedLines = new List<TutorialLine>
        {
            new TutorialLine("じゃーん！！"),
            new TutorialLine("これは呼び鈴"),
            new TutorialLine("この鈴を鳴らすと便利な悪魔を召喚できるんだよね"),
        };

        private static readonly List<TutorialLine> AbilityExorcistLines = new List<TutorialLine>
        {
            new TutorialLine("あっ知らなかった？ エクソシストやってんだよねぇアタシ"),
            new TutorialLine("それで便利なことができるんだけど……まぁとりあえず鳴らしてみてよ！"),
        };

        private static readonly List<TutorialLine> AbilityBelleLines = new List<TutorialLine>
        {
            new TutorialLine("コイツはベルル ギャンブルの悪魔なんだ"),
            new TutorialLine("コイツに点を払うと いろんなイカサマ能力が使えるんだ"),
            new TutorialLine("まっ 試しにやってみよっか"),
        };

        private static readonly List<TutorialLine> AbilityPickLines = new List<TutorialLine>
        {
            new TutorialLine("じゃ好きなの適当に押してみてよ"),
        };

        // 能力ごとの説明。**フロー図の並びのまま。** 種類で引く
        private static readonly Dictionary<string, string[]> AbilityExplainLines =
            new Dictionary<string, string[]>
            {
                { SkillNames.Mulligan, new[] {
                    "牌交換はいらない牌を配られてない牌の一つと交換する能力",
                    "あと1枚で強い役が作れるのにって時に使うといいよ" } },
                { SkillNames.Perspective, new[] {
                    "透視は相手の山牌を３枚だけ透かしてみれるよ",
                    "透視した牌は手牌に組み込まれると手牌も透けて見えるから",
                    "うっかりロンされにくくなったりするよ" } },
                // **役強化の2行目以降は 2026-09-30 の改訂で差し替えた。**
                // 旧「よく出る役に使ってより高い手を目指すのが定石かな」は
                // 使いどころの話だったが、改訂版は**値段の話**に寄せている。
                { SkillNames.BoostHand, new[] {
                    "役強化は指定の役の点を上げる能力",
                    "簡単に強い役を作れるようになるけど…ちょっとお高いね",
                    "最初に使ってもいいし儲けた分を回すのもいいかもね" } },
            };

        private const string AbilityMoreQuestion = "「まぁこんな感じかな まだ知りたい？」";

        private static readonly List<TutorialLine> AbilityEagerLines = new List<TutorialLine>
        {
            new TutorialLine("おっ勉強熱心だねー"),
        };

        private static readonly List<TutorialLine> AbilityEnoughLines = new List<TutorialLine>
        {
            new TutorialLine("そっか 知りたくなったら資料を確認してね"),
        };

        private static readonly List<TutorialLine> AbilityTooPoorLines = new List<TutorialLine>
        {
            new TutorialLine("……もうさすがによくない？"),
            new TutorialLine("知りたくなったら資料を確認すればいいからさ"),
        };

        private static readonly List<TutorialLine> AbilityCostWarnLines = new List<TutorialLine>
        {
            new TutorialLine("そうそう重要なことだから言っとくけど"),
        };

        /// <summary>
        /// フラッシュのあとの締め（2026-09-30 の改訂）。
        ///
        /// **「負けないように注意」から「最低賭け金が払えなくならないように」へ変えた。**
        /// 何がまずいのかが曖昧だったのを、負け方まで言い切る形にしている。
        /// そのあと「点が０になっても負け」まで補足して、能力の話を締める。
        /// </summary>
        private static readonly List<TutorialLine> AbilityCostWarnAfterLines = new List<TutorialLine>
        {
            new TutorialLine("能力の発動には点を消費するからね"),
            new TutorialLine("能力の使いすぎで最低賭け金が払えなくならないように！"),
            new TutorialLine("賭け金を支払って点が０になっても負けだからね…"),
            new TutorialLine("イカサマ能力についてはこんなもんかな"),
            new TutorialLine("他にも何個かオモシロルールはあるけど…"),
        };

        /// <summary>
        /// フロー図の「if / 残り持ち点が 8000点以上か」の境目。
        /// **ここを下回ったら、もう能力を試させない。** 練習で身を削らせないため。
        /// </summary>
        private const int AbilityMoreMinScore = 8000;

        /// <summary>画面を揺らす。出せない場面（UIが無い等）では黙って素通りする。</summary>
        private void ShakeScreen(float duration, float magnitude, Vector2 axis)
        {
            var pt = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            if (pt != null) StartCoroutine(pt.PlayScreenShakeRoutine(duration, magnitude, axis));
        }

        private void FlashScreen()
        {
            UI.Effects.ScreenFlash.Play();
        }

        /// <summary>いまの自分の残り持ち点（= 血）。取れないときは境目より上として扱う。</summary>
        private int GetLocalScoreForAbilityGate()
        {
            return PlayerShownHp;
        }

        private bool CanRepeatAbilityIntroduction() => GetLocalScoreForAbilityGate() >= AbilityMoreMinScore;

        /// <summary>
        /// 能力の紹介ひとまとまり。フロー図シート3の後半。
        ///
        /// **途中でどれかのUIが居ないときは、言うだけにして先へ進める。**
        /// 局を指定して始めたときなど、盤面が揃っていない入り方があるため、
        /// ここで待つと二度と進めなくなる。
        /// </summary>
        private IEnumerator RunAbilityIntroduction()
        {
            System.Action restoreBoard = null;
            _introPerspectiveUses = 0;
            if (gameUIManager != null && gameUIManager.AbilityUI != null)
                gameUIManager.AbilityUI.TutorialUnusableReason = IntroSkillUnusableReason;
            try
            {
                yield return StartCoroutine(PlayLines(AbilityOpeningLines));
                // 「それじゃあ第二局目だね」のあとに「２局目」の暗転を出し、暗いあいだに盤面を出す（2026-10-29 ユーザー指示）
                if (_pendingRoundBoard != null || !_boardVisible) yield return StartCoroutine(PlayRoundLabelRoutine(2));
                // 第2局の配牌が済んでから、能力実演の復帰先を保存する。
                restoreBoard = CaptureIntroDemoBoard();

                FlashScreen();
                yield return new WaitForSeconds(0.35f);
                yield return StartCoroutine(PlayLines(AbilityRichLines));

                // 縦揺れ＆フラッシュ → 上から能力ベルが落ちてくる
                ShakeScreen(0.25f, 18f, new Vector2(0f, 1f));
                FlashScreen();
                yield return new WaitForSeconds(0.3f);
                yield return StartCoroutine(DropAbilityBellRoutine());

                yield return StartCoroutine(PlayLines(AbilityBellArrivedLines));

                ShakeScreen(0.2f, 16f, new Vector2(1f, 0f));
                yield return new WaitForSeconds(0.2f);
                yield return StartCoroutine(PlayLines(AbilityExorcistLines));

                yield return StartCoroutine(RingBellRoutine());

                yield return StartCoroutine(PlayLines(AbilityBelleLines));

                yield return StartCoroutine(RunAbilityShowcaseLoop());

                yield return StartCoroutine(PlayLines(AbilityCostWarnLines));
                FlashScreen();
                yield return new WaitForSeconds(0.35f);
                yield return StartCoroutine(PlayLines(AbilityCostWarnAfterLines));
            }
            finally
            {
                ClearIntroSkillSelection();
                restoreBoard?.Invoke();
                if (gameUIManager != null && gameUIManager.AbilityUI != null)
                {
                    gameUIManager.AbilityUI.TutorialActivationHandler = null;
                    gameUIManager.AbilityUI.TutorialUnusableReason = null;
                }
            }
        }

        /// <summary>
        /// 「上から能力ベルが落ちてくる」。
        /// **落ちる絵は自前で動かす。** ベルは普段ただ出し入れするだけなので、
        /// 出した位置から本来の位置まで下ろす動きをここで作る。
        /// </summary>
        private IEnumerator DropAbilityBellRoutine()
        {
            var ability = gameUIManager != null ? gameUIManager.AbilityUI : null;
            if (ability == null)
            {
                SetAbilityBellVisible(true);
                _abilityBellRevealed = true;
                yield break;
            }

            SetAbilityBellVisible(true);
            _abilityBellRevealed = true;
            yield return null;   // 出した次のフレームまで待たないと位置が取れない

            RectTransform bell = ability.BellRect;
            if (bell == null) yield break;

            Vector2 goal = bell.anchoredPosition;
            float rise = 420f;                    // 画面の外まで持ち上げる高さ
            const float fall = 0.45f;

            float t = 0f;
            while (t < fall)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / fall);
                // 落ちるので終わりを速く。**等速だと浮いて見える**
                float e = u * u;
                bell.anchoredPosition = goal + new Vector2(0f, rise * (1f - e));
                yield return null;
            }
            bell.anchoredPosition = goal;

            // 着地の衝撃。フロー図の「拳で机をドシンと叩きつけるイメージ」に寄せる
            ShakeScreen(0.18f, 14f, new Vector2(0f, 1f));
        }

        /// <summary>
        /// 「能力ベルハイライト → プレイヤー能力ベル押す → 初期非表示能力：強襲
        /// → 能力ベルのアニメーションが再生終わるまで待つ」。
        /// </summary>
        private IEnumerator RingBellRoutine()
        {
            var ability = gameUIManager != null ? gameUIManager.AbilityUI : null;
            RectTransform bell = ability != null ? ability.BellRect : null;
            if (ability == null || bell == null) yield break;

            GuideTo(bell, false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return new WaitUntil(() => ability.IsWindowOpen);
            ClearGuide();

            // **強襲はここでは見せない。** フロー図の「初期非表示能力：強襲」。
            // 使い方を教えていないものを並べると、押してよいのか分からない。
            //
            // **窓が開いたあとに隠すこと。** 行は窓を最初に開いたときに作られるので、
            // 開く前に呼んでも相手が居らず、何も起きない（2026-09-28 に実機で確認。
            // 一覧に強襲が出たままだった）。作られる次のフレームまで待つ。
            yield return null;
            ability.SetAbilityHidden(SkillNames.Assault, true);

            // 能力ベルのアニメーションが再生終わるまで待つ
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>
        /// 「じゃ好きなの適当に押してみてよ」から「まだ知りたい？」の輪。
        /// **はいを選ぶと、持ち点が足りている限りもう一度選ばせる。**
        /// </summary>
        private IEnumerator RunAbilityShowcaseLoop()
        {
            var ability = gameUIManager != null ? gameUIManager.AbilityUI : null;

            if (ability == null) yield break;

            while (!_aborted)
            {
                yield return StartCoroutine(PlayLines(AbilityPickLines));

                string picked = null;
                yield return StartCoroutine(RunOneAbilityShowcase(v => picked = v));
                if (_aborted) yield break;
                if (picked == null) continue; // 対象選択のキャンセルは発動にも説明済みにもしない。

                // 説明。フロー図では能力ごとに文面が違う
                if (picked != null && AbilityExplainLines.ContainsKey(picked))
                {
                    var lines = new List<TutorialLine>();
                    foreach (var text in AbilityExplainLines[picked]) lines.Add(new TutorialLine(text));
                    yield return StartCoroutine(PlayLines(lines));
                }

                // 「まぁこんな感じかな まだ知りたい？」
                int answer = -1;
                yield return StartCoroutine(AskYesNo(AbilityMoreQuestion, v => answer = v));

                if (answer == 1)   // いいえ
                {
                    yield return StartCoroutine(PlayLines(AbilityEnoughLines));
                    yield break;
                }

                // はい → 残り持ち点が 8000点以上か
                // 「……もうさすがによくない？」は、**持ち点が 8000 を切ったときだけ**（2026-10-22 プランナー指摘
                // 「役の使いすぎでライフ8000以下になりそうな時に出るやつ」）。以前は周回の上限に達したときにも
                // 出していて、16000 残っていても言われていた
                if (!CanRepeatAbilityIntroduction())
                {
                    yield return StartCoroutine(PlayLines(AbilityTooPoorLines));
                    yield break;
                }

                yield return StartCoroutine(PlayLines(AbilityEagerLines));
            }
        }

        /// <summary>
        /// 能力一覧ハイライト → どれかを押す → 発動ボタンハイライト → 押す → 効果発動。
        /// 押された種類を <paramref name="onPicked"/> で返す。
        /// </summary>
        private IEnumerator RunOneAbilityShowcase(System.Action<string> onPicked)
        {
            var ability = gameUIManager != null ? gameUIManager.AbilityUI : null;
            if (ability == null) { onPicked(null); yield break; }

            if (!ability.IsWindowOpen) ability.OpenWindow();
            yield return null;

            string picked = null;
            string suit = null;
            bool applied = false;
            System.Func<string, string, bool> activation = (type, selectedSuit) =>
            {
                if (this == null || !isActiveAndEnabled || picked != null || !CanActivateIntroSkill(type)) return false;
                picked = type;
                suit = selectedSuit;
                return true;
            };
            ability.TutorialActivationHandler = activation;
            try
            {
                RectTransform highlighted = null;
                GuideTo(ability.AbilityListRect, false, null, UI.TutorialHighlightUI.Style.Frame);
                // 選んだ行の再クリックが現在の発動操作。選択解除・Xでは実演済みにしない。
                //
                // **枠は選んだ行そのものを追う**（2026-10-29 バグ報告「透視スキルを使う際、選択演出がクリックに連動しない」）。
                // 以前は能力の種類で見ていたので、透視の種別（萬子〜字牌）を選び替えても「透視」のままで、
                // 枠が最初の行（萬子）に残っていた
                while (picked == null && !_aborted)
                {
                    if (!ability.IsWindowOpen) { ability.OpenWindow(); highlighted = null; }
                    var selected = ability.SelectedItemRect;
                    if (selected != highlighted)
                    {
                        highlighted = selected;
                        GuideTo(selected == null ? ability.AbilityListRect
                            : ability.ActivateButtonRect ?? selected,
                            false, null, UI.TutorialHighlightUI.Style.Frame);
                    }
                    yield return null;
                }
                ClearGuide();
                if (!_aborted) yield return PlayIntroSkillEffect(picked, suit, done => applied = done);
            }
            finally
            {
                if (ability != null && ability.TutorialActivationHandler == activation)
                    ability.TutorialActivationHandler = null;
                ClearGuide();
            }

            // 効果発動の余韻
            yield return new WaitForSeconds(0.4f);
            onPicked(applied ? picked : null);
        }

        /// <summary>
        /// はい／いいえの問いかけ。**冒頭の「麻雀とか知ってたっけ？」と同じ見た目にする。**
        /// 0=はい / 1=いいえ を返す。
        /// </summary>
        /// <param name="yesLabel">0 を返すボタンの文字。省略すると「はい」</param>
        /// <param name="noLabel">1 を返すボタンの文字。省略すると「いいえ」</param>
        /// <param name="asDialogue">
        /// false なら問いかけをセリフの吹き出しに出さず、選択肢の上に地の文として出す
        /// （2026-10-23 プランナー指摘「対局動画の続きをみますか？はセリフじゃないほうがいい」）
        /// </param>
        private IEnumerator AskYesNo(string question, System.Action<int> onAnswered,
            string yesLabel = null, string noLabel = null, bool asDialogue = true)
        {
            int answer = -1;
            TMP_FontAsset font = BorrowJapaneseFont();
            Transform parent = BuildIntroCanvas(dim: false);
            if (parent == null) { onAnswered(1); yield break; }

            if (!asDialogue)
            {
                if (dialogueUI != null) dialogueUI.HideText();
                CreateSystemPrompt(parent, font, question, new Vector2(0f, -40f));
            }
            else if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                // 問いかけの文にも、フロー図の青い太字（→黄色の太字）を当てる
                dialogueUI.ShowText(Tutorial.TutorialEmphasis.Apply(question));
            }
            BuildQuestionPanel(parent, font, onYes: () => answer = 0, onNo: () => answer = 1,
                yesLabel: yesLabel, noLabel: noLabel);
            yield return new WaitUntil(() => answer >= 0);
            CloseIntro();
            onAnswered(answer);
        }
    }
}
