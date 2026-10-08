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
    ///   ・出るとき … **四隅が横にくっついた細い柱の形で現れ、左右へ開いて決まった幅になり、
    ///     開ききった所で2回点滅する**（2026-10-08 のユーザー指定）。
    ///     その前の版は「外側から縮んできて、行きすぎて戻る」だった
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
        private const float ClosedHoldSeconds = 0.10f;     // 閉じた形（細い柱）のまま見せておく時間
        private const float OpenSeconds = 0.30f;           // 左右へ開ききるまでの時間

        /// <summary>
        /// 開ききったあとの点滅。**「消える → 白く点く」を2回**やってから金色で落ち着く。
        /// 並びは 消・点・消・点 の長さ（秒）。刻み（<see cref="StepSeconds"/>）の倍数にしておくこと。
        /// </summary>
        private static readonly float[] BlinkPattern = { 0.05f, 0.10f, 0.05f, 0.10f };
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

        /// <summary>
        /// 出てからの経過秒。**実時間（Time.unscaledTime）ではなく、フレームごとに足していく。**
        ///
        /// 録画（Unity Recorder）はフレームを固定の間隔で進めるが、実時間はそのあいだも流れる。
        /// 実時間で動かすと、録画の上では出るときの動きが2〜3フレームで終わってしまい、映らない。
        /// 足す量は「ゲームの時間の進み」を使い、止めてある（timeScale が 0）ときだけ実時間にする。
        /// 説明の途中でゲームの時間を止めても、枠は動き続ける。
        /// </summary>
        private float _clock;
        private int _lastStep = -1;
        private Vector2 _lastSize;

        /// <summary>出るときの動きを最初からやり直す。対象が替わったときに呼ぶ。</summary>
        public void Restart()
        {
            _clock = 0f;
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
            _clock += Time.timeScale > 0f ? Time.deltaTime / Time.timeScale : Time.unscaledDeltaTime;

            // 絵が変わる刻みをまたいだときと、対象の大きさが変わったときだけ描き直す
            int step = Mathf.FloorToInt(_clock / StepSeconds);
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
            float t = Mathf.Max(0f, Mathf.Floor(_clock / StepSeconds) * StepSeconds);

            // --- 出るときの動き: 細い柱で現れる → 左右へ開く → 2回点滅 → 落ち着く ---
            //
            // 縦は最初から対象の高さ。**横だけ**を、かぎ形の縦棒2本ぶんの幅から対象の幅まで広げる。
            // 閉じているあいだは左右のかぎ形の腕が重なるので、腕は「幅の半分まで」に切って描く。
            // 開くにつれて腕が伸び、あいだに点線の通り道が現れる
            float fullHalf = area.width * 0.5f;
            float closedHalf = Mathf.Min(BracketThickness, fullHalf);
            float halfWidth;
            float blinkStart = ClosedHoldSeconds + OpenSeconds;
            if (t < ClosedHoldSeconds)
            {
                halfWidth = closedHalf;
            }
            else if (t < blinkStart)
            {
                float p = (t - ClosedHoldSeconds) / OpenSeconds;
                float eased = 1f - (1f - p) * (1f - p) * (1f - p);   // 勢いよく開いて、最後に減速
                halfWidth = Mathf.Min(fullHalf, SnapToDot(Mathf.Lerp(closedHalf, fullHalf, eased)));
            }
            else
            {
                halfWidth = fullHalf;
            }

            // 開ききったあとの点滅。消えている刻みでは何も描かない
            bool blinkOn = false;
            bool settled = false;
            float idle = 0f;
            if (t >= blinkStart)
            {
                float into = t - blinkStart;
                float edge = 0f;
                settled = true;
                for (int i = 0; i < BlinkPattern.Length; i++)
                {
                    edge += BlinkPattern[i];
                    if (into < edge - 0.0001f)
                    {
                        settled = false;
                        if (i % 2 == 0) return;   // 「消」の刻み
                        blinkOn = true;           // 「点」の刻み（白く光らせる）
                        break;
                    }
                }
                if (settled) idle = into - edge;
            }

            // --- 出ている間: かぎ形が1ドット開閉する ---
            bool tickOpen = settled && (Mathf.FloorToInt(idle / TickSeconds) % 2 == 1);

            Rect frame = Rect.MinMaxRect(area.center.x - halfWidth, area.yMin, area.center.x + halfWidth, area.yMax);
            Rect bracketFrame = Expand(frame, tickOpen ? Dot : 0f);

            float shortSide = Mathf.Min(area.width, area.height);
            float arm = Mathf.Clamp(SnapToDot(shortSide * BracketArmRatio), BracketMinArm, BracketMaxArm);
            // 対象が小さいと、向かい合う腕どうしがぶつかる。半分より少し短く止める
            arm = Mathf.Min(arm, SnapToDot(shortSide * 0.5f - Dot));
            if (arm < BracketThickness) arm = BracketThickness;

            // 横の腕は、いまの幅の半分を超えない（閉じているあいだは縦棒だけになる）
            float armX = Mathf.Max(BracketThickness, Mathf.Min(arm, bracketFrame.width * 0.5f));
            float armY = arm;

            Color bracketColor = blinkOn ? Flash : (tickOpen ? GoldBright : Gold);

            // 1) 辺の暗い帯と、その上を流れる点線
            DrawRailsAndDashes(vh, frame, armX, armY, t);

            // 2) 波紋（落ち着いてから）
            if (settled) DrawRipple(vh, frame, idle);

            // 3) 四隅のかぎ形。**黒い縁を先に全部描いてから、金色を重ねる。**
            //    1つずつ「縁→金」と描くと、あとから描いた縁が隣の金色を欠けさせる
            DrawBrackets(vh, bracketFrame, armX, armY, Dot, Outline);
            DrawBrackets(vh, bracketFrame, armX, armY, 0f, bracketColor);
        }

        private void DrawRailsAndDashes(VertexHelper vh, Rect f, float armX, float armY, float t)
        {
            // 帯は、かぎ形の太さの真ん中の1ドットを通す
            float inset = Dot;
            float railHalf = BracketThickness * 0.5f;

            float left = f.xMin + armX;
            float right = f.xMax - armX;
            float bottom = f.yMin + armY;
            float top = f.yMax - armY;

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
        private void DrawBrackets(VertexHelper vh, Rect f, float armX, float armY, float grow, Color c)
        {
            float t = BracketThickness;

            // 左下
            Quad(vh, f.xMin - grow, f.yMin - grow, f.xMin + armX + grow, f.yMin + t + grow, c);
            Quad(vh, f.xMin - grow, f.yMin - grow, f.xMin + t + grow, f.yMin + armY + grow, c);
            // 右下
            Quad(vh, f.xMax - armX - grow, f.yMin - grow, f.xMax + grow, f.yMin + t + grow, c);
            Quad(vh, f.xMax - t - grow, f.yMin - grow, f.xMax + grow, f.yMin + armY + grow, c);
            // 左上
            Quad(vh, f.xMin - grow, f.yMax - t - grow, f.xMin + armX + grow, f.yMax + grow, c);
            Quad(vh, f.xMin - grow, f.yMax - armY - grow, f.xMin + t + grow, f.yMax + grow, c);
            // 右上
            Quad(vh, f.xMax - armX - grow, f.yMax - t - grow, f.xMax + grow, f.yMax + grow, c);
            Quad(vh, f.xMax - t - grow, f.yMax - armY - grow, f.xMax + grow, f.yMax + grow, c);
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
