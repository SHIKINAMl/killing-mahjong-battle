using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // 対局フェイズ①が終わったあとの締め（2026-09-24）。
        //
        // 出どころはフロー図（km-docs/tutorial/flow_20260927.drawio）の3枚目
        // 「流局＆能力について」。**2026-09-28 に能力の中身まで描かれた。**
        // ここが受け持つのは前半で、
        //
        //   対局フェイズ①終了 → 手牌選択フェイズ②へ → 初期非表示UI：能力ベル
        //   → 「とまぁこれがゲームの流れね」 → 「わかった？」 → 分岐
        //        はい　 → 「おっ後輩ちゃんできるねぇ」 → 「うーん…じゃあいらないと思うけど……ハイコレ」
        //        いいえ → 「そうだねぇ……気持ちわかるわー」 → 「ならとりあえず……ハイコレ！」
        //   → 設定ボタンを枠で囲み、資料への入口を案内（押させない）
        //   → 「ここに今までのチュートリアルがまとめてあるから困ったら見て」
        //
        // まで。**この続き（能力の紹介）は `TutorialManager.AbilityIntro.cs`。**

        private static readonly List<TutorialLine> WrapUpLines = new List<TutorialLine>
        {
            new TutorialLine("とまぁこれがゲームの流れね"),
        };

        /// <summary>分岐の問いかけ。選ばせている間もこの吹き出しを出したままにする。</summary>
        private const string WrapUpQuestion = "「わかった？」";

        private static readonly List<TutorialLine> WrapUpUnderstoodLines = new List<TutorialLine>
        {
            new TutorialLine("おっ後輩ちゃんできるねぇ"),
            new TutorialLine("うーん…じゃあいらないと思うけど……ハイコレ"),
        };

        private static readonly List<TutorialLine> WrapUpConfusedLines = new List<TutorialLine>
        {
            new TutorialLine("そうだねぇ……気持ちわかるわー"),
            new TutorialLine("ならとりあえず……ハイコレ！"),
        };

        private static readonly List<TutorialLine> ArchiveClosingLines = new List<TutorialLine>
        {
            new TutorialLine("ここに今までのチュートリアルがまとめてあるから困ったら見て"),
        };

        /// <summary>能力ベルを一度でも出したか。出したら以降は出したままにする。</summary>
        private bool _abilityBellRevealed;

        /// <summary>第2局か。締めのくだりはここの頭で一度だけ流す。</summary>
        private bool IsSecondTutorialRound(TutorialRoundData data)
        {
            return _scenario != null && _scenario.rounds != null
                   && _scenario.rounds.Count > 1 && _scenario.rounds[1] == data;
        }

        /// <summary>
        /// 能力ベルの出し入れ。フロー図の「初期非表示UI：能力ベル」。
        /// まだ説明していないものを置いておくと、押してよいのか分からない。
        /// </summary>
        private void SetAbilityBellVisible(bool visible)
        {
            if (gameUIManager == null || gameUIManager.AbilityUI == null) return;

            // **入れ物ごと起こす。** `AbilityUI` の GameObject 自体を伏せている場所が
            // 6箇所あり（賭け・マッチング・ロン・局送りなど）、子のベルだけ
            // SetActive(true) にしても `activeInHierarchy` は false のまま。
            // 2026-09-28 に実機で詰まった。ベルが出ないのに枠だけ出て、
            // 押しようがないまま止まる。
            if (visible) gameUIManager.AbilityUI.gameObject.SetActive(true);

            gameUIManager.AbilityUI.SetBellVisible(visible);
        }

        private IEnumerator RunAfterFirstRoundWrapUp()
        {
            yield return StartCoroutine(PlayLines(WrapUpLines));

            // 「わかった？」。**見た目は冒頭の「麻雀とか知ってたっけ？」と同じにする。**
            // 問いかけを吹き出しに出したまま選ばせないと、答えずに読み飛ばせてしまう。
            int answer = -1;                       // 0=はい / 1=いいえ
            TMP_FontAsset font = BorrowJapaneseFont();
            Transform parent = BuildIntroCanvas(dim: false);
            if (parent != null)
            {
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(true);
                    dialogueUI.ShowText(WrapUpQuestion);
                }

                BuildQuestionPanel(parent, font, onYes: () => answer = 0, onNo: () => answer = 1);
                yield return new WaitUntil(() => answer >= 0);
                CloseIntro();
            }

            yield return StartCoroutine(PlayLines(
                answer == 1 ? WrapUpConfusedLines : WrapUpUnderstoodLines));

            yield return StartCoroutine(RunTutorialArchiveGuide());

            // フロー図では「ここに今までの…」から「それじゃあ第二局目だね」へ
            // そのまま矢印が伸びている。**続けて能力の紹介に入る。**
            yield return StartCoroutine(RunAbilityIntroduction());
        }

        /// <summary>
        /// 資料への入口を、設定ボタンを枠で囲んで示す。**押させはしない。**
        ///
        /// 2026-10-30 の要望（「設定の説明」）:「画像を表示するんじゃなくて、
        /// 設定ボタンをハイライトするだけでいいかな」。以前は左下に設定画面の画像
        /// （archive_access_hint）と「設定 → チュートリアル資料」の字を出していた。
        /// 画像はコレクションの CG 一覧で使っているので消さない。
        ///
        /// 枠にするのは、押せるボタンに帯を敷くと押せないものに見えるため（2026-09-24）。
        /// </summary>
        private IEnumerator RunTutorialArchiveGuide()
        {
            ClearGuide();
            RectTransform optionButton = gameUIManager != null ? gameUIManager.OptionButtonRect : null;
            if (optionButton != null)
                GuideTo(optionButton, false, null, UI.TutorialHighlightUI.Style.Frame);
            try
            {
                yield return StartCoroutine(PlayLines(ArchiveClosingLines));
            }
            finally
            {
                ClearGuide();
            }
        }
    }
}