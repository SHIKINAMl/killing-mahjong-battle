using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 負けた側の全身から、血が霧になって一気に噴き出し、卓へ吸い寄せられる演出（2026-10-19、**没案**）。
    ///
    /// ユーザーから伝えられたイメージ:「全身の毛細血管からドパって霧状に抜かれてる」。
    /// ただし「ボツアンになる可能性が高い」ので、本編には使わず、
    /// 演出コレクションの没案（ron.mist_local / ron.mist_enemy）でだけ流す。
    /// 本編の精算は <see cref="BloodStreamEffect"/>（ホースを通る筋）のまま。
    ///
    /// 絵は使わない。<see cref="BloodStreamEffect"/> と同じく四角いドットを並べ、
    /// 霧らしさは透明度を数段に分けたドットと、大きな薄いドットで出す。
    /// 位置はすべて**ワールド座標（Overlay ならスクリーン座標）**。
    /// </summary>
    public sealed class BloodMistEffect : MonoBehaviour
    {
        private static Sprite _dot;
        private static Sprite Dot
        {
            get
            {
                if (_dot == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.filterMode = FilterMode.Point;
                    tex.Apply();
                    _dot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                    _dot.name = "BloodMistDot";
                }
                return _dot;
            }
        }

        private static readonly Color Bright = new Color32(232, 44, 40, 255);
        private static readonly Color Body = new Color32(170, 14, 34, 255);
        private static readonly Color Dark = new Color32(104, 6, 20, 255);

        /// <summary>ドットの1辺（800x600 基準の px）。</summary>
        private const float Grid = 4f;
        /// <summary>噴き出す時間[秒]。この間に全部の粒が出る（「ドパッ」）。</summary>
        private const float BurstSeconds = 0.14f;
        /// <summary>噴き出してから漂う時間[秒]。このあと卓へ吸い寄せられる。</summary>
        private const float HangSeconds = 0.38f;
        /// <summary>吸い寄せが終わらなくても、ここで打ち切る[秒]。</summary>
        private const float MaxSeconds = 1.9f;

        private struct Mote
        {
            public Image Img;
            public Vector2 Pos, Vel;
            public float Born, Pull;
            public float Alpha;
            public bool Alive;
        }

        private readonly List<Mote> motes = new List<Mote>();
        private RectTransform stage;
        private Vector2 target;
        private float scale, elapsed;
        private bool running;

        public bool IsRunning => running;

        /// <summary>
        /// <paramref name="sources"/> の各点から霧を噴き出させ、<paramref name="targetWorld"/> へ吸い寄せる。
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
            int count = Mathf.RoundToInt(900f * amount);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = sources[Random.Range(0, sources.Count)];
                p += Random.insideUnitCircle * Grid * 2f * scale;
                Vector2 dir = p - center;
                dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Random.insideUnitCircle.normalized;
                if (inward) dir = -dir;
                // 体から外へ、少しばらつかせて噴く
                dir = (dir + Random.insideUnitCircle * 0.6f).normalized;

                var go = new GameObject("Mote", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(stage, false);
                var img = go.GetComponent<Image>();
                img.sprite = Dot;
                img.raycastTarget = false;
                // 大きく薄い粒（霧のかたまり）と、小さく濃い粒（しぶき）
                float roll = Random.value;
                bool cloud = roll < 0.4f;
                float size = roll < 0.15f ? Grid * 3f : cloud ? Grid * 2f : Grid;
                ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
                float r = Random.value;
                Color c = cloud ? Body : r < 0.35f ? Bright : r < 0.8f ? Body : Dark;
                float alpha = roll < 0.15f ? 0.22f : cloud ? 0.35f : Random.value < 0.5f ? 1f : 0.6f;
                img.color = new Color(c.r, c.g, c.b, alpha);
                go.SetActive(false);

                motes.Add(new Mote
                {
                    Img = img,
                    Pos = p,
                    // 遠くへ飛ばさず、体のまわりに赤いもやがまとわりつく速さ
                    Vel = dir * Random.Range(70f, 300f) * (inward ? 1.8f : 1f) * scale,
                    Born = Random.Range(0f, BurstSeconds),
                    Pull = Random.Range(0f, 0.25f),
                    Alpha = alpha,
                    Alive = true,
                });
            }
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

            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                if (!m.Alive) continue;
                if (elapsed < m.Born) { alive++; continue; }
                if (!m.Img.gameObject.activeSelf) m.Img.gameObject.SetActive(true);

                float age = elapsed - m.Born;
                float pullStart = BurstSeconds + HangSeconds + m.Pull;
                if (elapsed < pullStart)
                {
                    // 噴いた勢いがすぐ抜けて、宙に漂う。霧なので少しだけ沈む
                    m.Vel *= Mathf.Exp(-7f * dt);
                    m.Vel += Vector2.down * 30f * scale * dt;
                    m.Vel += Random.insideUnitCircle * 60f * scale * dt;
                }
                else
                {
                    // 卓の血だまりへ吸い寄せられる。近いほど速い
                    Vector2 to = target - m.Pos;
                    float dist = to.magnitude;
                    float k = Mathf.Clamp01((elapsed - pullStart) / 0.35f);
                    Vector2 want = to.normalized * Mathf.Lerp(200f, 900f, k) * scale;
                    m.Vel = Vector2.Lerp(m.Vel, want, 1f - Mathf.Exp(-9f * dt));
                    if (dist < Grid * 3f * scale || Vector2.Dot(to, m.Vel) < 0f && dist < Grid * 8f * scale)
                    {
                        Destroy(m.Img.gameObject);
                        m.Alive = false;
                        motes[i] = m;
                        continue;
                    }
                }
                m.Pos += m.Vel * dt;
                m.Img.rectTransform.position = Snap(m.Pos);

                // 透明度は段で落とす（なめらかにしない）
                float a = m.Alpha * (age > 0.9f ? 0.5f : 1f);
                var col = m.Img.color;
                if (!Mathf.Approximately(col.a, a)) m.Img.color = new Color(col.r, col.g, col.b, a);

                motes[i] = m;
                alive++;
            }

            if (alive == 0 || elapsed > MaxSeconds)
            {
                running = false;
                Destroy(gameObject);
            }
        }
    }
}
