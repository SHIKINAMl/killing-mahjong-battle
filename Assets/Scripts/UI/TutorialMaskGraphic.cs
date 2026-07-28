using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 全画面を覆い、指定した矩形だけを角丸＋ふちぼかしでくり抜くグラフィック。
    ///
    /// 上下左右4枚のパネルでは「角丸」も「ぼかし」も作れないため、
    /// 1枚のメッシュを自前で組み立てて置き換えたもの。
    /// ぼかしは頂点カラーのアルファ勾配で表現しているので、専用シェーダーも画像も要らない。
    ///
    /// メッシュの構成:
    ///   1. 穴の外側を埋める4枚の矩形（上下左右）
    ///   2. 外側矩形の角と円弧のあいだを埋めるくさび4つ
    ///   3. 穴のふち（内側=透明 → 外側=不透明）のリング
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    public class TutorialMaskGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        [Tooltip("穴の角丸半径")]
        [SerializeField] private float cornerRadius = 16f;

        [Tooltip("穴のふちのぼかし幅。0 でくっきり切り抜く。")]
        [SerializeField] private float edgeSoftness = 14f;

        [Tooltip("角ひとつあたりの分割数。大きいほど滑らかで頂点数が増える。")]
        [Range(1, 16)]
        [SerializeField] private int segmentsPerCorner = 6;

        private Rect _hole;
        private bool _hasHole;

        private readonly List<Vector2> _innerPoints = new List<Vector2>();
        private readonly List<Vector2> _outerPoints = new List<Vector2>();

        public float CornerRadius
        {
            get => cornerRadius;
            set { if (!Mathf.Approximately(cornerRadius, value)) { cornerRadius = value; SetVerticesDirty(); } }
        }

        public float EdgeSoftness
        {
            get => edgeSoftness;
            set { if (!Mathf.Approximately(edgeSoftness, value)) { edgeSoftness = value; SetVerticesDirty(); } }
        }

        /// <summary>くり抜く矩形を設定する（このグラフィックのローカル座標）。</summary>
        public void SetHole(Rect holeRect)
        {
            _hole = holeRect;
            _hasHole = true;
            SetVerticesDirty();
        }

        /// <summary>穴なし（全画面べた塗り）にする。</summary>
        public void ClearHole()
        {
            if (!_hasHole) return;
            _hasHole = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect canvasRect = GetPixelAdjustedRect();
            float minX = canvasRect.xMin, maxX = canvasRect.xMax;
            float minY = canvasRect.yMin, maxY = canvasRect.yMax;

            Color32 opaque = color;
            Color32 transparent = new Color(color.r, color.g, color.b, 0f);

            if (!_hasHole)
            {
                AddQuad(vh, minX, minY, maxX, maxY, opaque);
                return;
            }

            float softness = Mathf.Max(0f, edgeSoftness);

            // 外側＝完全に不透明になる境界。内側＝穴そのもの。
            float outerX0 = Mathf.Clamp(_hole.xMin - softness, minX, maxX);
            float outerX1 = Mathf.Clamp(_hole.xMax + softness, minX, maxX);
            float outerY0 = Mathf.Clamp(_hole.yMin - softness, minY, maxY);
            float outerY1 = Mathf.Clamp(_hole.yMax + softness, minY, maxY);

            float innerX0 = Mathf.Clamp(_hole.xMin, outerX0, outerX1);
            float innerX1 = Mathf.Clamp(_hole.xMax, outerX0, outerX1);
            float innerY0 = Mathf.Clamp(_hole.yMin, outerY0, outerY1);
            float innerY1 = Mathf.Clamp(_hole.yMax, outerY0, outerY1);

            // 穴が角丸半径より小さいときは半径を詰める
            float innerRadius = Mathf.Max(0f, Mathf.Min(
                cornerRadius,
                (innerX1 - innerX0) * 0.5f,
                (innerY1 - innerY0) * 0.5f));
            float outerRadius = innerRadius + softness;

            // --- 1. 穴の外側 ---
            AddQuad(vh, minX, outerY1, maxX, maxY, opaque);          // 上
            AddQuad(vh, minX, minY, maxX, outerY0, opaque);          // 下
            AddQuad(vh, minX, outerY0, outerX0, outerY1, opaque);    // 左
            AddQuad(vh, outerX1, outerY0, maxX, outerY1, opaque);    // 右

            // --- 2. 角のくさび ---
            AddCornerWedges(vh, outerX0, outerY0, outerX1, outerY1, outerRadius, opaque);

            // --- 3. ふちのぼかし ---
            BuildContour(_innerPoints, innerX0, innerY0, innerX1, innerY1, innerRadius);
            BuildContour(_outerPoints, outerX0, outerY0, outerX1, outerY1, outerRadius);
            AddSoftRing(vh, _innerPoints, _outerPoints, transparent, opaque);
        }

        /// <summary>穴の中はクリックを通す。全画面1枚になったぶん、ここで穴を開け直す必要がある。</summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!_hasHole) return true;

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out local))
            {
                return true;
            }

            return !IsInsideHole(local);
        }

        private bool IsInsideHole(Vector2 p)
        {
            if (p.x < _hole.xMin || p.x > _hole.xMax || p.y < _hole.yMin || p.y > _hole.yMax) return false;

            float r = Mathf.Max(0f, Mathf.Min(cornerRadius, _hole.width * 0.5f, _hole.height * 0.5f));
            if (r <= 0f) return true;

            // 角の内側だけ円で判定する
            float cx = Mathf.Clamp(p.x, _hole.xMin + r, _hole.xMax - r);
            float cy = Mathf.Clamp(p.y, _hole.yMin + r, _hole.yMax - r);
            return (p - new Vector2(cx, cy)).sqrMagnitude <= r * r;
        }

        // ==================== メッシュ組み立て ====================

        private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 c)
        {
            if (x1 - x0 <= 0.001f || y1 - y0 <= 0.001f) return;

            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        /// <summary>角丸矩形の輪郭を反時計回りに作る。内側と外側で点数が揃うのでリングが張れる。</summary>
        private void BuildContour(List<Vector2> dst, float x0, float y0, float x1, float y1, float radius)
        {
            dst.Clear();
            int seg = Mathf.Max(1, segmentsPerCorner);

            AddArcPoints(dst, x1 - radius, y0 + radius, radius, -90f, 0f, seg);   // 右下
            AddArcPoints(dst, x1 - radius, y1 - radius, radius, 0f, 90f, seg);    // 右上
            AddArcPoints(dst, x0 + radius, y1 - radius, radius, 90f, 180f, seg);  // 左上
            AddArcPoints(dst, x0 + radius, y0 + radius, radius, 180f, 270f, seg); // 左下
        }

        private static void AddArcPoints(List<Vector2> dst, float cx, float cy, float r,
                                         float fromDeg, float toDeg, int seg)
        {
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (float)i / seg);
                dst.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
        }

        private void AddCornerWedges(VertexHelper vh, float x0, float y0, float x1, float y1,
                                     float radius, Color32 c)
        {
            if (radius <= 0.001f) return;
            int seg = Mathf.Max(1, segmentsPerCorner);

            AddWedge(vh, x1 - radius, y0 + radius, radius, -90f, 0f, new Vector2(x1, y0), seg, c);
            AddWedge(vh, x1 - radius, y1 - radius, radius, 0f, 90f, new Vector2(x1, y1), seg, c);
            AddWedge(vh, x0 + radius, y1 - radius, radius, 90f, 180f, new Vector2(x0, y1), seg, c);
            AddWedge(vh, x0 + radius, y0 + radius, radius, 180f, 270f, new Vector2(x0, y0), seg, c);
        }

        /// <summary>角の正方形から四分円を除いた部分を、外角を扇の中心にして埋める。</summary>
        private static void AddWedge(VertexHelper vh, float cx, float cy, float r,
                                     float fromDeg, float toDeg, Vector2 corner, int seg, Color32 c)
        {
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (float)i / seg);
                float a1 = Mathf.Deg2Rad * Mathf.Lerp(fromDeg, toDeg, (float)(i + 1) / seg);

                int b = vh.currentVertCount;
                vh.AddVert(corner, c, Vector2.zero);
                vh.AddVert(new Vector3(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r), c, Vector2.zero);
                vh.AddVert(new Vector3(cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r), c, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
            }
        }

        private static void AddSoftRing(VertexHelper vh, List<Vector2> inner, List<Vector2> outer,
                                        Color32 innerColor, Color32 outerColor)
        {
            int n = Mathf.Min(inner.Count, outer.Count);
            if (n < 2) return;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;

                int b = vh.currentVertCount;
                vh.AddVert(inner[i], innerColor, Vector2.zero);
                vh.AddVert(inner[j], innerColor, Vector2.zero);
                vh.AddVert(outer[j], outerColor, Vector2.zero);
                vh.AddVert(outer[i], outerColor, Vector2.zero);
                vh.AddTriangle(b, b + 1, b + 2);
                vh.AddTriangle(b, b + 2, b + 3);
            }
        }
    }
}
