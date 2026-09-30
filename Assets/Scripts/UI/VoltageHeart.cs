using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ボルテージの点灯ブロックの中で脈打つ心臓（2026-09-30 のユーザー指示
    /// 「炎の土台、心臓っぽいものをモチーフに」）。
    ///
    /// **絵は使わない。コードでドットを置いて描いている。**
    /// AIでの画像生成は用途を問わず禁止されており（km-docs/AGENTS.md 第18項）、
    /// ゲームに入れる素材は勝手に作らずユーザーに相談する決まりになっている。
    /// 炎（<see cref="VoltageFlame"/>）が四角いドットを撒いて作られているのと同じ考え方で、
    /// ここも 8x7 のドット絵を配列で持つ。
    ///
    /// **拍は「ドクン・ドクン」の二段打ち。** ただの sin で伸び縮みさせると
    /// 呼吸に見えて心臓に見えない。短い大きな鼓動のすぐ後に小さな鼓動を打ち、
    /// そのあと休む形にしている。
    ///
    /// **段が上がるほど速く打つ。** 炎が激しくなるのと揃える。
    /// </summary>
    public class VoltageHeart : MaskableGraphic
    {
        /// <summary>
        /// 心臓のドット絵（8x7）。上の行が画面の上。
        /// **8px しか無いので、輪郭が読める最小限の形にしてある。**
        /// </summary>
        private static readonly string[] Shape =
        {
            ".##..##.",
            "########",
            "########",
            "########",
            ".######.",
            "..####..",
            "...##...",
        };

        /// <summary>ドット1個の一辺[px]。ブロックが 20x8 なので、等倍で置くと収まる。</summary>
        private const float DotSize = 1f;

        /// <summary>
        /// 段ごとの脈の速さ[回/秒]。添字が段数（0段＝出さない）。
        /// 4段でおよそ 1.7 回/秒＝毎分100拍。走ったときくらいの速さ。
        /// </summary>
        private static readonly float[] BeatsPerSecond = { 0f, 0.85f, 1.1f, 1.4f, 1.7f };

        /// <summary>鼓動で膨らむ割合。**大きくしすぎるとブロックからはみ出す。**</summary>
        private const float BeatScale = 0.22f;

        /// <summary>
        /// 心臓の色。**段によらず白熱色**にしてある。
        /// ブロック自体が段の色（赤→橙→黄→青白）なので、その上に置く心臓まで
        /// 段で色を変えると、4段の青白ブロックに青白い心臓が乗って形が消える。
        /// 白熱色なら、どの段でも「いちばん熱い芯」として読める。
        /// </summary>
        private static readonly Color HeartColor = new Color32(255, 246, 232, 255);

        private int _level;
        private bool _broken;
        private float _phase;
        private float _scale = 1f;

        /// <summary>ブロックの中心に心臓を1つ作る。すでに付いていればそれを返す。</summary>
        public static VoltageHeart Attach(RectTransform pip)
        {
            if (pip == null) return null;

            var existing = pip.GetComponentInChildren<VoltageHeart>(true);
            if (existing != null) return existing;

            // **CanvasRenderer を自分で付けること。** `new GameObject(..., typeof(Graphic派生))`
            // では RequireComponent が効かず、絵が一切出ない
            // （VoltageFlame で 2026-09-25 に踏んだのと同じ罠）。
            var go = new GameObject("Heart", typeof(RectTransform), typeof(CanvasRenderer), typeof(VoltageHeart));
            var rect = (RectTransform)go.transform;
            rect.SetParent(pip, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(Shape[0].Length * DotSize, Shape.Length * DotSize);

            var heart = go.GetComponent<VoltageHeart>();
            heart.raycastTarget = false;
            heart.ApplyVisibility();
            return heart;
        }

        /// <summary>段と破棄状態を伝える。<see cref="VoltageUI"/> が段を変えるたびに呼ぶ。</summary>
        public void SetLevel(int level, bool broken)
        {
            _level = Mathf.Clamp(level, 0, BeatsPerSecond.Length - 1);
            _broken = broken;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            // 破棄された段では打たない。炎を止めているのと揃える
            bool show = _level > 0 && !_broken;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
        }

        private void Update()
        {
            if (_level <= 0 || _broken) return;

            // **実時間で回す。** 演出で timeScale をいじられても脈が止まらないように
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _phase += dt * BeatsPerSecond[_level];
            if (_phase >= 1f) _phase -= 1f;

            float next = 1f + BeatEnvelope(_phase) * BeatScale;
            if (!Mathf.Approximately(next, _scale))
            {
                _scale = next;
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// 1拍ぶんの膨らみ（0〜1）。**「ドクン・ドクン」の二段打ち。**
        ///
        ///   0.00〜0.12  大きい鼓動（立ち上がりが速く、落ちが緩い）
        ///   0.18〜0.28  小さい鼓動
        ///   それ以降     休み
        ///
        /// 心音の波形（S1 が強く、少し遅れて S2 が弱く鳴る）をなぞっている。
        /// </summary>
        private static float BeatEnvelope(float t)
        {
            float a = Pulse(t, 0.00f, 0.12f) * 1.0f;
            float b = Pulse(t, 0.18f, 0.28f) * 0.55f;
            return Mathf.Clamp01(a + b);
        }

        private static float Pulse(float t, float from, float to)
        {
            if (t < from || t > to) return 0f;
            float u = (t - from) / (to - from);
            // 立ち上がりを速く、落ちを緩く
            return u < 0.3f ? u / 0.3f : 1f - (u - 0.3f) / 0.7f;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_level <= 0 || _broken) return;

            int w = Shape[0].Length;
            int h = Shape.Length;
            float dot = DotSize * _scale;

            // 中心が原点に来るように置く
            float ox = -w * dot * 0.5f;
            float oy = -h * dot * 0.5f;

            for (int row = 0; row < h; row++)
            {
                string line = Shape[row];
                for (int col = 0; col < w; col++)
                {
                    if (line[col] != '#') continue;

                    // 配列の 0 行目を上にしたいので、y を反転して積む
                    float x = ox + col * dot;
                    float y = oy + (h - 1 - row) * dot;
                    AddQuad(vh, x, y, dot, HeartColor);
                }
            }
        }

        private static void AddQuad(VertexHelper vh, float x, float y, float size, Color c)
        {
            int start = vh.currentVertCount;

            var v = UIVertex.simpleVert;
            v.color = c;

            v.position = new Vector3(x, y); vh.AddVert(v);
            v.position = new Vector3(x, y + size); vh.AddVert(v);
            v.position = new Vector3(x + size, y + size); vh.AddVert(v);
            v.position = new Vector3(x + size, y); vh.AddVert(v);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
