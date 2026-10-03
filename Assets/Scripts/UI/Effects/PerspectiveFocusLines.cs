using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 透視スキルの集中線（2026-10-03）。敵の山牌へ視線を集めるために出す。
    ///
    /// **漫画の効果線に寄せる。** ユーザーが参考に挙げた映像を見ると、
    /// 白い均一な楔ではなく
    ///   ・黒っぽい（濃い紫を含む）
    ///   ・太さも長さもばらばらで、縁がざらついている
    ///   ・本数が多く、中心は大きく空いている
    /// という引き方だった。均一な楔だと模様に見えてしまうので、1本ずつ
    /// 太さ・始まり・色をばらす。左右の太さも変えて、まっすぐに見せない。
    ///
    /// 出入りは <see cref="Progress"/> で動かす。0 のとき線は遠くにあって薄く、
    /// 1 へ向かって中心へ寄りながら濃くなる。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PerspectiveFocusLines : MaskableGraphic
    {
        /// <summary>本数。少ないと模様に見えるので多めに引く。</summary>
        private const int LineCount = 96;

        /// <summary>
        /// 線の太さの候補（px）。**800px 幅の画面を基準にした値**で、
        /// 実際の画面幅に合わせて <see cref="_scale"/> 倍して使う。
        /// 細い線を多めにして、たまに太いものを混ぜる。
        /// </summary>
        private static readonly float[] WidthChoices = { 2f, 3f, 4f, 5f, 7f, 10f, 14f, 20f };

        /// <summary>出始めに、線がどれだけ外から寄ってくるか。</summary>
        private const float ApproachFrom = 1.55f;

        private struct Line
        {
            public float Angle;
            public float Start;      // 中心から線が始まるまでの距離
            public float WidthLeft;
            public float WidthRight;
            public float Tip;        // 内側の尖り
            public Color Color;
        }

        private Line[] _lines;
        private Vector2 _focusLocal;
        private float _scale = 1f;
        private float _progress;

        /// <summary>0 で消えている、1 で出きっている。</summary>
        public float Progress
        {
            get => _progress;
            set
            {
                if (Mathf.Approximately(_progress, value)) return;
                _progress = value;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 線を引き直す。
        /// </summary>
        /// <param name="focusLocal">集まる先（ローカル座標）</param>
        /// <param name="innerRadius">中心に空ける穴の半径。山牌がすっぽり入る大きさにする</param>
        /// <param name="scale">画面幅 800px を 1 とした倍率。太さに掛ける</param>
        /// <param name="seed">同じ値なら同じ引き方になる</param>
        public void Build(Vector2 focusLocal, float innerRadius, float scale, int seed = 7)
        {
            _focusLocal = focusLocal;
            _scale = Mathf.Max(0.2f, scale);

            var rnd = new System.Random(seed);
            _lines = new Line[LineCount];

            for (int i = 0; i < LineCount; i++)
            {
                float angle = (i / (float)LineCount) * Mathf.PI * 2f + Range(rnd, -0.016f, 0.016f);

                // 始まりの距離をばらす。揃っていると、空けた穴が円に見えてしまう
                float start = innerRadius * Range(rnd, 0.82f, 1.45f);

                float width = WidthChoices[rnd.Next(WidthChoices.Length)] * Range(rnd, 0.8f, 1.3f) * _scale;

                _lines[i] = new Line
                {
                    Angle = angle,
                    Start = start,
                    WidthLeft = width * Range(rnd, 0.6f, 1.4f),
                    WidthRight = width * Range(rnd, 0.6f, 1.4f),
                    Tip = Range(rnd, 0f, 1.6f) * _scale,
                    Color = PickColor(rnd),
                };
            }

            SetVerticesDirty();
        }

        /// <summary>基本は黒〜濃い紫。たまに赤紫を混ぜて滲みを出す。</summary>
        private static Color PickColor(System.Random rnd)
        {
            double r = rnd.NextDouble();
            float a = Range(rnd, 150f, 245f) / 255f;

            if (r < 0.78)
            {
                return new Color(Range(rnd, 8f, 26f) / 255f, Range(rnd, 6f, 20f) / 255f,
                                 Range(rnd, 18f, 42f) / 255f, a);
            }
            if (r < 0.92)
            {
                return new Color(Range(rnd, 60f, 110f) / 255f, Range(rnd, 10f, 30f) / 255f,
                                 Range(rnd, 90f, 150f) / 255f, a);
            }
            return new Color(Range(rnd, 150f, 200f) / 255f, Range(rnd, 40f, 80f) / 255f,
                             Range(rnd, 150f, 200f) / 255f, a);
        }

        private static float Range(System.Random rnd, float min, float max)
        {
            return min + (float)rnd.NextDouble() * (max - min);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_lines == null || _progress <= 0.001f) return;

            Rect r = GetPixelAdjustedRect();

            // 画面の外まで必ず届かせる。半端に止めると、線の先が宙に浮いて見える
            float far = new Vector2(r.width, r.height).magnitude + 40f;

            // 出始めは外から寄ってくる
            float approach = Mathf.Lerp(ApproachFrom, 1f, _progress);

            var v = UIVertex.simpleVert;

            for (int i = 0; i < _lines.Length; i++)
            {
                Line line = _lines[i];

                float cos = Mathf.Cos(line.Angle);
                float sin = Mathf.Sin(line.Angle);
                float start = line.Start * approach;

                Vector2 inner = _focusLocal + new Vector2(cos, sin) * start;
                Vector2 outer = _focusLocal + new Vector2(cos, sin) * far;
                Vector2 normal = new Vector2(-sin, cos);

                Color c = line.Color;
                c.a *= _progress;

                int i0 = vh.currentVertCount;
                v.color = c;

                v.position = inner + normal * line.Tip;             vh.AddVert(v);
                v.position = outer + normal * line.WidthLeft;       vh.AddVert(v);
                v.position = outer - normal * line.WidthRight;      vh.AddVert(v);
                v.position = inner - normal * line.Tip;             vh.AddVert(v);

                vh.AddTriangle(i0 + 0, i0 + 1, i0 + 2);
                vh.AddTriangle(i0 + 2, i0 + 3, i0 + 0);
            }
        }
    }
}
