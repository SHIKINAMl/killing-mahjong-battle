using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 部屋の待機画面に、地下の空気をかぶせる（2026-09-17 のユーザー指示）。
    ///
    /// 重ねるのは2枚:
    ///   1. 周辺減光 … 四隅を落として、真ん中へ目を集める
    ///   2. 粒子     … ざらつき。止まった画面が「生きている」ように見える
    ///
    /// **絵はその場で焼く。** 外部の素材を持たないので、差し替え漏れが起きない
    /// （`VoltageFlame` の丸と同じ考え方）。
    ///
    /// **薄くかけること。** このゲームはドット絵なので、濃くすると絵が潰れる。
    /// 濃さを変えるときは下の2つの定数だけ触ればよい。
    ///
    /// **クリックを吸わない。** 全画面に重ねるので、`raycastTarget` を切らないと
    /// 裏のメニューが永久に押せなくなる。
    /// </summary>
    public sealed class RoomAtmosphere : MonoBehaviour
    {
        /// <summary>四隅の暗さ。0.45 で「少し落ちた」程度。</summary>
        private const float VignetteAlpha = 0.45f;

        /// <summary>ざらつきの濃さ。**0.05 を超えるとドット絵が汚れる。**</summary>
        private const float GrainAlpha = 0.030f;

        /// <summary>粒子の焼き置き枚数。順に見せてざらつきを動かす。</summary>
        private const int GrainFrames = 8;

        /// <summary>粒子の切り替え速度[枚/秒]。速すぎると落ち着かない。</summary>
        private const float GrainFps = 11f;

        private Image _grain;
        private Sprite[] _grainSprites;
        private float _elapsed;

        /// <summary>
        /// 画面へ空気の層を足す。**呼ぶ場所で重なり順が決まる。**
        /// 部屋の中身を作ったあと、メニューを作る前に呼ぶこと。
        /// </summary>
        public static RoomAtmosphere Attach(Transform parent)
        {
            if (parent == null) return null;

            var go = new GameObject("RoomAtmosphere", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);

            var atmosphere = go.AddComponent<RoomAtmosphere>();
            atmosphere.Build(rt);
            return atmosphere;
        }

        private void Build(RectTransform rt)
        {
            AddLayer(rt, "Vignette", BakeVignette(), new Color(0f, 0f, 0f, VignetteAlpha),
                     Image.Type.Simple);

            // **粒子は引き伸ばさず、敷き詰める（2026-09-17 に直した）。**
            // 64px の絵を画面いっぱいへ広げたら、1粒が 12x9px の塊になって
            // ドット絵が泥のように汚れた。等倍で並べれば、粒が1画素になる。
            _grainSprites = BakeGrain();
            _grain = AddLayer(rt, "Grain", _grainSprites[0],
                              new Color(1f, 1f, 1f, GrainAlpha), Image.Type.Tiled);

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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>ざらつきを何枚か焼く。毎フレーム作ると重いので、順に見せて回す。</summary>
        private static Sprite[] BakeGrain()
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
                    // 白と黒を混ぜる。**中間を多めに**しないとチカチカする
                    float v = (float)rng.NextDouble();
                    v = 0.5f + (v - 0.5f) * 0.9f;
                    px[i] = new Color(v, v, v, 1f);
                }
                tex.SetPixels(px);
                tex.Apply();
                // **1画素を1単位で作る。** ここを 100 にすると、
                // 敷き詰めたとき 100分の1 に縮んで模様が消える
                sprites[f] = Sprite.Create(tex, new Rect(0, 0, size, size),
                                           new Vector2(0.5f, 0.5f), 1f);
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
