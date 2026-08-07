using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ドラ牌に「キラーン」と光る演出を足す。
    ///
    /// 専用の画像アセットは使わず、十字＋対角に伸びる星を実行時にテクスチャとして生成する。
    /// ドット感を保つため Point フィルタで拡大し、明るさは段に量子化している。
    ///
    /// 牌はプールで使い回されるので、状態は必ず SetActive で入り切りできるようにしてある。
    /// TileVisual.SetTile から isDora に応じて Enable/Disable される。
    ///
    /// このコンポーネントはシーンに置かれておらず TileVisual が AddComponent するだけなので、
    /// 下の SerializeField の既定値がそのまま効く。見た目を変えるときはここを触る。
    /// </summary>
    public class DoraShine : MonoBehaviour
    {
        // 芯と外側で色が違うので、色の組み合わせごとにテクスチャを作って使い回す。
        // 既定値のままなら実際には1枚しか作られない。
        private static readonly Dictionary<(Color, Color), Sprite> _starSprites =
            new Dictionary<(Color, Color), Sprite>();

        /// <summary>
        /// 1本のトゲ。太さも明るさも先端へ向かって細く暗くする。
        /// 太さを一定のまま伸ばすと、先端が四角い塊に見えてしまう。
        /// </summary>
        /// <param name="t">トゲの軸方向の距離（0〜1に正規化）</param>
        /// <param name="perp">軸からの距離（0〜1に正規化）</param>
        private static float Needle(float t, float perp, float length)
        {
            if (t >= length) return 0f;
            float u = t / length;
            // 先端でも1ドットは残るよう下限を設ける
            float halfWidth = Mathf.Max(0.16f * Mathf.Pow(1f - u, 0.8f), 0.045f);
            return Mathf.Clamp01(1f - perp / halfWidth) * Mathf.Pow(1f - u, 0.5f);
        }

        /// <summary>中心が白く熱を持ち、十字と対角へ伸びる光。</summary>
        private static Sprite GetStarSprite(Color coreColor, Color edgeColor)
        {
            var key = (coreColor, edgeColor);
            if (_starSprites.TryGetValue(key, out var cached) && cached != null) return cached;

            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;   // ドット感を残す
            tex.wrapMode = TextureWrapMode.Clamp;

            const float invSqrt2 = 0.70710678f;
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    // 中心を原点にした -1〜1 の座標
                    float sx = (x - c) / c;
                    float sy = (y - c) / c;
                    float dx = Mathf.Abs(sx);
                    float dy = Mathf.Abs(sy);
                    float r = Mathf.Sqrt(sx * sx + sy * sy);

                    // 十字
                    float cross = Mathf.Max(Needle(dx, dy, 1f), Needle(dy, dx, 1f));

                    // 対角。45度回した座標で同じことをする。短く淡くして密度だけ上げる
                    float du = Mathf.Abs(sx + sy) * invSqrt2;
                    float dv = Mathf.Abs(sx - sy) * invSqrt2;
                    float diag = Mathf.Max(Needle(du, dv, 0.55f), Needle(dv, du, 0.55f)) * 0.6f;

                    // 中心の芯
                    float core = Mathf.Clamp01(1f - r * 3.6f);

                    float a = Mathf.Clamp01(Mathf.Max(cross, diag) + core);
                    a = Mathf.Round(a * 5f) / 5f;   // 5段に量子化してレトロに

                    // 芯へ寄るほど白く、外へ向かうほど金色にする。
                    // 明度差そのものを絵に持たせておくと、下地の牌が明るくても埋もれない。
                    float coreness = Mathf.Clamp01(1f - r * 2.8f);
                    coreness *= coreness;
                    Color rgb = Color.Lerp(edgeColor, coreColor, coreness);

                    tex.SetPixel(x, y, new Color(rgb.r, rgb.g, rgb.b, a));
                }
            }
            tex.Apply();

            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
            sprite.name = "DoraShineStar";
            _starSprites[key] = sprite;
            return sprite;
        }

        [Header("光り方")]
        [Tooltip("1回のきらめきにかかる時間")]
        [SerializeField] private float flashDuration = 0.55f;
        [Tooltip("次に光るまでの間隔（この値を中心にばらつく）")]
        [SerializeField] private float interval = 1.6f;
        [Tooltip("光の大きさ。牌の短辺に対する倍率。1を超えると牌からはみ出す。" +
                 "上げすぎるとトゲが隣の牌に届いて、どの牌がドラか分かりにくくなる")]
        [SerializeField] private float sizeRatio = 1.4f;

        [Header("色")]
        [Tooltip("光の芯の色。白いほど強く見える")]
        [SerializeField] private Color coreColor = new Color32(255, 255, 255, 255);
        [Tooltip("光の外側の色。金色寄りにするとドラらしくなる")]
        [SerializeField] private Color edgeColor = new Color32(255, 205, 60, 255);
        [Tooltip("全体にかける色。既定は白（＝上の2色をそのまま出す）")]
        [SerializeField] private Color tint = Color.white;

        private RectTransform _star;
        private Image _img;
        private Coroutine _loop;
        private float _baseAngle;

        private void OnEnable()
        {
            // この演出は Canvas 配下の Image としてしか描けない。
            // 牌のプレハブには SpriteRenderer 版（イーピン.prefab）もあり、そちらは
            // RectTransform を持っているのに Canvas 配下ではないので、型だけ見ても弾けない。
            // 中途半端に見えない子を作ると原因が分かりにくいので、黙って自分を止める。
            if (!(transform is RectTransform) || GetComponentInParent<Canvas>() == null)
            {
                enabled = false;
                return;
            }

            EnsureStar();
            if (_loop == null) _loop = StartCoroutine(Loop());
        }

        private void OnDisable()
        {
            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            if (_star != null) _star.gameObject.SetActive(false);
        }

        private void EnsureStar()
        {
            if (_star != null) return;

            var go = new GameObject("DoraShineStar", typeof(RectTransform));
            _star = go.GetComponent<RectTransform>();
            _star.SetParent(transform, false);
            _star.anchorMin = _star.anchorMax = new Vector2(0.5f, 0.5f);
            _star.pivot = new Vector2(0.5f, 0.5f);
            _star.anchoredPosition = Vector2.zero;

            // 牌側にレイアウトが付いていても位置と大きさを奪われないようにする
            var ignore = go.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            _img = go.AddComponent<Image>();
            _img.sprite = GetStarSprite(coreColor, edgeColor);
            _img.color = new Color(tint.r, tint.g, tint.b, 0f);
            _img.raycastTarget = false;

            _baseAngle = Random.Range(0f, 90f);

            go.SetActive(false);
        }

        /// <summary>
        /// 牌の実寸に合わせて光の大きさを決める。
        /// OnEnable の時点ではレイアウトが未確定で rect が 0 のことがあるため、
        /// 光らせる直前に測り直す。
        ///
        /// 測れなければ false を返して、その回は光らせない。
        /// 固定値でごまかすと、牌が UI（短辺 40px）かワールド空間（短辺 0.63）かで
        /// 桁が変わってしまい、大きさが牌と合わなくなる。
        /// </summary>
        private bool ApplySize()
        {
            if (!(transform is RectTransform host)) return false;

            Rect r = host.rect;
            float shortSide = Mathf.Min(r.width, r.height);
            if (shortSide <= Mathf.Epsilon) return false;

            float s = shortSide * sizeRatio;
            _star.sizeDelta = new Vector2(s, s);
            return true;
        }

        private IEnumerator Loop()
        {
            // 牌ごとに位相をずらして、一斉に光らないようにする
            yield return new WaitForSeconds(Random.Range(0f, interval));

            while (true)
            {
                yield return Flash();
                yield return new WaitForSeconds(interval * Random.Range(0.7f, 1.3f));
            }
        }

        private IEnumerator Flash()
        {
            if (_star == null) yield break;
            if (!ApplySize()) yield break;   // レイアウト待ち。次の周回で出し直す

            _star.gameObject.SetActive(true);
            // 牌に後から足された子（オーバーレイ等）より手前に出す
            _star.SetAsLastSibling();

            // 牌の中で少しだけ位置をばらつかせる
            if (transform is RectTransform host)
            {
                _star.anchoredPosition = new Vector2(
                    Random.Range(-host.rect.width, host.rect.width) * 0.10f,
                    Random.Range(-host.rect.height, host.rect.height) * 0.10f);
            }

            const float attack = 0.15f;   // 立ち上がりに使う割合
            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / flashDuration);

                // 一気に出て、速く落ちてから尾を引く
                float a = p < attack
                    ? p / attack
                    : 1f - Mathf.Pow((p - attack) / (1f - attack), 1.6f);
                a = Mathf.Clamp01(a);

                // 小さく出て、伸びてから少し縮む
                float scale = 0.7f + Mathf.Sin(p * Mathf.PI) * 0.55f;

                _star.localScale = new Vector3(scale, scale, 1f);
                _star.localRotation = Quaternion.Euler(0f, 0f, _baseAngle + p * 30f);
                _img.color = new Color(tint.r, tint.g, tint.b, tint.a * a);

                yield return null;
            }

            _img.color = new Color(tint.r, tint.g, tint.b, 0f);
            _star.gameObject.SetActive(false);
        }
    }
}
