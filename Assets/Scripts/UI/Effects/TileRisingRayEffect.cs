using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 牌の下から黄色い線が伸びるエフェクト（2026-10-02）。
    ///
    /// **まだどこからも呼んでいない。** <see cref="TileSparkleEffect"/> と同じく、
    /// 作っただけ。使うときは <see cref="Attach"/> して <see cref="SetContinuous"/>
    /// か <see cref="Burst"/>。両方を同じ牌に付けて重ねられる。
    ///
    /// 線は**根元が牌の下辺に貼り付いたまま、上へ伸びて消える**。
    /// 根元が明るく、先へ行くほど薄い。粒を飛ばす <see cref="TileSparkleEffect"/> と違い、
    /// こちらは「立ちのぼる光の筋」なので動かさず、長さだけを変える。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TileRisingRayEffect : MaskableGraphic
    {
        // ------------------------------------------------------------
        //  見た目の調整値
        // ------------------------------------------------------------

        /// <summary>根元の色。白に寄せて光らせる。</summary>
        private static readonly Color RootColor = new Color32(255, 250, 210, 255);

        /// <summary>先端の色。黄色のまま透明へ抜く。</summary>
        private static readonly Color TipColor = new Color32(255, 206, 48, 0);

        private const int MaxRays = 20;

        /// <summary>線の寿命（秒）。</summary>
        private const float LifeMin = 0.30f;
        private const float LifeMax = 0.62f;

        /// <summary>
        /// 伸びきったときの長さ。**牌の高さに対する倍率**で持つ。
        /// 固定pxにすると牌の面の中で収まってしまい、白い面に埋もれて見えない。
        /// 1.0 を超えるぶんが牌の上へ抜けて、背景に対して見える。
        /// </summary>
        private const float LengthRatioMin = 0.75f;
        private const float LengthRatioMax = 1.55f;

        /// <summary>線の太さ（px）。ドット絵なので1か2に限る。</summary>
        private const float WidthMin = 1f;
        private const float WidthMax = 2f;

        /// <summary>太い線が出る割合。細いだけだと牌の柄に負ける。</summary>
        private const float ThickChance = 0.45f;

        /// <summary>伸びきるまでの割合。残りは薄れながら少しだけ伸び続ける。</summary>
        private const float GrowPortion = 0.28f;

        /// <summary>湧く範囲を牌の幅の何割に収めるか。</summary>
        private const float SpawnWidthRatio = 0.86f;

        /// <summary>根元を牌の下辺からどれだけ内側に置くか（px）。</summary>
        private const float RootInset = 1f;

        // ------------------------------------------------------------

        private struct Ray
        {
            public float X;         // 牌の中心からの横位置
            public float Width;
            public float Length;    // 伸びきったときの長さ
            public float Age;
            public float Life;
            public bool Alive;
        }

        private Ray[] _rays;
        private float _spawnCarry;

        /// <summary>出し続けるか。</summary>
        public bool Continuous { get; set; }

        /// <summary>出し続けるときの、1秒あたりの本数。</summary>
        public float RatePerSecond { get; set; } = 14f;

        public static TileRisingRayEffect Attach(RectTransform tile)
        {
            if (tile == null) return null;

            var existing = tile.GetComponentInChildren<TileRisingRayEffect>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return existing;
            }

            // CanvasRenderer を自分で付ける（new GameObject 経由では RequireComponent が
            // 効かず、絵が一切出ない。VoltageFlame / TileSparkleEffect と同じ）。
            var go = new GameObject("TileRisingRay", typeof(RectTransform), typeof(CanvasRenderer), typeof(TileRisingRayEffect));
            var rect = (RectTransform)go.transform;
            rect.SetParent(tile, false);

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var fx = go.GetComponent<TileRisingRayEffect>();
            fx.raycastTarget = false;       // 牌のクリック判定を吸わない
            return fx;
        }

        /// <summary>線を <paramref name="count"/> 本ぶん、まとめて出す。</summary>
        public void Burst(int count = 8)
        {
            EnsureBuffer();
            for (int i = 0; i < count; i++) Spawn();
        }

        public void SetContinuous(bool on)
        {
            Continuous = on;
            if (on) EnsureBuffer();
        }

        private void EnsureBuffer()
        {
            if (_rays == null) _rays = new Ray[MaxRays];
        }

        private void Spawn()
        {
            if (_rays == null) return;

            int slot = -1;
            float oldest = -1f;
            for (int i = 0; i < _rays.Length; i++)
            {
                if (!_rays[i].Alive) { slot = i; break; }
                float left = _rays[i].Life - _rays[i].Age;
                if (left > oldest) { oldest = left; slot = i; }
            }
            if (slot < 0) return;

            float halfW = rectTransform.rect.width * SpawnWidthRatio * 0.5f;

            _rays[slot] = new Ray
            {
                X = Random.Range(-halfW, halfW),
                Width = Random.value < ThickChance ? WidthMax : WidthMin,
                Length = rectTransform.rect.height * Random.Range(LengthRatioMin, LengthRatioMax),
                Age = 0f,
                Life = Random.Range(LifeMin, LifeMax),
                Alive = true,
            };
        }

        private void Update()
        {
            if (_rays == null)
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
            for (int i = 0; i < _rays.Length; i++)
            {
                if (!_rays[i].Alive) continue;
                _rays[i].Age += dt;
                if (_rays[i].Age >= _rays[i].Life) { _rays[i].Alive = false; continue; }
                any = true;
            }

            if (any || Continuous) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_rays == null) return;

            float bottom = rectTransform.rect.yMin + RootInset;

            for (int i = 0; i < _rays.Length; i++)
            {
                if (!_rays[i].Alive) continue;

                float t = Mathf.Clamp01(_rays[i].Age / Mathf.Max(0.0001f, _rays[i].Life));

                // **伸びきってから薄れる。** 伸びながら消すと、ただの明滅に見える。
                float grow = t < GrowPortion
                    ? t / GrowPortion
                    : 1f + (t - GrowPortion) / (1f - GrowPortion) * 0.18f;   // 伸びきった後もわずかに伸ばす
                float len = _rays[i].Length * Mathf.Min(grow, 1.18f);

                float alpha = t < GrowPortion ? 1f : 1f - (t - GrowPortion) / (1f - GrowPortion);
                if (alpha <= 0.01f) continue;

                // **整数に丸める。** ドット絵の中で半端な位置に置くとにじむ。
                float x = Mathf.Round(_rays[i].X);
                float w = _rays[i].Width;
                float h = Mathf.Max(1f, Mathf.Round(len));

                Color root = RootColor; root.a *= alpha;
                Color tip = TipColor;   tip.a = 0f;

                AddVerticalGradientQuad(vh, x, bottom, w, h, root, tip);
            }
        }

        /// <summary>
        /// 下辺 (cx, baseY) から高さ h ぶん立てた細い四角。下が <paramref name="bottom"/>、
        /// 上が <paramref name="top"/> の色になる（頂点色で抜くので、追加の絵は要らない）。
        /// </summary>
        private static void AddVerticalGradientQuad(VertexHelper vh, float cx, float baseY,
                                                    float w, float h, Color bottom, Color top)
        {
            float hw = w * 0.5f;
            int i0 = vh.currentVertCount;

            var v = UIVertex.simpleVert;

            v.color = bottom;
            v.position = new Vector3(cx - hw, baseY); vh.AddVert(v);
            v.color = top;
            v.position = new Vector3(cx - hw, baseY + h); vh.AddVert(v);
            v.position = new Vector3(cx + hw, baseY + h); vh.AddVert(v);
            v.color = bottom;
            v.position = new Vector3(cx + hw, baseY); vh.AddVert(v);

            vh.AddTriangle(i0 + 0, i0 + 1, i0 + 2);
            vh.AddTriangle(i0 + 2, i0 + 3, i0 + 0);
        }
    }
}
