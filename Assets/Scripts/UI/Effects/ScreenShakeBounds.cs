using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    internal static class ScreenShakeBounds
    {
        /// <summary>右端で切って配置してある自分の体力画像は、揺れで切断面を画面内へ出さない。</summary>
        public static bool KeepRightEdgeCovered(Transform target)
        {
            foreach (var player in Object.FindObjectsByType<PlayerInfoUI>(FindObjectsSortMode.None))
            {
                var fill = player.HpGaugeAnchor;
                var meter = fill != null ? fill.parent as RectTransform : null;
                if (meter == null || (meter != target && !meter.IsChildOf(target))) continue;
                var canvas = meter.GetComponentInParent<Canvas>();
                if (canvas == null) continue;
                canvas = canvas.rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var corners = new Vector3[4];
                meter.GetWorldCorners(corners);
                float right = float.NegativeInfinity;
                foreach (var corner in corners)
                    right = Mathf.Max(right, RectTransformUtility.WorldToScreenPoint(camera, corner).x);
                if (right >= Screen.width - 1f) return true;
            }
            return false;
        }

        /// <summary>
        /// <paramref name="target"/> の絵（見えている Graphic）が、画面のどの端で切れているか（端から 2px 以内に接しているか）。
        /// 切れている端から内側へ動かすと、絵の無いところが見えてしまう
        /// </summary>
        public static void EdgeCuts(Transform target, out bool left, out bool right, out bool bottom, out bool top)
        {
            left = right = bottom = top = false;
            if (target == null) return;
            const float touch = 2f;
            var corners = new Vector3[4];
            foreach (var graphic in target.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            {
                if (!graphic.isActiveAndEnabled || graphic.color.a <= 0.01f) continue;
                var canvas = graphic.canvas;
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                graphic.rectTransform.GetWorldCorners(corners);
                for (int i = 0; i < 4; i++)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                    if (p.x <= touch) left = true;
                    if (p.x >= Screen.width - touch) right = true;
                    if (p.y <= touch) bottom = true;
                    if (p.y >= Screen.height - touch) top = true;
                }
            }
        }

        /// <summary>切れている端から内側へは動かさない（外側へだけ動かす）</summary>
        public static Vector3 KeepCutEdgesHidden(Vector3 offset, bool left, bool right, bool bottom, bool top)
        {
            if (right) offset.x = Mathf.Max(offset.x, 0f);
            if (left) offset.x = Mathf.Min(offset.x, 0f);
            if (top) offset.y = Mathf.Max(offset.y, 0f);
            if (bottom) offset.y = Mathf.Min(offset.y, 0f);
            return offset;
        }

        public static Vector3 Constrain(Vector3 offset, bool keepRightCovered)
        {
            if (keepRightCovered) offset.x = Mathf.Max(0f, offset.x);
            return offset;
        }
    }
}
