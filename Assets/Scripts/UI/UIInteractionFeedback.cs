using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>ホバー・押下・選択済みを別状態で描画。入力対象のTransformは一切動かさない。</summary>
    [DisallowMultipleComponent]
    public sealed class UIInteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private string frameIdOverride = "";
        [SerializeField] private PixelShapeGraphic frame;
        [SerializeField] private Image hoverArt, selectedArt;
        private Selectable selectable;
        private TileInteraction tile;
        private Image tileImage;
        private Sprite boundsSprite;
        private Rect spriteVisibleRect;
        private CanvasGroup[] groups;
        private bool hovered, pressed, selected, focused, dirty = true;
        private bool wasAllowed;
        private Rect lastRect;
        private string lastFrameId;
        private int lastRevision = -1;
        public bool IsHovered => hovered;
        public bool IsPressed => pressed;
        public bool IsSelected => selected;
        public string FrameId => UIFrameCatalog.Resolve(string.IsNullOrEmpty(frameIdOverride)
            ? UIFrameSelection.GetForPlayer(GetComponentInParent<UIFrameContext>()?.PlayerKey ?? UIFrameSelection.LocalPlayer)
            : frameIdOverride).Id;

        public static UIInteractionFeedback Attach(GameObject target)
        {
            var feedback = target.GetComponent<UIInteractionFeedback>();
            if (feedback == null) feedback = target.AddComponent<UIInteractionFeedback>();
            feedback.PrepareVisuals(); return feedback;
        }

        private void Awake() { CacheInputs(); PrepareVisuals(); }
        private void CacheInputs()
        {
            selectable = GetComponent<Selectable>(); tile = GetComponent<TileInteraction>();
            tileImage = tile != null ? GetComponent<Image>() : null;
            groups = GetComponentsInParent<CanvasGroup>(true);
        }
        private void OnTransformParentChanged() { CacheInputs(); dirty = true; }
        private void OnCanvasGroupChanged() { CacheInputs(); dirty = true; }
        private void OnEnable() { CacheInputs(); dirty = true; }
        private void OnDisable() { Clear(true); }

        public void PrepareVisuals()
        {
            if (!(transform is RectTransform)) return;
            CacheInputs();
            if (frame == null) frame = Graphic<PixelShapeGraphic>(transform, "InteractionFrame");
            if (hoverArt == null) hoverArt = Graphic<Image>(transform, "HoverFrameArt");
            if (selectedArt == null) selectedArt = Graphic<Image>(transform, "SelectedFrameArt");
            frame.raycastTarget = hoverArt.raycastTarget = selectedArt.raycastTarget = false;
            hoverArt.type = selectedArt.type = Image.Type.Sliced;
            // 標準の無効色は残す。ネイティブColorTintと新しい枠を二重にホバー・押下させない。
            if (selectable != null && selectable.transition == Selectable.Transition.ColorTint)
            {
                var colors = selectable.colors;
                colors.highlightedColor = colors.pressedColor = colors.selectedColor = colors.normalColor;
                selectable.colors = colors;
            }
            hoverArt.enabled = selectedArt.enabled = false;
            dirty = true;
        }

        /// <summary>牌の共通プレハブには描画だけを保存。河やドラなどの情報牌に入力ハンドラを付けない。</summary>
        public static void PrepareTileGraphics(GameObject target)
        {
            if (!(target.transform is RectTransform)) return;
            Graphic<PixelShapeGraphic>(target.transform, "InteractionFrame");
            Graphic<Image>(target.transform, "HoverFrameArt").enabled = false;
            Graphic<Image>(target.transform, "SelectedFrameArt").enabled = false;
        }

        private static T Graphic<T>(Transform parent, string name) where T : Graphic
        {
            var go = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(T), typeof(LayoutElement));
            var graphic = go.GetComponent<T>();
            if (created)
            {
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                go.GetComponent<LayoutElement>().ignoreLayout = true;
            }
            graphic.raycastTarget = false; return graphic;
        }

        public void SetFrameId(string id) { frameIdOverride = id ?? ""; dirty = true; }
        public void SetSelected(bool value) { if (selected != value) { selected = value; dirty = true; } Refresh(); }
        public void Clear(bool clearSelection = false)
        {
            hovered = pressed = focused = false;
            if (clearSelection) selected = false;
            dirty = true; Refresh();
        }

        private bool Allowed()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
            if (selectable != null && (!selectable.enabled || !selectable.IsInteractable())) return false;
            if (tile != null && !tile.CanReceivePointerInput) return false;
            // CanvasGroup.ignoreParentGroupsは、その階層より上だけを無視する。
            Transform previous = null;
            bool stop = false;
            foreach (var group in groups)
            {
                if (group == null) continue;
                if (previous != group.transform && stop) break;
                previous = group.transform;
                if (!group.enabled) continue;
                if (!group.interactable || !group.blocksRaycasts || group.alpha <= .001f) return false;
                stop |= group.ignoreParentGroups;
            }
            return true;
        }

        private bool Contains(PointerEventData e)
        {
            if (e == null) return hovered;
            var rect = transform as RectTransform;
            if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, e.position, e.pressEventCamera)) return false;
            return tile == null || tile.IsRaycastLocationValid(e.position, e.pressEventCamera);
        }

        public void OnPointerEnter(PointerEventData e) { hovered = Allowed(); dirty = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered = pressed = focused = false; dirty = true; Refresh(); }
        public void OnPointerDown(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            pressed = Allowed() && hovered; dirty = true; Refresh();
        }
        public void OnPointerUp(PointerEventData e)
        {
            if (e != null && e.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            if (!Allowed() || !Contains(e)) hovered = false;
            dirty = true; Refresh();
        }
        public void OnSelect(BaseEventData e) { focused = !(e is PointerEventData); dirty = true; Refresh(); }
        public void OnDeselect(BaseEventData e) { focused = false; dirty = true; Refresh(); }
        private void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (frame == null) return;
            bool allowed = Allowed();
            if (!allowed && (hovered || pressed || focused)) { hovered = pressed = focused = false; dirty = true; }
            var toggle = selectable as Toggle;
            if (isActiveAndEnabled && toggle != null && selected != toggle.isOn) { selected = toggle.isOn; dirty = true; }
            string id = FrameId;
            int revision = UIFrameCatalog.Revision + UIFrameSelection.Revision;
            var displayRect = TileFrameRect();
            if (!dirty && allowed == wasAllowed && lastRect == displayRect && lastFrameId == id && lastRevision == revision) return;
            wasAllowed = allowed; lastRect = displayRect; lastFrameId = id; lastRevision = revision; dirty = false;
            var style = UIFrameCatalog.Resolve(id);
            bool hover = allowed && (hovered || focused);
            var rect = lastRect;
            rect.xMin++; rect.yMin++; rect.xMax--; rect.yMax--;
            bool isTile = tile != null;
            frame.Begin();
            if (isTile && (hover || selected)) frame.Frame(rect, 4, style.edgeColor);
            if (pressed && allowed)
            {
                frame.Box(rect, style.pressedColor);
                frame.Box(rect.xMin + 2, rect.yMin + 2, rect.xMax - 2, rect.yMin + 4, new Color(0, 0, 0, .45f));
            }
            if (selected && style.selectedSprite == null)
            {
                var selectedRect = rect;
                if (isTile) selectedRect = Inset(selectedRect, 1);
                frame.Frame(selectedRect, Mathf.Max(1, style.selectedWidth), style.selectedColor);
            }
            if (selected && isTile)
            {
                float x = rect.center.x, y = rect.yMax;
                frame.Box(x - 5, y + 9, x + 5, y + 11, style.selectedColor);
                frame.Box(x - 3, y + 7, x + 3, y + 9, style.selectedColor);
                frame.Box(x - 1, y + 5, x + 1, y + 7, style.selectedColor);
            }
            if (hover && style.hoverSprite == null)
            {
                if (selected) rect = Inset(rect, 3);
                else if (isTile) rect = Inset(rect, 1);
                frame.Frame(rect, Mathf.Max(1, isTile ? style.tileHoverWidth : style.hoverWidth), style.hoverColor);
            }
            frame.End();
            if (isTile)
            {
                FitArt(hoverArt.rectTransform, rect);
                FitArt(selectedArt.rectTransform, Inset(lastRect, 2));
            }
            selectedArt.sprite = style.selectedSprite; selectedArt.enabled = selected && style.selectedSprite != null;
            hoverArt.sprite = style.hoverSprite; hoverArt.enabled = hover && style.hoverSprite != null;
        }

        private static Rect Inset(Rect rect, float amount)
        { rect.xMin += amount; rect.yMin += amount; rect.xMax -= amount; rect.yMax -= amount; return rect; }

        // 入力矩形ではなく、スプライトの透明な余白を除いた範囲に枠を合わせる。
        private Rect TileFrameRect()
        {
            if (tileImage == null || tileImage.type != Image.Type.Simple) return frame.rectTransform.rect;
            var sprite = tileImage.overrideSprite;
            if (sprite == null) return frame.rectTransform.rect;
            if (boundsSprite != sprite)
            {
                boundsSprite = sprite;
                var vertices = sprite.vertices;
                if (vertices.Length == 0) return frame.rectTransform.rect;
                Vector2 min = vertices[0], max = min;
                foreach (var vertex in vertices) { min = Vector2.Min(min, vertex); max = Vector2.Max(max, vertex); }
                min = (min * sprite.pixelsPerUnit + sprite.pivot) / sprite.rect.size;
                max = (max * sprite.pixelsPerUnit + sprite.pivot) / sprite.rect.size;
                spriteVisibleRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
            var imageRect = tileImage.GetPixelAdjustedRect();
            if (tileImage.preserveAspect)
            {
                float scale = Mathf.Min(imageRect.width / sprite.rect.width, imageRect.height / sprite.rect.height);
                Vector2 size = sprite.rect.size * scale;
                imageRect.position += Vector2.Scale(imageRect.size - size, tileImage.rectTransform.pivot);
                imageRect.size = size;
            }
            Vector2 bottom = imageRect.position + Vector2.Scale(imageRect.size, spriteVisibleRect.min);
            Vector2 top = imageRect.position + Vector2.Scale(imageRect.size, spriteVisibleRect.max);
            bottom = frame.rectTransform.InverseTransformPoint(tileImage.rectTransform.TransformPoint(bottom));
            top = frame.rectTransform.InverseTransformPoint(tileImage.rectTransform.TransformPoint(top));
            // 色枠は輪郭の外側1単位、黒縁はさらに1単位だけ外へ出す。
            return Rect.MinMaxRect(bottom.x - 3, bottom.y - 3, top.x + 3, top.y + 3);
        }

        private void FitArt(RectTransform art, Rect rect)
        {
            var parentRect = ((RectTransform)transform).rect;
            art.offsetMin = rect.min - parentRect.min;
            art.offsetMax = rect.max - parentRect.max;
        }
    }
}
