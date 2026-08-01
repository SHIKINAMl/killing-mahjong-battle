using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.Visuals
{
    /// <summary>
    /// ドット絵風の血しぶき。
    ///
    /// 専用の画像アセットは使わず、1x1 の白テクスチャを四角いドットとして飛ばす。
    /// レトロ感は次の3つで出している:
    ///   1. 座標をグリッドへ量子化する（なめらかに動かさず、カクッと飛ばす）
    ///   2. 色を数色のパレットに限定する
    ///   3. 透明度を段階的に落とす（じわっと消さない）
    ///
    /// 使い方: PixelBloodEffect.Play(親RectTransform, 出したい位置);
    /// 呼び出し側でインスタンスを管理する必要はない。演出が終わると自分で消える。
    /// </summary>
    public class PixelBloodEffect : MonoBehaviour
    {
        // 1x1 の白スプライトは全エフェクトで使い回す
        private static Sprite _dotSprite;

        private static Sprite DotSprite
        {
            get
            {
                if (_dotSprite == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.filterMode = FilterMode.Point; // にじませない
                    tex.Apply();
                    _dotSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _dotSprite.name = "PixelBloodDot";
                }
                return _dotSprite;
            }
        }

        // 明るい順。飛び散った直後は明るく、乾くほど暗い色へ寄せる
        private static readonly Color[] Palette =
        {
            new Color32(214, 36, 17, 255),
            new Color32(160, 20, 12, 255),
            new Color32(105, 10, 10, 255),
            new Color32(60, 6, 6, 255),
        };

        private class Dot
        {
            public RectTransform Rt;
            public Image Img;
            public Vector2 Pos;      // 量子化前の実座標
            public Vector2 Vel;
            public Color Color;
            public float TrailTimer;
            public bool Landed;
            public float LandedAt;
        }

        private readonly List<Dot> _dots = new List<Dot>();
        private readonly List<RectTransform> _stains = new List<RectTransform>();

        private float _grid = 6f;
        private float _gravity = 1500f;
        private float _life = 2.2f;
        private int _maxStains = 180;

        /// <summary>
        /// 血しぶきを再生する。
        /// </summary>
        /// <param name="parent">UI の親。ここの座標系で anchoredPosition を扱う</param>
        /// <param name="anchoredPos">飛び散りの原点</param>
        /// <param name="dotCount">飛ばすドットの数</param>
        /// <param name="gridSize">量子化するグリッド幅(px)。大きいほど粗くレトロになる</param>
        public static PixelBloodEffect Play(RectTransform parent, Vector2 anchoredPos,
                                            int dotCount = 70, float gridSize = 6f)
        {
            if (parent == null) return null;

            var go = new GameObject("PixelBloodEffect", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var fx = go.AddComponent<PixelBloodEffect>();
            fx._grid = Mathf.Max(1f, gridSize);
            fx.StartCoroutine(fx.Run(anchoredPos, Mathf.Max(1, dotCount)));
            return fx;
        }

        private float Snap(float v) => Mathf.Round(v / _grid) * _grid;

        private RectTransform CreateQuad(float size, Color c)
        {
            var go = new GameObject("dot", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);

            var img = go.AddComponent<Image>();
            img.sprite = DotSprite;
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        private IEnumerator Run(Vector2 origin, int dotCount)
        {
            // --- 1. 飛び散り ---
            for (int i = 0; i < dotCount; i++)
            {
                // 上向きを中心に扇状へ。真横〜斜め上に散らす
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float speed = Random.Range(180f, 780f);
                var vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                // 左右どちらにも飛ぶように反転を混ぜる
                if (Random.value < 0.5f) vel.x = -vel.x;

                // サイズはグリッドの整数倍にしてドットらしさを保つ
                float size = _grid * Random.Range(1, 4);
                var color = Palette[Random.Range(0, Palette.Length)];

                var rt = CreateQuad(size, color);
                var dot = new Dot
                {
                    Rt = rt,
                    Img = rt.GetComponent<Image>(),
                    Pos = origin,
                    Vel = vel,
                    Color = color,
                };
                rt.anchoredPosition = new Vector2(Snap(origin.x), Snap(origin.y));
                _dots.Add(dot);
            }

            // --- 2. 落下と流れ ---
            float t = 0f;
            while (t < _life)
            {
                float dt = Time.deltaTime;
                t += dt;

                foreach (var d in _dots)
                {
                    if (d.Rt == null) continue;

                    if (!d.Landed)
                    {
                        d.Vel.y -= _gravity * dt;
                        d.Pos += d.Vel * dt;

                        // 垂れた跡を点々と残す（これが「流れている」感じになる）
                        d.TrailTimer -= dt;
                        if (d.TrailTimer <= 0f && _stains.Count < _maxStains)
                        {
                            d.TrailTimer = 0.04f;
                            var s = CreateQuad(_grid, d.Color * 0.75f);
                            s.anchoredPosition = new Vector2(Snap(d.Pos.x), Snap(d.Pos.y));
                            _stains.Add(s);
                        }

                        // 十分落ちたら止めて「垂れ」に変える
                        if (d.Pos.y < origin.y - 420f)
                        {
                            d.Landed = true;
                            d.LandedAt = t;
                        }
                    }
                    else
                    {
                        // 着いたあとはゆっくり下へ滲ませる
                        d.Pos.y -= 26f * dt;
                    }

                    d.Rt.anchoredPosition = new Vector2(Snap(d.Pos.x), Snap(d.Pos.y));
                }

                // --- 3. 段階的に薄くする（なめらかに消さない） ---
                float remain = 1f - (t / _life);
                float stepped = Mathf.Ceil(remain * 4f) / 4f; // 1, 0.75, 0.5, 0.25, 0
                foreach (var d in _dots)
                {
                    if (d.Img == null) continue;
                    var c = d.Color; c.a = stepped;
                    d.Img.color = c;
                }
                foreach (var s in _stains)
                {
                    if (s == null) continue;
                    var img = s.GetComponent<Image>();
                    if (img == null) continue;
                    var c = img.color; c.a = stepped * 0.85f;
                    img.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
