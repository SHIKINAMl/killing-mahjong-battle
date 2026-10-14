using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>スキル使用ログの左右配置と登場。自分は右、敵は左。</summary>
    public sealed class SkillLogRowUI : MonoBehaviour
    {
        private RectTransform panel;
        private PixelShapeGraphic border;
        private CanvasGroup group;
        private Color color;
        private bool isLocal;

        public void Configure(TMP_Text label, Color tint, bool local)
        {
            color = tint; isLocal = local;
            var go = new GameObject("SkillPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel = go.GetComponent<RectTransform>(); panel.SetParent(transform, false);
            panel.anchorMin = new Vector2(local ? .16f : 0, 0);
            panel.anchorMax = new Vector2(local ? 1 : .84f, 1);
            panel.sizeDelta = Vector2.zero;
            var image = go.GetComponent<Image>(); image.raycastTarget = false;
            image.enabled = false;
            group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false;
            var edge = new GameObject("SkillBorder", typeof(RectTransform), typeof(PixelShapeGraphic));
            var rt = edge.GetComponent<RectTransform>(); rt.SetParent(panel, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            border = edge.GetComponent<PixelShapeGraphic>(); border.raycastTarget = false; border.color = tint;
            if (label != null)
            {
                label.transform.SetParent(panel, false);
                var textRect = label.rectTransform;
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(local ? 22 : 12, 0);
                textRect.offsetMax = new Vector2(local ? -12 : -22, 0);
                label.margin = Vector4.zero; label.color = new Color32(239, 238, 216, 255);
                label.alignment = local ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            }
            Redraw();
            if (isActiveAndEnabled) Enter();
        }

        private void OnEnable() { if (panel != null) Enter(); }
        private void OnRectTransformDimensionsChange() { Redraw(); }
        private void Redraw()
        {
            if (border == null) return;
            var rect = border.rectTransform.rect;
            var fill = new Color(color.r * .14f, color.g * .14f, color.b * .14f, 1);
            float half = rect.height * .5f;
            if (half <= 0) return;
            border.Begin();
            // 斜めの先端も2ドット刻み。自分と敵で左右を反転する。
            for (float y = rect.yMin; y < rect.yMax; y += 2)
            {
                float top = Mathf.Min(y + 2, rect.yMax);
                float inset = Mathf.Round(12 * Mathf.Abs((y + top) * .5f - rect.center.y) / half / 2) * 2;
                float left = rect.xMin + (isLocal ? inset : 0);
                float right = rect.xMax - (isLocal ? 0 : inset);
                border.Box(left, y, right, top, fill);
                // 色の線は中央を向く斜めの先端だけ。四角い側は囲わない。
                if (isLocal) border.Box(left, y, left + 2, top, color);
                else border.Box(right - 2, y, right, top, color);
            }
            border.End();
        }
        private void Enter()
        {
            panel.DOKill(); group.DOKill(); Redraw();
            panel.anchoredPosition = new Vector2(isLocal ? 24 : -24, 0); group.alpha = 0;
            panel.DOAnchorPosX(0, .18f).SetEase(Ease.OutQuad).SetUpdate(true);
            group.DOFade(1, .18f).SetUpdate(true);
        }
        private void OnDisable()
        {
            if (panel == null) return;
            panel.DOKill(); group.DOKill(); panel.anchoredPosition = Vector2.zero; group.alpha = 1;
        }
    }
}
