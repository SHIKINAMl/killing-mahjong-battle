using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 透視スキルで「敵の山牌のまわりだけ残して、画面全体を暗く落とす」板（2026-10-03）。
    ///
    /// 見るべき場所をはっきりさせるためのもの。山牌は**横に長く縦に薄い**ので、
    /// 丸ではなく横長の楕円で抜く。四角で抜くと「暗い枠が乗っている」ように見える。
    ///
    /// <see cref="PerspectiveGhostLayer"/> と同じく、抜き方は格子の頂点色で作る。
    /// シェーダーもテクスチャも要らない。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PerspectiveDarkenLayer : MaskableGraphic
    {
        private const int GridX = 32;
        private const int GridY = 24;

        /// <summary>
        /// 楕円の縁から外へ、どこまでかけて暗さを足しきるか。
        /// 1.0 だと縁が線に見えるので、少し広げてぼかす。
        /// </summary>
        private const float Feather = 1.9f;

        private Vector2 _centerLocal;
        private float _radiusX = 250f;
        private float _radiusY = 130f;
        private float _maxAlpha = 0.58f;
        private float _strength = 1f;

        /// <summary>抜く楕円の中心（ローカル座標）。</summary>
        public Vector2 CenterLocal
        {
            get => _centerLocal;
            set { _centerLocal = value; SetVerticesDirty(); }
        }

        public float RadiusX
        {
            get => _radiusX;
            set { _radiusX = value; SetVerticesDirty(); }
        }

        public float RadiusY
        {
            get => _radiusY;
            set { _radiusY = value; SetVerticesDirty(); }
        }

        /// <summary>いちばん暗い所の濃さ。</summary>
        public float MaxAlpha
        {
            get => _maxAlpha;
            set { _maxAlpha = value; SetVerticesDirty(); }
        }

        /// <summary>全体の濃さ。出入りのフェードに使う。</summary>
        public float Strength
        {
            get => _strength;
            set
            {
                if (Mathf.Approximately(_strength, value)) return;
                _strength = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_strength <= 0.001f) return;

            Rect r = GetPixelAdjustedRect();
            Color baseColor = color;

            var v = UIVertex.simpleVert;

            for (int j = 0; j <= GridY; j++)
            {
                float fy = (float)j / GridY;
                for (int i = 0; i <= GridX; i++)
                {
                    float fx = (float)i / GridX;

                    float x = r.xMin + r.width * fx;
                    float y = r.yMin + r.height * fy;

                    Color c = baseColor;
                    c.a = DarkAlphaAt(x, y) * _strength;

                    v.position = new Vector3(x, y, 0f);
                    v.uv0 = new Vector2(fx, fy);
                    v.color = c;
                    vh.AddVert(v);
                }
            }

            int stride = GridX + 1;
            for (int j = 0; j < GridY; j++)
            {
                for (int i = 0; i < GridX; i++)
                {
                    int i0 = j * stride + i;
                    vh.AddTriangle(i0, i0 + stride, i0 + stride + 1);
                    vh.AddTriangle(i0 + stride + 1, i0 + 1, i0);
                }
            }
        }

        /// <summary>楕円の内側は 0、外へ向かって <see cref="MaxAlpha"/> まで。</summary>
        private float DarkAlphaAt(float x, float y)
        {
            float nx = (x - _centerLocal.x) / Mathf.Max(1f, _radiusX);
            float ny = (y - _centerLocal.y) / Mathf.Max(1f, _radiusY);
            float dn = Mathf.Sqrt(nx * nx + ny * ny);

            if (dn <= 1f) return 0f;

            float t = Mathf.Clamp01((dn - 1f) / (Feather - 1f));
            return _maxAlpha * Mathf.Pow(t, 0.85f);
        }
    }
}
