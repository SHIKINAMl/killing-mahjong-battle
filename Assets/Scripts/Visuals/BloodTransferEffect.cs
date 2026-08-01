using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.Visuals
{
    /// <summary>
    /// 決着時のHP増減を「血が敗者から勝者へ移る」形で見せる演出。
    ///
    /// ドット絵の血が敗者側から勝者側へ流れ、その間だけ画面の外周が赤く縁取られる。
    /// PixelBloodEffect と同じく専用の画像アセットは使わず、
    /// 1x1 の白テクスチャと実行時生成のグラデーションだけで描く。
    ///
    /// 使い方:
    ///   BloodTransferEffect.Play(親RectTransform, 敗者側の位置, 勝者側の位置, 時間);
    /// </summary>
    public class BloodTransferEffect : MonoBehaviour
    {
        private static Sprite _dotSprite;
        private static Sprite _edgeSprite;

        /// <summary>血のドット用。にじませないよう Point フィルタの 1x1。</summary>
        private static Sprite DotSprite
        {
            get
            {
                if (_dotSprite == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    _dotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _dotSprite.name = "BloodTransferDot";
                }
                return _dotSprite;
            }
        }

        /// <summary>
        /// 画面外周を赤く縁取るための板。中心が透明で、縁に近いほど不透明になる。
        /// 粗い解像度＋Pointフィルタで、あえてドットの段が見えるようにしている。
        /// </summary>
        private static Sprite EdgeSprite
        {
            get
            {
                if (_edgeSprite == null)
                {
                    const int w = 64, h = 48;
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Point;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            // 端からの距離を 0..1 で。0 が画面の縁
                            float dx = Mathf.Min(x, w - 1 - x) / (w * 0.5f);
                            float dy = Mathf.Min(y, h - 1 - y) / (h * 0.5f);
                            float d = Mathf.Min(dx, dy);
                            // 縁から 35% までを使って落とす。外周だけ濃くする
                            float a = Mathf.Clamp01(1f - (d / 0.35f));
                            a = a * a; // 中心側をより速く抜く
                            // 4段階に量子化してレトロなバンドにする
                            a = Mathf.Round(a * 4f) / 4f;
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                        }
                    }
                    tex.Apply();
                    _edgeSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
                    _edgeSprite.name = "BloodEdgeVignette";
                }
                return _edgeSprite;
            }
        }

        private static readonly Color[] Palette =
        {
            new Color32(214, 36, 17, 255),
            new Color32(178, 24, 14, 255),
            new Color32(130, 14, 12, 255),
        };

        private class Drop
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 From;
            public Vector2 To;
            public float T;        // 0..1 の進捗
            public float Speed;
            public float Swing;    // 横揺れの振幅
            public float Phase;
            public Color Color;
        }

        private readonly List<Drop> _drops = new List<Drop>();
        private float _grid = 6f;

        /// <param name="parent">UI の親。この座標系で位置を指定する</param>
        /// <param name="from">敗者側（血が出る方）</param>
        /// <param name="to">勝者側（血が向かう方）</param>
        /// <param name="duration">流し続ける時間。HPカウントと合わせる</param>
        /// <param name="gridSize">量子化するグリッド幅(px)</param>
        public static BloodTransferEffect Play(RectTransform parent, Vector2 from, Vector2 to,
                                               float duration, float gridSize = 6f)
        {
            if (parent == null) return null;

            var go = new GameObject("BloodTransferEffect", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var fx = go.AddComponent<BloodTransferEffect>();
            fx._grid = Mathf.Max(1f, gridSize);
            fx.StartCoroutine(fx.Run(from, to, Mathf.Max(0.2f, duration)));
            return fx;
        }

        private float Snap(float v) => Mathf.Round(v / _grid) * _grid;

        private RectTransform CreateQuad(Transform parent, float w, float h, Color c, Sprite sprite)
        {
            var go = new GameObject("q", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        private IEnumerator Run(Vector2 from, Vector2 to, float duration)
        {
            // --- 画面外周の赤い縁取り ---
            // 親のさらに上（ルートCanvas）いっぱいに広げる
            var edge = CreateQuad(transform, 10f, 10f, new Color(1f, 0f, 0f, 0f), EdgeSprite);
            edge.anchorMin = Vector2.zero;
            edge.anchorMax = Vector2.one;
            edge.offsetMin = Vector2.zero;
            edge.offsetMax = Vector2.zero;
            edge.SetAsFirstSibling();
            var edgeImg = edge.GetComponent<Image>();

            float emitTimer = 0f;
            float t = 0f;

            while (t < duration)
            {
                float dt = Time.deltaTime;
                t += dt;
                float k = t / duration;

                // --- 縁の明滅。段階的に変えてレトロに見せる ---
                float pulse = 0.55f + 0.45f * Mathf.Sin(t * 9f);
                float envelope = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI); // 出て消える
                float ea = Mathf.Round(pulse * envelope * 4f) / 4f;
                edgeImg.color = new Color(0.85f, 0.05f, 0.05f, ea);

                // --- 血の粒を出し続ける ---
                emitTimer -= dt;
                if (emitTimer <= 0f && k < 0.85f)
                {
                    emitTimer = 0.035f;
                    int burst = Random.Range(2, 5);
                    for (int i = 0; i < burst; i++)
                    {
                        float size = _grid * Random.Range(1, 4);
                        var color = Palette[Random.Range(0, Palette.Length)];
                        var rt = CreateQuad(transform, size, size, color, DotSprite);
                        var d = new Drop
                        {
                            Rt = rt,
                            Img = rt.GetComponent<Image>(),
                            From = from + new Vector2(Random.Range(-90f, 90f), Random.Range(-30f, 30f)),
                            To = to + new Vector2(Random.Range(-90f, 90f), Random.Range(-30f, 30f)),
                            T = 0f,
                            Speed = Random.Range(0.9f, 1.8f),
                            Swing = Random.Range(-40f, 40f),
                            Phase = Random.Range(0f, Mathf.PI * 2f),
                            Color = color,
                        };
                        rt.anchoredPosition = new Vector2(Snap(d.From.x), Snap(d.From.y));
                        _drops.Add(d);
                    }
                }

                // --- 粒を敗者側から勝者側へ運ぶ ---
                for (int i = _drops.Count - 1; i >= 0; i--)
                {
                    var d = _drops[i];
                    if (d.Rt == null) { _drops.RemoveAt(i); continue; }

                    d.T += dt * d.Speed;
                    if (d.T >= 1f)
                    {
                        Destroy(d.Rt.gameObject);
                        _drops.RemoveAt(i);
                        continue;
                    }

                    // 直線だと機械的なので、進行方向に対して横へ揺らす
                    Vector2 pos = Vector2.Lerp(d.From, d.To, d.T);
                    Vector2 dir = (d.To - d.From).normalized;
                    Vector2 side = new Vector2(-dir.y, dir.x);
                    pos += side * (Mathf.Sin(d.T * Mathf.PI * 2f + d.Phase) * d.Swing * (1f - d.T));

                    d.Rt.anchoredPosition = new Vector2(Snap(pos.x), Snap(pos.y));

                    // 到達間際で段階的に薄くする
                    float a = d.T < 0.75f ? 1f : Mathf.Round((1f - (d.T - 0.75f) / 0.25f) * 4f) / 4f;
                    var c = d.Color; c.a = a;
                    d.Img.color = c;
                }

                yield return null;
            }

            // 残った粒を掃く
            foreach (var d in _drops) if (d.Rt != null) Destroy(d.Rt.gameObject);
            _drops.Clear();

            Destroy(gameObject);
        }
    }
}
