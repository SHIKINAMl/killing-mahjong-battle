using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 強襲を使ったあとの演出（2026-10-10 に、背景を決めて作り直した）。
    ///
    /// **強襲の背景（ユーザーと決めた）:**
    ///   強襲は、アガったときに自分がもらえるはずだった血が0になり、同じ額が相手の損失に上乗せされる能力
    ///   （撃った時点では何も起きない。血3000を払う。1局1回）。
    ///   これは**ベルル（ギャンブルの悪魔）との追加の契約**。
    ///   「この局、私は配当を受け取らない。その分をあいつから余計に取り立てろ」。
    ///   血3000はその手数料で、自分の血で契約に判を押す。相手に残るのは、ベルルの差し押さえの札。
    ///   アガれなければ手数料だけ取られる。
    ///
    /// 流れ（約4秒。うち白く光るまでが約3.4秒）
    ///   ① 暗転     … 画面が落ち、自分と相手の血の表示だけが残る（牌交換と同じ暗転）
    ///   ② 血を抜く … 血の粒が3つ、自分の血の表示から真ん中へ。1つごとに心音。
    ///                 そのあいだに、上に黄色い輪が描かれてベルルが出てくる
    ///   ③ 契約     … ベルルの板に「取り立て」の字。集まった血が脈を打ち、輪が締まって縮む（覚悟）
    ///   ④ 血判     … 血が板へ飛んで、判になる。ベルルの目が光る
    ///   ⑤ 差し押さえ … 板から札が飛んで、相手の血の表示に貼り付く。相手が跳ねる。印が残る（<see cref="AssaultMarkUI"/>）
    ///   ⑥ 解除     … ベルルは輪へ帰り、白く光って暗転が消える
    ///
    /// これまでの版:
    ///   1つ目 … 刻む動きと照準の線。「雰囲気が合っていない」
    ///   2つ目 … 透視の舞台（赤い画面の重ね・集中線）。「透視には合うが、役強化と強襲には合わない」
    ///   3つ目 … 牌交換と同じ暗転の上で、自分の血を抜いて相手へ撃ち込む。背景が無かった
    ///
    /// ベルルの絵が読めないときは③の字と④を飛ばし、血は集まった所から札になって相手へ飛ぶ。
    /// シーンには置かない。出しているのは血の表示の写し、ベルルの絵（もとからある物）、図形、文字だけ。
    /// </summary>
    public class AssaultSkillEffect : MonoBehaviour
    {
        // 牌交換（MulliganSwapAnimator）と同じ濃さ。色は黒に少しだけ赤みを入れた
        private static readonly Color DimColor = new Color(0.07f, 0f, 0.015f, 0.85f);
        private const float DimSeconds = 0.30f;

        // 透視（ExposedTileEffectPlayer）と同じ間合い
        private const float DropGap = 0.28f;
        private const float HoldBeforeFlash = 0.45f;
        private const float PopSeconds = 0.15f;

        private const int DropCount = 3;
        private const float DropFlySeconds = 0.40f;
        private const float DropRadius = 10f;

        /// <summary>集まった血の大きさ。粒が1つ届くごとに1段ふくらむ。</summary>
        private static readonly float[] PoolRadius = { 0f, 16f, 23f, 30f };

        private const float ResolveSeconds = 0.60f;   // 脈を打って縮む
        private const float ResolvedScale = 0.6f;     // 縮みきった大きさ（集まった血に対して）
        private const float StampFlySeconds = 0.22f;  // 血が板へ飛ぶ
        private const float StampSettleSeconds = 0.30f;
        private const float TagFlySeconds = 0.22f;    // 札が相手へ飛ぶ
        private const float BelleLeaveSeconds = 0.35f;

        /// <summary>板に押した血判の大きさ。</summary>
        private const float SealRadius = 14f;

        private const int SplashCount = 8;

        // ベルル。置き場所と大きさは役強化（BoostHandSkillEffect）と同じ
        private static readonly Vector2 BelleCenter = new Vector2(0f, 150f);
        private const float BelleScale = 0.55f;

        private static readonly Color ClauseColor = new Color32(0xF2, 0xE6, 0xC8, 0xFF);

        private SkillTranceAudio _trance;
        private SkillEffectStage _stage;

        public static AssaultSkillEffect Create()
        {
            if (!Application.isPlaying) return null;
            var go = new GameObject("AssaultSkillEffect");
            return go.AddComponent<AssaultSkillEffect>();
        }

        public void Dispose()
        {
            if (this == null || gameObject == null) return;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 途中で打ち切られたら、沈めた音はここで戻す（残すと音が沈んだままになる）
            if (_trance != null && !_trance.IsReleased) _trance.Dispose();
            _trance = null;
        }

        /// <param name="targetHpAnchor">狙われる側の血の表示。札はここへ貼り付き、印が残る</param>
        /// <param name="casterHpAnchor">撃った側の血の表示。血の粒はここから抜ける</param>
        /// <param name="onHit">札が貼り付いた瞬間に呼ぶ（相手の立ち絵を跳ねさせる、など）</param>
        /// <param name="demonPowered">
        /// 目が光っているベルルの絵（自分のスキルのカットインの絵。<c>PhaseTransitionUI.PlayerCutinSprite</c>）。
        /// 無ければ目は光らず、構えた絵のままで進む
        /// </param>
        public IEnumerator Play(RectTransform targetHpAnchor, RectTransform casterHpAnchor, Action onHit,
            Sprite demonPowered = null)
        {
            _trance = SkillTranceAudio.Begin(DimSeconds);

            _stage = new SkillEffectStage("Stage", UISortingOrders.SkillEffectContent, transform);
            Image dim = _stage.AddDim("Dim", Color.clear);

            // 暗転の上に、血の表示だけを浮かせる。どこから抜いて、どこへ貼るのかが見えるように
            Image casterCopy = _stage.AddCopyOf("CasterHp", casterHpAnchor);
            Image targetCopy = _stage.AddCopyOf("TargetHp", targetHpAnchor);
            Color casterColor = casterCopy != null ? casterCopy.color : Color.white;
            Color targetColor = targetCopy != null ? targetCopy.color : Color.white;

            BelleFigure belle = BelleFigure.Create(_stage, BelleCenter, BelleScale, demonPowered);

            // ベルルの板に出す、契約の中身
            TextMeshProUGUI clause = null;
            if (belle != null)
            {
                clause = _stage.AddText("Clause", "取り立て", 30f, ClauseColor);
                clause.fontStyle = FontStyles.Bold;
                clause.alpha = 0f;
                clause.rectTransform.anchoredPosition = belle.SignPosition + new Vector2(-16f, 0f);
            }

            var shapes = _stage.AddShapes("Blood");

            Rect caster = _stage.LocalVisibleRectOf(casterHpAnchor);
            Vector2 from = caster.width > 1f ? caster.center : new Vector2(300f, -120f);
            // 血が集まる所。画面の真ん中から少し自分寄り
            Vector2 pool = new Vector2(40f, -40f);
            Vector2 to = AssaultMarkUI.MarkPosition(_stage, targetHpAnchor);
            // 血判を押す所（板の字の右下。書類に判を押すのと同じ位置）。ベルルが居なければ、集まった所
            Vector2 sealAt = belle != null ? belle.SignPosition + new Vector2(56f, -6f) : pool;

            var audio = AudioManager.Instance;

            // ---- ① 暗転 ----
            for (float t = 0f; t < DimSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Sin(Mathf.Clamp01(t / DimSeconds) * Mathf.PI * 0.5f);
                SetStage(dim, casterCopy, casterColor, targetCopy, targetColor, p);
                yield return null;
            }
            SetStage(dim, casterCopy, casterColor, targetCopy, targetColor, 1f);

            // ---- ② 血を抜く。1つずつ、心音と一緒に。途中からベルルが出てくる ----
            float drawSeconds = DropGap * (DropCount - 1) + DropFlySeconds;
            int beats = 0;
            float poolPop = 0f;      // 粒が届いた瞬間のふくらみ（1 → 0 へ戻る）
            int arrived = 0;
            bool belleCalled = false;
            for (float t = 0f; t < drawSeconds; t += Time.deltaTime)
            {
                // 心音は粒が出る瞬間に。3つ目だけ強く
                while (beats < DropCount && t >= beats * DropGap)
                {
                    if (audio != null)
                    {
                        audio.PlayHeartbeat(beats == DropCount - 1 ? HeartbeatStrength.Strong : HeartbeatStrength.Medium,
                                            HeartbeatSpacing.Compact);
                    }
                    beats++;
                }

                // 2つ目の粒が出るころに呼ぶ。血が集まりきるころに、板を持って構えている
                if (belle != null && !belleCalled && t >= DropGap)
                {
                    belleCalled = true;
                    if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 880f, 1320f, 0.25f, 0.35f);
                    StartCoroutine(belle.Arrive());
                }

                int nowArrived = 0;
                shapes.Begin();
                for (int i = 0; i < DropCount; i++)
                {
                    float u = (t - i * DropGap) / DropFlySeconds;
                    if (u >= 1f) { nowArrived++; continue; }
                    if (u < 0f) continue;
                    DrawFlyingDrop(shapes, from, pool, u, i);
                }
                if (nowArrived > arrived) { arrived = nowArrived; poolPop = 1f; }
                poolPop = Mathf.MoveTowards(poolPop, 0f, Time.deltaTime / 0.15f);

                // 集まった血。届いた瞬間に一度ふくらむ（透視で牌を返した瞬間と同じ）
                float r = PoolRadius[Mathf.Clamp(arrived, 0, DropCount)] * (1f + 0.3f * Mathf.PingPong(poolPop * 2f, 1f));
                AssaultMarkUI.DrawDrop(shapes, pool, r, 1f);
                shapes.End();
                yield return null;
            }

            float full = PoolRadius[DropCount];
            if (belle != null)
            {
                // ベルルが構えきるのを待つ（ふつうはもう構えている）
                float giveUp = Time.time + 1f;
                while (!belle.Arrived && Time.time < giveUp)
                {
                    shapes.Begin();
                    AssaultMarkUI.DrawDrop(shapes, pool, full, 1f);
                    shapes.End();
                    yield return null;
                }
            }

            // ---- ③ 契約。板に字が出る。血が脈を1つ打って、ぎゅっと縮む（覚悟）----
            if (audio != null) audio.PlayHeartbeat(HeartbeatStrength.Strong, HeartbeatSpacing.Compact);
            for (float t = 0f; t < ResolveSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / ResolveSeconds);
                if (clause != null) clause.alpha = Mathf.Clamp01(t / 0.2f);

                // 前半でいったん大きく脈打ち、後半で小さく固まる
                float r = p < 0.35f
                    ? Mathf.Lerp(full, full * 1.25f, Mathf.Sin(p / 0.35f * Mathf.PI))
                    : Mathf.Lerp(full, full * ResolvedScale, Mathf.SmoothStep(0f, 1f, (p - 0.35f) / 0.65f));
                shapes.Begin();
                // 外から輪が締まってくる。腹を決める間
                Color close = AssaultMarkUI.BloodShine;
                close.a = p * 0.9f;
                shapes.Ring(pool, Mathf.Lerp(110f, full * ResolvedScale + 6f, Mathf.SmoothStep(0f, 1f, p)), 4f, close);
                AssaultMarkUI.DrawDrop(shapes, pool, r, 1f);
                shapes.End();
                yield return null;
            }
            if (clause != null) clause.alpha = 1f;

            float small = full * ResolvedScale;
            if (belle != null)
            {
                // ---- ④ 血判。血が板へ飛んで、判になる。ベルルの目が光る ----
                for (float t = 0f; t < StampFlySeconds; t += Time.deltaTime)
                {
                    float p = Mathf.Clamp01(t / StampFlySeconds);
                    float eased = p * Mathf.Sqrt(p);             // 出だしは溜めて、一気に届く
                    shapes.Begin();
                    DrawStreak(shapes, pool, sealAt, eased, small, SealRadius, 1f);
                    shapes.End();
                    yield return null;
                }

                belle.PowerOn();
                ScreenQuake.Play(8f, 0.18f);
                if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Noise, 240f, 70f, 0.22f, 0.9f);
                for (float t = 0f; t < StampSettleSeconds; t += Time.deltaTime)
                {
                    // 押した瞬間は大きく、すぐ落ち着く。ベルルも一度ふくらみ、目の光が輪になって広がる
                    float pop = t < PopSeconds ? Mathf.PingPong(t * (1f / (PopSeconds / 2f)), 1f) : 0f;
                    belle.SetScale(Mathf.Lerp(1f, 1.12f, pop));

                    float q = Mathf.Clamp01(t / StampSettleSeconds);
                    Color ring = BelleFigure.Yellow;
                    ring.a = (1f - q) * 0.9f;
                    shapes.Begin();
                    shapes.Ring(belle.Center, 70f + q * 150f, 4f, ring);
                    AssaultMarkUI.DrawSeal(shapes, sealAt, SealRadius * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(t / 0.1f)), 1f);
                    shapes.End();
                    yield return null;
                }
                belle.SetScale(1f);
                shapes.Begin();
                AssaultMarkUI.DrawSeal(shapes, sealAt, SealRadius, 1f);
                shapes.End();

                yield return new WaitForSeconds(0.2f);
            }

            // ---- ⑤ 差し押さえ。札が相手の血の表示へ飛んで、貼り付く ----
            if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 520f, 260f, 0.2f, 0.5f);
            for (float t = 0f; t < TagFlySeconds; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / TagFlySeconds);
                float eased = p * Mathf.Sqrt(p);
                Vector2 at = Vector2.Lerp(sealAt, to, eased);
                shapes.Begin();
                if (belle != null) AssaultMarkUI.DrawSeal(shapes, sealAt, SealRadius, 1f);
                else AssaultMarkUI.DrawDrop(shapes, pool, small * (1f - p), 1f - p);
                // 後ろに血の粒を引く
                for (int k = 3; k >= 1; k--)
                {
                    float back = eased - k * 0.08f;
                    if (back <= 0f) continue;
                    AssaultMarkUI.DrawDrop(shapes, Vector2.Lerp(sealAt, to, back), 5f - k, 0.7f - k * 0.15f);
                }
                AssaultMarkUI.DrawTag(shapes, at, Mathf.Lerp(1.8f, 1f, eased), 1f);
                shapes.End();
                yield return null;
            }

            // 貼り付いた。相手が跳ね、印が残る
            if (onHit != null) onHit();
            ScreenQuake.Play(10f, 0.2f);
            AssaultMarkUI.Show(targetHpAnchor);

            // ベルルは輪へ帰る。板の字と判は、板が畳まれる前に消す
            if (belle != null) StartCoroutine(belle.Leave(BelleLeaveSeconds));

            // 貼り付いた所から輪が2つ広がり、しぶきが散る。
            // 印（AssaultMarkUI）は暗転より奥に居て、白く光るまで見えない。そのあいだはここで同じ札を描く
            for (float t = 0f; t < HoldBeforeFlash; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / 0.35f);
                float boardFade = 1f - Mathf.Clamp01(t / 0.1f);
                if (clause != null) clause.alpha = boardFade;

                shapes.Begin();
                if (belle != null && boardFade > 0f) AssaultMarkUI.DrawSeal(shapes, sealAt, SealRadius, boardFade);

                for (int k = 0; k < 2; k++)
                {
                    float q = Mathf.Clamp01(p - k * 0.25f);
                    if (q <= 0f) continue;
                    Color c = Color.Lerp(AssaultMarkUI.BloodShine, AssaultMarkUI.Blood, q);
                    c.a = 1f - q;
                    shapes.Ring(to, AssaultMarkUI.Radius + 6f + q * 46f, 4f, c);
                }

                for (int k = 0; k < SplashCount; k++)
                {
                    float a = (k + 0.5f) * Mathf.PI * 2f / SplashCount;
                    float reach = (k % 2 == 0 ? 44f : 30f) * Mathf.Sin(p * Mathf.PI * 0.5f);
                    Vector2 at = to + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
                    at.y -= 26f * p * p;                      // 少し垂れる
                    AssaultMarkUI.DrawDrop(shapes, at, 4f * (1f - p), 1f - p);
                }

                AssaultMarkUI.DrawTag(shapes, to, 1f, 1f);
                shapes.End();
                yield return null;
            }

            // ---- ⑥ 白く光って暗転が消える。心音と BGM の戻りは待たない ----
            Action clear = () =>
            {
                if (this == null) return;
                SetStage(dim, casterCopy, casterColor, targetCopy, targetColor, 0f);
                if (belle != null) belle.Hide();
                if (clause != null) clause.alpha = 0f;
                shapes.Begin();
                shapes.End();
            };
            if (_trance != null)
            {
                var trance = _trance;
                _trance = null;            // ここから先は音の部品が自分で鳴らしきる
                trance.ReleaseDetached(clear);
            }
            else
            {
                ScreenFlash.Play();
            }
            yield return new WaitForSeconds(0.12f);
            clear();

            Dispose();
        }

        /// <summary>暗転と、その上に浮かせた血の表示の濃さをまとめて決める。</summary>
        private static void SetStage(Image dim, Image casterCopy, Color casterColor, Image targetCopy, Color targetColor, float strength)
        {
            if (dim != null)
            {
                Color c = DimColor;
                c.a *= strength;
                dim.color = c;
            }
            if (casterCopy != null) { casterColor.a *= strength; casterCopy.color = casterColor; }
            if (targetCopy != null) { targetColor.a *= strength; targetCopy.color = targetColor; }
        }

        /// <summary>
        /// 飛んでいく血。頭の粒と、その後ろに伸びる筋。<paramref name="head"/> は 0（出発）〜 1（到着）。
        /// 筋は粒を詰めて並べて作る。後ろほど細く薄い。
        /// </summary>
        private static void DrawStreak(PixelShapeGraphic g, Vector2 from, Vector2 to, float head,
            float startRadius, float endRadius, float alpha)
        {
            const int steps = 12;
            const float length = 0.55f;                       // 筋の長さ（道のりに対して）
            float headRadius = Mathf.Lerp(startRadius, endRadius, head);
            for (int k = steps; k >= 1; k--)
            {
                float back = head - length * k / steps;
                if (back < 0f) continue;
                float thin = 1f - (float)k / (steps + 1);
                AssaultMarkUI.DrawDrop(g, Vector2.Lerp(from, to, back), headRadius * (0.35f + 0.6f * thin), alpha * (0.25f + 0.6f * thin));
            }
            AssaultMarkUI.DrawDrop(g, Vector2.Lerp(from, to, head), headRadius, alpha);
        }

        /// <summary>自分の血の表示から、集まる所へ山なりに飛ぶ粒。後ろに小さな尾を引く。</summary>
        private static void DrawFlyingDrop(PixelShapeGraphic g, Vector2 from, Vector2 to, float u, int index)
        {
            // 粒ごとに山の高さを変える（3つが同じ線をなぞると1つに見える）
            float arc = 70f + index * 26f;
            for (int k = 3; k >= 0; k--)
            {
                float v = Mathf.Clamp01(u - k * 0.06f);
                float eased = Mathf.SmoothStep(0f, 1f, v);
                Vector2 p = Vector2.Lerp(from, to, eased);
                p.y += arc * 4f * eased * (1f - eased);
                float alpha = k == 0 ? 1f : 0.55f - k * 0.13f;
                AssaultMarkUI.DrawDrop(g, p, DropRadius * (1f - k * 0.17f), alpha);
            }
        }
    }
}
