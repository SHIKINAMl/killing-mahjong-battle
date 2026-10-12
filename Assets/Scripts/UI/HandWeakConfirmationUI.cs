using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>満貫以上の待ちがないときだけ開く確認枠。判定・送信・白フラッシュは持たない。</summary>
    public sealed class HandWeakConfirmationUI : MonoBehaviour
    {
        public const string RootName = "HandWeakConfirmation";
        private TextMeshProUGUI reason;
        private Button confirm, back;
        private Action onConfirm, onBack;
        public bool IsOpen { get; private set; }
        private static readonly Color Ink = new Color32(37, 27, 35, 250);
        private static readonly Color Cream = new Color32(239, 238, 216, 255);

        public static HandWeakConfirmationUI Ensure(Transform parent, RectTransform buttonTemplate = null)
        {
            var go = SceneFirst.Child(parent, RootName, out bool made, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HandWeakConfirmationUI));
            var ui = go.GetComponent<HandWeakConfirmationUI>();
            if (made)
            {
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UISortingOrders.ConfirmationDialog + 1;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800, 600); scaler.matchWidthOrHeight = .5f;
            }
            ui.Build(buttonTemplate);
            if (made) go.SetActive(false);
            return ui;
        }

        private void Build(RectTransform template)
        {
            var blocker = Rect(transform, "TransparentInputBlocker", 0, 0, 800, 600, out bool made);
            if (made) { var image = blocker.gameObject.AddComponent<Image>(); image.color = Color.clear; }
            var panel = Rect(transform, "ConfirmationPanel", 244, 322, 480, 98, out made);
            if (made) { var image = panel.gameObject.AddComponent<Image>(); image.color = Ink; }
            var border = Rect(panel, "PixelBorder", 0, 0, 480, 98, out made);
            if (made) { var graphic = border.gameObject.AddComponent<PixelShapeGraphic>(); graphic.raycastTarget = false; graphic.color = Cream; }
            var shape = border.GetComponent<PixelShapeGraphic>(); shape.Begin(); shape.Frame(border.rect, 2, shape.color); shape.End();
            var label = Rect(panel, "Reason", 10, 10, 460, 28, out made);
            if (made) ConfigureLabel(label.gameObject.AddComponent<TextMeshProUGUI>(), 20);
            reason = label.GetComponent<TextMeshProUGUI>();
            back = MakeButton(panel, "BackToSelection", "選び直す", 16, 170, template, new Color32(63, 57, 81, 255));
            confirm = MakeButton(panel, "ConfirmWeakHand", "この手牌で決定", 204, 260, template, new Color32(186, 25, 48, 255));
            back.onClick.RemoveListener(Cancel); back.onClick.AddListener(Cancel);
            confirm.onClick.RemoveListener(Confirm); confirm.onClick.AddListener(Confirm);
        }

        public void Show(bool noWaits, Action accepted, Action cancelled)
        {
            IsOpen = true; onConfirm = accepted; onBack = cancelled;
            reason.text = noWaits ? "待ち牌がありません" : "満貫以上の待ち牌がありません";
            confirm.interactable = back.interactable = true;
            gameObject.SetActive(true);
        }
        public void Confirm()
        {
            if (!IsOpen) return;
            var action = onConfirm; Hide(); action?.Invoke();
        }
        public void Cancel()
        {
            if (!IsOpen) return;
            var action = onBack; Hide(); action?.Invoke();
        }
        public void Hide()
        {
            IsOpen = false; onConfirm = onBack = null;
            if (confirm != null) confirm.interactable = false;
            if (back != null) back.interactable = false;
            gameObject.SetActive(false);
        }
        private void OnDisable() { IsOpen = false; onConfirm = onBack = null; }

        private static Button MakeButton(Transform parent, string name, string caption, float x, float width,
            RectTransform template, Color color)
        {
            var go = SceneFirst.FindChild(parent, name);
            if (go == null)
            {
                if (template != null)
                {
                    go = Instantiate(template.gameObject, parent, false); go.name = name; SceneFirst.NoteCreated(name);
                    go.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
                }
                else go = SceneFirst.Child(parent, name, out _, typeof(RectTransform), typeof(Image), typeof(Button));
                var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(x, -49); r.sizeDelta = new Vector2(width, 38); r.localScale = Vector3.one;
                var image = go.GetComponent<Image>();
                if (image == null) image = go.AddComponent<Image>();
                image.color = color;
                go.GetComponent<Button>().targetGraphic = image;
                var label = go.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label == null)
                {
                    var lr = Rect(r, "Label", 4, 0, width - 8, 38, out _);
                    label = lr.gameObject.AddComponent<TextMeshProUGUI>();
                }
                ConfigureLabel(label, 18); label.text = caption;
                go.SetActive(true);
            }
            UIInteractionFeedback.Attach(go);
            return go.GetComponent<Button>();
        }
        private static void ConfigureLabel(TextMeshProUGUI text, int size)
        {
            text.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed"); text.color = Cream;
            text.fontSize = size; text.fontSizeMax = size; text.fontSizeMin = 14; text.enableAutoSizing = true;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.margin = Vector4.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h, out bool made)
        {
            var go = SceneFirst.Child(parent, name, out made, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            if (made) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
            return r;
        }
    }
}
