using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 卓の上の血だまり。横長のドットの楕円で、量（0〜1）に合わせて広がる・縮む。
    /// 「場に出ている血」を形で見せる。
    /// </summary>
    public sealed class BloodPoolEffect : MonoBehaviour
    {
        private const int TexW = 64, TexH = 20;
        private static Sprite _sprite;
        private Image image;
        private RectTransform rt;
        private float shown, target;
        private Vector2 fullSize;

        /// <summary>見えている量（0〜1）。</summary>
        public float Amount => shown;

        private static Sprite PoolSprite
        {
            get
            {
                if (_sprite != null) return _sprite;
                // 楕円をドットで描く。縁はでこぼこさせ、上の縁に明るい線を入れて濡れて見せる
                var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var clear = new Color(0, 0, 0, 0);
                Color dark = new Color32(96, 6, 18, 255), body = new Color32(150, 12, 30, 255), shine = new Color32(222, 52, 52, 255);
                for (int y = 0; y < TexH; y++)
                for (int x = 0; x < TexW; x++)
                {
                    float nx = (x + 0.5f - TexW * 0.5f) / (TexW * 0.5f);
                    float ny = (y + 0.5f - TexH * 0.5f) / (TexH * 0.5f);
                    float bump = 1f + 0.08f * Mathf.Sin(x * 1.7f) + 0.05f * Mathf.Sin(x * 0.6f + 1f);
                    float r = nx * nx + ny * ny;
                    Color c = clear;
                    if (r <= bump)
                    {
                        c = r > bump * 0.78f ? dark : body;
                        if (ny > 0.25f && ny < 0.55f && Mathf.Abs(nx) < 0.55f && r < bump * 0.7f) c = shine;
                    }
                    tex.SetPixel(x, y, c);
                }
                tex.Apply();
                _sprite = Sprite.Create(tex, new Rect(0, 0, TexW, TexH), new Vector2(0.5f, 0.5f), 1f);
                _sprite.name = "BloodPool";
                return _sprite;
            }
        }

        /// <summary>
        /// <paramref name="worldPos"/> に血だまりを置く。最初は量 0（見えない）。
        /// <paramref name="fullWidth"/> は量 1 のときの横幅（800x600 基準の px）。
        /// </summary>
        public static BloodPoolEffect Create(RectTransform stage, Vector3 worldPos, float fullWidth = 220f)
        {
            if (stage == null) return null;
            var go = new GameObject("BloodPool", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(stage, false);
            var pool = go.AddComponent<BloodPoolEffect>();
            pool.rt = (RectTransform)go.transform;
            pool.rt.anchorMin = pool.rt.anchorMax = new Vector2(0.5f, 0.5f);
            pool.rt.position = worldPos;
            pool.image = go.GetComponent<Image>();
            pool.image.sprite = PoolSprite;
            pool.image.raycastTarget = false;
            // 親の大きさは画面に合わせて縮むので、幅は 800x600 基準のまま渡せばよい
            pool.fullSize = new Vector2(fullWidth, fullWidth * TexH / TexW);
            pool.Apply(0f);
            return pool;
        }

        /// <summary>量（0〜1）を目標にする。少しずつ寄っていく。</summary>
        public void SetTarget(float amount) { target = Mathf.Clamp01(amount); }

        /// <summary>量をすぐ決める。</summary>
        public void SetNow(float amount) { target = shown = Mathf.Clamp01(amount); Apply(shown); }

        private void Apply(float a)
        {
            // 面積が量に比例するように、幅は平方根で広げる。ドット感を崩さないよう 4px 刻み
            float k = Mathf.Sqrt(a);
            float w = Mathf.Round(fullSize.x * k / 4f) * 4f;
            float h = Mathf.Round(fullSize.y * k / 4f) * 4f;
            rt.sizeDelta = new Vector2(w, Mathf.Max(h, w > 0 ? 4f : 0f));
            image.enabled = w > 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(shown, target)) return;
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 1.6f);
            Apply(shown);
        }
    }
}
