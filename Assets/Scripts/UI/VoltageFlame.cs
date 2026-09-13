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
        /// <summary>段ごとの激しさ。添字が段数（0段＝炎なし）。</summary>
        private struct Tuning
        {
            public float Height;     // 背の高さの倍率
            public float Speed;      // 揺れの速さ
            public float Sway;       // 横揺れの幅[px]
            public float CoreAlpha;  // 芯の濃さ（0で芯なし）

            public Tuning(float h, float s, float w, float c) { Height = h; Speed = s; Sway = w; CoreAlpha = c; }
        }

        private static readonly Tuning[] ByLevel =
        {
            new Tuning(0.00f,  0f, 0.0f, 0.00f),   // 0段: 出さない
            new Tuning(1.00f,  4f, 0.8f, 0.00f),   // 1段: ちろちろ
            new Tuning(1.25f,  6f, 1.3f, 0.00f),   // 2段
            new Tuning(1.55f,  8f, 1.9f, 0.35f),   // 3段: 芯が見え始める
            new Tuning(1.90f, 11f, 2.6f, 0.70f),   // 4段: 一番激しい
        };

        /// <summary>舌の数。増やすほど重くなるので、この大きさなら3枚で足りる。</summary>
        private const int TongueCount = 3;

        /// <summary>一番下の舌の直径[px]。四角（13px）より少し細くして、乗っている感を出す。</summary>
        private const float BaseWidth = 12f;

        /// <summary>舌1枚ぶんの積み上げ量[px]。`Height` 倍されて使われる。</summary>
        private const float StackStep = 5.5f;

        private static readonly Color FlameLow = new Color32(255, 120, 30, 230);
        private static readonly Color FlameHigh = new Color32(255, 190, 60, 235);
        private static readonly Color CoreColor = new Color32(255, 245, 180, 255);

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

                // 先端ほど明るい。揺れに合わせて少し明滅させる
                Color c = Color.Lerp(FlameLow, FlameHigh, up);
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
