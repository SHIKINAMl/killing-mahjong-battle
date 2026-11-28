using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// いまの画面を写し取って、ぼかした絵にする（友達対戦の画面の後ろに、メインメニューをぼかして敷くため。2026-11-28）。
    ///
    /// **必ず <c>WaitForEndOfFrame</c> のあとに呼ぶ。** 合成し終えた画面を撮るため。
    /// メインメニューの部屋の絵は ScreenSpace-Overlay の Canvas に描いているので、カメラの絵からは取れない。
    ///
    /// 上下の向きがグラフィックスの種類で変わる <c>CaptureScreenshotIntoRenderTexture</c> は使わず、
    /// 向きのそろった <c>CaptureScreenshotAsTexture</c> で撮り、縮める・ぼかすは CPU でやる（開くときに一度だけ）。
    /// </summary>
    public static class ScreenBlurCapture
    {
        /// <param name="width">ぼかした絵の幅（小さいほど速く、ぼけも大きく見える）。高さは画面の比で決める</param>
        /// <param name="radius">箱型のぼかしの半径（画素）。3回かけて、ほぼガウスのぼけにする</param>
        public static Texture2D Capture(int width = 200, int radius = 2)
        {
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            if (shot == null) return null;
            int sw = shot.width, sh = shot.height;
            var src = shot.GetPixels32();
            Object.Destroy(shot);

            int w = Mathf.Max(8, width);
            int h = Mathf.Max(8, Mathf.RoundToInt(w * sh / (float)sw));

            // 面積の平均で縮める（間引くと、細い線がちらついて残る）
            var r = new float[w * h]; var g = new float[w * h]; var b = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                int y0 = y * sh / h, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / h);
                for (int x = 0; x < w; x++)
                {
                    int x0 = x * sw / w, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / w);
                    float sr = 0, sg = 0, sb = 0; int n = 0;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        int row = yy * sw;
                        for (int xx = x0; xx < x1; xx++)
                        {
                            var c = src[row + xx];
                            sr += c.r; sg += c.g; sb += c.b; n++;
                        }
                    }
                    int i = y * w + x;
                    r[i] = sr / n; g[i] = sg / n; b[i] = sb / n;
                }
            }

            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlur(r, w, h, radius); BoxBlur(g, w, h, radius); BoxBlur(b, w, h, radius);
            }

            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(r[i]), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(g[i]), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(b[i]), 0, 255), 255);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                // 拡大したときもなめらかに（ぼかした絵なので、ドットの格子にしない）
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "ScreenBlurCapture"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>横・縦の順に、半径 <paramref name="radius"/> の平均をとる。端は端の色を伸ばす。</summary>
        private static void BoxBlur(float[] v, int w, int h, int radius)
        {
            if (radius <= 0) return;
            var tmp = new float[v.Length];
            float span = radius * 2 + 1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float sum = 0;
                for (int k = -radius; k <= radius; k++) sum += v[row + Mathf.Clamp(k, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = sum / span;
                    sum += v[row + Mathf.Min(x + radius + 1, w - 1)] - v[row + Mathf.Max(x - radius, 0)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0;
                for (int k = -radius; k <= radius; k++) sum += tmp[Mathf.Clamp(k, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    v[y * w + x] = sum / span;
                    sum += tmp[Mathf.Min(y + radius + 1, h - 1) * w + x] - tmp[Mathf.Max(y - radius, 0) * w + x];
                }
            }
        }
    }
}
