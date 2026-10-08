using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// チュートリアルの注目枠の絵（2026-10-08）。<see cref="TutorialHighlightUI"/> の枠の中身。
    ///
    /// **以前は太さ4の金色の線で四角く囲み、ゆっくり明滅させるだけだった。**
    /// プランナーから「ダサい」と言われ、ユーザーから「枠そのものを目立たせる」方向で
    /// 作り直す指示を受けた。細い単色の線は背景（赤い壁・緑の卓）に負けるうえ、
    /// 動きが明滅だけなので、出た瞬間にも出ている間にも目が行かなかった。
    ///
    /// いまの作り
    ///   ・四隅 … 太いかぎ形。**黒い縁を付けて**、どんな背景の上でも形が読めるようにする
    ///     （ドット絵なので、ぼかした光ではなく縁で浮かせる）
    ///   ・辺   … 暗い帯の上を、金色の点線が時計回りに流れる。止まっている枠より目が行く
    ///   ・出るとき … 外側から縮んできて、行きすぎて1ドット食い込み、戻る。最初の一瞬は白く光る
    ///   ・出ている間 … かぎ形が1ドットぶん開いたり閉じたりし、同じ拍で色も明るくなる。
    ///     ときどき枠が外へ広がって消える（波紋）
    ///
    /// **動きはなめらかにせず、刻む。** 位置は必ず1ドット（UIの2単位）の倍数に置き、
    /// 時間も 1/20 秒ごとにしか進めない。半端な位置に置くと、ドット絵の中でそこだけ滲む。
    ///
    /// シェーダーもテクスチャも使わない。四角形を頂点で並べているだけ。
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TutorialHighlightFrameGraphic : MaskableGraphic
    {
        /// <summary>1ドットの大きさ（UIの単位）。このゲームのドット絵は 800x600 に対して2倍で置いてある。</summary>
        private const float Dot = 2f;

        // ---- 形 ----
        private const float BracketThickness = 3f * Dot;   // かぎ形の太さ
        private const float BracketMinArm = 5f * Dot;      // かぎ形の腕の長さ（小さい対象のとき）
        private const float BracketMaxArm = 9f * Dot;      // 同（大きい対象のとき）
        private const float BracketArmRatio = 0.35f;       // 対象の短い辺に対する腕の長さ
        private const float DashLength = 3f * Dot;
        private const float DashGap = 3f * Dot;

        // ---- 動き ----
        private const float StepSeconds = 0.05f;           // 絵を進める刻み。1/20 秒
        private const float EnterSeconds = 0.18f;          // 外から縮んでくる時間
        private const float OvershootSeconds = 0.08f;      // 行きすぎて食い込んでいる時間
        private const float EnterStartOffset = 12f * Dot;  // どれだけ外から始めるか
        private const float FlashSeconds = 0.10f;          // 出た直後に白く光る時間
        private const float TickSeconds = 0.45f;           // かぎ形が開閉する間隔
        private const float DashSpeed = 20f;               // 点線が流れる速さ（単位/秒）
        private const float RippleInterval = 1.6f;         // 波紋を出す間隔
        private const float RippleStepSeconds = 0.12f;     // 波紋が1段広がる時間

        /// <summary>波紋の各段の濃さ。外へ行くほど薄くする。</summary>
        private static readonly float[] RippleAlphas = { 0.70f, 0.50f, 0.30f, 0.15f };

        // ---- 色 ----
        private static readonly Color Gold = new Color32(0xFF, 0xD7, 0x00, 0xFF);
        private static readonly Color GoldBright = new Color32(0xFF, 0xF0, 0x8A, 0xFF);
        private static readonly Color Flash = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color Outline = new Color32(0x24, 0x0C, 0x08, 0xE6);
        private static readonly Color Rail = new Color32(0x24, 0x0C, 0x08, 0x73);

        private float _startTime;
        private int _lastStep = -1;
        private Vector2 _lastSize;

        /// <summary>出るときの動きを最初からやり直す。対象が替わったときに呼ぶ。</summary>
        public void Restart()
        {
            _startTime = Time.unscaledTime;
            _lastStep = -1;
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _lastStep = -1;
        }

        private void Update()
        {
            // 絵が変わる刻みをまたいだときと、対象の大きさが変わったときだけ描き直す
            int step = Mathf.FloorToInt((Time.unscaledTime - _startTime) / StepSeconds);
            Vector2 size = rectTransform.rect.size;
            if (step == _lastStep && size == _lastSize) return;
            _lastStep = step;
            _lastSize = size;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect area = rectTransform.rect;
            if (area.width < 1f || area.height < 1f) return;

            // 時間は刻みの頭にそろえる（同じ刻みの中では同じ絵）
            float t = Mathf.Max(0f, Mathf.Floor((Time.unscaledTime - _startTime) / StepSeconds) * StepSeconds);

            // --- 出るときの動き: 外から縮む → 1ドット食い込む → 定位置 ---
            float offset;
            bool settled = false;
            if (t < EnterSeconds)
            {
                float p = t / EnterSeconds;
                float eased = 1f - (1f - p) * (1f - p) * (1f - p);
                offset = SnapToDot((1f - eased) * EnterStartOffset);
            }
            else if (t < EnterSeconds + OvershootSeconds)
            {
                offset = -Dot;
            }
            else
            {
                settled = true;
                offset = 0f;
            }

            // --- 出ている間: かぎ形が1ドット開閉する ---
            float idle = t - (EnterSeconds + OvershootSeconds);
            bool tickOpen = settled && (Mathf.FloorToInt(idle / TickSeconds) % 2 == 1);
            float bracketOffset = offset + (tickOpen ? Dot : 0f);

            Rect frame = Expand(area, offset);
            Rect bracketFrame = Expand(area, bracketOffset);

            float shortSide = Mathf.Min(bracketFrame.width, bracketFrame.height);
            float arm = Mathf.Clamp(SnapToDot(shortSide * BracketArmRatio), BracketMinArm, BracketMaxArm);
            // 対象が小さいと、向かい合う腕どうしがぶつかる。半分より少し短く止める
            arm = Mathf.Min(arm, SnapToDot(shortSide * 0.5f - Dot));
            if (arm < BracketThickness) arm = BracketThickness;

            Color bracketColor = t < FlashSeconds ? Flash : (tickOpen ? GoldBright : Gold);

            // 1) 辺の暗い帯と、その上を流れる点線
            DrawRailsAndDashes(vh, frame, arm, t);

            // 2) 波紋（定位置に着いてから）
            if (settled) DrawRipple(vh, frame, idle);

            // 3) 四隅のかぎ形。**黒い縁を先に全部描いてから、金色を重ねる。**
            //    1つずつ「縁→金」と描くと、あとから描いた縁が隣の金色を欠けさせる
            DrawBrackets(vh, bracketFrame, arm, Dot, Outline);
            DrawBrackets(vh, bracketFrame, arm, 0f, bracketColor);
        }

        private void DrawRailsAndDashes(VertexHelper vh, Rect f, float arm, float t)
        {
            // 帯は、かぎ形の太さの真ん中の1ドットを通す
            float inset = Dot;
            float railHalf = BracketThickness * 0.5f;

            float left = f.xMin + arm;
            float right = f.xMax - arm;
            float bottom = f.yMin + arm;
            float top = f.yMax - arm;

            // かぎ形どうしのあいだに隙間が無いほど小さい対象には、辺は描かない
            bool hasHorizontal = right - left >= DashLength;
            bool hasVertical = top - bottom >= DashLength;

            float period = DashLength + DashGap;
            float phase = SnapToDot(t * DashSpeed) % period;

            if (hasHorizontal)
            {
                float yTopCenter = f.yMax - BracketThickness * 0.5f;
                float yBottomCenter = f.yMin + BracketThickness * 0.5f;

                Quad(vh, left, yTopCenter - railHalf, right, yTopCenter + railHalf, Rail);
                Quad(vh, left, yBottomCenter - railHalf, right, yBottomCenter + railHalf, Rail);

                // 上は右へ、下は左へ流す（時計回り）
                for (float s = phase - period; s < right - left; s += period)
                {
                    float a = Mathf.Max(0f, s), b = Mathf.Min(right - left, s + DashLength);
                    if (b <= a) continue;
                    Quad(vh, left + a, yTopCenter - inset * 0.5f, left + b, yTopCenter + inset * 0.5f, Gold);
                    Quad(vh, right - b, yBottomCenter - inset * 0.5f, right - a, yBottomCenter + inset * 0.5f, Gold);
                }
            }

            if (hasVertical)
            {
                float xLeftCenter = f.xMin + BracketThickness * 0.5f;
                float xRightCenter = f.xMax - BracketThickness * 0.5f;

                Quad(vh, xLeftCenter - railHalf, bottom, xLeftCenter + railHalf, top, Rail);
                Quad(vh, xRightCenter - railHalf, bottom, xRightCenter + railHalf, top, Rail);

                // 右は下へ、左は上へ流す（時計回り）
                for (float s = phase - period; s < top - bottom; s += period)
                {
                    float a = Mathf.Max(0f, s), b = Mathf.Min(top - bottom, s + DashLength);
                    if (b <= a) continue;
                    Quad(vh, xRightCenter - inset * 0.5f, top - b, xRightCenter + inset * 0.5f, top - a, Gold);
                    Quad(vh, xLeftCenter - inset * 0.5f, bottom + a, xLeftCenter + inset * 0.5f, bottom + b, Gold);
                }
            }
        }

        private void DrawRipple(VertexHelper vh, Rect f, float idle)
        {
            float inCycle = idle % RippleInterval;
            int stage = Mathf.FloorToInt(inCycle / RippleStepSeconds);
            if (stage < 0 || stage >= RippleAlphas.Length) return;

            Rect r = Expand(f, (stage + 1) * 2f * Dot);
            Color c = GoldBright;
            c.a = RippleAlphas[stage];

            Quad(vh, r.xMin, r.yMax - Dot, r.xMax, r.yMax, c);            // 上
            Quad(vh, r.xMin, r.yMin, r.xMax, r.yMin + Dot, c);            // 下
            Quad(vh, r.xMin, r.yMin + Dot, r.xMin + Dot, r.yMax - Dot, c); // 左
            Quad(vh, r.xMax - Dot, r.yMin + Dot, r.xMax, r.yMax - Dot, c); // 右
        }

        /// <summary>四隅のかぎ形。<paramref name="grow"/> だけ全方向へ太らせて描く（縁を描くのに使う）。</summary>
        private void DrawBrackets(VertexHelper vh, Rect f, float arm, float grow, Color c)
        {
            float t = BracketThickness;

            // 左下
            Quad(vh, f.xMin - grow, f.yMin - grow, f.xMin + arm + grow, f.yMin + t + grow, c);
            Quad(vh, f.xMin - grow, f.yMin - grow, f.xMin + t + grow, f.yMin + arm + grow, c);
            // 右下
            Quad(vh, f.xMax - arm - grow, f.yMin - grow, f.xMax + grow, f.yMin + t + grow, c);
            Quad(vh, f.xMax - t - grow, f.yMin - grow, f.xMax + grow, f.yMin + arm + grow, c);
            // 左上
            Quad(vh, f.xMin - grow, f.yMax - t - grow, f.xMin + arm + grow, f.yMax + grow, c);
            Quad(vh, f.xMin - grow, f.yMax - arm - grow, f.xMin + t + grow, f.yMax + grow, c);
            // 右上
            Quad(vh, f.xMax - arm - grow, f.yMax - t - grow, f.xMax + grow, f.yMax + grow, c);
            Quad(vh, f.xMax - t - grow, f.yMax - arm - grow, f.xMax + grow, f.yMax + grow, c);
        }

        private static Rect Expand(Rect r, float by)
        {
            return Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
        }

        private static float SnapToDot(float v)
        {
            return Mathf.Round(v / Dot) * Dot;
        }

        private static void Quad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color c)
        {
            if (xMax <= xMin || yMax <= yMin) return;

            int i = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;

            v.position = new Vector3(xMin, yMin, 0f); vh.AddVert(v);
            v.position = new Vector3(xMin, yMax, 0f); vh.AddVert(v);
            v.position = new Vector3(xMax, yMax, 0f); vh.AddVert(v);
            v.position = new Vector3(xMax, yMin, 0f); vh.AddVert(v);

            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
