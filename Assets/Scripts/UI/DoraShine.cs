using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ドラ牌に「キラーン」と光る演出を足す。
    ///
    /// 専用の画像アセットは使わず、4方向に伸びる star（十字＋対角の淡い光）を
    /// 実行時にテクスチャとして生成する。ドット感を保つため Point フィルタで拡大する。
    ///
    /// 牌はプールで使い回されるので、状態は必ず SetActive で入り切りできるようにしてある。
    /// TileVisual.SetTile から isDora に応じて Enable/Disable される。
    /// </summary>
    public class DoraShine : MonoBehaviour
    {
        private static Sprite _starSprite;

        /// <summary>中心が最も明るく、十字方向へ伸びる光。</summary>
        private static Sprite StarSprite
        {
            get
            {
                if (_starSprite != null) return _starSprite;

                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;   // ドット感を残す
                tex.wrapMode = TextureWrapMode.Clamp;

                float c = (n - 1) * 0.5f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = Mathf.Abs(x - c) / c;
                        float dy = Mathf.Abs(y - c) / c;

                        // 縦棒と横棒。中心から離れるほど細く暗く
                        float bar = Mathf.Max(
                            Mathf.Clamp01(1f - dy * 6f) * Mathf.Clamp01(1f - dx),
                            Mathf.Clamp01(1f - dx * 6f) * Mathf.Clamp01(1f - dy));
                        // 中心の芯
                        float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.6f);

                        float a = Mathf.Clamp01(bar + core);
                        a = Mathf.Round(a * 4f) / 4f;   // 4段に量子化してレトロに
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                _starSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f);
                _starSprite.name = "DoraShineStar";
                return _starSprite;
            }
        }

        [Header("光り方")]
        [Tooltip("1回のきらめきにかかる時間")]
        [SerializeField] private float flashDuration = 0.42f;
        [Tooltip("次に光るまでの間隔（この値を中心にばらつく）")]
        [SerializeField] private float interval = 2.6f;
        [Tooltip("光の大きさ。牌の短辺に対する倍率")]
        [SerializeField] private float sizeRatio = 0.95f;
        [Tooltip("光の色。金色寄りにするとドラらしくなる")]
        [SerializeField] private Color shineColor = new Color32(255, 236, 150, 255);

        private RectTransform _star;
        private Image _img;
        private Coroutine _loop;

        private void OnEnable()
        {
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

            // 牌の大きさに合わせる。取れないときは控えめな固定値
            var host = transform as RectTransform;
            float shortSide = 40f;
            if (host != null && host.rect.width > 1f && host.rect.height > 1f)
                shortSide = Mathf.Min(host.rect.width, host.rect.height);
            float s = shortSide * sizeRatio;
            _star.sizeDelta = new Vector2(s, s);
            _star.anchoredPosition = Vector2.zero;

            _img = go.AddComponent<Image>();
            _img.sprite = StarSprite;
            _img.color = new Color(shineColor.r, shineColor.g, shineColor.b, 0f);
            _img.raycastTarget = false;

            go.SetActive(false);
        }

        private IEnumerator Loop()
        {
            // 牌ごとに位相をずらして、一斉に光らないようにする
            yield return new WaitForSeconds(Random.Range(0f, interval));

            while (true)
            {
                yield return Flash();
                yield return new WaitForSeconds(interval * Random.Range(0.75f, 1.35f));
            }
        }

        private IEnumerator Flash()
        {
            if (_star == null) yield break;
            _star.gameObject.SetActive(true);

            // 牌の中で少しだけ位置をばらつかせる
            var host = transform as RectTransform;
            float jx = 0f, jy = 0f;
            if (host != null)
            {
                jx = Random.Range(-host.rect.width, host.rect.width) * 0.14f;
                jy = Random.Range(-host.rect.height, host.rect.height) * 0.14f;
            }
            _star.anchoredPosition = new Vector2(jx, jy);

            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / flashDuration);

                // 出て消えるカーブ。立ち上がりを速く、余韻を残す
                float a = p < 0.25f ? (p / 0.25f) : (1f - (p - 0.25f) / 0.75f);
                a = Mathf.Clamp01(a);

                // 大きさは小さく出て、伸びてから縮む
                float scale = Mathf.Sin(p * Mathf.PI) * 0.75f + 0.35f;

                _star.localScale = new Vector3(scale, scale, 1f);
                _star.localRotation = Quaternion.Euler(0f, 0f, p * 45f);
                var c = shineColor; c.a = a;
                _img.color = c;

                yield return null;
            }

            _img.color = new Color(shineColor.r, shineColor.g, shineColor.b, 0f);
            _star.gameObject.SetActive(false);
        }
    }
}
