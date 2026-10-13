using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>届いた役の説明だけを表示。役判定・通信を持たず、本編と試写で共用する。</summary>
    public sealed class HandRoleConditionsUI : MonoBehaviour
    {
        public const string RootName = "HandRoleConditions";
        public const string EffectId = "hand.role_conditions";
        [SerializeField] private string frameIdOverride = "";
        [SerializeField] private float stagger = .09f, duration = .12f, slide = 44, gap = 4;
        private CanvasGroup gate;
        private RectTransform template;
        private readonly List<RectTransform> cards = new List<RectTransform>();
        private readonly List<CanvasGroup> groups = new List<CanvasGroup>();
        private readonly List<Vector2> positions = new List<Vector2>();
        private readonly List<string> names = new List<string>();
        private readonly List<string> baseNames = new List<string>();
        private string[] raw = Array.Empty<string>();
        private Color background;
        private float bottom, elapsed;
        private bool prepared, hovered;
        private string lastFrameId;
        private string focusedRole;
        private int lastRevision = -1;
        public bool IsHovered => hovered;
        public int CardCount => names.Count;
        public int VisibleCardCount { get { int n = 0; for (int i = 0; i < names.Count; i++) if (gate.alpha > 0 && groups[i].alpha > .001f) n++; return n; } }
        public string FrameId => !string.IsNullOrEmpty(frameIdOverride) ? UIFrameCatalog.Resolve(frameIdOverride).Id
            : ResolvePlayerFrame();
        public Color BackgroundColor => background;

        private string ResolvePlayerFrame()
        {
            string player = GetComponentInParent<UIFrameContext>()?.PlayerKey ?? UIFrameSelection.LocalPlayer;
            string selected = UIFrameSelection.GetForPlayer(player);
            return selected == UIFrameCatalog.DefaultId ? UIFrameCatalog.RoleConditionsId : selected;
        }
        public void SetFrameId(string id) { frameIdOverride = id ?? ""; lastRevision = -1; RefreshStyle(); }

        public static HandRoleConditionsUI Ensure(Transform parent)
        {
            var go = SceneFirst.Child(parent, RootName, out bool made, typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup), typeof(HandRoleConditionsUI));
            if (made) Place((RectTransform)go.transform, 246, 270, 462, 148);
            var ui = go.GetComponent<HandRoleConditionsUI>(); ui.BuildStatic();
            if (made) ui.HideImmediate();
            return ui;
        }
        private void BuildStatic()
        {
            if (prepared) return;
            gate = GetComponent<CanvasGroup>(); gate.blocksRaycasts = gate.interactable = false;
            var rt = (RectTransform)transform; bottom = -rt.anchoredPosition.y + rt.sizeDelta.y;
            var go = SceneFirst.Child(transform, "CardTemplate", out bool made, typeof(RectTransform), typeof(CanvasGroup), typeof(PixelShapeGraphic));
            template = (RectTransform)go.transform;
            if (made) { Place(template, 0, 0, 456, 34); go.GetComponent<CanvasGroup>().blocksRaycasts = false; }
            template.GetComponent<PixelShapeGraphic>().raycastTarget = false;
            Label(template, "RoleName", 26, 88, 18, 12);
            Label(template, "Condition", 120, 306, 14, 12);
            go.SetActive(false); prepared = true;
        }
        private static void Place(RectTransform rt, float x, float y, float w, float h)
        { rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); }
        private static void Label(Transform parent, string name, float x, float width, int size, int min)
        {
            var go = SceneFirst.Child(parent, name, out bool made, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (made)
            {
                var rt = (RectTransform)go.transform; Place(rt, x, 0, width, 34);
                var text = go.GetComponent<TextMeshProUGUI>(); text.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
                text.fontSize = text.fontSizeMax = size; text.fontSizeMin = min; text.enableAutoSizing = true;
                text.color = new Color32(239, 238, 216, 255); text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
                text.margin = Vector4.zero; text.text = "";
            }
            go.GetComponent<TMP_Text>().raycastTarget = false;
        }

        public void SetRoles(string[] data, Color panelBackground)
        {
            BuildStatic(); background = panelBackground;
            data = data ?? Array.Empty<string>();
            bool same = raw.Length == data.Length;
            for (int i = 0; same && i < data.Length; i++) same = raw[i] == data[i];
            if (same) { lastRevision = -1; RefreshStyle(); return; }
            HideImmediate(); raw = (string[])data.Clone(); names.Clear(); baseNames.Clear(); positions.Clear();
            var entries = YakuNameUtil.Summarize(raw);
            float rowHeight = template.sizeDelta.y;
            float height = entries.Count == 0 ? 0 : entries.Count * (rowHeight + gap) - gap;
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(root.sizeDelta.x, height);
            root.anchoredPosition = new Vector2(root.anchoredPosition.x, -(bottom - height));
            for (int i = 0; i < entries.Count; i++)
            {
                if (i == cards.Count)
                {
                    var card = Instantiate(template, transform, false); card.name = "RoleCard" + i;
                    cards.Add(card); groups.Add(card.GetComponent<CanvasGroup>());
                }
                var r = cards[i]; r.gameObject.SetActive(true);
                var pos = template.anchoredPosition + new Vector2(i % 2 == 0 ? 0 : 6, -i * (rowHeight + gap));
                positions.Add(pos); r.anchoredPosition = pos;
                string name = YakuNameUtil.ToDisplayText(entries[i]); names.Add(name);
                baseNames.Add(YakuNameUtil.ToVoiceKey(entries[i].BaseName));
                r.Find("RoleName").GetComponent<TMP_Text>().text = name;
                string description = YakuInfo.GetDescription(entries[i].BaseName);
                r.Find("Condition").GetComponent<TMP_Text>().text = string.IsNullOrEmpty(description) ? "説明未登録" : description;
                groups[i].alpha = 0; groups[i].blocksRaycasts = groups[i].interactable = false;
            }
            for (int i = entries.Count; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
            lastRevision = -1; RefreshStyle();
        }
        private void RefreshStyle()
        {
            if (!prepared) return;
            string id = FrameId; int revision = UIFrameCatalog.Revision + UIFrameSelection.Revision;
            if (id == lastFrameId && revision == lastRevision) return;
            lastFrameId = id; lastRevision = revision;
            var style = UIFrameCatalog.Resolve(id);
            Color fill = id == UIFrameCatalog.RoleConditionsId ? background : style.panelBackground;
            for (int i = 0; i < names.Count; i++)
            {
                var graphic = cards[i].GetComponent<PixelShapeGraphic>(); var rect = graphic.rectTransform.rect;
                float skew = Mathf.Clamp(style.panelSkew, 0, rect.width * .2f), thickness = Mathf.Max(1, style.panelBorderWidth);
                graphic.Begin();
                for (float y = 0; y < rect.height; y += 2)
                {
                    float step = Mathf.Min(2, rect.height - y), shift = Mathf.Round((y + step / 2) / rect.height * skew);
                    float x = rect.xMin + shift, end = rect.xMax - skew + shift;
                    float top = rect.yMax - y, low = top - step;
                    if (y < thickness || y + step > rect.height - thickness) graphic.Box(x, low, end, top, style.panelBorder);
                    else
                    {
                        // 枠と背景は別領域。半透明の背景の下に不透明な面を置かない。
                        graphic.Box(x + thickness, low, end - thickness, top, fill);
                        graphic.Box(x, low, x + thickness, top, style.panelBorder);
                        graphic.Box(end - thickness, low, end, top, style.panelBorder);
                    }
                }
                graphic.End();
                cards[i].Find("RoleName").GetComponent<TMP_Text>().color = style.panelText;
                cards[i].Find("Condition").GetComponent<TMP_Text>().color = style.panelText;
            }
            ApplyFocus();
        }
        public string GetHoveredRole(Vector2 point)
        {
            if (!hovered || gate == null || gate.alpha <= 0 || !gameObject.activeInHierarchy) return null;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, point, camera)) return null;
            for (int i = 0; i < names.Count; i++)
                if (groups[i].alpha > .95f && RectTransformUtility.RectangleContainsScreenPoint(cards[i], point, camera))
                    return baseNames[i];
            return null;
        }
        public void SetFocusedRole(string role)
        {
            string next = YakuNameUtil.ToVoiceKey(role); if (next == focusedRole) return;
            focusedRole = next; ApplyFocus();
        }
        private void ApplyFocus()
        {
            var style = UIFrameCatalog.Resolve(FrameId);
            for (int i = 0; i < names.Count; i++)
                cards[i].Find("RoleName").GetComponent<TMP_Text>().color =
                    focusedRole != null && baseNames[i] == focusedRole
                        ? new Color32(255, 214, 59, 255) : style.panelText;
        }
        public void SetHovered(bool value)
        {
            if (!value || names.Count == 0 || !isActiveAndEnabled) { HideImmediate(); return; }
            if (hovered) return;
            hovered = true; elapsed = 0; gate.alpha = 1; RenderElapsed(0);
        }
        private void Update()
        {
            RefreshStyle();
            if (hovered) { elapsed += Time.unscaledDeltaTime; RenderElapsed(elapsed); }
        }
        private void RenderElapsed(float time)
        {
            for (int i = 0; i < names.Count; i++)
            {
                float u = Mathf.Clamp01((time - i * stagger) / Mathf.Max(.001f, duration));
                float ease = 1 - Mathf.Pow(1 - u, 3);
                groups[i].alpha = Mathf.Min(1, u * 4);
                cards[i].anchoredPosition = positions[i] + new Vector2(Mathf.Round(slide * (1 - ease)), 0);
            }
        }
        public void HideImmediate()
        {
            hovered = false; elapsed = 0;
            focusedRole = null;
            if (prepared) ApplyFocus();
            if (gate != null) gate.alpha = 0;
            for (int i = 0; i < groups.Count; i++) { groups[i].alpha = 0; if (i < positions.Count) cards[i].anchoredPosition = positions[i]; }
        }
        public void Clear() { SetRoles(Array.Empty<string>(), background); HideImmediate(); }
        private void OnDisable() { HideImmediate(); }
    }
}
