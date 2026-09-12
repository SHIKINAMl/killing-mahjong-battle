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
        /// 一度 AI が描いた `麻雀の基本_アガリの形.jpg` に置き換わったが、
        /// あれは「3枚組×4＋2枚組×1＝14枚」という一般的な麻雀の説明で、
        /// **このゲームの「山牌から13枚選んで満貫以上を作る」という決まりと食い違う。**
        /// 未経験者に、この対局では使わない知識を教えることになるので戻した。
        /// </summary>
        private const string GuideBoardPath = "Tutorial/案内板_満貫";

        // ユーザーが選んだ B 案の問いかけと選択肢。
        private const string ExperienceQuestion = "あなた、麻雀は打てるの？";
        private const string YesLabel = "経験あり";
        private const string NoLabel = "初めて";
        private const string GuideStartLabel = "わかった";

        // 未経験を選んだ時だけ、説明画像の前に見せる短い会話。
        //
        // **最後の一行は案内板の中身に合わせること。** 案内板は「13枚から満貫手を狙う」
        // という、この対局の決まりを説明したもの。ここで別の話（アガリの形など）を
        // 予告すると、出てくる板と食い違う。
        private static readonly List<TutorialLine> BeginnerIntroLines = new List<TutorialLine>
        {
            new TutorialLine("……素人が紛れ込んできたわけね。"),
            new TutorialLine("いい度胸だこと。自分の命のルールも知らないで契約したの？"),
            new TutorialLine("仕方ないわ。『満貫』の意味くらいは頭に叩き込んでおきなさい。"),
        };

        private GameObject _introRoot;

        /// <summary>
        /// 導入セリフのあとに経験を聞くか。**`StartTutorial()` でだけ立つ。**
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
        /// 導入セリフのあとに経験を聞く（2026-09-12 に位置を移した）。
        ///
        /// **セリフとして聞く。暗転はしない（ユーザーの指示）。**
        /// あそこは会話の途中なので、暗く落とすと流れが切れて見える。
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

            int answer = -1;                      // 0=経験あり / 1=初めて
            BuildQuestionPanel(parent, font, onYes: () => answer = 0, onNo: () => answer = 1);
            yield return new WaitUntil(() => answer >= 0);

            CloseIntro();

            if (answer == 0) yield break;         // 経験ありはそのまま先へ

            yield return PlayLines(BeginnerIntroLines);

            // 画像の下に直前の吹き出しが残らないようにしてからカードを前面に出す。
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            yield return ShowGuideBoardRoutine(font);
        }

        /// <summary>
        /// 未経験者向け案内板を1枚見せて、「わかった」を待つ。
        /// **こちらは暗転したまま。** 1枚の絵を読ませる場なので、盤面が透けていると読みにくい。
        /// 画像が無いなど組み立てに失敗したときは、**黙って止まらず**すぐ先へ進める。
        /// </summary>
        private IEnumerator ShowGuideBoardRoutine(TMP_FontAsset font)
        {
            CloseIntro();

            Sprite board = Resources.Load<Sprite>(GuideBoardPath);
            if (board == null)
            {
                // 画像が見つからないだけで進めなくなるのは行き過ぎ。記録して先へ進める。
                Debug.LogWarning("[TutorialIntro] 案内板の画像が見つかりません: " + GuideBoardPath);
                yield break;
            }

            Transform parent = BuildIntroCanvas();
            if (parent == null) yield break;

            var image = new GameObject("GuideBoard", typeof(RectTransform), typeof(Image));
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

            bool done = false;
            CreateButton(parent, font, GuideStartLabel, new Vector2(0f, -250f), () => done = true);
            yield return new WaitUntil(() => done);

            CloseIntro();
        }

        /// <summary>問いかけの文と、経験あり／初めての2つ。</summary>
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

            // 後ろの盤面を暗く落とす。ここを押しても何も起きないようにする覆いも兼ねる。
            // **落とさないときは覆いごと作らない。** 透明な覆いを残すと、
            // 背後のセリフ送り（画面のどこでも押せる）をこれが食ってしまう。
            if (dim)
            {
                var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
                scrim.transform.SetParent(_introRoot.transform, false);
                StretchFull((RectTransform)scrim.transform);
                scrim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
            }

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
