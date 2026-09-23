using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// チュートリアルを始める前の問いかけ。ユーザーの指示（2026-09-07）。
    ///
    /// 「あなた、麻雀は打てるの？」を先に聞き、
    /// **経験あり → そのまま開始 / 初めて → 短い説明会話と案内板の後に開始** とする。
    ///
    /// **シーンには置かず実行時に組み立てる。** 対局シーンが2つ（UIテストシーン /
    /// OpeningScene）あるので、シーンに置くと片方だけ直る事故が起きる（AGENTS.md §2）。
    ///
    /// **`StartTutorialFrom` には入れない。** あちらは局を指定して直接始める入口で、
    /// 自動でチュートリアルを通す仕掛け（AGENTS.md §14）が毎フレーム叩いている。
    /// ここで待ちを挟むと自動走行が止まる。問いかけるのは `StartTutorial()` だけ。
    /// </summary>
    public partial class TutorialManager
    {
        /// <summary>
        /// 未経験者向け案内板。Resources 直下からの相対パス（拡張子なし）。
        ///
        /// **ユーザーが用意した画像を使う。差し替えないこと（2026-09-07 の指示）。**
        /// 一度 AI が描いた別の絵に**置き換わって**いたが、
        /// あれは「3枚組×4＋2枚組×1＝14枚」という一般的な麻雀の説明で、
        /// **このゲームの「山牌から13枚選んで満貫以上を作る」という決まりと食い違う。**
        /// 未経験者に、この対局では使わない知識を**代わりに**教えることになるので戻した
        /// （その絵は 2026-09-12 に削除済み）。
        ///
        /// **2026-09-12 から、麻雀そのものの説明が前に入る**（<see cref="MahjongRulePath"/>）。
        /// ただし置き換えではなく、麻雀を知らない人にだけ**先に**見せて、
        /// そのあと必ずこの板へ来る。この順なら食い違いは起きない。
        /// </summary>
        private const string GuideBoardPath = "Tutorial/案内板_満貫";

        // 問いかけと選択肢。**文言はフロー図から（2026-09-12）。**
        private const string ExperienceQuestion = "「って後輩ちゃんは麻雀とか知ってたっけ？」";
        private const string YesLabel = "はい";
        private const string NoLabel = "いいえ";
        private const string GuideStartLabel = "閉じる";

        // 「いいえ」を選んだときの流れ。**麻雀そのものを知らない人向け。**
        // 麻雀ルール説明UI を挟んでから、17歩ルール説明UI へ合流する。
        private static readonly List<TutorialLine> NoviceBeforeMahjongUi = new List<TutorialLine>
        {
            new TutorialLine("「そっかじゃあ説明するわ」"),
        };

        private static readonly List<TutorialLine> NoviceAfterMahjongUi = new List<TutorialLine>
        {
            new TutorialLine("「まっ難しいと思うけど一旦説明続けるね」"),
            new TutorialLine("「ここのギャンブルは麻雀をベースにした特殊なギャンブルなんだよ」"),
            new TutorialLine("「とりまこれを見てー」"),
        };

        // 「はい」を選んだときの流れ。**麻雀は分かっている前提で、この賭場の決まりだけ。**
        private static readonly List<TutorialLine> ExperiencedLines = new List<TutorialLine>
        {
            new TutorialLine("「そかそか昔一緒にやったことあったもんね」"),
            new TutorialLine("「じゃあここのギャンブルのルールだけ説明するわ」"),
        };

        // 17歩ルール説明UI を閉じたあと。**ここで2つの経路が合流して実践へ向かう。**
        private static readonly List<TutorialLine> AfterRuleUiLines = new List<TutorialLine>
        {
            new TutorialLine("「わかった？\nま、難しいと思うから、アタシと実践してみよっかー」"),
        };

        // ここから下はフロー図の続き（2026-09-19 に追加）。
        // 「わかった？…」のあと、手牌を選ばせる直前までの案内。
        // 使用フォントは Regular のみなので、青太字の指定は黄色 + 1pt で表現する。
        private const string HighlightOpen = "<color=#FFD700><size=16>";
        private const string HighlightClose = "</size></color>";

        /// <summary>山牌を見せてから、何をさせるかを言う。**どちらの経路でも共通。**</summary>
        private static readonly List<TutorialLine> ShowWallLines = new List<TutorialLine>
        {
            new TutorialLine("「じゃーん！\nこれが君の" + HighlightOpen + "山牌" + HighlightClose + "ね」"),
            new TutorialLine("「君はこの中から" + HighlightOpen + "13枚選んでテンパイな手牌" + HighlightClose + "を作ってもらうよ」"),
        };

        /// <summary>
        /// **2つめの分岐。** 最初の「麻雀を知っていますか」で『いいえ』だった人にだけ、
        /// テンパイの意味を足す。知っている人には冗長なので出さない。
        /// </summary>
        private static readonly List<TutorialLine> TenpaiExplainLines = new List<TutorialLine>
        {
            new TutorialLine("「えっテンパイって何かって？」"),
            new TutorialLine("「テンパイってのは" + HighlightOpen + "あと1牌でアガりって状態" + HighlightClose + "のこと」"),
            new TutorialLine("「ま普通にアガる手を作って１牌抜くのがラクだよ」"),
        };

        /// <summary>合流後。満貫を作らせる話へ。</summary>
        private static readonly List<TutorialLine> ManganRequestLines = new List<TutorialLine>
        {
            new TutorialLine("「で今回作ってもらうのはただのテンパイじゃないよ」"),
            new TutorialLine("「君には" + HighlightOpen + "満貫" + HighlightClose + "なテンパイを作ってもらうねー」"),
        };

        /// <summary>
        /// 満貫説明UIを閉じたあとの念押し（2026-09-23 に足した。フロー図にあったが抜けていた）。
        /// </summary>
        private static readonly List<TutorialLine> ManganRuleLines = new List<TutorialLine>
        {
            new TutorialLine("「このゲームはまず" + HighlightOpen + "満貫以上の形" + HighlightClose + "を作るのがルールでね」"),
            new TutorialLine("「満貫以上を作れないと" + HighlightOpen + "賭けにならない" + HighlightClose + "から注意して」"),
            new TutorialLine("「んじゃあ満貫を作る練習ねー」"),
        };

        /// <summary>最後のひと押し。この直後に手牌選択へ入る。</summary>
        private static readonly List<TutorialLine> StartBuildingLines = new List<TutorialLine>
        {
            new TutorialLine("「とりあえず適当に13牌触ってみてよ」"),
            new TutorialLine("「アタシが見てやるからさー」"),
        };

        /// <summary>
        /// 最初の問いかけの答え。**2つめの分岐で使うので覚えておく。**
        /// 0=はい（麻雀を知っている） / 1=いいえ / -1=まだ聞いていない。
        /// </summary>
        private int _mahjongExperienceAnswer = -1;

        /// <summary>
        /// 「17歩ルール説明UI」の**仮の板**（2026-09-19）。
        ///
        /// フロー図は説明UIを3枚（麻雀ルール／17歩ルール／満貫）要求しているが、絵は2枚しかない。
        /// `案内板_満貫` は中身が「13枚の牌から満貫手を組もう」なので**満貫説明UI**に当たり、
        /// 17歩ルール（この賭場の決まり）に当たる絵が無い。
        /// ルールの文面は企画の領分なので**こちらで書かない**。絵が届いたら差し替えること。
        /// **AIで絵を描かないこと。**
        /// </summary>
        private const string Rule17Title = "17歩ルール説明";
        private static readonly string[] Rule17Placeholder =
        {
            "（ここに17歩ルール説明UIが入ります）",
            "画像が届いたら差し替えます",
        };

        /// <summary>
        /// 山牌を「じゃーん！これが君の山牌ね」の行まで出さずにおくか（2026-09-19 のユーザー指示）。
        /// 最初から始めたチュートリアルの第1局だけ立つ。立っていれば、その行で1枚ずつ起こす。
        /// </summary>
        private bool _wallRevealDeferred;

        private GameObject _introRoot;

        /// <summary>
        /// 導入のセリフが終わったあとに問いかけるか。**`StartTutorial()` でだけ立つ。**
        /// 局を指定して始める入口（自動走行が使う）では立たないので、あちらは止まらない。
        /// </summary>
        private bool _askExperienceAfterIntro;

        /// <summary>
        /// 立ち絵を出してほしいときに呼ぶ。**1行目のセリフの後に1度だけ。**
        /// 立ち絵を持っているのは `OpeningSequenceManager`（シーン側）なので、
        /// こちらは「出して」と言うだけにしてある。
        /// </summary>
        public System.Action CharacterRevealRequested;

        /// <summary>
        /// 導入セリフのあとに「麻雀を知っているか」を聞き、答えに応じて説明を見せる
        /// （2026-09-12、フロー図どおり）。
        ///
        /// **セリフとして聞く。暗転はしない（ユーザーの指示）。**
        /// あそこは会話の途中なので、暗く落とすと流れが切れて見える。
        ///
        /// 経路は2つ。**どちらも 17歩ルール説明UI で合流する。**
        /// - いいえ … 麻雀ルール説明UI を挟んでから合流
        /// - はい　 … この賭場の決まりだけ聞いて合流
        ///
        /// 聞かない設定のとき（局を指定して始めたとき）は、何もせずに抜ける。
        /// </summary>
        private IEnumerator AskExperienceIfNeeded()
        {
            if (!_askExperienceAfterIntro) yield break;
            _askExperienceAfterIntro = false;

            CloseIntro();

            TMP_FontAsset font = BorrowJapaneseFont();
            Transform parent = BuildIntroCanvas(dim: false);
            if (parent == null) yield break;

            // 問いかけは吹き出しに出す。**「どこでも押して送る」は出さない。**
            // 出すと、答えないまま読み飛ばせてしまう。
            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(ExperienceQuestion);
            }

            int answer = -1;                      // 0=はい / 1=いいえ
            BuildQuestionPanel(parent, font, onYes: () => answer = 0, onNo: () => answer = 1);
            yield return new WaitUntil(() => answer >= 0);
            _mahjongExperienceAnswer = answer;    // 2つめの分岐で使う

            CloseIntro();

            if (answer == 1)
            {
                // 麻雀そのものを知らない人。まず麻雀の形から
                yield return PlayLines(NoviceBeforeMahjongUi);
                yield return ShowRulePanelRoutine(font, MahjongRuleTitle, MahjongRuleBody, MahjongRulePath);
                yield return PlayLines(NoviceAfterMahjongUi);
            }
            else
            {
                yield return PlayLines(ExperiencedLines);
            }

            // ここで合流。**この賭場の決まりは、どちらの経路でも必ず見せる。**
            // フロー図の「17歩ルール説明UI」。絵がまだ無いので仮の文字板（Rule17Placeholder）。
            // 以前はここに `案内板_満貫` を出していたが、あれは満貫の説明なので下へ移した（2026-09-19）
            yield return ShowRulePanelRoutine(font, Rule17Title, Rule17Placeholder, null);

            yield return PlayLines(AfterRuleUiLines);

            // ここからフロー図の続き（2026-09-19）。山牌を見せて、満貫を作らせるまで。
            // **「じゃーん！これが君の山牌ね」と同時に山牌を1枚ずつ起こす**（ユーザー指示）。
            // 待たずに走らせて、セリフの表示と重ねる
            if (_wallRevealDeferred)
            {
                _wallRevealDeferred = false;
                StartCoroutine(DealTilesRoutine());
            }
            yield return PlayLines(ShowWallLines);

            // **知らないと答えた人にだけテンパイの説明。** 知っている人はそのまま合流する
            if (_mahjongExperienceAnswer == 1)
            {
                yield return PlayLines(TenpaiExplainLines);
            }

            yield return PlayLines(ManganRequestLines);

            // フロー図の「満貫説明UI」→「閉じるボタンをクリック」。
            // `案内板_満貫` は「13枚の牌から満貫手を組もう」「困ったら左下のオートボタン」という中身で、
            // 手を組ませる直前のここが正しい位置（以前は合流点に出していた）
            yield return ShowRulePanelRoutine(font, null, null, GuideBoardPath);

            yield return PlayLines(ManganRuleLines);
            yield return PlayLines(StartBuildingLines);
        }

        /// <summary>
        /// 麻雀ルール説明UIの絵。**ユーザーが用意したもの（2026-09-12 に受け取った）。**
        /// 元のファイル名は `UI.png`。`Assets/Resources/UI.png`（別物）と紛らわしいので、
        /// 中身のとおり `麻雀のあそびかた` に改名して取り込んである。
        ///
        /// **一度 AI が描いた `麻雀の基本_アガリの形.jpg` を繋いでしまい、**
        /// 「出す画像が違う」と指摘されて差し替えた（2026-09-12）。
        /// あの絵は同日に削除済み。**AIで描いた絵を当てないこと。**
        ///
        /// 絵は 400x300 と小さいので、拡大しても潰れないよう Point フィルタで取り込んでいる。
        /// </summary>
        private const string MahjongRulePath = "Tutorial/麻雀のあそびかた";

        /// <summary>
        /// 絵が読めなかったときの控え。**ふだんは使われない。**
        /// 絵が消えただけで説明が丸ごと落ちるのを避けるために残してある。
        /// </summary>
        private const string MahjongRuleTitle = "麻雀の基本";

        private static readonly string[] MahjongRuleBody =
        {
            "牌は 萬子・筒子・索子 の3種類と、字牌。",
            "",
            "同じ牌を3枚そろえる、または同じ種類で数字を",
            "3つ続ける。これを「面子（メンツ）」という。",
            "",
            "同じ牌2枚を「雀頭（ジャントウ）」という。",
            "",
            "面子を4つと、雀頭を1つ。これがアガリの形。",
        };

        /// <summary>
        /// ルール説明の板を1枚見せて、「閉じる」を待つ。
        /// **こちらは暗転したまま。** 読ませる場なので、盤面が透けていると読みにくい。
        ///
        /// <paramref name="spritePath"/> があれば画像を、無ければ文字の板を出す。
        /// **画像はユーザーが用意したものだけを使う。AIで描かない**（AGENTS.md 第7項）。
        /// 画像が見つからないなど組み立てに失敗したときは、**黙って止まらず**すぐ先へ進める。
        /// </summary>
        private IEnumerator ShowRulePanelRoutine(TMP_FontAsset font, string title,
                                                 string[] body, string spritePath)
        {
            CloseIntro();

            Sprite board = string.IsNullOrEmpty(spritePath) ? null : Resources.Load<Sprite>(spritePath);
            if (board == null && (body == null || body.Length == 0))
            {
                Debug.LogWarning("[TutorialIntro] 説明の中身がありません: " + spritePath);
                yield break;
            }

            Transform parent = BuildIntroCanvas();
            if (parent == null) yield break;

            if (board != null)
            {
                var image = new GameObject("RuleBoard", typeof(RectTransform), typeof(Image));
                image.transform.SetParent(parent, false);
                var imageRect = (RectTransform)image.transform;
                imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                imageRect.anchoredPosition = new Vector2(0f, 30f);
                // 元画像の縦横比のまま、画面に収まる大きさへ。
                float scale = Mathf.Min(700f / board.rect.width, 450f / board.rect.height);
                imageRect.sizeDelta = new Vector2(board.rect.width * scale, board.rect.height * scale);

                var boardImage = image.GetComponent<Image>();
                boardImage.sprite = board;
                boardImage.raycastTarget = false;
            }
            else
            {
                BuildTextCard(parent, font, title, body);
            }

            bool done = false;
            CreateButton(parent, font, GuideStartLabel, new Vector2(0f, -250f), () => done = true);
            yield return new WaitUntil(() => done);

            CloseIntro();
        }

        /// <summary>文字だけの説明板。**絵を使わずに済ませるための受け皿。**</summary>
        private void BuildTextCard(Transform parent, TMP_FontAsset font, string title, string[] body)
        {
            var card = new GameObject("RuleCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(0f, 30f);
            cardRect.sizeDelta = new Vector2(620f, 380f);
            card.GetComponent<Image>().color = new Color32(28, 16, 20, 245);

            if (!string.IsNullOrEmpty(title))
            {
                var head = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                head.transform.SetParent(card.transform, false);
                var headRect = (RectTransform)head.transform;
                headRect.anchorMin = new Vector2(0.5f, 1f);
                headRect.anchorMax = new Vector2(0.5f, 1f);
                headRect.pivot = new Vector2(0.5f, 1f);
                headRect.anchoredPosition = new Vector2(0f, -22f);
                headRect.sizeDelta = new Vector2(560f, 46f);

                var headText = head.GetComponent<TextMeshProUGUI>();
                if (font != null) headText.font = font;
                headText.text = title;
                headText.fontSize = 26f;
                headText.color = new Color32(244, 214, 120, 255);
                headText.alignment = TextAlignmentOptions.Center;
                headText.raycastTarget = false;
            }

            var lines = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            lines.transform.SetParent(card.transform, false);
            var linesRect = (RectTransform)lines.transform;
            linesRect.anchorMin = new Vector2(0.5f, 1f);
            linesRect.anchorMax = new Vector2(0.5f, 1f);
            linesRect.pivot = new Vector2(0.5f, 1f);
            linesRect.anchoredPosition = new Vector2(0f, -76f);
            linesRect.sizeDelta = new Vector2(560f, 280f);

            var bodyText = lines.GetComponent<TextMeshProUGUI>();
            if (font != null) bodyText.font = font;
            bodyText.text = string.Join("\n", body);
            bodyText.fontSize = 19f;
            bodyText.lineSpacing = 12f;
            bodyText.color = new Color32(240, 232, 236, 255);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.raycastTarget = false;
        }

        /// <summary>
        /// 「経験あり」「初めて」の2つ。**問いかけの文はここには出さない。**
        /// 文は吹き出し（DialogueUI）が受け持つ（2026-09-12）。
        /// </summary>
        private void BuildQuestionPanel(Transform parent, TMP_FontAsset font, Action onYes, Action onNo)
        {
            // **卓の空いている帯に置く（2026-09-12）。** 暗転をやめたので置き場所が効く。
            // y=-40 だと相手の体に重なり、y=-150 だと手牌に被る。
            // 相手の下端と手牌の上端のあいだ（800x600 基準で y=-105 あたり）が唯一空いている。
            // **手牌を出したあとに聞く場面なので、ここは必ず空けておくこと。**
            CreateButton(parent, font, YesLabel, new Vector2(-130f, -105f), onYes);
            CreateButton(parent, font, NoLabel, new Vector2(130f, -105f), onNo);
        }

        /// <summary>全画面を覆う Canvas を作る。すでにあれば作り直さない。</summary>
        /// <param name="dim">
        /// 後ろを暗く落とすか。**問いかけでは落とさない（2026-09-12 の指示）。**
        /// あそこはセリフで聞くので、暗転すると会話が途切れて見える。
        /// 案内板は1枚の絵を読ませる場なので、こちらは落としたまま。
        /// </param>
        private Transform BuildIntroCanvas(bool dim = true)
        {
            _introRoot = new GameObject("TutorialIntro", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = _introRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.TutorialIntro;

            var scaler = _introRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // このゲーム本来の画面比。他の実行時UI（RoomScreenUI）と揃えてある。
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;
            StretchFull((RectTransform)_introRoot.transform);

            // 覆いは**必ず敷く。** `dim` は「暗くするか」であって「覆うか」ではない。
            //
            // **透明でも押さえは効く。** Unity の UI は既定で透明度を見ずに拾うので、
            // alpha 0 の Image でも背後へクリックを通さない。
            //
            // **敷かないと牌が触れてしまう（2026-09-12 に実際に起きた）。**
            // 問いかけの時点で盤面はもう出ている（牌を配る演出が先にある）。
            // 暗転をやめたときに覆いごと外したせいで、
            // 「麻雀を知ってたっけ？」の裏で牌が選べるようになっていた。
            //
            // ここで覆ってもセリフ送りは死なない。**問いかけの間は
            // `ShowAdvanceOnAnyClick` を出していない**ので、食うものが無い。
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(_introRoot.transform, false);
            StretchFull((RectTransform)scrim.transform);
            scrim.GetComponent<Image>().color = dim
                ? new Color(0f, 0f, 0f, 0.82f)
                : new Color(0f, 0f, 0f, 0f);

            return _introRoot.transform;
        }

        private void CreateButton(Transform parent, TMP_FontAsset font, string label,
                                  Vector2 position, Action onClick)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(200f, 56f);

            go.GetComponent<Image>().color = new Color32(120, 24, 32, 255);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(go.transform, false);
            StretchFull((RectTransform)textObject.transform);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = label;
            text.fontSize = 22f;
            text.color = new Color32(240, 232, 236, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }

        private void CloseIntro()
        {
            if (_introRoot == null) return;
            Destroy(_introRoot);
            _introRoot = null;
        }

        /// <summary>
        /// 画面にある文字から日本語の使えるフォントを借りる。
        /// `PixelMplus10_DynamicFixed` は同名のアセットが複数あって直接引くと当たりが不定なため、
        /// RoomScreenUI と同じやり方に揃えている。
        /// </summary>
        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label != null && label.font != null) return label.font;
            }

            return TMP_Settings.defaultFontAsset;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }
}
