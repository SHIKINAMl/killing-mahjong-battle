using System;
using KillingMahjong.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// オプションから読む、6ページのチュートリアル資料。
    /// 対局シーンは本編とチュートリアルの2つあるため、どちらにも保存せず実行時に組み立てる。
    /// </summary>
    public class TutorialArchiveUI : MonoBehaviour
    {
        private const int MaximumLinesPerPage = 7;
        private const int MaximumCharactersPerLine = 24;
        private const float ReferenceWidth = 800f;
        private const float ReferenceHeight = 600f;

        private readonly struct ArchivePage
        {
            public readonly string Title;
            public readonly string Body;

            public ArchivePage(string title, string body)
            {
                Title = title;
                Body = body;
            }
        }

        private static readonly ArchivePage[] Pages =
        {
            new ArchivePage(
                "① このギャンブルについて",
                "ここは麻雀をベースにした特殊なギャンブル。\n"
                + "勝てば賭けた点数が増え、負ければ減る。\n"
                + "先に累計30000点を取るか、\n"
                + "相手の点数を0にした方が勝ち。\n\n"
                + "（この賭場の決まりは、ここに入ります）"),
            new ArchivePage(
                "② 麻雀のおさらい",
                "配られた牌から強い役を作って戦う。\n\n"
                + "「テンパイ」とは、あと1牌でアガれる状態のこと。\n"
                + "普通にアガる手を作って、\n"
                + "そこから1牌抜くのがラク。"),
            new ArchivePage(
                "③ 手牌の作り方",
                "山牌から13枚選んでテンパイの形を作る。\n"
                + "ただし満貫以上の手でないと座れない。\n\n"
                + "牌をクリックすると手牌に登録できる。\n"
                + "困ったら「おまかせ」ボタンを押せば、\n"
                + "満貫以上の形を自動で組んでくれる。"),
            new ArchivePage(
                "④ 賭け金",
                "手牌が決まったら賭け金を決める。\n"
                + "賭けた分はその場で持ち点から引かれる。\n"
                + "勝てば役の倍率をかけて返ってくる。"),
            new ArchivePage(
                "⑤ 対局の流れ",
                "お互い1ターンに1枚ずつ、山牌から打っていく。\n"
                + "先に相手に自分の待ち牌を出させた方の勝ち。\n\n"
                + "自分の待ち牌は画面に表示されるので、\n"
                + "相手がそれを出すのを祈りながら打つ。"),
            new ArchivePage(
                "⑥ 点数",
                "勝つと賭けた分が戻り、\n"
                + "さらに役の分のボーナスがもらえる。\n"
                + "満貫なら2000賭けて2000の儲け。\n"
                + "役満などもっと強い役ならさらに増える。\n\n"
                + "負けると賭け金分を失う。\n"
                + "賭けた額も戻らないので、マイナスは2倍。")
        };

        private TMP_FontAsset font;
        private TextMeshProUGUI pageTitle;
        private TextMeshProUGUI pageBody;
        private TextMeshProUGUI pageNumber;
        private Button previousButton;
        private Button nextButton;
        private int pageIndex;
        private Action onClosed;

        public static TutorialArchiveUI Create()
        {
            var root = new GameObject("TutorialArchiveUI", typeof(RectTransform));
            return root.AddComponent<TutorialArchiveUI>();
        }

        private void Awake()
        {
            Build();
            ValidatePageBounds();
            gameObject.SetActive(false);
        }

        /// <summary>先頭ページから資料を開き、閉じたら呼び出し元へ戻す。</summary>
        public void Open(Action closedCallback)
        {
            onClosed = closedCallback;
            pageIndex = 0;
            gameObject.SetActive(true);
            RefreshPage();
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
            dimmer.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.04f, 0.78f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panel.transform.SetParent(transform, false);
            var panelRect = (RectTransform)panel.transform;
            Center(panelRect, new Vector2(740f, 480f));
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.06f, 0.12f, 0.98f);
            var panelOutline = panel.GetComponent<Outline>();
            panelOutline.effectColor = new Color32(214, 40, 62, 220);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            CreateText(panelRect, "ArchiveTitle", "チュートリアル資料", new Vector2(0f, 202f), new Vector2(460f, 34f), 26f, TextAlignmentOptions.Center);
            pageNumber = CreateText(panelRect, "PageNumber", string.Empty, new Vector2(302f, 202f), new Vector2(96f, 28f), 18f, TextAlignmentOptions.Center);
            pageTitle = CreateText(panelRect, "PageTitle", string.Empty, new Vector2(0f, 157f), new Vector2(620f, 32f), 22f, TextAlignmentOptions.Center);

            var bodyPanel = new GameObject("BodyPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            bodyPanel.transform.SetParent(panelRect, false);
            var bodyPanelRect = (RectTransform)bodyPanel.transform;
            Center(bodyPanelRect, new Vector2(640f, 280f));
            bodyPanelRect.anchoredPosition = new Vector2(0f, 0f);
            bodyPanel.GetComponent<Image>().color = new Color(0.025f, 0.02f, 0.05f, 0.9f);
            var bodyOutline = bodyPanel.GetComponent<Outline>();
            bodyOutline.effectColor = new Color(0.42f, 0.23f, 0.48f, 0.9f);
            bodyOutline.effectDistance = new Vector2(1f, -1f);

            pageBody = CreateBodyText(bodyPanelRect);
            previousButton = CreateButton(panelRect, "PreviousButton", "◀ 前へ", new Vector2(-210f, -195f), new Vector2(150f, 48f));
            nextButton = CreateButton(panelRect, "NextButton", "次へ ▶", new Vector2(0f, -195f), new Vector2(150f, 48f));
            Button closeButton = CreateButton(panelRect, "CloseButton", "閉じる", new Vector2(210f, -195f), new Vector2(150f, 48f));

            previousButton.onClick.AddListener(ShowPreviousPage);
            nextButton.onClick.AddListener(ShowNextPage);
            closeButton.onClick.AddListener(CloseArchive);
        }

        private TextMeshProUGUI CreateBodyText(RectTransform parent)
        {
            var text = CreateText(parent, "Body", string.Empty, Vector2.zero, new Vector2(592f, 240f), 20f, TextAlignmentOptions.TopLeft);
            text.margin = new Vector4(0f, 0f, 0f, 0f);
            text.enableWordWrapping = false;
            text.lineSpacing = 5f;
            // 800x600基準で検証済みの本文量を超えた場合も、枠の外へ文字を出さない。
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = (RectTransform)buttonObject.transform;
            Center(rect, size);
            rect.anchoredPosition = position;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.8f, 0.2f, 0.2f, 1f);
            var outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.03f, 0.05f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Stretch((RectTransform)labelObject.transform);
            var labelText = labelObject.GetComponent<TextMeshProUGUI>();
            ApplyFont(labelText);
            labelText.text = label;
            labelText.fontSize = 18f;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.raycastTarget = false;
            labelText.color = Color.white;

            // 既存のオプションボタンと同じホバー処理を付け、生成UIだけ操作感が変わらないようにする。
            buttonObject.AddComponent<UIButtonHoverEffect>();
            return buttonObject.GetComponent<Button>();
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
            if (pageIndex >= Pages.Length - 1) return;

            pageIndex++;
            RefreshPage();
        }

        private void RefreshPage()
        {
            ArchivePage page = Pages[pageIndex];
            pageTitle.text = page.Title;
            pageBody.text = page.Body;
            pageNumber.text = string.Format("{0} / {1}", pageIndex + 1, Pages.Length);
            SetPagingButtonEnabled(previousButton, pageIndex > 0);
            SetPagingButtonEnabled(nextButton, pageIndex < Pages.Length - 1);
        }

        /// <summary>端のページで送りボタンを無効にする色（2026-09-23 に実機で見て足した）。</summary>
        private static readonly Color PagingEnabledColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        private static readonly Color PagingDisabledColor = new Color(0.32f, 0.14f, 0.16f, 1f);

        /// <summary>
        /// 送りボタンの可否を、見た目にも出す。
        ///
        /// `interactable` を false にするだけでは、このボタンは**色が変わらない**。
        /// 標準の ColorTint は `targetGraphic` に当たるが、生成時に色を直接塗っているため、
        /// 見た目は押せるままになる（最終ページで「次へ」が明るいままだった）。
        /// </summary>
        private static void SetPagingButtonEnabled(Button button, bool enabled)
        {
            if (button == null) return;

            button.interactable = enabled;

            var image = button.targetGraphic as Image;
            if (image != null) image.color = enabled ? PagingEnabledColor : PagingDisabledColor;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.color = enabled ? Color.white : new Color(0.65f, 0.6f, 0.62f, 1f);
        }

        private void CloseArchive()
        {
            gameObject.SetActive(false);
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private static void ValidatePageBounds()
        {
            for (int i = 0; i < Pages.Length; i++)
            {
                string[] lines = Pages[i].Body.Split('\n');
                int longestLine = 0;
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    longestLine = Mathf.Max(longestLine, lines[lineIndex].Length);
                }

                if (lines.Length > MaximumLinesPerPage || longestLine > MaximumCharactersPerLine)
                {
                    Debug.LogError($"[TutorialArchiveUI] {i + 1}ページ目が表示枠の上限を超えています。");
                }
            }
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
