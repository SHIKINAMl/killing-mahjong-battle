using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 負けた側の全身から、血が霧になって一気に噴き出し、霧散する演出（2026-10-19、**没案**）。
    ///
    /// ユーザーから伝えられたイメージ:「全身の毛細血管からドパって霧状に抜かれてる」。
    /// ただし「ボツアンになる可能性が高い」ので、本編には使わず、
    /// 演出コレクションの没案（ron.mist_local / ron.mist_enemy）でだけ流す。
    /// 本編の精算は <see cref="BloodStreamEffect"/>（ホースを通る筋）のまま。
    ///
    /// **作り方はフォーラム「ドット絵」に貼られた参考（2026-10-16、粒子を点滅させながら消す）に合わせた**
    /// （同日の指示「もっと霧が霧散している感じで」「これのドット絵の作り方を参考に」）:
    ///   ・半透明は使わない。色は数色だけで、消えるときは**点滅させながら消す**
    ///   ・粒は最初が大きなかたまりで、時間とともに 3→2→1 マスへ縮み、縮むときに小さな粒へ割れる
    ///   ・色は明るい→濃い→暗い、と年をとるほど沈む
    ///   ・ところどころ「＋」の形のきらめきを混ぜる
    /// 最初の版は半透明の点を体のまわりに密に並べたので、もやの塊に見えて霧散しなかった。
    /// 今は大半の粒が広がって点滅しながら消え、一部の大きな粒だけが卓の血だまりへ吸い寄せられる。
    ///
    /// 位置はすべて**ワールド座標（Overlay ならスクリーン座標）**。
    /// </summary>
    public sealed class BloodMistEffect : MonoBehaviour
    {
        private static Sprite _dot, _plus;
        private static Sprite Dot => _dot != null ? _dot : (_dot = MakeSprite("BloodMistDot", new[] { "#" }));
        /// <summary>「＋」のきらめき。3x3 の真ん中の十字。</summary>
        private static Sprite Plus => _plus != null ? _plus : (_plus = MakeSprite("BloodMistPlus", new[] { ".#.", "###", ".#." }));

        private static Sprite MakeSprite(string name, string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, h - 1 - y, rows[y][x] == '#' ? Color.white : Color.clear);
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            sp.name = name;
            return sp;
        }

        // 明るい順。いちばん明るい色は噴いた直後の芯だけに使う
        private static readonly Color Hot = new Color32(255, 150, 140, 255);
        private static readonly Color Bright = new Color32(232, 44, 40, 255);
        private static readonly Color Body = new Color32(170, 14, 34, 255);
        private static readonly Color Dark = new Color32(104, 6, 20, 255);

        /// <summary>ドットの1辺（800x600 基準の px）。</summary>
        private const float Grid = 4f;
        /// <summary>噴き出す時間[秒]。この間に全部の粒が出る（「ドパッ」）。</summary>
        private const float BurstSeconds = 0.12f;
        /// <summary>噴き出してから漂う時間[秒]。このあと大きな粒だけ卓へ吸い寄せられる。</summary>
        private const float HangSeconds = 0.36f;
        /// <summary>点滅の速さ[回/秒]。ドット絵のコマ送りに合わせて速すぎない値。</summary>
        private const float BlinkRate = 15f;
        /// <summary>寿命のこの割合を過ぎたら点滅させながら消す。</summary>
        private const float BlinkFrom = 0.55f;
        /// <summary>吸い寄せが終わらなくても、ここで打ち切る[秒]。</summary>
        private const float MaxSeconds = 2.0f;

        private struct Mote
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Born, Life, Pull;
            public int Size, Size0;   // マス数
            public int Phase;          // 点滅のずれ
            public bool Pulled, Sparkle, Split, Alive;
        }

        private readonly List<Mote> motes = new List<Mote>();
        private readonly List<Mote> born = new List<Mote>();
        private RectTransform stage;
        private Vector2 target;
        private float scale, elapsed;
        private bool running;

        public bool IsRunning => running;

        /// <summary>
        /// <paramref name="sources"/> の各点から霧を噴き出させる。大きな粒は <paramref name="targetWorld"/> へ吸い寄せる。
        /// <paramref name="center"/> は体の真ん中（噴き出す向きを決める）。
        /// <paramref name="inward"/> なら真ん中へ向かって噴く（画面の縁から噴かせる、一人称の自分の体）。
        /// </summary>
        public static BloodMistEffect Burst(RectTransform stage, IList<Vector2> sources, Vector2 center,
                                            Vector3 targetWorld, float amount = 1f, bool inward = false)
        {
            if (stage == null || sources == null || sources.Count == 0) return null;
            var go = new GameObject("BloodMist", typeof(RectTransform));
            go.transform.SetParent(stage, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var fx = go.AddComponent<BloodMistEffect>();
            fx.stage = rt;
            var canvas = stage.GetComponentInParent<Canvas>();
            fx.scale = canvas != null ? Mathf.Max(0.25f, canvas.rootCanvas.transform.lossyScale.x) : 1f;
            fx.target = targetWorld;
            fx.Spawn(sources, center, Mathf.Clamp(amount, 0.4f, 2f), inward);
            fx.running = true;
            return fx;
        }

        public IEnumerator WaitDone()
        {
            while (this != null && running) yield return null;
        }

        private void Spawn(IList<Vector2> sources, Vector2 center, float amount, bool inward)
        {
            int count = Mathf.RoundToInt(320f * amount);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = sources[Random.Range(0, sources.Count)];
                Vector2 dir = p - center;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Random.insideUnitCircle.normalized;
                if (inward) dir = -dir;
                dir = (dir + Random.insideUnitCircle * 0.8f).normalized;

                float roll = Random.value;
                int size = roll < 0.25f ? 3 : roll < 0.6f ? 2 : 1;
                bool sparkle = size == 1 && Random.value < 0.15f;
                motes.Add(NewMote(p, dir * Random.Range(120f, 380f) * (inward ? 1.6f : 1f) * scale,
                                  Random.Range(0f, BurstSeconds), size, sparkle,
                                  // 大きなかたまりの半分ほどが卓へ向かう。残りは広がって消える
                                  pulled: size == 3 && Random.value < 0.6f,
                                  life: Random.Range(0.55f, 1.15f)));
            }
        }

        private Mote NewMote(Vector2 pos, Vector2 vel, float bornAt, int size, bool sparkle, bool pulled, float life)
        {
            var go = new GameObject(sparkle ? "Glint" : "Mote", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(stage, false);
            var img = go.GetComponent<Image>();
            img.sprite = sparkle ? Plus : Dot;
            img.raycastTarget = false;
            go.SetActive(false);
            return new Mote
            {
                Img = img, Pos = pos, Vel = vel, Born = bornAt, Life = life,
                Pull = Random.Range(0f, 0.2f), Size = size, Size0 = size,
                Phase = Random.Range(0, 2), Pulled = pulled, Sparkle = sparkle, Alive = true,
            };
        }

        private Vector2 Snap(Vector2 p)
        {
            float g = Grid * scale;
            return new Vector2(Mathf.Round(p.x / g) * g, Mathf.Round(p.y / g) * g);
        }

        private void Update()
        {
            if (!running) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            int alive = 0;
            int frame = Mathf.FloorToInt(elapsed * BlinkRate);

            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                if (!m.Alive) continue;
                if (elapsed < m.Born) { alive++; continue; }

                float age = elapsed - m.Born;
                float f = age / m.Life;
                float pullStart = BurstSeconds + HangSeconds + m.Pull;
                bool pulling = m.Pulled && elapsed >= pullStart;

                if (!pulling)
                {
                    // 噴いた勢いがすぐ抜けて、ふわっと広がる。霧なので少しだけ浮く
                    m.Vel *= Mathf.Exp(-4.5f * dt);
                    m.Vel += (Vector2.up * 18f + Random.insideUnitCircle * 90f) * scale * dt;
                }
                else
                {
                    // 卓の血だまりへ吸い寄せられる。近いほど速い
                    Vector2 to = target - m.Pos;
                    float k = Mathf.Clamp01((elapsed - pullStart) / 0.35f);
                    m.Vel = Vector2.Lerp(m.Vel, to.normalized * Mathf.Lerp(200f, 900f, k) * scale, 1f - Mathf.Exp(-9f * dt));
                    float dist = to.magnitude;
                    if (dist < Grid * 3f * scale || Vector2.Dot(to, m.Vel) < 0f && dist < Grid * 8f * scale) { Kill(ref m); motes[i] = m; continue; }
                }
                m.Pos += m.Vel * dt;

                if (!m.Pulled)
                {
                    if (f >= 1f) { Kill(ref m); motes[i] = m; continue; }
                    // 年をとるほど小さく。3マスのかたまりは縮むときに小さな粒へ割れる
                    int size = f < 0.3f ? m.Size0 : f < 0.6f ? Mathf.Max(1, m.Size0 - 1) : 1;
                    if (size < m.Size && m.Size0 == 3 && !m.Split)
                    {
                        m.Split = true;
                        for (int c = 0; c < 2; c++)
                            born.Add(NewMote(m.Pos, m.Vel * 0.6f + Random.insideUnitCircle * 140f * scale, elapsed, 1,
                                             Random.value < 0.2f, false, Random.Range(0.3f, 0.6f)));
                    }
                    m.Size = size;
                }
                else if (pulling)
                {
                    m.Size = Mathf.Max(1, m.Size0 - 1);
                }

                // 色は数色だけ。明るい芯 → 明るい → 濃い → 暗い
                Color col = m.Sparkle ? (f < 0.5f ? Hot : Bright)
                          : age < 0.06f ? Hot : f < 0.25f ? Bright : f < 0.6f ? Body : Dark;
                if (m.Pulled) col = age < 0.06f ? Hot : pulling ? Body : Bright;

                // 消えぎわは点滅させながら消す（半透明にはしない）
                bool shown = m.Pulled || f < BlinkFrom || ((frame + m.Phase) & 1) == 0;
                if (m.Img.gameObject.activeSelf != shown) m.Img.gameObject.SetActive(shown);
                if (shown)
                {
                    float px = m.Sparkle ? 3f * Grid : m.Size * Grid;
                    m.Img.rectTransform.sizeDelta = new Vector2(px, px);
                    m.Img.rectTransform.position = Snap(m.Pos);
                    if (m.Img.color != col) m.Img.color = col;
                }

                motes[i] = m;
                alive++;
            }

            if (born.Count > 0) { motes.AddRange(born); alive += born.Count; born.Clear(); }

            if (alive == 0 || elapsed > MaxSeconds)
            {
                running = false;
                Destroy(gameObject);
            }
        }

        private static void Kill(ref Mote m)
        {
            if (m.Img != null) Destroy(m.Img.gameObject);
            m.Alive = false;
        }
    }
}
