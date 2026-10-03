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
    /// 青を抜くのは丸く・ぼかして行う。四角で抜くと枠が見えてしまうので、
    /// <see cref="InnerRadius"/> の内側は完全に抜き、<see cref="OuterRadius"/> へ
    /// 向かってなだらかに戻す。
    ///
    /// **抜き方は格子の頂点色でやる。** シェーダーを足さずに済むように、
    /// 板を <see cref="GridX"/>×<see cref="GridY"/> に割って、頂点ごとの
    /// アルファを GPU に補間させている。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PerspectiveGhostLayer : RawImage
    {
        /// <summary>格子の細かさ。丸いぼかしが角張らない程度にあればよい。</summary>
        private const int GridX = 24;
        private const int GridY = 18;

        private Vector2 _focusLocal;
        private float _innerRadius = 120f;
        private float _outerRadius = 260f;
        private float _strength = 1f;

        /// <summary>青を抜く中心。このレイヤーのローカル座標で渡す。</summary>
        public Vector2 FocusLocal
        {
            get => _focusLocal;
            set { _focusLocal = value; SetVerticesDirty(); }
        }

        /// <summary>この距離までは完全に抜く（＝元の画面がそのまま見える）。</summary>
        public float InnerRadius
        {
            get => _innerRadius;
            set { _innerRadius = value; SetVerticesDirty(); }
        }

        /// <summary>この距離から外は抜かない（＝青が全部乗る）。</summary>
        public float OuterRadius
        {
            get => _outerRadius;
            set { _outerRadius = value; SetVerticesDirty(); }
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

                    float d = Vector2.Distance(new Vector2(x, y), _focusLocal);

                    Color c = baseColor;
                    c.a *= GhostAlphaAt(d) * _strength;

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
        /// 中心からの距離に対する青の濃さ。内側 0、外側 1。
        ///
        /// 間は 1.6 乗で戻す。直線で戻すと、抜いた所の縁が輪になって見える。
        /// </summary>
        private float GhostAlphaAt(float distance)
        {
            if (distance <= _innerRadius) return 0f;
            if (distance >= _outerRadius) return 1f;

            float t = (distance - _innerRadius) / Mathf.Max(0.0001f, _outerRadius - _innerRadius);
            return 1f - Mathf.Pow(1f - t, 1.6f);
        }
    }
}
