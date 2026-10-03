using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 透視スキルの「青くした同じ画面を、少しずらして周りに重ねる」の1枚ぶん（2026-10-03）。
    ///
    /// 発動した瞬間の画面を1枚撮って、それをこのレイヤーに貼る。同じものを
    /// 5枚ぶん、少しずつ違う向きへずらして重ねると、周りだけが青く覆われて見える。
    ///
    /// **主画面は縮めない。** 小さくした絵を中央に置くと「画面の中に画面がある」
    /// 見え方になってしまう。原寸のまま重ね、**見せたい所だけ青を抜く**ことで
    /// 「周りが青くなった」ように見せる。
    ///
    /// 青を抜くのはぼかして行う。四角で抜くと枠が見えてしまう。
    ///
    /// **抜く形は丸ではなく楕円。** 画面は横に長いので、丸で抜くと
    /// 左右が先に青くなって上下が残り、「周りが青い」に見えない
    /// （2026-10-03 のユーザー指摘「もっと画面の周りは青くなるように」）。
    /// <see cref="RadiusX"/>／<see cref="RadiusY"/> の内側は完全に抜き、
    /// そこから <see cref="OuterScale"/> 倍の所までで青を戻しきる。
    ///
    /// **抜き方は格子の頂点色でやる。** シェーダーを足さずに済むように、
    /// 板を <see cref="GridX"/>×<see cref="GridY"/> に割って、頂点ごとの
    /// アルファを GPU に補間させている。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PerspectiveGhostLayer : RawImage
    {
        /// <summary>格子の細かさ。ぼかしの縁が角張らない程度にあればよい。</summary>
        private const int GridX = 24;
        private const int GridY = 18;

        private Vector2 _centerLocal;
        private float _radiusX = 200f;
        private float _radiusY = 140f;
        private float _outerScale = 1.6f;
        private float _strength = 1f;

        /// <summary>青を抜く楕円の中心。このレイヤーのローカル座標で渡す。</summary>
        public Vector2 CenterLocal
        {
            get => _centerLocal;
            set { _centerLocal = value; SetVerticesDirty(); }
        }

        /// <summary>完全に抜く楕円の横半径。</summary>
        public float RadiusX
        {
            get => _radiusX;
            set { _radiusX = value; SetVerticesDirty(); }
        }

        /// <summary>完全に抜く楕円の縦半径。</summary>
        public float RadiusY
        {
            get => _radiusY;
            set { _radiusY = value; SetVerticesDirty(); }
        }

        /// <summary>楕円の何倍の所で青が乗りきるか。小さいほど急に青くなる。</summary>
        public float OuterScale
        {
            get => _outerScale;
            set { _outerScale = Mathf.Max(1.01f, value); SetVerticesDirty(); }
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
            if (texture == null || _strength <= 0.001f) return;

            Rect r = GetPixelAdjustedRect();
            Rect uv = uvRect;
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
                    c.a *= GhostAlphaAt(x, y) * _strength;

                    v.position = new Vector3(x, y, 0f);
                    v.uv0 = new Vector2(uv.x + uv.width * fx, uv.y + uv.height * fy);
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

        /// <summary>
        /// 楕円の内側は 0、外へ向かって 1。
        ///
        /// 間は 1.6 乗で戻す。直線で戻すと、抜いた所の縁が輪になって見える。
        /// </summary>
        private float GhostAlphaAt(float x, float y)
        {
            float nx = (x - _centerLocal.x) / Mathf.Max(1f, _radiusX);
            float ny = (y - _centerLocal.y) / Mathf.Max(1f, _radiusY);
            float dn = Mathf.Sqrt(nx * nx + ny * ny);

            if (dn <= 1f) return 0f;
            if (dn >= _outerScale) return 1f;

            float t = (dn - 1f) / (_outerScale - 1f);
            return 1f - Mathf.Pow(1f - t, 1.6f);
        }
    }
}
