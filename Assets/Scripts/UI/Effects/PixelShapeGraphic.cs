using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 四角と線を、その場で並べて描くための板（2026-10-08）。
    ///
    /// 能力の演出（役強化・強襲）は、血の粒・光の柱・照準の線のように
    /// 「毎コマ形が変わる図形」を何十個も出す。1つずつ Image を作って動かすと
    /// オブジェクトが増えるだけなので、**コマごとに形を全部並べ直して1枚で描く。**
    ///
    ///     shapes.Begin();
    ///     shapes.Box(...); shapes.Line(...);
    ///     shapes.End();
    ///
    /// 座標は、この板の中心を原点にした Canvas の単位。
    /// シェーダーもテクスチャも使わない（<see cref="PerspectiveDarkenLayer"/> などと同じ流儀）。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PixelShapeGraphic : MaskableGraphic
    {
        private struct Quad
        {
            public Vector2 A, B, C, D;
            public Color32 Color;
        }

        private readonly List<Quad> _quads = new List<Quad>(256);

        /// <summary>並べ直しを始める。前のコマの形は捨てる。</summary>
        public void Begin()
        {
            _quads.Clear();
        }

        /// <summary>並べ終えた。次の描画で反映する。</summary>
        public void End()
        {
            SetVerticesDirty();
        }

        public void Box(float xMin, float yMin, float xMax, float yMax, Color color)
        {
            if (xMax <= xMin || yMax <= yMin || color.a <= 0f) return;
            _quads.Add(new Quad
            {
                A = new Vector2(xMin, yMin),
                B = new Vector2(xMin, yMax),
                C = new Vector2(xMax, yMax),
                D = new Vector2(xMax, yMin),
                Color = color,
            });
        }

        public void Box(Rect rect, Color color)
        {
            Box(rect.xMin, rect.yMin, rect.xMax, rect.yMax, color);
        }

        public void BoxCentered(Vector2 center, float width, float height, Color color)
        {
            Box(center.x - width * 0.5f, center.y - height * 0.5f,
                center.x + width * 0.5f, center.y + height * 0.5f, color);
        }

        /// <summary>斜めも引ける線。太さは線に直角な向きに取る。</summary>
        public void Line(Vector2 from, Vector2 to, float thickness, Color color)
        {
            Vector2 dir = to - from;
            if (dir.sqrMagnitude < 0.0001f || color.a <= 0f) return;

            Vector2 n = new Vector2(-dir.y, dir.x).normalized * (thickness * 0.5f);
            _quads.Add(new Quad { A = from - n, B = from + n, C = to + n, D = to - n, Color = color });
        }

        /// <summary>四角い枠（中は抜く）。</summary>
        public void Frame(Rect r, float thickness, Color color)
        {
            Box(r.xMin, r.yMax - thickness, r.xMax, r.yMax, color);
            Box(r.xMin, r.yMin, r.xMax, r.yMin + thickness, color);
            Box(r.xMin, r.yMin + thickness, r.xMin + thickness, r.yMax - thickness, color);
            Box(r.xMax - thickness, r.yMin + thickness, r.xMax, r.yMax - thickness, color);
        }

        /// <summary>
        /// 四隅のかぎ形。<paramref name="grow"/> だけ全方向へ太らせて描ける
        /// （先に暗い色で太らせて描き、上から本来の色で描くと縁になる）。
        /// </summary>
        public void Brackets(Rect f, float arm, float thickness, float grow, Color color)
        {
            float t = thickness;
            arm = Mathf.Min(arm, Mathf.Min(f.width, f.height) * 0.5f);

            Box(f.xMin - grow, f.yMin - grow, f.xMin + arm + grow, f.yMin + t + grow, color);
            Box(f.xMin - grow, f.yMin - grow, f.xMin + t + grow, f.yMin + arm + grow, color);

            Box(f.xMax - arm - grow, f.yMin - grow, f.xMax + grow, f.yMin + t + grow, color);
            Box(f.xMax - t - grow, f.yMin - grow, f.xMax + grow, f.yMin + arm + grow, color);

            Box(f.xMin - grow, f.yMax - t - grow, f.xMin + arm + grow, f.yMax + grow, color);
            Box(f.xMin - grow, f.yMax - arm - grow, f.xMin + t + grow, f.yMax + grow, color);

            Box(f.xMax - arm - grow, f.yMax - t - grow, f.xMax + grow, f.yMax + grow, color);
            Box(f.xMax - t - grow, f.yMax - arm - grow, f.xMax + grow, f.yMax + grow, color);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var v = UIVertex.simpleVert;
            for (int i = 0; i < _quads.Count; i++)
            {
                Quad q = _quads[i];
                int index = vh.currentVertCount;
                v.color = q.Color;

                v.position = q.A; vh.AddVert(v);
                v.position = q.B; vh.AddVert(v);
                v.position = q.C; vh.AddVert(v);
                v.position = q.D; vh.AddVert(v);

                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index + 2, index + 3, index);
            }
        }
    }
}
