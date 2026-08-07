using UnityEngine;

namespace KillingMahjong.Common
{
    /// <summary>
    /// Canvas をまたいで矩形を変換するためのユーティリティ。
    ///
    /// RectTransform.GetWorldCorners が返すワールド座標は、その Canvas の RenderMode によって
    /// 意味が変わる。ScreenSpaceOverlay ならスクリーンのピクセル座標そのものだが、
    /// ScreenSpaceCamera や WorldSpace ではカメラ基準のワールド座標になる。
    ///
    /// 前者のつもりで後者の値を別 Canvas の InverseTransformPoint に渡すと、
    /// 矩形が原点付近に潰れて画面の隅に張り付く。実際にチュートリアルの誘導が
    /// 役Canvas（ScreenSpaceCamera）を指せず、画面隅を指していた原因がこれ。
    ///
    /// Canvas をまたぐときは必ずスクリーン座標を経由すること。
    /// </summary>
    public static class UIRectUtility
    {
        /// <summary>
        /// 対象を描画している Canvas のカメラ。Overlay なら null（スクリーン変換では null を渡す）。
        /// </summary>
        public static Camera GetEventCamera(RectTransform target)
        {
            if (target == null) return null;

            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>対象の矩形をスクリーン座標で返す。</summary>
        public static Rect GetScreenRect(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Camera cam = GetEventCamera(target);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            return Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));
        }

        /// <summary>
        /// 対象の矩形を、別の Canvas のローカル座標へ変換する。
        /// RenderMode が違う Canvas 同士でも正しく対応づく。
        /// </summary>
        /// <param name="target">変換したい対象</param>
        /// <param name="destCanvas">変換先の Canvas</param>
        /// <param name="result">変換先 Canvas のローカル座標での矩形</param>
        public static bool TryGetLocalRect(RectTransform target, Canvas destCanvas, out Rect result)
        {
            result = default;
            if (destCanvas == null) return false;

            return TryGetLocalRect(target, destCanvas.GetComponent<RectTransform>(), destCanvas, out result);
        }

        /// <summary>
        /// 対象の矩形を、任意の RectTransform のローカル座標へ変換する。
        /// 変換先が Canvas 直下とは限らない場合（矢印を親パネルに置いている等）はこちらを使う。
        /// </summary>
        /// <param name="destRect">変換先の RectTransform</param>
        /// <param name="destCanvas">変換先を描画している Canvas（カメラの判定に使う）</param>
        public static bool TryGetLocalRect(RectTransform target, RectTransform destRect, Canvas destCanvas, out Rect result)
        {
            result = default;
            if (target == null || destRect == null) return false;

            Rect screen = GetScreenRect(target);

            Camera destCam = null;
            if (destCanvas != null)
            {
                var destRoot = destCanvas.rootCanvas;
                destCam = destRoot.renderMode == RenderMode.ScreenSpaceOverlay ? null : destRoot.worldCamera;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    destRect, screen.min, destCam, out Vector2 bottomLeft)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    destRect, screen.max, destCam, out Vector2 topRight)) return false;

            result = Rect.MinMaxRect(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));
            return true;
        }
    }
}
