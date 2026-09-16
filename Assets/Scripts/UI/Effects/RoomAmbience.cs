using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 部屋の待機画面を「生きている場所」に見せる（2026-09-17 のユーザー指示）。
    ///
    /// 4つをまとめて面倒を見る:
    ///   1. 光   … 窓とランプをゆっくり明滅させる
    ///   2. 影   … 足元の影が、歩きに追従して伸び縮みする
    ///   3. 奥行き… 歩くと、遠い物ほどゆっくり流れる
    ///   4. つぶやき… ときどき一言しゃべる
    ///
    /// **1つの Update にまとめてある。** 別々の部品に散らすと、
    /// 同じ「女の子の位置」を何度も読みに行くことになる。
    ///
    /// **絵は持たない。** 影の楕円もその場で焼く。
    /// </summary>
    public sealed class RoomAmbience : MonoBehaviour
    {
        // ---- 1. 光 ----
        /// <summary>窓明かりの揺れ幅（元の濃さに対する割合）と速さ。</summary>
        private const float WindowPulse = 0.35f;
        private const float WindowSpeed = 0.42f;

        /// <summary>ランプの揺れ幅と速さ。**窓より速く、少し不規則に。** 炎の揺らぎに寄せる。</summary>
        private const float LampPulse = 0.45f;
        private const float LampSpeed = 1.7f;

        // ---- 2. 影 ----
        /// <summary>足元の影の濃さ。濃いと貼り付いて見える。</summary>
        private const float ShadowAlpha = 0.42f;

        /// <summary>
        /// 影の大きさ。**立ち絵の幅より確実に大きくすること。**
        ///
        /// 150x34 では**濃い中心がスカートの裏に丸ごと隠れ**、外へはみ出すのは
        /// 減衰しきった縁だけだった。影の有無で撮り比べたら、差は 255 階調中
        /// たったの 1〜2（2026-09-17 に実測）。スカートの裾は約110px あるので、
        /// 横幅はその倍近くを取り、中心の平らな部分を裾の外まで届かせる。
        /// </summary>
        private const float ShadowWidth = 210f;
        private const float ShadowHeight = 44f;

        /// <summary>
        /// 影の下げ幅[px]。**立ち絵の足の位置に合わせること。**
        /// 女の子の矩形の下辺が実際の足元なので、そこへ楕円の中心が来る値にする。
        /// -132 では足より 30px 上に浮いていた（2026-09-17 に実測して -166 へ）。
        /// </summary>
        private const float ShadowFootOffset = -166f;

        // ---- 3. 奥行き ----
        // **遠い物ほど小さく動かす。** 数字は「女の子の移動量に対する割合」。
        // 逆向きに動かすと、視点が女の子を追っているように見える。
        private const float FarShift = 0.05f;
        private const float MidShift = 0.11f;
        private const float NearShift = 0.19f;

        // ---- 4. つぶやき ----
        private const float MurmurInterval = 13f;
        private const float MurmurHold = 3.4f;
        private const float MurmurFade = 0.35f;

        private Image _windowGlow;
        private Image _lampGlow;
        private float _windowBaseAlpha;
        private float _lampBaseAlpha;

        private RectTransform _girl;
        private float _girlHomeX;

        private RectTransform _shadow;
        private Image _shadowImage;

        private RectTransform _far;
        private RectTransform _mid;
        private RectTransform _near;
        private Vector2 _farHome;
        private Vector2 _midHome;
        private Vector2 _nearHome;

        private RectTransform _bubble;
        private CanvasGroup _bubbleGroup;
        private TextMeshProUGUI _bubbleText;
        private float _murmurTimer;
        private int _murmurIndex;

        /// <summary>
        /// つぶやき。**短く、説明をしない。** 待機画面なので、
        /// ここで遊び方を教えようとすると説明くさくなる。
        /// </summary>
        private static readonly string[] Murmurs =
        {
            "「……ひま」",
            "「次はどこの卓かしらね」",
            "「牌の音がしないと、落ち着かないの」",
            "「窓の外、ずっと夜のまま」",
            "「あなた、まだいたの」",
        };

        public static RoomAmbience Attach(Transform parent, RectTransform girl,
                                          Image windowGlow, Image lampGlow,
                                          RectTransform far, RectTransform mid, RectTransform near,
                                          TMP_FontAsset font)
        {
            if (parent == null || girl == null) return null;

            var go = new GameObject("RoomAmbience", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var a = go.AddComponent<RoomAmbience>();
            a.Setup(rt, girl, windowGlow, lampGlow, far, mid, near, font);
            return a;
        }

        private void Setup(RectTransform self, RectTransform girl, Image windowGlow, Image lampGlow,
                           RectTransform far, RectTransform mid, RectTransform near, TMP_FontAsset font)
        {
            _girl = girl;
            _girlHomeX = girl.anchoredPosition.x;

            _windowGlow = windowGlow;
            _lampGlow = lampGlow;
            if (_windowGlow != null) _windowBaseAlpha = _windowGlow.color.a;
            if (_lampGlow != null) _lampBaseAlpha = _lampGlow.color.a;

            _far = far; _mid = mid; _near = near;
            if (_far != null) _farHome = _far.anchoredPosition;
            if (_mid != null) _midHome = _mid.anchoredPosition;
            if (_near != null) _nearHome = _near.anchoredPosition;

            BuildShadow();
            BuildBubble(self, font);

            _murmurTimer = MurmurInterval * 0.4f;   // 最初の一言は早めに
        }

        /// <summary>
        /// 足元の影。**女の子のすぐ後ろに差し込む。**
        ///
        /// 最初 `SetAsFirstSibling()` で先頭へ送ったら、影は**部屋ごと後ろに回って
        /// 床の絵に隠れ、一度も見えなかった**（2026-09-17 に測って気づいた。
        /// 足元の明るさを横に走査しても、周りと同じ 56 前後のまま凹まなかった）。
        /// 女の子の親は `content` で、背景の層もそこの子なので、先頭 = 背景より奥。
        ///
        /// 正しくは「女の子の1つ手前」に入れること。輪郭(`RoomGirlOutline`)が
        /// 女の子の直前にいるので、そのさらに手前を狙う。
        /// </summary>
        private void BuildShadow()
        {
            var go = new GameObject("RoomGirlShadow", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_girl.parent, false);
            rt.SetSiblingIndex(Mathf.Max(0, _girl.GetSiblingIndex() - 1));
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ShadowWidth, ShadowHeight);

            _shadowImage = go.GetComponent<Image>();
            _shadowImage.sprite = BakeEllipse();
            _shadowImage.raycastTarget = false;
            _shadowImage.color = new Color(0f, 0f, 0f, ShadowAlpha);
            _shadow = rt;
        }

        /// <summary>外へ向かって薄くなる楕円。縁を硬くすると貼り紙に見える。</summary>
        private static Sprite BakeEllipse()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color[w * h];
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cy) / cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // **中心は平らに、縁だけぼかす。**
                    // `a * a` にすると中心から外へすぐ薄くなり、体で隠れない
                    // 範囲には何も残らなかった。d が 0.55 までは濃さを保つ。
                    float a = Mathf.Clamp01((1f - d) / 0.45f);
                    a = a * a * (3f - 2f * a);          // 縁をなめらかに落とす
                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            // **`SpriteMeshType.FullRect` を必ず指定する（2026-09-17 に判明）。**
            //
            // 既定は `Tight`。透明な画素を刈り込んだ多角形を作るので、
            // **縁が完全に透明なこの楕円は外周を削られ、`Image` が一枚も描かなかった。**
            // 実際 `sprite.vertices` は 9 点しかなく、赤に塗っても画面に出なかった。
            // スプライトを外して無地の板にすると描けたので、絵ではなく形の問題と分かった。
            //
            // 周辺減光や粒子が無事なのは、あちらの縁が不透明で刈られないから。
            // **その場で焼いた絵を `Image` に貼るときは FullRect。**
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect);
        }

        private void BuildBubble(RectTransform parent, TMP_FontAsset font)
        {
            var go = new GameObject("RoomMurmur", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 54f);

            var back = new GameObject("Back", typeof(RectTransform), typeof(Image));
            var backRt = (RectTransform)back.transform;
            backRt.SetParent(rt, false);
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(0.08f, 0.05f, 0.09f, 0.82f);
            backImg.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRt = (RectTransform)label.transform;
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(10f, 4f);
            labelRt.offsetMax = new Vector2(-10f, -4f);

            _bubbleText = label.GetComponent<TextMeshProUGUI>();
            if (font != null) _bubbleText.font = font;
            _bubbleText.fontSize = 20f;
            _bubbleText.alignment = TextAlignmentOptions.Center;
            _bubbleText.color = new Color(0.94f, 0.9f, 0.92f, 1f);
            _bubbleText.raycastTarget = false;

            _bubbleGroup = go.GetComponent<CanvasGroup>();
            _bubbleGroup.alpha = 0f;
            _bubbleGroup.blocksRaycasts = false;
            _bubble = rt;
        }

        private void Update()
        {
            // **実時間で回す。** 待機画面は時間が止まっていることがある
            float dt = Time.unscaledDeltaTime;
            float t = Time.unscaledTime;

            UpdateLights(t);
            UpdateShadowAndParallax();
            UpdateMurmur(dt);
        }

        private void UpdateLights(float t)
        {
            if (_windowGlow != null)
            {
                // 窓は月明かり。ゆっくり、ひと呼吸ぶんだけ
                float k = 1f + Mathf.Sin(t * WindowSpeed) * WindowPulse;
                var c = _windowGlow.color;
                c.a = _windowBaseAlpha * k;
                _windowGlow.color = c;
            }

            if (_lampGlow != null)
            {
                // **2つの波を重ねる。** 1つだけだと機械的な明滅になり、
                // 火に見えない
                float k = 1f
                    + Mathf.Sin(t * LampSpeed) * LampPulse * 0.6f
                    + Mathf.Sin(t * LampSpeed * 2.7f + 1.3f) * LampPulse * 0.4f;
                var c = _lampGlow.color;
                c.a = _lampBaseAlpha * Mathf.Max(0.2f, k);
                _lampGlow.color = c;
            }
        }

        private void UpdateShadowAndParallax()
        {
            if (_girl == null) return;

            Vector2 girlPos = _girl.anchoredPosition;
            float moved = girlPos.x - _girlHomeX;

            if (_shadow != null)
            {
                _shadow.anchoredPosition = new Vector2(girlPos.x, girlPos.y + ShadowFootOffset);

                // **ランプへ近づくほど影を濃く小さく。** 光源に寄ると影が締まる
                float toLamp = Mathf.Abs(girlPos.x - LampX);
                float near = 1f - Mathf.Clamp01(toLamp / 420f);
                _shadow.sizeDelta = new Vector2(ShadowWidth * (1.12f - near * 0.24f),
                                                ShadowHeight * (1.10f - near * 0.22f));
                var c = _shadowImage.color;
                c.a = ShadowAlpha * (0.78f + near * 0.42f);
                _shadowImage.color = c;
            }

            // 遠いものほど小さく、逆向きに流す
            if (_far != null) _far.anchoredPosition = _farHome + new Vector2(-moved * FarShift, 0f);
            if (_mid != null) _mid.anchoredPosition = _midHome + new Vector2(-moved * MidShift, 0f);
            if (_near != null) _near.anchoredPosition = _nearHome + new Vector2(-moved * NearShift, 0f);
        }

        /// <summary>ランプのある横位置。`RoomScreenUI` の机まわりに合わせてある。</summary>
        private const float LampX = 257f;

        private void UpdateMurmur(float dt)
        {
            if (_bubble == null) return;

            _murmurTimer -= dt;

            // 出ている間は、女の子の頭の上に付いていく
            if (_girl != null)
            {
                var p = _girl.anchoredPosition;
                // **頭より上へ逃がす。** 150 だと吹き出しの下辺が
                // 頭頂より 54px 下に来て、髪にかぶさった（2026-09-17 に実測）。
                _bubble.anchoredPosition = new Vector2(p.x, p.y + 220f);
            }

            if (_murmurTimer > 0f)
            {
                // 消えていく
                if (_bubbleGroup.alpha > 0f)
                    _bubbleGroup.alpha = Mathf.MoveTowards(_bubbleGroup.alpha, 0f, dt / MurmurFade);
                return;
            }

            // しゃべっている最中か、次を出すか
            float sinceDue = -_murmurTimer;
            if (sinceDue < MurmurHold)
            {
                if (_bubbleText.text.Length == 0 || _bubbleGroup.alpha <= 0f)
                {
                    _bubbleText.text = Murmurs[_murmurIndex % Murmurs.Length];
                    _murmurIndex++;
                }
                _bubbleGroup.alpha = Mathf.MoveTowards(_bubbleGroup.alpha, 1f, dt / MurmurFade);
            }
            else
            {
                _murmurTimer = MurmurInterval;
                _bubbleText.text = string.Empty;
            }
        }
    }
}
