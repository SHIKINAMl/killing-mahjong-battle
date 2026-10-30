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

        public static Vector3 Constrain(Vector3 offset, bool keepRightCovered)
        {
            if (keepRightCovered) offset.x = Mathf.Max(0f, offset.x);
            return offset;
        }
    }
}
