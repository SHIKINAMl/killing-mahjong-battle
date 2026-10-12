using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.EngineData;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>サーバーから届いた待ち牌の登場・情報・光の移送。通信や役判定を持たない。</summary>
    public sealed class HandWaitRevealUI : MonoBehaviour
    {
        public const string RootName = "HandWaitReveal";
        private readonly List<RectTransform> cards = new List<RectTransform>();
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private readonly List<PixelShapeGraphic> marks = new List<PixelShapeGraphic>();
        private WaitData[] waits = Array.Empty<WaitData>();
        private CanvasGroup info;
        private Transform content;
        private PixelShapeGraphic pixels;
        private CanvasGroup presentation;
        private RectTransform noWaitBox;
        private HandUI hoverHand;
        private bool hasPresentation, forceVisible;
        public bool IsPresentationVisible => gameObject.activeInHierarchy && (presentation == null || presentation.alpha > .001f);
        private int version;
        public bool IsTransferring { get; private set; }
        public int LastArrivalFrame { get; private set; } = -1;
        public int ArrivalCount { get; private set; }

        private static readonly Color Ink = new Color32(37, 27, 35, 238);
        private static readonly Color Cream = new Color32(239, 238, 216, 255);

        public static HandWaitRevealUI Ensure(Transform parent)
        {
            var root = SceneFirst.Child(parent, RootName, out bool made, typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HandWaitRevealUI));
            var ui = root.GetComponent<HandWaitRevealUI>();
            if (made)
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UISortingOrders.ConfirmationDialog;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800, 600);
                scaler.matchWidthOrHeight = .5f;
            }
            ui.BuildStatic();
            if (made) root.SetActive(false);
            return ui;
        }

        private void BuildStatic()
        {
            presentation = GetComponent<CanvasGroup>();
            if (presentation == null) presentation = gameObject.AddComponent<CanvasGroup>();
            var panel = Rect(transform, "StrengthPanel", 16, 418, 220, 178, out bool made);
            if (made)
            {
                panel.gameObject.AddComponent<Image>().color = Ink;
                panel.gameObject.GetComponent<Image>().raycastTarget = false;
                var frame = Rect(panel, "Frame", 0, 0, 220, 178, out _);
                var border = frame.gameObject.AddComponent<PixelShapeGraphic>();
                border.raycastTarget = false;
                border.Begin(); border.Frame(frame.rect, 2, Cream); border.End();
                panel.gameObject.AddComponent<CanvasGroup>();
            }
            Label(panel, "InfoLabel", 12, 10, 130, 25, 18);
            Label(panel, "Han", 148, 10, 60, 25, 18, TextAlignmentOptions.MidlineRight);
            Label(panel, "Rank", 12, 39, 196, 34, 26);
            Label(panel, "Yaku", 12, 80, 196, 88, 18, TextAlignmentOptions.TopLeft);
            Rect(transform, "Candidates", 0, 0, 800, 600, out _);
            info = panel.GetComponent<CanvasGroup>();
            content = transform.Find("Candidates");
            // PixelShapeGraphicの形は保存されないので、保存済みの位置・色から描き直す。
            var frameGraphic = panel.Find("Frame").GetComponent<PixelShapeGraphic>();
            if (made) frameGraphic.color = Cream;
            frameGraphic.Begin(); frameGraphic.Frame(frameGraphic.rectTransform.rect, 2, frameGraphic.color); frameGraphic.End();
            noWaitBox = Rect(content, "NoWaitBox", 254, 430, 64, 74, out made);
            if (made)
            {
                var image = noWaitBox.gameObject.AddComponent<Image>(); image.color = Ink; image.raycastTarget = false;
                var border = Rect(noWaitBox, "PixelBorder", 0, 0, 64, 74, out _);
                var graphic = border.gameObject.AddComponent<PixelShapeGraphic>(); graphic.raycastTarget = false; graphic.color = Cream;
                Label(noWaitBox, "None", 2, 2, 60, 70, 22, TextAlignmentOptions.Center).text = "なし";
                noWaitBox.gameObject.SetActive(false);
            }
            var noneFrame = noWaitBox.Find("PixelBorder").GetComponent<PixelShapeGraphic>();
            noneFrame.Begin(); noneFrame.Frame(noneFrame.rectTransform.rect, 2, noneFrame.color); noneFrame.End();
        }

        private static Vector2 PointerPosition => UnityEngine.InputSystem.Mouse.current != null
            ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : new Vector2(-100000, -100000);
        public void BindHoverTargets(HandUI hand) { hoverHand = hand; RefreshHoverVisibility(PointerPosition); }
        public void SetForcedVisible(bool visible) { forceVisible = visible; RefreshHoverVisibility(PointerPosition); }
        private void Update() { RefreshHoverVisibility(PointerPosition); }
        public void RefreshHoverVisibility(Vector2 point)
        {
            if (presentation == null) return;
            bool show = hoverHand == null || forceVisible || IsTransferring;
            if (!show)
            {
                foreach (var tile in hoverHand.GetHandSlots()) if (Contains(tile, point)) { show = true; break; }
                show |= Contains(hoverHand.DecideButtonRect, point);
                // 開いている候補にカーソルを移しても、選ぶ前に消さない。
                if (!show && presentation.alpha > .001f)
                    foreach (var card in cards) if (Contains(card, point)) { show = true; break; }
            }
            presentation.alpha = show && (hoverHand == null || forceVisible || IsTransferring) ? 1
                : Mathf.MoveTowards(presentation.alpha, show ? 1 : 0, Time.unscaledDeltaTime / .08f);
            presentation.blocksRaycasts = show && !IsTransferring;
            presentation.interactable = show && !IsTransferring;
        }
        private static bool Contains(RectTransform rect, Vector2 point)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            return RectTransformUtility.RectangleContainsScreenPoint(rect, point,
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null);
        }

        public void Show(WaitData[] data, TileResourceManager tiles, bool animate = true)
        {
            Hide();
            ArrivalCount = 0; LastArrivalFrame = -1;
            if (data == null || tiles == null) return;
            gameObject.SetActive(true);
            BuildStatic();
            var valid = new List<WaitData>();
            foreach (var w in data) if (w != null) valid.Add(w);
            waits = valid.ToArray();
            hasPresentation = true;
            info.alpha = animate ? 0f : 1f;
            info.blocksRaycasts = false;
            if (waits.Length == 0)
            {
                noWaitBox.gameObject.SetActive(true);
                info.transform.Find("InfoLabel").GetComponent<TMP_Text>().text = "待ち牌 0種類";
                info.transform.Find("Han").GetComponent<TMP_Text>().text = "";
                info.transform.Find("Rank").GetComponent<TMP_Text>().text = "待ち牌なし";
                info.transform.Find("Yaku").GetComponent<TMP_Text>().text = "";
            }
            float step = Mathf.Min(72f, 538f / Mathf.Max(1, waits.Length));
            for (int i = 0; i < waits.Length; i++)
            {
                int index = i;
                var card = Rect(content, "WaitChoice" + i, 254 + i * step, 430, Mathf.Min(64, step - 4), 74, out _);
                var image = card.gameObject.AddComponent<Image>(); image.color = Ink;
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                button.onClick.AddListener(() => Select(index));
                var tile = Rect(card, "WaitTile", (card.sizeDelta.x - 44) / 2, 0, 44, 59, out _);
                var tileImage = tile.gameObject.AddComponent<Image>();
                tileImage.sprite = tiles.GetTileSprite(waits[i].tile); tileImage.preserveAspect = true; tileImage.raycastTarget = false;
                Label(card, "WaitRank", 2, 49, card.sizeDelta.x - 4, 23, 16, TextAlignmentOptions.Center).text = waits[i].mangan_or_more ? Rank(waits[i]) : waits[i].han + "翻";
                var markRect = Rect(card, "SelectedFrame", 0, 0, card.sizeDelta.x, 74, out _);
                var mark = markRect.gameObject.AddComponent<PixelShapeGraphic>(); mark.raycastTarget = false; marks.Add(mark);
                var cg = card.gameObject.AddComponent<CanvasGroup>(); cg.alpha = animate ? 0 : 1;
                cards.Add(card); groups.Add(cg);
            }
            int best = 0;
            for (int i = 1; i < waits.Length; i++) if (waits[i].multiplier > waits[best].multiplier) best = i;
            if (waits.Length > 0) Select(best);
            if (animate) StartCoroutine(Reveal(version));
            presentation.alpha = hoverHand == null ? 1 : 0; RefreshHoverVisibility(PointerPosition);
        }

        private IEnumerator Reveal(int stamp)
        {
            var positions = new Vector2[cards.Count];
            for (int i = 0; i < cards.Count; i++) positions[i] = cards[i].anchoredPosition;
            float duration = .2f + Mathf.Max(0, cards.Count - 1) * .085f;
            for (float elapsed = 0; elapsed <= duration && stamp == version; elapsed += Time.deltaTime)
            {
                info.alpha = Mathf.Clamp01(elapsed / .2f);
                for (int i = 0; i < cards.Count; i++)
                {
                    float u = Mathf.Clamp01((elapsed - i * .085f) / .2f);
                    float ease = 1 - Mathf.Pow(1 - u, 3);
                    cards[i].anchoredPosition = positions[i] + new Vector2(42, -8) * (1 - ease);
                    groups[i].alpha = Mathf.Min(1, u * 3);
                    cards[i].localScale = Vector3.one * Mathf.Lerp(.82f, 1, ease);
                }
                yield return null;
            }
            if (stamp != version) yield break;
            info.alpha = 1;
            for (int i = 0; i < cards.Count; i++) { cards[i].anchoredPosition = positions[i]; cards[i].localScale = Vector3.one; groups[i].alpha = 1; }
        }

        private void Select(int index)
        {
            var w = waits[index];
            info.transform.Find("InfoLabel").GetComponent<TMP_Text>().text = "待ち牌 " + waits.Length + "種類";
            info.transform.Find("Han").GetComponent<TMP_Text>().text = w.han + "翻";
            info.transform.Find("Rank").GetComponent<TMP_Text>().text = Rank(w) + (w.multiplier > 0 ? " ×" + w.multiplier.ToString("0.#", CultureInfo.InvariantCulture) : "");
            info.transform.Find("Yaku").GetComponent<TMP_Text>().text = w.yaku == null ? "" : string.Join("\n", w.yaku);
            for (int i = 0; i < marks.Count; i++)
            {
                var mark = marks[i]; mark.Begin();
                UIInteractionFeedback.Attach(cards[i].gameObject).SetSelected(i == index);
                mark.End();
            }
        }

        public static string Rank(WaitData w) => !string.IsNullOrEmpty(w.multiplier_label)
            ? w.multiplier_label : w.mangan_or_more ? "満貫" : "満貫未満";

        public bool HasCandidates(WaitData[] data)
        {
            if (!hasPresentation || !gameObject.activeInHierarchy || data == null || data.Length != waits.Length) return false;
            for (int i = 0; i < data.Length; i++) if (data[i] == null || data[i].tile != waits[i].tile) return false;
            return true;
        }

        public IEnumerator Transfer(WaitUI target)
        {
            if (target == null || waits.Length == 0) { Hide(); yield break; }
            StopAllCoroutines();
            int stamp = ++version;
            IsTransferring = true; ArrivalCount = 0;
            RefreshHoverVisibility(PointerPosition);
            info.alpha = 0;
            foreach (var cg in groups) cg.blocksRaycasts = false;
            var go = new GameObject("WaitFlightPixels", typeof(RectTransform), typeof(PixelShapeGraphic));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(800, 600);
            pixels = go.GetComponent<PixelShapeGraphic>(); pixels.raycastTarget = false;
            var ids = new List<int>(); var nonMangan = new List<int>();
            foreach (var w in waits) { ids.Add(w.tile); if (!w.mangan_or_more) nonMangan.Add(w.tile); }
            bool finished = false;
            try
            {
                yield return new WaitForSeconds(.12f);
                if (stamp != version) yield break;
                target.PrepareArrival(ids, nonMangan);
                for (int page = 0; page * 3 < waits.Length && stamp == version; page++)
                {
                    int count = Mathf.Min(3, waits.Length - page * 3);
                    target.PrepareArrivalPage(page);
                    var starts = new Vector2[count]; var ends = new Vector2[count]; var arrived = new bool[count];
                    for (int i = 0; i < count; i++) { starts[i] = Center(cards[page * 3 + i]); ends[i] = Center(target.GetArrivalTileRect(i)); }
                    float duration = .70f + (count - 1) * .055f;
                    for (float t = 0; t < duration + Time.deltaTime && stamp == version; t += Time.deltaTime)
                    {
                        pixels.Begin();
                        for (int i = 0; i < count; i++)
                        {
                            int index = page * 3 + i;
                            float raw = (t - i * .055f) / .70f;
                            if (raw < 0) continue;
                            groups[index].alpha = 0;
                            if (raw >= 1)
                            {
                                if (!arrived[i]) { arrived[i] = true; LastArrivalFrame = Time.frameCount; target.RevealArrivalTile(i); ArrivalCount++; }
                                continue;
                            }
                            float u = raw < .5f ? 4 * raw * raw * raw : 1 - Mathf.Pow(-2 * raw + 2, 3) / 2;
                            DrawFlight(index, starts[i], ends[i], u);
                        }
                        pixels.End();
                        yield return null;
                    }
                    if (page * 3 + count < waits.Length) yield return new WaitForSeconds(Mathf.Max(0, 1.5f - duration));
                }
                if (stamp != version) yield break;
                target.FinishArrival(); finished = true;
            }
            finally
            {
                if (!finished && target != null) target.Hide();
                IsTransferring = false;
                ClearFlight();
            }
            Hide();
        }

        private Vector2 Center(RectTransform rect)
        {
            if (rect == null) return Vector2.zero;
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,
                RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)), null, out Vector2 pos);
            return pos;
        }

        private void DrawFlight(int index, Vector2 start, Vector2 end, float progress)
        {
            float height = 140 + index % 3 * 8;
            for (int j = 5; j >= 0; j--)
            {
                float u = Mathf.Clamp01(progress - j * .055f);
                var p = Vector2.Lerp(start, end, u) + new Vector2(0, 4 * height * u * (1 - u));
                p = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
                bool gold = j == 0 ? index % 3 == 1 : (j + index) % 3 == 1;
                var tint = gold ? new Color(1, .93f, .55f, 1) : new Color(1, .99f, .94f, 1);
                tint.a = j == 0 ? 1 : (6 - j) * .14f;
                if (j == 0) { var halo = tint; halo.a = .24f; pixels.BoxCentered(p, 16, 16, halo); }
                float size = j == 0 ? 10 : Mathf.Max(2, 7 - j);
                pixels.BoxCentered(p, size, size, tint);
                if (j == 0) pixels.BoxCentered(p, 4, 4, Color.white);
            }
        }

        public void Hide()
        {
            hasPresentation = false; forceVisible = false;
            if (noWaitBox != null) noWaitBox.gameObject.SetActive(false);
            version++; StopAllCoroutines(); ClearFlight();
            foreach (var card in cards) if (card != null) { card.SetParent(null, false); Destroy(card.gameObject); }
            cards.Clear(); groups.Clear(); marks.Clear(); waits = Array.Empty<WaitData>();
            gameObject.SetActive(false);
        }
        private void ClearFlight() { if (pixels != null) { pixels.gameObject.SetActive(false); Destroy(pixels.gameObject); pixels = null; } }
        private void OnDisable() { version++; ClearFlight(); }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h, out bool made)
        {
            var go = SceneFirst.Child(parent, name, out made, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            if (made) { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
            return rect;
        }
        private static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float w, float h, int size, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var rt = Rect(parent, name, x, y, w, h, out bool made);
            if (!made) return rt.GetComponent<TextMeshProUGUI>();
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            label.fontSize = size; label.fontSizeMax = size; label.fontSizeMin = size == 26 ? 20 : 12;
            label.enableAutoSizing = name != "Yaku"; label.color = Cream; label.raycastTarget = false; label.margin = Vector4.zero;
            label.alignment = align; label.text = "";
            label.textWrappingMode = name == "Yaku" ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
    }
}
