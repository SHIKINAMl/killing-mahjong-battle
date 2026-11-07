using System;
using KillingMahjong.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Symbol = KillingMahjong.UI.NavigationBadgeButton.Symbol;

namespace KillingMahjong.UI
{
    /// <summary>
    /// オプションから読む、チュートリアル資料。中身はユーザーが描いた「まとめ①〜⑤」の絵（2026-10-18 に差し替え）。
    /// 以前は文字だけの6ページで、ルールが変わった後も古い文面のままだった。
    /// 冒頭のルール説明（TutorialManager の17歩ルール説明UI）と同じ絵なので、読み返しても食い違わない。
    /// 対局シーンは本編とチュートリアルの2つあるため、どちらにも保存せず実行時に組み立てる。
    /// </summary>
    public class TutorialArchiveUI : MonoBehaviour
    {
        private const float ReferenceWidth = 800f;
        private const float ReferenceHeight = 600f;

        private static readonly string[] PageSpritePaths =
        {
            "Tutorial/ゲームの流れ_まとめ1",
            "Tutorial/ゲームの流れ_まとめ2",
            "Tutorial/ゲームの流れ_まとめ3",
            "Tutorial/ゲームの流れ_まとめ4",
            "Tutorial/ゲームの流れ_まとめ5",
        };

        private TMP_FontAsset font;
        private Sprite[] pages;
        private Image pageImage;
        private TextMeshProUGUI pageNumber;
        private Button previousButton;
        private Button nextButton;
        private Button closeButton;
        private int pageIndex;
        private Action onClosed;

        public static TutorialArchiveUI Create()
        {
            var root = new GameObject("TutorialArchiveUI", typeof(RectTransform));
            return root.AddComponent<TutorialArchiveUI>();
        }

        private void Awake()
        {
            LoadPages();
            Build();
            gameObject.SetActive(false);
        }

        /// <summary>いま開いているページ（0 始まり）。</summary>
        public int PageIndex { get { return pageIndex; } }

        /// <summary>ページの総数。</summary>
        public int PageCount { get { return pages.Length; } }

        /// <summary>最後のページを開いているか。</summary>
        public bool IsLastPage { get { return pageIndex >= pages.Length - 1; } }

        /// <summary>ページの絵。</summary>
        public RectTransform PageBodyRect
        {
            get { return pageImage != null ? pageImage.rectTransform : null; }
        }

        /// <summary>「次へ」。押せないときは null を返す。</summary>
        public RectTransform NextButtonRect
        {
            get
            {
                if (nextButton == null || !nextButton.interactable) return null;
                return nextButton.transform as RectTransform;
            }
        }

        /// <summary>「閉じる」。</summary>
        public RectTransform CloseButtonRect
        {
            get { return closeButton != null ? closeButton.transform as RectTransform : null; }
        }

        /// <summary>先頭ページから資料を開き、閉じたら呼び出し元へ戻す。</summary>
        public void Open(Action closedCallback)
        {
            onClosed = closedCallback;
            pageIndex = 0;
            gameObject.SetActive(true);
            RefreshPage();
        }

        private void LoadPages()
        {
            var loaded = new System.Collections.Generic.List<Sprite>();
            foreach (string path in PageSpritePaths)
            {
                var sprite = Resources.Load<Sprite>(path);
                if (sprite != null) loaded.Add(sprite);
                else Debug.LogWarning("[TutorialArchiveUI] 資料の絵が見つかりません: " + path);
            }
            pages = loaded.ToArray();
        }

        private void Build()
        {
            font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            if (font == null)
            {
                Debug.LogWarning("[TutorialArchiveUI] PixelMplus10_DynamicFixed が見つかりません。");
            }

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.TutorialArchive;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dimmer.transform.SetParent(transform, false);
            Stretch((RectTransform)dimmer.transform);
            dimmer.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.86f);

            var root = (RectTransform)transform;
            CreateText(root, "ArchiveTitle", "チュートリアル資料", new Vector2(0f, 262f), new Vector2(460f, 34f), 24f, TextAlignmentOptions.Center);

            // 絵は 722x418。等倍で出す（拡大縮小するとドットが不揃いになる）
            var picture = new GameObject("Page", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(root, false);
            Center((RectTransform)picture.transform, new Vector2(722f, 418f));
            ((RectTransform)picture.transform).anchoredPosition = new Vector2(0f, 20f);
            pageImage = picture.GetComponent<Image>();
            pageImage.raycastTarget = false;

            pageNumber = CreateText(root, "PageNumber", string.Empty, new Vector2(330f, 262f), new Vector2(96f, 34f), 20f, TextAlignmentOptions.Center);
            var buttonSize = new Vector2(150f, 48f);
            previousButton = NavigationBadgeButton.Create(root, "PreviousButton", "前へ", font,
                new Vector2(-210f, -250f), buttonSize, Symbol.Back);
            nextButton = NavigationBadgeButton.Create(root, "NextButton", "次へ", font,
                new Vector2(0f, -250f), buttonSize, Symbol.Next);
            closeButton = NavigationBadgeButton.Create(root, "CloseButton", "閉じる", font,
                new Vector2(210f, -250f), buttonSize, Symbol.Close);

            previousButton.onClick.AddListener(ShowPreviousPage);
            nextButton.onClick.AddListener(ShowNextPage);
            closeButton.onClick.AddListener(CloseArchive);
        }

