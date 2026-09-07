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
        /// <summary>未経験者向け案内板。Resources 直下からの相対パス（拡張子なし）。</summary>
        private const string GuideBoardPath = "Tutorial/麻雀の基本_アガリの形";

        // ユーザーが選んだ B 案の問いかけと選択肢。
        private const string ExperienceQuestion = "あなた、麻雀は打てるの？";
        private const string YesLabel = "経験あり";
        private const string NoLabel = "初めて";
        private const string GuideStartLabel = "わかった";

        // 未経験を選んだ時だけ、説明画像の前に見せる短い会話。
        private static readonly List<TutorialLine> BeginnerIntroLines = new List<TutorialLine>
        {
            new TutorialLine("……素人が紛れ込んできたわけね。"),
            new TutorialLine("いい度胸だこと。自分の命のルールも知らないで契約したの？"),
            new TutorialLine("仕方ないわ。基本のアガリの形くらいは頭に叩き込んでおきなさい。"),
        };

        private GameObject _introRoot;

        /// <summary>
        /// 問いかけを出し、答えが出てから <paramref name="onDecided"/> を呼ぶ。
        /// 画像が無いなど組み立てに失敗したときは、**黙って止まらず**すぐ先へ進める。
        /// </summary>
        private void AskExperienceThenStart(int roundIndex)
        {
            CloseIntro();

            TMP_FontAsset font = BorrowJapaneseFont();
            Transform parent = BuildIntroCanvas();
            if (parent == null)
            {
                StartTutorialFrom(roundIndex);
                return;
            }

            BuildQuestionPanel(parent, font,
                onYes: () =>
                {
                    CloseIntro();
                    StartTutorialFrom(roundIndex);
                },
                onNo: () => StartCoroutine(PlayBeginnerIntroThenShowGuide(roundIndex, font)));
        }

        /// <summary>
        /// 未経験者への短い会話を送り、説明画像を表示する。
        /// 会話は既存の DialogueUI を使い、画像だけを実行時生成の Canvas に置く。
        /// </summary>
        private IEnumerator PlayBeginnerIntroThenShowGuide(int roundIndex, TMP_FontAsset font)
        {
            CloseIntro();

            yield return PlayLines(BeginnerIntroLines);

            // 画像の下に直前の吹き出しが残らないようにしてからカードを前面に出す。
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            ShowGuideBoard(roundIndex, font);
        }

        /// <summary>未経験者向け案内板を1枚見せて、「わかった」で本編へ合流する。</summary>
        private void ShowGuideBoard(int roundIndex, TMP_FontAsset font)
        {
            CloseIntro();

            Sprite board = Resources.Load<Sprite>(GuideBoardPath);
            if (board == null)
            {
                // 画像が見つからないだけで進めなくなるのは行き過ぎ。記録して先へ進める。
                Debug.LogWarning("[TutorialIntro] 案内板の画像が見つかりません: " + GuideBoardPath);
                StartTutorialFrom(roundIndex);
                return;
            }

            Transform parent = BuildIntroCanvas();
            if (parent == null)
            {
                StartTutorialFrom(roundIndex);
                return;
            }

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

            CreateButton(parent, font, GuideStartLabel, new Vector2(0f, -250f), () =>
            {
                CloseIntro();
                StartTutorialFrom(roundIndex);
            });
        }

        /// <summary>問いかけの文と、経験あり／初めての2つ。</summary>
        private void BuildQuestionPanel(Transform parent, TMP_FontAsset font, Action onYes, Action onNo)
        {
            var label = new GameObject("Question", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(parent, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(0f, 80f);
            labelRect.sizeDelta = new Vector2(600f, 80f);

            var text = label.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = ExperienceQuestion;
            text.fontSize = 28f;
            text.color = new Color32(240, 232, 236, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            CreateButton(parent, font, YesLabel, new Vector2(-115f, -40f), onYes);
            CreateButton(parent, font, NoLabel, new Vector2(115f, -40f), onNo);
        }

        /// <summary>全画面を覆う Canvas を作る。すでにあれば作り直さない。</summary>
        private Transform BuildIntroCanvas()
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
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(_introRoot.transform, false);
            StretchFull((RectTransform)scrim.transform);
            scrim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

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
