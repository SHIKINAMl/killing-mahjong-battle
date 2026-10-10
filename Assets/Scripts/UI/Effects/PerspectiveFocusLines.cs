using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 透視スキルの集中線（2026-10-03）。敵の山牌へ視線を集めるために出す。
    ///
    /// **止めずに、毎フレーム引き直す。**
    /// ユーザーが参考に挙げた映像（`画面録画 2026-10-02 201845.mp4`）を1コマずつ
    /// 測ると、線の柄が**丸ごと別のものに入れ替わり続けていた**。
    /// 新しい柄になる間隔は録画30fpsで 2〜4コマ、つまり**毎秒10〜15回**。
    /// 止まった線を薄く出し入れするのではなく、これが「びびびび」の正体だった。
    ///
    /// 引き方も参考映像に寄せる:
    ///   ・黒っぽい（濃い紫をわずかに含む）
    ///   ・太さも長さもばらばらで、縁がざらついている
    ///   ・本数が多く、画面の端はほとんど黒で埋まる（実測で上端の58%が黒）
    ///   ・中心は空ける。**空ける形は丸ではなく、山牌に合わせた横長の楕円**。
    ///     丸で空けると、横に長い山牌の上下だけが遠くなって締まらない
    ///
    /// 出入りは <see cref="Progress"/> の濃さだけで行う。位置を動かすと、
    /// 毎フレーム引き直しているせいで「寄ってくる」には見えず、ただ乱れる。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PerspectiveFocusLines : MaskableGraphic
    {
        /// <summary>本数。参考映像の密度に合わせて多めに引く。</summary>
        private const int LineCount = 120;

        /// <summary>
        /// 線の太さの候補（px）。**800px 幅の画面を基準にした値**で、
        /// 実際の画面幅に合わせて <see cref="_scale"/> 倍して使う。
        /// 細い線を多めにして、たまに太いものを混ぜる。
        /// </summary>
        private static readonly float[] WidthChoices = { 3f, 4f, 6f, 8f, 11f, 16f, 22f, 30f };

        /// <summary>
        /// 1秒あたり何回、柄を引き直すか。参考映像2本とも実測で 15 回（30fps の2コマ打ち）。
        /// 上げすぎると「ざらざら」になって線に見えなくなる。
        /// </summary>
        private const float RedrawPerSecond = 15f;

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
        private Vector2 _holeRadius = new Vector2(200f, 100f);
        private float _scale = 1f;
        private float _progress;
        private float _redrawCarry;
        private int _seed = 7;

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
        /// 集中線を出す場所と大きさを決める。以後は毎フレーム、この条件のまま引き直す。
        /// </summary>
        /// <param name="focusLocal">集まる先（ローカル座標）</param>
        /// <param name="holeRadius">中心に空ける楕円の半径。山牌がすっぽり入る大きさにする</param>
        /// <param name="scale">画面幅 800px を 1 とした倍率。太さに掛ける</param>
        public void Setup(Vector2 focusLocal, Vector2 holeRadius, float scale)
        {
            _focusLocal = focusLocal;
            _holeRadius = new Vector2(Mathf.Max(1f, holeRadius.x), Mathf.Max(1f, holeRadius.y));
            _scale = Mathf.Max(0.2f, scale);
            Redraw();
        }

        private void Update()
        {
            if (_progress <= 0.001f) return;

            _redrawCarry += Time.deltaTime * RedrawPerSecond;
            if (_redrawCarry < 1f) return;

            // **余りは捨てずに持ち越す。** 0 に戻すと、1フレームで 1 に届かないぶんが
            // 毎回切り捨てられて実際の回数が落ちる（録画実測で 15 のつもりが 10 だった）
            _redrawCarry -= Mathf.Floor(_redrawCarry);
            Redraw();
        }

        /// <summary>柄を丸ごと引き直す。</summary>
        private void Redraw()
        {
            var rnd = new System.Random(_seed++);
            if (_lines == null || _lines.Length != LineCount) _lines = new Line[LineCount];

            for (int i = 0; i < LineCount; i++)
            {
                float angle = (i / (float)LineCount) * Mathf.PI * 2f + Range(rnd, -0.020f, 0.020f);

                // 空ける形は楕円。横に長い山牌に沿わせるので、角度ごとに半径が変わる
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float nx = cos / _holeRadius.x;
                float ny = sin / _holeRadius.y;
                float holeAt = 1f / Mathf.Sqrt(nx * nx + ny * ny);

                // 始まりの距離をばらす。揃っていると、空けた穴が縁として見えてしまう
                float start = holeAt * Range(rnd, 1.00f, 1.55f);

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

        /// <summary>
        /// ほぼ黒。参考映像の線はほとんど真っ黒で、たまに濃い紫が混ざる程度だった。
        /// 色を散らしすぎると、集中線ではなく模様に見える。
        /// </summary>
        private static Color PickColor(System.Random rnd)
        {
            double r = rnd.NextDouble();
            float a = Range(rnd, 200f, 255f) / 255f;

            if (r < 0.86)
            {
                return new Color(Range(rnd, 6f, 22f) / 255f, Range(rnd, 4f, 18f) / 255f,
                                 Range(rnd, 14f, 38f) / 255f, a);
            }
            if (r < 0.96)
            {
                return new Color(Range(rnd, 55f, 100f) / 255f, Range(rnd, 10f, 28f) / 255f,
                                 Range(rnd, 85f, 140f) / 255f, a);
            }
            return new Color(Range(rnd, 140f, 190f) / 255f, Range(rnd, 40f, 75f) / 255f,
                             Range(rnd, 140f, 190f) / 255f, a);
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

            var v = UIVertex.simpleVert;

            for (int i = 0; i < _lines.Length; i++)
            {
                Line line = _lines[i];

                float cos = Mathf.Cos(line.Angle);
                float sin = Mathf.Sin(line.Angle);

                Vector2 inner = _focusLocal + new Vector2(cos, sin) * line.Start;
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
