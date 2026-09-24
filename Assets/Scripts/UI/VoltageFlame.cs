using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ボルテージの四角の上で燃える炎（2026-09-13 に作り、2026-09-25 に作り直した）。
    ///
    /// **絵は使わない。** 炎の素材がプロジェクトに無く、AIで描くことは
    /// 禁じられている（AGENTS.md 第7項）。四角いドットを撒いて炎にしている。
    ///
    /// ---
    ///
    /// **粒はカールノイズの流れ場で動かす（2026-09-25）。**
    /// ユーザーが参考にくれた動画（ドット絵の炎を RANDOM と CURL NOISE で
    /// 並べたもの）で、違いがはっきり出ていた。
    ///
    ///   - RANDOM … 粒が銘々に散る。**煙のように濁って、炎に見えない**
    ///   - CURL NOISE … 粒が同じ流れに乗る。**縄のようにねじれて立ち上がる**
    ///
    /// カールノイズは、スカラー場 ψ の勾配を90度ひねったもの
    /// （`v = (∂ψ/∂y, -∂ψ/∂x)`）。**湧き出しも吸い込みも生まれない**ので、
    /// 粒が一点に集まったり抜けたりせず、流体らしいうねりになる。
    /// ψ には Perlin ノイズを使い、場そのものを上へ流している。
    ///
    /// 前の作りは丸いぼかし画像を3枚重ねて sin で揺らすものだった。
    /// 小さく光る点にしか見えず、炎として読めなかったので捨てた。
    ///
    /// ---
    ///
    /// **描画は自前のメッシュ1枚。** 粒ごとに `Image` を置くと、
    /// 自分と相手で 4区画 × 2 = 8本ぶん、百個を超える `Image` が毎フレーム
    /// レイアウトを揺らすことになる。`Graphic` を1つだけ持ち、
    /// <see cref="OnPopulateMesh"/> で粒のぶんの四角形を積む。
    ///
    /// **段が上がるほど激しくする**（ユーザーの指示）。
    /// 背が伸び、粒が増え、速くなる。段ごとの値は <see cref="Tuning"/> に集めてある。
    ///
    /// **揺れは実時間で回す。** 演出中に `Time.timeScale` をいじられても
    /// 炎だけ止まったり倍速になったりしないようにする。
    /// </summary>
    public class VoltageFlame : MaskableGraphic
    {
        /// <summary>段ごとの激しさと色。添字が段数（0段＝炎なし）。</summary>
        private struct Tuning
        {
            public float Height;     // 粒が消えるまでに上がる高さ[px]
            public float Rise;       // 立ち上がる速さ[px/秒]
            public float Swirl;      // 流れ場のうねりの強さ[px/秒]
            public int Count;        // 同時に出ている粒の数
            public float Dot;        // 粒の一辺[px]
            public Color Root;       // 中ほどの色
            public Color Tip;        // 消えぎわの色
            public Color Pip;        // 四角そのものの色

            public Tuning(float h, float rise, float swirl, int count, float dot, Color root, Color tip, Color pip)
            {
                Height = h; Rise = rise; Swirl = swirl; Count = count; Dot = dot;
                Root = root; Tip = tip; Pip = pip;
            }
        }

        /// <summary>
        /// 段ごとの値。**調整はここだけ触ればよい。**
        ///
        /// **段ごとに別の色にしてある**（2026-09-13 の指示）。
        /// 以前は赤→白の熱の階調にしていたが、四角も炎も小さいので差が読み取れなかった。
        /// いまは 赤 → 橙 → 黄 → 青白 と**色相ごと変えて**、ひと目で段が分かるようにしている。
        /// 4段が青白いのは、炎は一番熱いところが青くなるため。
        ///
        /// **四角の色も同じ表から取る。** 炎より四角のほうが大きいので、
        /// そちらが段の色になっているほうが早く読める。
        /// </summary>
        private static readonly Tuning[] ByLevel =
        {
            // 0段: 出さない
            new Tuning( 0f,  0f,  0f,  0, 0f, Color.clear, Color.clear, new Color32(255, 150,  40, 255)),
            // 1段: 赤
            new Tuning(20f, 26f, 12f, 16, 2f, new Color32(235,  80,  35, 255), new Color32(150,  25,  20, 255), new Color32(230,  70,  50, 255)),
            // 2段: 橙
            new Tuning(26f, 32f, 16f, 22, 2f, new Color32(255, 140,  35, 255), new Color32(180,  45,  25, 255), new Color32(255, 150,  40, 255)),
            // 3段: 黄
            new Tuning(32f, 40f, 20f, 28, 2f, new Color32(255, 205,  60, 255), new Color32(210,  85,  30, 255), new Color32(255, 225,  70, 255)),
            // 4段: 青白（一番熱い）
            new Tuning(40f, 50f, 25f, 34, 2f, new Color32(120, 195, 255, 255), new Color32( 40,  80, 200, 255), new Color32(120, 195, 255, 255)),
        };

        /// <summary>
        /// その段の四角の色。<see cref="VoltageUI"/> が点いた四角に塗るのに使う。
        /// **色を2箇所に書かないため、ここから配る。**
        /// </summary>
        public static Color PipColorFor(int level)
        {
            int i = Mathf.Clamp(level, 0, ByLevel.Length - 1);
            return ByLevel[i].Pip;
        }

        /// <summary>根元の色。**段によらず白寄り**にしてある（一番熱い場所なので）。</summary>
        private static readonly Color CoreColor = new Color32(255, 248, 225, 255);

        /// <summary>粒が出てくる幅[px]。四角（20px）より細くして、乗っている感を出す。</summary>
        private const float BaseWidth = 11f;

        /// <summary>
        /// ノイズの細かさ。小さいほど大きなうねりになる。
        /// 0.06 で、おおよそ 16px ごとに流れの向きが変わる。炎の幅が 11px なので
        /// **1本の炎の中に、ひとうねりが収まる**大きさ。
        /// </summary>
        private const float NoiseScale = 0.06f;

        /// <summary>流れ場そのものが上へ流れる速さ。止めると模様が貼り付いて見える。</summary>
        private const float FieldScroll = 0.55f;

        /// <summary>差分で勾配を取るときの幅[px]。</summary>
        private const float Epsilon = 1.5f;

        private struct Particle
        {
            public Vector2 Pos;
            public float Age;
            public float Life;
        }

        private Particle[] _particles;
        private int _level;
        private bool _broken;
        private float _fieldOffset;

        /// <summary>四角の上に炎を1つ作る。すでに付いていればそれを返す。</summary>
        public static VoltageFlame Attach(RectTransform pip)
        {
            if (pip == null) return null;

            var existing = pip.GetComponentInChildren<VoltageFlame>(true);
            if (existing != null) return existing;

            // **CanvasRenderer を自分で付けること。** `Graphic` は RequireComponent で
            // 付くことになっているが、`new GameObject(..., typeof(VoltageFlame))` で
            // 作るとこの経路では付かず、**絵が一切出ない**（2026-09-25 に実機で確認。
            // canvas も material も正しいのに CanvasRenderer だけ無かった）。
            var go = new GameObject("Flame", typeof(RectTransform), typeof(CanvasRenderer), typeof(VoltageFlame));
            var rect = (RectTransform)go.transform;
            rect.SetParent(pip, false);

            // 四角の上辺に足を置く。下から上へ伸ばす
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, -2f);   // 少し埋めて、浮いて見えないように
            rect.sizeDelta = new Vector2(BaseWidth, 1f);

            var flame = go.GetComponent<VoltageFlame>();
            flame.raycastTarget = false;                    // 牌のクリック判定を吸わない
            flame.ApplyVisibility();
            return flame;
        }

        // ------------------------------------------------------------
        //  丸いぼかし画像（他所へ貸しているので残す）
        // ------------------------------------------------------------

        private static Sprite _blob;

        /// <summary>
        /// ボルテージへ飛ぶ光（<see cref="VoltageTileFlightEffect"/>）へ貸している円。
        /// **四角のまま出すと点にしか見えない。** 外へ薄くなる円だと、
        /// 小さくても光って見える。
        ///
        /// 炎そのものは 2026-09-25 からこれを使っていない（四角いドットを撒いている）。
        /// 借り手が居るあいだは置いておく。
        /// </summary>
        public static Sprite SharedBlob
        {
            get
            {
                if (_blob != null) return _blob;

                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;

                float c = (size - 1) * 0.5f;
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - c) / c;
                        float dy = (y - c) / c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();

                _blob = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _blob;
            }
        }

        /// <summary>段と破棄状態を伝える。<see cref="VoltageUI"/> が段を変えるたびに呼ぶ。</summary>
        public void SetLevel(int level, bool broken)
        {
            int next = Mathf.Clamp(level, 0, ByLevel.Length - 1);
            bool changed = next != _level || broken != _broken;

            _level = next;
            _broken = broken;

            if (changed && _level > 0 && !_broken) Reseed();
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            // **破棄された段では燃やさない。** あそこは灰色で「落ちた」ことを示す場なので、
            // 炎が残っていると勢いがあるように見える。
            bool show = _level > 0 && !_broken;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
        }

        private void Reseed()
        {
            Tuning t = ByLevel[_level];
            if (_particles == null || _particles.Length != t.Count)
            {
                _particles = new Particle[t.Count];
            }

            for (int i = 0; i < _particles.Length; i++)
            {
                Spawn(ref _particles[i], t);
                // **最初から一様に散らす。** 全部を足元から始めると、
                // 段が上がった瞬間に炎が「生えてくる」動きになって目立つ。
                _particles[i].Age = Random.Range(0f, _particles[i].Life);
            }
        }

        private static void Spawn(ref Particle p, Tuning t)
        {
            // 中央ほど濃く撒く。端から同じだけ出すと、根元が四角く見える
            float x = (Random.value + Random.value - 1f) * BaseWidth * 0.5f;
            p.Pos = new Vector2(x, Random.Range(-1f, 1f));
            p.Age = 0f;
            p.Life = t.Height / Mathf.Max(1f, t.Rise) * Random.Range(0.75f, 1.25f);
        }

        /// <summary>
        /// カールノイズの速度。スカラー場 ψ の勾配を90度ひねる。
        /// **こうすると流れに湧き出しが無くなり、粒が固まらずに回り込む。**
        /// </summary>
        private Vector2 Curl(float x, float y)
        {
            float px = x * NoiseScale;
            float py = y * NoiseScale - _fieldOffset;
            float e = Epsilon * NoiseScale;

            float ddx = Mathf.PerlinNoise(px + e, py) - Mathf.PerlinNoise(px - e, py);
            float ddy = Mathf.PerlinNoise(px, py + e) - Mathf.PerlinNoise(px, py - e);

            // (∂ψ/∂y, -∂ψ/∂x)
            return new Vector2(ddy, -ddx) / (2f * e);
        }

        private void Update()
        {
            if (_level <= 0 || _broken) return;

            Tuning t = ByLevel[_level];
            if (_particles == null || _particles.Length != t.Count) Reseed();

            // **実時間で回す。** timeScale を落とす演出に巻き込まれないように
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);   // 重いフレームで飛ばない
            _fieldOffset += dt * FieldScroll;

            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Age += dt;
                if (_particles[i].Age >= _particles[i].Life)
                {
                    Spawn(ref _particles[i], t);
                    continue;
                }

                Vector2 v = Curl(_particles[i].Pos.x, _particles[i].Pos.y) * t.Swirl;

                // 上ほど速く、外へ広がる。炎は上で開く
                float up = _particles[i].Age / _particles[i].Life;
                v.y += t.Rise * (0.6f + 0.8f * up);
                v.x *= 0.7f + 1.1f * up;

                _particles[i].Pos += v * dt;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_level <= 0 || _broken || _particles == null) return;

            Tuning t = ByLevel[_level];

            for (int i = 0; i < _particles.Length; i++)
            {
                float up = Mathf.Clamp01(_particles[i].Age / Mathf.Max(0.0001f, _particles[i].Life));

                // **根元は白熱、中ほどが段の色、消えぎわは暗く落とす。**
                // 参考動画の炎も 白 → 黄 → 橙 → 赤 と、寿命で色が移っていた。
                Color c = up < 0.35f
                    ? Color.Lerp(CoreColor, t.Root, up / 0.35f)
                    : Color.Lerp(t.Root, t.Tip, (up - 0.35f) / 0.65f);

                // 最後の2割で消す。ふっと無くなるより、薄れて終わるほうが炎に見える
                if (up > 0.8f) c.a *= 1f - (up - 0.8f) / 0.2f;
                if (c.a <= 0.01f) continue;

                // **座標も大きさも整数に丸める。** ドット絵の中で半端な位置に置くと、
                // にじんで他のUIと質感が合わなくなる。
                // 下half は太く、上へ行くほど細く。参考動画も**胴が太くて先が散る**形だった
                float size = up < 0.55f ? t.Dot + 1f : t.Dot;
                float x = Mathf.Round(_particles[i].Pos.x);
                float y = Mathf.Round(_particles[i].Pos.y);

                AddQuad(vh, x, y, size, c);
            }
        }

        private static void AddQuad(VertexHelper vh, float x, float y, float size, Color c)
        {
            int start = vh.currentVertCount;
            float h = size * 0.5f;

            var v = UIVertex.simpleVert;
            v.color = c;

            v.position = new Vector3(x - h, y - h); vh.AddVert(v);
            v.position = new Vector3(x - h, y + h); vh.AddVert(v);
            v.position = new Vector3(x + h, y + h); vh.AddVert(v);
            v.position = new Vector3(x + h, y - h); vh.AddVert(v);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
