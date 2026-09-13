using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ボルテージの四角の上で揺れる炎（2026-09-13）。
    ///
    /// **絵は使わず、丸を積んで作っている。** 炎の素材がプロジェクトに無く、
    /// AIで描くことは禁じられている（AGENTS.md 第7項）ため。
    /// 中心から外へ薄くなる円をその場で焼き（<see cref="GetBlob"/>）、
    /// それを3枚重ねて、位置と大きさと色を毎フレーム動かして炎に見せている。
    ///
    /// **段が上がるほど激しくする**（ユーザーの指示）。
    /// 背が伸び、揺れが速く大きくなり、芯（明るい黄色）が出る。
    /// 段ごとの値は <see cref="Tuning"/> にまとめてあるので、そこだけ触ればよい。
    ///
    /// **揺れは実時間で回す。** 演出中に `Time.timeScale` をいじられても
    /// 炎だけ止まったり倍速になったりしないようにする。
    /// </summary>
    public class VoltageFlame : MonoBehaviour
    {
        /// <summary>段ごとの激しさと色。添字が段数（0段＝炎なし）。</summary>
        private struct Tuning
        {
            public float Height;     // 背の高さの倍率
            public float Speed;      // 揺れの速さ
            public float Sway;       // 横揺れの幅[px]
            public float CoreAlpha;  // 芯の濃さ（0で芯なし）
            public Color Root;       // 炎の根元の色
            public Color Tip;        // 炎の先端の色
            public Color Pip;        // 四角そのものの色

            public Tuning(float h, float s, float w, float c, Color root, Color tip, Color pip)
            {
                Height = h; Speed = s; Sway = w; CoreAlpha = c; Root = root; Tip = tip; Pip = pip;
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
            new Tuning(0.00f,  0f, 0.0f, 0.00f, Color.clear, Color.clear, new Color32(255, 150,  40, 255)),
            // 1段: 赤
            new Tuning(1.00f,  4f, 0.8f, 0.00f, new Color32(205,  45,  30, 235), new Color32(255, 115,  80, 240), new Color32(230,  70,  50, 255)),
            // 2段: 橙
            new Tuning(1.30f,  6f, 1.3f, 0.00f, new Color32(240, 105,  25, 235), new Color32(255, 190,  85, 240), new Color32(255, 150,  40, 255)),
            // 3段: 黄
            new Tuning(1.60f,  8f, 1.9f, 0.40f, new Color32(250, 200,  40, 240), new Color32(255, 250, 170, 245), new Color32(255, 225,  70, 255)),
            // 4段: 青白（一番熱い）
            new Tuning(1.95f, 12f, 2.6f, 0.75f, new Color32( 70, 150, 255, 240), new Color32(225, 245, 255, 250), new Color32(120, 195, 255, 255)),
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

        /// <summary>舌の数。増やすほど重くなるので、この大きさなら3枚で足りる。</summary>
        private const int TongueCount = 3;

        /// <summary>一番下の舌の直径[px]。四角（13px）より少し細くして、乗っている感を出す。</summary>
        private const float BaseWidth = 12f;

        /// <summary>舌1枚ぶんの積み上げ量[px]。`Height` 倍されて使われる。</summary>
        private const float StackStep = 5.5f;

        /// <summary>芯の色。**段によらず白寄り**にしてある（一番熱い場所なので）。</summary>
        private static readonly Color CoreColor = new Color32(255, 250, 225, 255);

        private Image[] _tongues;
        private Image _core;
        private RectTransform[] _tongueRects;
        private RectTransform _coreRect;

        private int _level;
        private bool _broken;
        private float _phase;

        /// <summary>四角の上に炎を1つ作る。すでに付いていればそれを返す。</summary>
        public static VoltageFlame Attach(RectTransform pip)
        {
            if (pip == null) return null;

            var existing = pip.GetComponentInChildren<VoltageFlame>(true);
            if (existing != null) return existing;

            var go = new GameObject("Flame", typeof(RectTransform), typeof(VoltageFlame));
            var rect = (RectTransform)go.transform;
            rect.SetParent(pip, false);

            // 四角の上辺に足を置く。下から上へ伸ばす
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, -2f);   // 少し埋めて、浮いて見えないように
            rect.sizeDelta = new Vector2(BaseWidth, 1f);

            var flame = go.GetComponent<VoltageFlame>();
            flame.Build();
            return flame;
        }

        /// <summary>
        /// 炎の粒に使う円。**その場で焼いて、全員で使い回す。**
        ///
        /// `Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd")` は
        /// このUnityでは null が返る（2026-09-13 に確認）ので当てにしない。
        /// 中心から外へ薄くなる作りにしてあり、縁が硬い円より炎に見える。
        /// </summary>
        private static Sprite _blob;

        private static Sprite GetBlob()
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
                    // 中心は不透明、外周でちょうど 0。二乗で落として芯を残す
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();

            _blob = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _blob;
        }

        private void Build()
        {
            Sprite circle = GetBlob();

            _tongues = new Image[TongueCount];
            _tongueRects = new RectTransform[TongueCount];

            for (int i = 0; i < TongueCount; i++)
            {
                var go = new GameObject("Tongue" + i, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                var img = go.GetComponent<Image>();
                img.sprite = circle;
                img.raycastTarget = false;      // 牌のクリック判定を吸わない
                _tongues[i] = img;
                _tongueRects[i] = rect;
            }

            var coreGo = new GameObject("Core", typeof(RectTransform), typeof(Image));
            _coreRect = (RectTransform)coreGo.transform;
            _coreRect.SetParent(transform, false);
            _coreRect.anchorMin = new Vector2(0.5f, 0f);
            _coreRect.anchorMax = new Vector2(0.5f, 0f);
            _coreRect.pivot = new Vector2(0.5f, 0.5f);

            _core = coreGo.GetComponent<Image>();
            _core.sprite = circle;
            _core.raycastTarget = false;
            _core.color = new Color(CoreColor.r, CoreColor.g, CoreColor.b, 0f);

            // 起きた瞬間に全部が同じ形にならないよう、個体ごとに位相をずらす
            _phase = Random.Range(0f, 10f);

            ApplyVisibility();
        }

        /// <summary>段と破棄状態を伝える。<see cref="VoltageUI"/> が段を変えるたびに呼ぶ。</summary>
        public void SetLevel(int level, bool broken)
        {
            _level = Mathf.Clamp(level, 0, ByLevel.Length - 1);
            _broken = broken;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            // **破棄された段では燃やさない。** あそこは灰色で「落ちた」ことを示す場なので、
            // 炎が残っていると勢いがあるように見える。
            bool show = _level > 0 && !_broken;
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
        }

        private void Update()
        {
            if (_level <= 0 || _broken) return;
            if (_tongueRects == null) return;

            Tuning t = ByLevel[_level];

            // **実時間で回す。** timeScale を落とす演出に巻き込まれないように
            _phase += Time.unscaledDeltaTime * t.Speed;

            for (int i = 0; i < _tongueRects.Length; i++)
            {
                var rect = _tongueRects[i];
                if (rect == null) continue;

                // 上にある舌ほど細く、よく揺れる
                float up = i / (float)(TongueCount - 1);          // 0=根元, 1=先端
                float width = Mathf.Lerp(BaseWidth, BaseWidth * 0.45f, up);

                // 舌ごとに位相をずらして、束が一体で動かないようにする
                float p = _phase + i * 1.7f;
                float sway = Mathf.Sin(p) * t.Sway * (0.3f + up);          // 先ほど大きく振れる
                float breathe = 1f + Mathf.Sin(p * 1.9f) * (0.12f + 0.10f * up);

                float y = (i * StackStep) * t.Height * breathe;

                rect.sizeDelta = new Vector2(width, width * 1.6f);         // 縦に伸ばして炎の形に近づける
                rect.anchoredPosition = new Vector2(sway, y);

                // 先端ほど明るい。色は段ごとの組み合わせから取る。
                // 揺れに合わせて少し明滅させる
                Color c = Color.Lerp(t.Root, t.Tip, up);
                c.a *= 0.85f + 0.15f * Mathf.Sin(p * 2.3f);
                _tongues[i].color = c;
            }

            // 芯は3段目から。根元に置いて、内側が白く燃えている感じにする
            if (_core != null)
            {
                if (t.CoreAlpha <= 0f)
                {
                    _core.color = new Color(CoreColor.r, CoreColor.g, CoreColor.b, 0f);
                }
                else
                {
                    float flicker = 0.75f + 0.25f * Mathf.Sin(_phase * 3.1f);
                    float w = BaseWidth * 0.45f;
                    _coreRect.sizeDelta = new Vector2(w, w * 1.6f);
                    _coreRect.anchoredPosition = new Vector2(Mathf.Sin(_phase * 1.3f) * t.Sway * 0.4f,
                                                            StackStep * 0.6f * t.Height);
                    _core.color = new Color(CoreColor.r, CoreColor.g, CoreColor.b, t.CoreAlpha * flicker);
                }
            }
        }
    }
}
