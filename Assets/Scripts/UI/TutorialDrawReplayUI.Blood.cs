using System.Collections;
using UnityEngine;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 動画の最後、倒れ伏した先輩の下から血が流れ出す様子（2026-10-23 ユーザー指示「血が流れている様子を作って」）。
    ///
    /// 以前は卓の手前を半透明の赤い台形で塗り広げていたが、牌まで赤く透けて「変な感じ」だった。
    /// いまは 8 単位のドットで描く。
    ///   1. 先輩の体のふちの下から、丸い血だまりが時間差でいくつも膨らみ、くっついて1つになる
    ///      （メタボール。丸どうしが近づくとなめらかにつながる）。丸は体の下から右手前へ順に並べてあり、
    ///      後から出る丸ほど手前にあるので、卓の手前へ流れていくように見える
    ///   2. ふちには雑音で少し揺らぎを入れ、まん丸にならないようにする
    ///   3. 上のふちは明るく、下と右のふちは暗くして厚みを出す。上のふちの少し内側に、ところどころつやを置く
    ///   4. 血だまりの上で、光の点をいくつか点滅させる（濡れた感じ。ドット絵の粒は点滅させると映える）
    /// 形は毎回同じ。
    ///
    /// 形は手元で同じ計算を絵に重ねて決めた。ふちを大きく雑音で崩した楕円＋細い筋（とげとげした生き物に見えた）、
    /// 遠くまで尾を引く丸（全部つながって平たい板になった）、手前で横へ張り出す丸（キノコのような形になった）はやめた。
    /// </summary>
    public sealed partial class TutorialDrawReplayUI
    {
        private const float BloodCell = 8f;
        private const int BloodCols = 100;   // 800 / 8
        private const int BloodRows = 75;    // 600 / 8

        /// <summary>卓は斜め上から見ているので、血だまりは縦につぶす（縦の長さ ÷ 横の長さ）</summary>
        private const float BloodSquash = 0.55f;
        /// <summary>丸の重なりがこれを超えたところを塗る。ふちの揺らぎはこの値を上下させて作る</summary>
        private const float BloodThreshold = 0.5f;
        /// <summary>卓の奥のふち。これより上（牌の壁や相手の側）には広げない。ふつうは届かない</summary>
        private const float TableFarEdgeY = -100f;

        private static readonly Color32 BloodBase = new Color32(132, 6, 20, 255);
        private static readonly Color32 BloodDark = new Color32(78, 0, 12, 255);
        private static readonly Color32 BloodLight = new Color32(188, 32, 44, 255);
        private static readonly Color32 BloodSheen = new Color32(214, 70, 78, 255);
        private static readonly Color32 BloodGlint = new Color32(250, 170, 170, 255);

        /// <summary>
        /// 血だまりの丸。x, y は中心（動画の枠の真ん中から）、z は広がり切ったときの半径、w は出始める秒。
        /// 先輩の体の右のふちは (-70,-120) から (70,-250) あたりを斜めに通る。最初の2つはその内側（体の下）から出す
        /// </summary>
        private static readonly Vector4[] BloodLobes =
        {
            new Vector4(-40f, -170f, 120f, 0.0f),
            new Vector4(40f, -222f, 110f, 0.3f),
            new Vector4(108f, -196f, 80f, 0.9f),
            new Vector4(140f, -250f, 78f, 1.5f),
            new Vector4(196f, -268f, 56f, 2.3f),
            new Vector4(232f, -292f, 40f, 3.1f),
        };
        /// <summary>1つの丸が広がり切るまでの秒数。最初が速く、だんだん遅くなる</summary>
        private const float LobeSeconds = 3.6f;

        /// <summary>光らせる点（x, y）と点滅のずれ（z）</summary>
        private static readonly Vector3[] Glints =
        {
            new Vector3(-8f, -176f, 0f), new Vector3(56f, -214f, 0.7f), new Vector3(112f, -180f, 1.3f),
            new Vector3(104f, -246f, 0.4f), new Vector3(170f, -226f, 1.9f),
        };

        /// <summary>動画の枠が残っている間、血を描き続ける（広がり終わったあとも光の点は点滅させる）</summary>
        private IEnumerator Bleed(PixelShapeGraphic stain)
        {
            var grid = new bool[BloodCols, BloodRows];
            var radii = new float[BloodLobes.Length];
            for (float t = 0f; stain != null; t += Time.unscaledDeltaTime)
            {
                DrawBlood(stain, grid, radii, t);
                yield return null;
            }
        }

        private static void DrawBlood(PixelShapeGraphic stain, bool[,] grid, float[] radii, float t)
        {
            System.Array.Clear(grid, 0, grid.Length);

            // 1. 血だまり。丸ごとの重なりを足し合わせ、しきい値を超えたところを塗る。
            //    重なりは丸の半径の外で 0 になる形にしてある（遠くまで尾を引く形だと、全部つながって平たい板になった）
            for (int k = 0; k < BloodLobes.Length; k++)
            {
                float q = Mathf.Clamp01((t - BloodLobes[k].w) / LobeSeconds);
                radii[k] = BloodLobes[k].z * (1f - (1f - q) * (1f - q) * (1f - q));
            }
            for (int i = 0; i < BloodCols; i++)
            {
                for (int j = 0; j < BloodRows; j++)
                {
                    Vector2 c = CellCenter(i, j);
                    if (c.y > TableFarEdgeY) continue;
                    float field = 0f;
                    for (int k = 0; k < BloodLobes.Length; k++)
                    {
                        if (radii[k] <= 0f) continue;
                        float dx = c.x - BloodLobes[k].x;
                        float dy = (c.y - BloodLobes[k].y) / BloodSquash;
                        float u = 1f - (dx * dx + dy * dy) / (radii[k] * radii[k]);
                        if (u > 0f) field += u * u;
                    }
                    // 2. ふちの揺らぎ
                    float threshold = BloodThreshold * (0.75f + 0.5f * ValueNoise(c.x * 0.045f, c.y * 0.07f));
                    if (field >= threshold) grid[i, j] = true;
                }
            }

            // 3. 塗る。同じ色の横並びはまとめて1枚で描く
            stain.Begin();
            for (int j = 0; j < BloodRows; j++)
            {
                int runStart = -1;
                Color32 runColor = default;
                for (int i = 0; i <= BloodCols; i++)
                {
                    bool filled = i < BloodCols && grid[i, j];
                    Color32 color = filled ? ShadeOf(grid, i, j) : default;
                    bool same = runStart >= 0 && filled && color.Equals(runColor);
                    if (same) continue;
                    if (runStart >= 0) EmitRun(stain, runStart, i, j, runColor);
                    runStart = filled ? i : -1;
                    runColor = color;
                }
            }

            // 4. 光の点。血だまりが届いてから、ゆっくり点滅させる
            foreach (var g in Glints)
            {
                int gi = Mathf.FloorToInt((g.x + 400f) / BloodCell);
                int gj = Mathf.FloorToInt((g.y + 300f) / BloodCell);
                if (!Filled(grid, gi, gj)) continue;
                if (Mathf.FloorToInt((t + g.z) * 1.6f) % 3 == 0) continue;
                Vector2 c = CellCenter(gi, gj) - new Vector2(BloodCell * 0.5f, BloodCell * 0.5f);
                stain.Box(c.x, c.y, c.x + BloodCell, c.y + BloodCell, BloodGlint);
            }
            stain.End();
        }

        private static bool Filled(bool[,] grid, int i, int j)
        {
            return i >= 0 && i < BloodCols && j >= 0 && j < BloodRows && grid[i, j];
        }

        private static Color32 ShadeOf(bool[,] grid, int i, int j)
        {
            if (!Filled(grid, i, j + 1)) return BloodLight;
            if (!Filled(grid, i, j - 1) || !Filled(grid, i + 1, j)) return BloodDark;
            // 上のふちから1つ内側に、ところどころつや
            if (!Filled(grid, i, j + 2) && Filled(grid, i - 1, j + 1) && Filled(grid, i + 1, j + 1) && Hash(i, 7) < 0.55f)
                return BloodSheen;
            return BloodBase;
        }

        private static void EmitRun(PixelShapeGraphic stain, int from, int to, int j, Color32 color)
        {
            float x0 = -400f + from * BloodCell;
            float x1 = -400f + to * BloodCell;
            float y0 = -300f + j * BloodCell;
            stain.Box(x0, y0, x1, y0 + BloodCell, color);
        }

        private static Vector2 CellCenter(int i, int j)
        {
            return new Vector2(-400f + (i + 0.5f) * BloodCell, -300f + (j + 0.5f) * BloodCell);
        }

        /// <summary>なめらかな雑音（0〜1）。格子の角に固定の値を置き、間をなめらかにつなぐ</summary>
        private static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