        private TextMeshProUGUI CreateText(RectTransform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            Center(rect, size);
            rect.anchoredPosition = position;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = Color.white;
            return text;
        }

        private void ApplyFont(TMP_Text text)
        {
            if (font != null) text.font = font;
        }

        private void ShowPreviousPage()
        {
            if (pageIndex <= 0) return;

            pageIndex--;
            RefreshPage();
        }

        private void ShowNextPage()
        {
            if (pageIndex >= pages.Length - 1) return;

            pageIndex++;
            RefreshPage();
        }

        private void RefreshPage()
        {
            pageImage.sprite = pages.Length > 0 ? pages[pageIndex] : null;
            pageImage.enabled = pages.Length > 0;
            pageNumber.text = string.Format("{0} / {1}", pages.Length > 0 ? pageIndex + 1 : 0, pages.Length);
            previousButton.interactable = pageIndex > 0;
            nextButton.interactable = pageIndex < pages.Length - 1;
        }

        private void CloseArchive()
        {
            gameObject.SetActive(false);
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>
    /// チュートリアル第1局でだけ開く、負けたときのダメージの説明。
    /// シーンに置くと本編にも混ざるため、資料UIと同じく実行時に1枚だけ組み立てる。
    /// </summary>
    public class TutorialDamageExplanationUI : MonoBehaviour
    {
        private const float ReferenceWidth = 800f;
        private const float ReferenceHeight = 600f;

        private Action _onClosed;
        private TMP_FontAsset _font;

        public static TutorialDamageExplanationUI Create()
        {
            var root = new GameObject("TutorialDamageExplanationUI", typeof(RectTransform));
            return root.AddComponent<TutorialDamageExplanationUI>();
        }

        private void Awake()
        {
            Build();
            gameObject.SetActive(false);
        }

        /// <summary>閉じるボタンが押されるまで、指定された1ページだけを表示する。</summary>
        public void Open(Action closedCallback)
        {
            _onClosed = closedCallback;
            gameObject.SetActive(true);
        }

        private void Build()
        {
            _font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.TutorialArchive;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var dimmer = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dimmer.transform.SetParent(transform, false);
            Stretch((RectTransform)dimmer.transform);
            dimmer.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.78f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panel.transform.SetParent(transform, false);
            var panelRect = (RectTransform)panel.transform;
            Center(panelRect, new Vector2(650f, 380f));
            panelRect.localScale = Vector3.one * (722f / 650f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.12f, 0.98f);
            var panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color32(214, 40, 62, 220);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(panelRect, "Title", "負けたときのダメージ", new Vector2(0f, 132f),
                new Vector2(560f, 34f), 26f, TextAlignmentOptions.Center);

            var body = new GameObject("Body", typeof(RectTransform), typeof(Image), typeof(Outline));
            body.transform.SetParent(panelRect, false);
            var bodyRect = (RectTransform)body.transform;
            Center(bodyRect, new Vector2(560f, 190f));
            bodyRect.anchoredPosition = new Vector2(0f, 18f);
            body.GetComponent<Image>().color = new Color(0.025f, 0.02f, 0.05f, 0.9f);
            var bodyOutline = body.GetComponent<Outline>();
            bodyOutline.effectColor = new Color(0.42f, 0.23f, 0.48f, 0.9f);
            bodyOutline.effectDistance = new Vector2(1f, -1f);

            var bodyText = CreateText(bodyRect, "BodyText",
                "賭けた分だけ持ち点を失う。\n賭けた額も戻らないので、マイナスは2倍。\n\n相手が満貫以上の役を作っていると、\nダメージはさらに増える。",
                Vector2.zero, new Vector2(510f, 150f), 20f, TextAlignmentOptions.TopLeft);
            bodyText.textWrappingMode = TextWrappingModes.NoWrap;
            bodyText.lineSpacing = 5f;
            bodyText.overflowMode = TextOverflowModes.Ellipsis;

            Button closeButton = CreateCloseButton(panelRect);
            closeButton.onClick.AddListener(Close);
        }

        private Button CreateCloseButton(RectTransform parent)
        {
            var buttonObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            Center(rect, new Vector2(180f, 48f));
            rect.anchoredPosition = new Vector2(0f, -132f);

            buttonObject.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.03f, 0.05f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Stretch((RectTransform)labelObject.transform);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            ApplyFont(label);
            label.text = "閉じる";
            label.fontSize = 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            // 資料UIと同じホバーを付け、チュートリアル中だけ操作感が変わらないようにする。
            buttonObject.AddComponent<UIButtonHoverEffect>();
            return buttonObject.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateText(RectTransform parent, string name, string textValue,
            Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var rect = (RectTransform)textObject.transform;
            Center(rect, size);
            rect.anchoredPosition = position;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            ApplyFont(text);
            text.text = textValue;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void ApplyFont(TMP_Text text)
        {
            if (_font != null) text.font = _font;
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Action callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
