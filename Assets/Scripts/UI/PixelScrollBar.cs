using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>読む範囲の長さを標準Scrollbarに任せ、縦の溝とつまみだけを2ドット格子で描く。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Scrollbar))]
    public sealed class PixelScrollBar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color32 Ink = new Color32(24, 15, 22, 255);
        private static readonly Color32 Recess = new Color32(47, 29, 40, 255);
        private static readonly Color32 Edge = new Color32(112, 79, 84, 255);
        private static readonly Color32 Blood = new Color32(163, 49, 58, 255);
        private static readonly Color32 Light = new Color32(212, 103, 91, 255);
        private static readonly Color32 Ivory = new Color32(239, 238, 216, 255);
        private static readonly Color32 Gold = new Color32(255, 211, 77, 255);
        private readonly Dictionary<Image, bool> nativeImages = new Dictionary<Image, bool>();
        private readonly Vector3[] corners = new Vector3[4];
        private Scrollbar bar;
        private RectTransform root;
        private PixelShapeGraphic art;
        private Graphic originalTarget;
        private Selectable.Transition originalTransition;
        private bool hovered, pressed;
        private Rect lastTrack, lastThumb;
        private int lastState = -1;

        public static PixelScrollBar Attach(Scrollbar target)
        {
            if (target == null) return null;
            return target.GetComponent<PixelScrollBar>() ?? target.gameObject.AddComponent<PixelScrollBar>();
        }

        private void Awake()
        {
            bar = GetComponent<Scrollbar>();
            root = (RectTransform)transform;
            originalTarget = bar.targetGraphic;
            originalTransition = bar.transition;
            Remember(root.GetComponent<Image>());
            if (bar.handleRect != null) Remember(bar.handleRect.GetComponent<Image>());
            var go = new GameObject("PixelScrollArt", typeof(RectTransform), typeof(PixelShapeGraphic));
            go.transform.SetParent(root, false);
            var rect = (RectTransform)go.transform;
            rect.pivot = root.pivot;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            art = go.GetComponent<PixelShapeGraphic>();
            art.raycastTarget = true;
        }

        private void Remember(Image image)
        { if (image != null && !nativeImages.ContainsKey(image)) nativeImages.Add(image, image.enabled); }

        private void OnEnable()
        {
            foreach (var item in nativeImages) if (item.Key != null) item.Key.enabled = false;
            bar.transition = Selectable.Transition.None;
            bar.targetGraphic = art;
            art.enabled = true;
            lastState = -1;
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            if (bar != null) { bar.targetGraphic = originalTarget; bar.transition = originalTransition; }
            foreach (var item in nativeImages) if (item.Key != null) item.Key.enabled = item.Value;
            if (art != null) art.enabled = false;
        }

        private void OnDestroy()
        {
            if (art == null) return;
            if (Application.isPlaying) Destroy(art.gameObject); else DestroyImmediate(art.gameObject);
        }

        private void LateUpdate() { Refresh(); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = false; }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData data) { pressed = false; }
        private static float Snap(float value) => Mathf.Round(value / 2f) * 2f;

        public void Refresh()
        {
            if (!isActiveAndEnabled || art == null || bar.handleRect == null) return;
            // 位置・長さは実際のhandleRectから取る。ScrollRectによるsize更新や向きの反転にも追従。
            bar.handleRect.GetWorldCorners(corners);
            Vector3 min = root.InverseTransformPoint(corners[0]);
            Vector3 max = root.InverseTransformPoint(corners[2]);
            Rect thumb = Rect.MinMaxRect(Snap(min.x), Snap(min.y), Snap(max.x), Snap(max.y));
            Rect track = root.rect;
            int state = !bar.IsInteractable() ? 0 : pressed ? 3 : hovered ? 2 : 1;
            if (lastState == state && lastTrack == track && lastThumb == thumb) return;
            lastState = state; lastTrack = track; lastThumb = thumb;
            art.Begin();
            float l = Snap(track.xMin), r = Snap(track.xMax);
            float b = Snap(track.yMin), t = Snap(track.yMax);
            art.Box(l, b, r, t, Ink);
            art.Box(l + 2, b + 2, r - 2, t - 2, Recess);
            float tb = Mathf.Clamp(thumb.yMin, b, t), tt = Mathf.Clamp(thumb.yMax, b, t);
            if (tt > tb)
            {
                Color face = state == 0 ? Edge : state == 3 ? Light : Blood;
                art.Box(l, tb, r, tt, Ink);
                art.Box(l + 2, tb + 2, r - 2, tt - 2, face);
                if (tt - tb >= 6)
                    art.Box(l + 2, tt - 4, r - 2, tt - 2, state == 2 ? Gold : Light);
                if (tt - tb >= 16 && r - l >= 10)
                {
                    float middle = Snap((tb + tt) * .5f);
                    Color grip = state == 2 ? Gold : state == 0 ? Recess : Ivory;
                    art.Box(l + 4, middle - 4, r - 4, middle - 2, grip);
                    art.Box(l + 4, middle + 2, r - 4, middle + 4, grip);
                }
            }
            art.End();
        }
    }
}
