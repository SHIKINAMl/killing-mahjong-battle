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
        // 出どころはフロー図（Drive の draw.io）の3枚目「流局＆能力について」。
        // **題は流局と能力だが、その中身はまだ描かれていない。** 描いてあるのは
        //
        //   対局フェイズ①終了 → 手牌選択フェイズ②へ → 初期非表示UI：能力ベル
        //   → 「とまぁこれがゲームの流れね」 → 「わかった？」 → 分岐
        //        はい　 → 「おっ後輩ちゃんできるねぇ」 → 「うーん…じゃあいらないと思うけど……ハイコレ」
        //        いいえ → 「そうだねぇ……気持ちわかるわー」 → 「ならとりあえず……ハイコレ！」
        //   → オプションハイライト → プレイヤーオプションクリック
        //   → オプション内：チュートリアル資料ハイライト → プレイヤーチュートリアル資料クリック
        //   → チュートリアル資料表示 → プレイヤーオプション画面を閉じる
        //   → 「ここに今までのチュートリアルがまとめてあるから困ったら見て」
        //
        // までで、ここではその範囲だけを作っている。

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
            if (gameUIManager != null && gameUIManager.AbilityUI != null)
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
        }

        /// <summary>
        /// 「ハイコレ」の中身。資料の開き方を、実際に開かせて覚えさせる。
        ///
        /// **押させる所は枠で囲み、押されるまで待つ。**
        /// ここだけ帯ではなく枠なのは、相手がボタンだから。実機で見比べると、
        /// 帯を敷いたボタンは色が濁って**押せないボタンに見える**（2026-09-24 に確認）。
        /// 読ませたい表や数字は帯、押させるボタンは枠、と使い分ける。
        ///
        /// マスクは使わない。オプション画面は自前で入力を受けるので、
        /// 上から穴あきマスクをかぶせるとスライダーもボタンも触れなくなる。
        /// </summary>
        private IEnumerator RunTutorialArchiveGuide()
        {
            var option = gameUIManager != null ? gameUIManager.OptionUI : null;
            RectTransform optionButton = gameUIManager != null ? gameUIManager.OptionButtonRect : null;

            // 盤面にオプションが無い状態（局を指定して始めた等）では、言うだけにして進む。
            // ここで待つと、開きようがないまま止まってしまう。
            if (option == null || optionButton == null)
            {
                yield return StartCoroutine(PlayLines(ArchiveClosingLines));
                yield break;
            }

            // オプションハイライト → プレイヤーオプションクリック
            GuideTo(optionButton, false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return new WaitUntil(() => option.IsOpen);
            ClearGuide();

            // オプション内：チュートリアル資料ハイライト → プレイヤーチュートリアル資料クリック
            // 資料ボタンは OptionUI が実行時に作るので、開いてから取りに行く。
            yield return null;
            GuideTo(option.TutorialArchiveButtonRect, false, null, UI.TutorialHighlightUI.Style.Frame);

            // 資料を開かずに閉じられたら、そこで誘導は終わりにする。付き合わせ続けない。
            yield return new WaitUntil(() => option.IsTutorialArchiveOpen || !option.IsOpen);
            ClearGuide();

            // チュートリアル資料表示 → プレイヤーオプション画面を閉じる
            // 資料を閉じるとオプションへ戻るので、**オプションが閉じるまで**待つ。
            yield return new WaitUntil(() => !option.IsOpen);

            yield return StartCoroutine(PlayLines(ArchiveClosingLines));
        }
    }
}
