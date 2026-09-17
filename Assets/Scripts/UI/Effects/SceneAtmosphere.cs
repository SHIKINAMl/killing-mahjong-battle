using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 画面に「空気」をかぶせる（2026-09-17 のユーザー指示）。
    /// 部屋の待機画面から始めて、対局とチュートリアルにも同じものを使う。
    ///
    /// 重ねるのは2枚:
    ///   1. 周辺減光 … 四隅を落として、真ん中へ目を集める
    ///   2. 粒子     … ざらつき。止まった画面が「生きている」ように見える
    ///
    /// **絵はその場で焼く。** 外部の素材を持たないので、差し替え漏れが起きない
    /// （`VoltageFlame` の丸と同じ考え方）。
    ///
    /// **薄くかけること。** このゲームはドット絵なので、濃くすると絵が潰れる。
    /// 濃さは <see cref="Attach(Transform, float, float, float)"/> の引数で決める。
    ///
    /// **クリックを吸わない。** 全画面に重ねるので、`raycastTarget` を切らないと
    /// 裏のUIが永久に押せなくなる。
    ///
    /// **情報の上に乗せない。** 対局画面では Canvas の `sortingOrder` で
    /// 卓や牌より上・数字やセリフより下に置く（<see cref="BattleAtmosphere"/>）。
    /// </summary>
    public sealed class SceneAtmosphere : MonoBehaviour
    {
        /// <summary>部屋の待機画面の濃さ。</summary>
        public const float RoomVignette = 0.45f;
        /// <summary>部屋の待機画面のざらつき。</summary>
        public const float RoomGrain = 0.07f;
        /// <summary>部屋の地の明るさ（255階調中 40 前後）。</summary>
        public const float RoomGrainMean = 0.16f;

        /// <summary>粒のばらつき幅。粒の明るさの上下へこの割合で振る。</summary>
        private const float GrainSpread = 0.16f;

        /// <summary>粒子の焼き置き枚数。順に見せてざらつきを動かす。</summary>
        private const int GrainFrames = 8;

        /// <summary>粒子の切り替え速度[枚/秒]。速すぎると落ち着かない。</summary>
        private const float GrainFps = 11f;

        private Image _grain;
        private Sprite[] _grainSprites;
        private float _elapsed;

        /// <summary>
        /// 画面へ空気の層を足す。**呼ぶ場所で重なり順が決まる。**
        /// 中身を作ったあと、手前に置きたい物を作る前に呼ぶこと。
        /// </summary>
        /// <param name="vignetteAlpha">四隅の暗さ。</param>
        /// <param name="grainAlpha">ざらつきの濃さ。0.05 前後まで。</param>
        /// <param name="grainMean">
        /// 粒の明るさの中心。**その画面の地の明るさに合わせること。**
        /// 白い粒(0.5)を暗い画面へかけると、ざらつきではなく**ただの白み**になる。
        /// 実測では四隅が +7.5階調 持ち上がり、周辺減光(-7.0)を打ち消していた。
        /// </param>
        public static SceneAtmosphere Attach(Transform parent, float vignetteAlpha,
                                             float grainAlpha, float grainMean)
        {
            if (parent == null) return null;

            var go = new GameObject("SceneAtmosphere", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);

            var atmosphere = go.AddComponent<SceneAtmosphere>();
            atmosphere.Build(rt, vignetteAlpha, grainAlpha, grainMean);
            return atmosphere;
        }

        private void Build(RectTransform rt, float vignetteAlpha, float grainAlpha, float grainMean)
        {
            AddLayer(rt, "Vignette", BakeVignette(), new Color(0f, 0f, 0f, vignetteAlpha),
                     Image.Type.Simple);

            // **粒子は引き伸ばさず、敷き詰める（2026-09-17 に直した）。**
            // 64px の絵を画面いっぱいへ広げたら、1粒が 12x9px の塊になって
            // ドット絵が泥のように汚れた。等倍で並べれば、粒が1画素になる。
            _grainSprites = BakeGrain(grainMean);
            _grain = AddLayer(rt, "Grain", _grainSprites[0],
                              new Color(1f, 1f, 1f, grainAlpha), Image.Type.Tiled);

            // **走査線は入れない（2026-09-17 に試してやめた）。**
            // `Image.Type.Tiled` で敷こうとしたが、横1pxでも64pxでも描画されず
            // （画面の明暗差を測って 0.37 / 0.51 = 効いていない）、
            // そもそも**ドット絵に横線を重ねるとゲーム自身の画素の並びと干渉して汚れる。**
            // 周辺減光と粒子の2枚で狙いの空気は出ているので、ここは足さない。
            // 入れたくなったら、全画面ぶんの絵を1枚焼いて Simple で貼るのが確実。
        }

        private static Image AddLayer(RectTransform parent, string name, Sprite sprite,
                                      Color color, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = false;     // 裏のメニューを押せなくしない
            return image;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 周辺減光。中心が透明、外へ行くほど濃い。
        /// **四隅ではなく「中心からの距離」で作る。** 辺の中ほども少し落ちるので、
        /// 画面がのぞき穴のように見える。
        /// </summary>
        private static Sprite BakeVignette()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var px = new Color[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.4142f;   // 角で 1.0

                    // 真ん中の平らな部分を残してから落とす。
                    // いきなり落とすと、中心にも影がかかって眠い絵になる
                    float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            // **`SpriteMeshType.FullRect` を明示する（2026-09-17）。**
            // 既定の `Tight` は透明な画素を刈った多角形を作るので、
            // 中心が透明なこの絵は形が崩れる（頂点が19個もできていた）。
            // 足元の影は同じ罠で一枚も描かれなかった。焼いた絵は FullRect。
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect);
        }

        /// <summary>ざらつきを何枚か焼く。毎フレーム作ると重いので、順に見せて回す。</summary>
        private static Sprite[] BakeGrain(float grainMean)
        {
            const int size = 64;
            var sprites = new Sprite[GrainFrames];

            for (int f = 0; f < GrainFrames; f++)
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Repeat;
                tex.filterMode = FilterMode.Point;   // ドット絵なので、ぼかさない

                var rng = new System.Random(1000 + f);
                var px = new Color[size * size];
                for (int i = 0; i < px.Length; i++)
                {
                    // 地の明るさを中心に振る。0〜1 いっぱいに振ると、
                    // 薄くかけても画面全体が白く浮く
                    float v = (float)rng.NextDouble();
                    v = grainMean + (v - 0.5f) * 2f * GrainSpread;
                    v = Mathf.Clamp01(v);
                    px[i] = new Color(v, v, v, 1f);
                }
                tex.SetPixels(px);
                tex.Apply();
                // **PPU は `Canvas.referencePixelsPerUnit` と同じ 100 にする。**
                //
                // `Image.Type.Tiled` の敷き詰め幅は
                //   絵の幅 ÷ (スプライトのPPU ÷ CanvasのreferencePixelsPerUnit)
                // で決まる。既定の referencePixelsPerUnit は 100 なので、
                // **PPU=1 にすると 64px の絵が 6400px に引き伸ばされ、
                // 1粒が 100x100px の塊になっていた**（2026-09-17 に実測。
                // 空気層の有無で撮り比べたら、四隅がむしろ +6.5階調 明るくなり、
                // 壁一面に四角い斑が出ていた）。
                // 100 なら 64px がそのまま 64px で並び、1粒=1画素になる。
                sprites[f] = Sprite.Create(tex, new Rect(0, 0, size, size),
                                           new Vector2(0.5f, 0.5f), 100f,
                                           0, SpriteMeshType.FullRect);
            }
            return sprites;
        }

        private void Update()
        {
            if (_grain == null || _grainSprites == null) return;

            // **実時間で回す。** 待機画面は時間が止まっていることがある
            _elapsed += Time.unscaledDeltaTime;
            int frame = (int)(_elapsed * GrainFps) % _grainSprites.Length;
            if (_grain.sprite != _grainSprites[frame]) _grain.sprite = _grainSprites[frame];
        }
    }
}
