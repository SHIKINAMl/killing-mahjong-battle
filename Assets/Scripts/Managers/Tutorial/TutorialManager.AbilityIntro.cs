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
                { SkillNames.BoostHand, new[] {
                    "役強化は指定の役の点を上げる能力",
                    "よく出る役に使ってより高い手を目指すのが定石かな" } },
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

        private static readonly List<TutorialLine> AbilityCostWarnAfterLines = new List<TutorialLine>
        {
            new TutorialLine("能力の発動には点を消費するからね"),
            new TutorialLine("能力の使いすぎで負けないように注意！"),
        };

        /// <summary>
        /// フロー図の「if / 残り持ち点が 8000点以上か」の境目。
        /// **ここを下回ったら、もう能力を試させない。** 練習で身を削らせないため。
        /// </summary>
        private const int AbilityMoreMinScore = 8000;

        /// <summary>
        /// 同じ能力を何周も試させない上限。フロー図には無いが、
        /// **「はい」を押し続けると無限に回る**ので歯止めを置く。
        /// 能力は3種類なので、3周すれば全部触れる。
        /// </summary>
        private const int AbilityShowcaseMaxRounds = 3;

        /// <summary>画面を揺らす。出せない場面（UIが無い等）では黙って素通りする。</summary>
        private void ShakeScreen(float duration, float magnitude, Vector2 axis)
        {
            var pt = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            if (pt != null) pt.PlayScreenShake(duration, magnitude, axis);
        }

        private void FlashScreen()
        {
            UI.Effects.ScreenFlash.Play();
        }

        /// <summary>いまの自分の残り持ち点（= 血）。取れないときは境目より上として扱う。</summary>
        private int GetLocalScoreForAbilityGate()
        {
            var board = BoardStateManager.Instance;
            if (board == null) return AbilityMoreMinScore;
            return board.LocalPlayerHp;
        }

        /// <summary>
        /// 能力の紹介ひとまとまり。フロー図シート3の後半。
        ///
        /// **途中でどれかのUIが居ないときは、言うだけにして先へ進める。**
        /// 局を指定して始めたときなど、盤面が揃っていない入り方があるため、
        /// ここで待つと二度と進めなくなる。
        /// </summary>
        private IEnumerator RunAbilityIntroduction()
        {
            yield return StartCoroutine(PlayLines(AbilityOpeningLines));

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

            for (int round = 0; round < AbilityShowcaseMaxRounds; round++)
            {
                yield return StartCoroutine(PlayLines(AbilityPickLines));

                string picked = null;
                yield return StartCoroutine(RunOneAbilityShowcase(v => picked = v));

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
                if (GetLocalScoreForAbilityGate() < AbilityMoreMinScore
                    || round == AbilityShowcaseMaxRounds - 1)
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

            // 能力一覧ハイライト。**一覧そのものを囲む。**
            // 行を1つだけ囲むと「これを押せ」に見えて、選ばせる意味が消える
            GuideTo(ability.AbilityListRect, false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return new WaitUntil(() => ability.SelectedSkillType != null);
            string picked = ability.SelectedSkillType;
            ClearGuide();

            // 発動ボタンハイライト → プレイヤー発動ボタンを押す
            RectTransform activate = ability.ActivateButtonRect;
            if (activate != null)
            {
                GuideTo(activate, false, null, UI.TutorialHighlightUI.Style.Frame);
                yield return new WaitUntil(() => ability.SelectedSkillType == null || !ability.IsWindowOpen);
                ClearGuide();
            }

            // 効果発動の余韻
            yield return new WaitForSeconds(0.4f);
            onPicked(picked);
        }

        /// <summary>
        /// はい／いいえの問いかけ。**冒頭の「麻雀とか知ってたっけ？」と同じ見た目にする。**
        /// 0=はい / 1=いいえ を返す。
        /// </summary>
        private IEnumerator AskYesNo(string question, System.Action<int> onAnswered)
        {
            int answer = -1;
            TMP_FontAsset font = BorrowJapaneseFont();
            Transform parent = BuildIntroCanvas(dim: false);
            if (parent == null) { onAnswered(1); yield break; }

            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(question);
            }
            BuildQuestionPanel(parent, font, onYes: () => answer = 0, onNo: () => answer = 1);
            yield return new WaitUntil(() => answer >= 0);
            CloseIntro();
            onAnswered(answer);
        }
    }
}
