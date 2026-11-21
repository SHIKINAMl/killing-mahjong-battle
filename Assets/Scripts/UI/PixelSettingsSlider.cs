using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>設定のスライダーを2UI単位の格子で描く。値と入力矩形は標準Sliderに任せる。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Slider))]
    public sealed class PixelSettingsSlider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private const float Pixel = 2f;
        private static readonly Color32 Ink = new Color32(24, 15, 22, 255);
        private static readonly Color32 Recess = new Color32(47, 29, 40, 255);
        private static readonly Color32 Edge = new Color32(112, 79, 84, 255);
        private static readonly Color32 Blood = new Color32(163, 49, 58, 255);
        private static readonly Color32 Ivory = new Color32(239, 238, 216, 255);
        private static readonly Color32 Gold = new Color32(255, 211, 77, 255);
        private readonly Dictionary<Image, bool> nativeImages = new Dictionary<Image, bool>();
        private Slider slider;
        private RectTransform root;
        private PixelShapeGraphic art;
        private Graphic originalTarget;
        private Selectable.Transition originalTransition;
        private bool hovered, pressed, dirty = true;
        private float lastValue = -1f;
        private Vector2 lastSize;
        private bool lastInteractable;

        public static PixelSettingsSlider Attach(Slider target)
        {
            if (target == null) return null;
            return target.GetComponent<PixelSettingsSlider>() ?? target.gameObject.AddComponent<PixelSettingsSlider>();
        }

        private void Awake()
        {
            slider = GetComponent<Slider>();
            root = (RectTransform)transform;
            originalTarget = slider.targetGraphic;
            originalTransition = slider.transition;
            Remember(transform.Find("Background"));
            Remember(slider.fillRect);
            Remember(slider.handleRect);
            var go = new GameObject("PixelSliderArt", typeof(RectTransform), typeof(PixelShapeGraphic));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            art = go.GetComponent<PixelShapeGraphic>();
            art.raycastTarget = true;
        }

        private void Remember(Transform target)
        {
            if (target == null) return;
            var image = target.GetComponent<Image>();
            if (image != null && !nativeImages.ContainsKey(image)) nativeImages.Add(image, image.enabled);
        }

        private void OnEnable()
        {
            foreach (var item in nativeImages) if (item.Key != null) item.Key.enabled = false;
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = art;
            art.enabled = true;
            slider.onValueChanged.AddListener(OnValueChanged);
            dirty = true;
            Refresh();
        }

        private void OnDisable()
        {
            hovered = pressed = false;
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnValueChanged);
                slider.targetGraphic = originalTarget;
                slider.transition = originalTransition;
            }
            foreach (var item in nativeImages) if (item.Key != null) item.Key.enabled = item.Value;
            if (art != null) art.enabled = false;
        }

        private void OnDestroy()
        {
            if (art == null) return;
            if (Application.isPlaying) Destroy(art.gameObject);
            else DestroyImmediate(art.gameObject);
        }
        private void OnValueChanged(float value) { dirty = true; }
        private void OnRectTransformDimensionsChange() { dirty = true; }
        private void LateUpdate() { Refresh(); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; dirty = true; }
        public void OnPointerExit(PointerEventData data) { hovered = false; dirty = true; }
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) { pressed = true; dirty = true; } }
        public void OnPointerUp(PointerEventData data) { pressed = false; dirty = true; }

        private static float Snap(float value) => Mathf.Round(value / Pixel) * Pixel;
        private Color Shade(Color color, bool available) => available ? color : Color.Lerp(color, Recess, .65f);

        public void Refresh()
        {
            if (slider == null || art == null || !isActiveAndEnabled) return;
            bool available = slider.IsInteractable();
            float value = slider.normalizedValue;
            Vector2 size = root.rect.size;
            if (!dirty && value == lastValue && size == lastSize && available == lastInteractable) return;
            lastValue = value; lastSize = size; lastInteractable = available; dirty = false;
            art.Begin();
            var rect = root.rect;
            float left = Snap(rect.xMin + 10f), right = Snap(rect.xMax - 10f);
            if (right <= left) { art.End(); return; }
            float center = Snap(rect.center.y);
            // 標準Sliderのつまみと同じ範囲。描画だけ格子へ寄せ、音量値は丸めない。
            float knob = Snap(Mathf.Lerp(left, right, value));
            // 細い一本のレール。量は連続した赤い線で示す。
            art.Box(left - 2, center - 4, right + 2, center + 4, Ink);
            art.Box(left, center - 2, right, center + 2, Recess);
            art.Box(left, center - 2, knob, center + 2, Shade(Blood, available));
            // つまみは平たい角形。押しても位置をずらさず、目盛り位置を読み取りやすくする。
            art.Box(knob - 8, center - 10, knob + 8, center + 10, Ink);
            Color face = pressed && available ? new Color32(206, 184, 149, 255)
                : hovered && available ? Gold : Ivory;
            art.Box(knob - 6, center - 8, knob + 6, center + 8, Shade(face, available));
            art.Box(knob - 6, center - 8, knob + 6, center - 6, Shade(Edge, available));
            art.End();
        }
    }
}
