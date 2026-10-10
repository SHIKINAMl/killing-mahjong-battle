using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 牌から黄色いキラキラを出す（2026-10-02）。
    ///
    /// **まだどこからも呼んでいない。** 作っただけで、ゲームの進行には繋いでいない。
    /// 使うときは <see cref="Attach"/> して <see cref="Burst"/> を叩く。
    ///
    /// 作りは <see cref="VoltageFlame"/> に合わせてある。粒ごとに GameObject を
    /// 作ると牌の数だけレイアウトが揺れるので、`Graphic` を1つ持って
    /// <see cref="OnPopulateMesh"/> で四角を積む。
    ///
    /// 絵柄は**4方向に尖った星**。ドット絵に合わせて、座標も大きさも整数に丸め、
    /// ぼかしは使わない（にじむと他のUIと質感が合わない）。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TileSparkleEffect : MaskableGraphic
    {
        // ------------------------------------------------------------
        //  見た目の調整値
        // ------------------------------------------------------------

        /// <summary>芯。ほぼ白で、星の中心だけ白熱させる。</summary>
        private static readonly Color CoreColor = new Color32(255, 253, 225, 255);

        /// <summary>腕の色。黄色いキラキラの本体。</summary>
        private static readonly Color ArmColor = new Color32(255, 214, 59, 255);

        /// <summary>消えぎわ。少し橙に寄せて落とす。</summary>
        private static readonly Color FadeColor = new Color32(255, 150, 32, 0);

        /// <summary>一度に出す粒の上限。これを超えたら古いものから使い回す。</summary>
        private const int MaxSparkles = 48;

        /// <summary>粒の寿命（秒）。短めにして、ちらっと光って消える感じにする。</summary>
        private const float LifeMin = 0.38f;
        private const float LifeMax = 0.72f;

        /// <summary>星の腕の長さ（px）。寿命の途中で最大になる。</summary>
        private const float ArmMin = 3f;
        private const float ArmMax = 7f;

        /// <summary>上へ流れる速さ（px/秒）。牌から立ちのぼる感じを出す。</summary>
        private const float RiseMin = 10f;
        private const float RiseMax = 26f;

        /// <summary>横の散り（px/秒）。左右どちらにも振れる。</summary>
        private const float DriftMax = 14f;

        /// <summary>湧く範囲を牌の内側へ寄せる割合。1.0 で牌いっぱい。</summary>
        private const float SpawnInset = 0.82f;

        // ------------------------------------------------------------

        private struct Sparkle
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Life;
            public float Phase;     // 瞬きの位相。粒ごとにずらす
            public bool Alive;
        }

        private Sparkle[] _sparkles;
        private RectTransform _tile;

        /// <summary>出し続けるか。false なら <see cref="Burst"/> のぶんだけ。</summary>
        public bool Continuous { get; set; }

        /// <summary>出し続けるときの、1秒あたりの湧く数。</summary>
        public float RatePerSecond { get; set; } = 18f;

        private float _spawnCarry;

        /// <summary>
        /// 牌にキラキラを付ける。すでに付いていれば使い回す。
        /// </summary>
        public static TileSparkleEffect Attach(RectTransform tile)
        {
            if (tile == null) return null;

            var existing = tile.GetComponentInChildren<TileSparkleEffect>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }

            // **CanvasRenderer を自分で付けること。** `new GameObject(..., typeof(TileSparkleEffect))`
            // では RequireComponent が効かず、絵が一切出ない（VoltageFlame で踏んだ）。
            var go = new GameObject("TileSparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TileSparkleEffect));
            var rect = (RectTransform)go.transform;
            rect.SetParent(tile, false);

            // 牌の全面に重ねる。**メッシュは枠からはみ出して構わない**
            // （`Graphic` は矩形で切られない。切るのは `RectMask2D` だけ）。
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var fx = go.GetComponent<TileSparkleEffect>();
            fx._tile = tile;
            fx.raycastTarget = false;        // 牌のクリック判定を吸わない
            return fx;
        }

        /// <summary>キラキラを <paramref name="count"/> 粒ぶん、まとめて出す。</summary>
        public void Burst(int count = 14)
        {
            EnsureBuffer();
            for (int i = 0; i < count; i++) Spawn();
        }

        /// <summary>出し続けるのを始める／止める。</summary>
        public void SetContinuous(bool on)
        {
            Continuous = on;
            if (on) EnsureBuffer();
        }

        private void EnsureBuffer()
        {
            if (_sparkles == null) _sparkles = new Sparkle[MaxSparkles];
        }

        private Rect SpawnArea()
        {
            Rect r = rectTransform.rect;
            float w = r.width * SpawnInset;
            float h = r.height * SpawnInset;
            return new Rect(-w * 0.5f, -h * 0.5f, w, h);
        }

        private void Spawn()
        {
            if (_sparkles == null) return;

            int slot = -1;
            float oldest = -1f;
            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (!_sparkles[i].Alive) { slot = i; break; }
                float left = _sparkles[i].Life - _sparkles[i].Age;
                if (left > oldest) { oldest = left; slot = i; }
            }
            if (slot < 0) return;

            Rect area = SpawnArea();
            var s = new Sparkle
            {
                Pos = new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax)),
                Vel = new Vector2(Random.Range(-DriftMax, DriftMax), Random.Range(RiseMin, RiseMax)),
                Age = 0f,
                Life = Random.Range(LifeMin, LifeMax),
                Phase = Random.value * Mathf.PI * 2f,
                Alive = true,
            };
            _sparkles[slot] = s;
        }

        private void Update()
        {
            if (_sparkles == null)
            {
                if (!Continuous) return;
                EnsureBuffer();
            }

            float dt = Time.deltaTime;

            if (Continuous && RatePerSecond > 0f)
            {
                _spawnCarry += RatePerSecond * dt;
                while (_spawnCarry >= 1f)
                {
                    _spawnCarry -= 1f;
                    Spawn();
                }
            }

            bool any = false;
            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (!_sparkles[i].Alive) continue;

                _sparkles[i].Age += dt;
                if (_sparkles[i].Age >= _sparkles[i].Life)
                {
                    _sparkles[i].Alive = false;
                    continue;
                }

                _sparkles[i].Pos += _sparkles[i].Vel * dt;
                any = true;
            }

            // 生きている粒が無く、湧かせてもいないなら描き直さない
            if (any || Continuous) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_sparkles == null) return;

            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (!_sparkles[i].Alive) continue;

                float t = Mathf.Clamp01(_sparkles[i].Age / Mathf.Max(0.0001f, _sparkles[i].Life));

                // **膨らんでから縮む。** 出た瞬間に最大だと、点滅しているようにしか見えない。
                // 0→0.3 で開き、0.3→1 で閉じる。
                float grow = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
                grow = Mathf.Clamp01(grow);

                // 瞬き。粒ごとに位相をずらして、ばらばらに光らせる
                float twinkle = 0.75f + 0.25f * Mathf.Sin(_sparkles[i].Phase + _sparkles[i].Age * 18f);

                float arm = Mathf.Lerp(ArmMin, ArmMax, grow) * twinkle;
                if (arm < 1f) continue;

                Color c = t < 0.5f
                    ? Color.Lerp(CoreColor, ArmColor, t / 0.5f)
                    : Color.Lerp(ArmColor, FadeColor, (t - 0.5f) / 0.5f);
                c.a *= grow;
                if (c.a <= 0.01f) continue;

                // **整数に丸める。** ドット絵の中で半端な位置に置くとにじむ。
                float x = Mathf.Round(_sparkles[i].Pos.x);
                float y = Mathf.Round(_sparkles[i].Pos.y);
                float a = Mathf.Round(arm);

                // 4方向に尖った星。横棒・縦棒・芯の3枚で作る
                AddQuad(vh, x, y, a * 2f + 1f, 1f, c);                       // 横
                AddQuad(vh, x, y, 1f, a * 2f + 1f, c);                       // 縦
                Color core = Color.Lerp(c, CoreColor, 0.6f);
                core.a = c.a;
                AddQuad(vh, x, y, Mathf.Max(1f, a * 0.6f), Mathf.Max(1f, a * 0.6f), core);  // 芯
            }
        }

        /// <summary>中心 (cx,cy)・幅 w・高さ h の四角を1枚積む。</summary>
        private static void AddQuad(VertexHelper vh, float cx, float cy, float w, float h, Color color)
        {
            float hw = w * 0.5f;
            float hh = h * 0.5f;
            int i0 = vh.currentVertCount;

            var v = UIVertex.simpleVert;
            v.color = color;

            v.position = new Vector3(cx - hw, cy - hh); vh.AddVert(v);
            v.position = new Vector3(cx - hw, cy + hh); vh.AddVert(v);
            v.position = new Vector3(cx + hw, cy + hh); vh.AddVert(v);
            v.position = new Vector3(cx + hw, cy - hh); vh.AddVert(v);

            vh.AddTriangle(i0 + 0, i0 + 1, i0 + 2);
            vh.AddTriangle(i0 + 2, i0 + 3, i0 + 0);
        }
    }
}
