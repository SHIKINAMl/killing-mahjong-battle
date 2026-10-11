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
    /// 強襲を使ったあとの演出（2026-10-11 に、短い攻撃の演出へ作り直した）。
    ///
    /// **ユーザーの指示:** それまでの版（ベルルとの契約。血を抜く → 板に血判 → 札が貼り付く、約4秒）は不評。
    /// 「長い・くどい」「ベルルや契約の筋立てが合わない」の両方。**短く、攻撃らしく。ベルルは出さない。**
    /// 相手の血の表示に札（アイコン）が残ること自体は不評ではないので、そこは残す（<see cref="AssaultMarkUI"/>）。
    ///
    /// 流れ（白く光るまで約1.3秒）
    ///   ① 暗転   … 画面が落ち、自分と相手の血の表示だけが残る（牌交換と同じ暗転）
    ///   ② 溜める … 自分の血の表示から血が湧いて、玉になる。心音が1つ。撃つ前に一瞬だけ縮む
    ///   ③ 撃つ   … 血の玉が筋を引いて、相手の血の表示へ一直線に飛ぶ
    ///   ④ 当たる … 画面が揺れ、しぶきと輪が散る。札が大きい所から叩きつけられて貼り付く。相手が跳ねる
    ///   ⑤ 解除   … 白く光って暗転が消える
    ///
    /// これまでの版:
    ///   1つ目 … 刻む動きと照準の線。「雰囲気が合っていない」
    ///   2つ目 … 透視の舞台（赤い画面の重ね・集中線）。「透視には合うが、役強化と強襲には合わない」
    ///   3つ目 … 牌交換と同じ暗転の上で、自分の血を3粒抜いて相手へ撃ち込む
    ///   4つ目 … ベルルとの追加の契約（血判・差し押さえの札）。不評
    ///
    /// シーンには置かない。出しているのは血の表示の写しと図形だけ。
    /// </summary>
    public class AssaultSkillEffect : MonoBehaviour
    {
        // 牌交換（MulliganSwapAnimator）と同じ濃さ。色は黒に少しだけ赤みを入れた
        private static readonly Color DimColor = new Color(0.07f, 0f, 0.015f, 0.85f);
        private const float DimSeconds = 0.20f;

        private const float ChargeSeconds = 0.42f;    // 血が湧いて玉になる
        private const float ChargeRadius = 26f;       // 溜まりきった玉の大きさ
        private const float ShotSeconds = 0.16f;      // 相手へ飛ぶ。速く
        private const float ShotRadius = 14f;         // 飛んでいる玉の大きさ
        private const float SlamSeconds = 0.12f;      // 札が叩きつけられる
        private const float HoldBeforeFlash = 0.40f;  // 当たったあと、光るまで

        private const int SplashCount = 10;

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

        /// <param name="targetHpAnchor">狙われる側の血の表示。血はここへ当たり、札が貼り付いて印が残る</param>
        /// <param name="casterHpAnchor">撃った側の血の表示。血はここから湧く</param>
        /// <param name="onHit">当たった瞬間に呼ぶ（相手の立ち絵を跳ねさせる、など）</param>
        /// <param name="demonPowered">使っていない（ベルルを出していた版の名残。呼ぶ側を変えないために残してある）</param>
        public IEnumerator Play(RectTransform targetHpAnchor, RectTransform casterHpAnchor, Action onHit,
            Sprite demonPowered = null)
        {
            _trance = SkillTranceAudio.Begin(DimSeconds);

            _stage = new SkillEffectStage("Stage", UISortingOrders.SkillEffectContent, transform);
            Image dim = _stage.AddDim("Dim", Color.clear);

            // 暗転の上に、血の表示だけを浮かせる。どこから撃って、どこへ当たるのかが見えるように
            Image casterCopy = _stage.AddCopyOf("CasterHp", casterHpAnchor);
            Image targetCopy = _stage.AddCopyOf("TargetHp", targetHpAnchor);
            Color casterColor = casterCopy != null ? casterCopy.color : Color.white;
            Color targetColor = targetCopy != null ? targetCopy.color : Color.white;

            var shapes = _stage.AddShapes("Blood");

            Rect caster = _stage.LocalVisibleRectOf(casterHpAnchor);
            Vector2 from = caster.width > 1f ? caster.center : new Vector2(300f, -120f);
            Vector2 to = AssaultMarkUI.MarkPosition(_stage, targetHpAnchor);
            // 玉は、自分の血の表示から相手のほうへ少し出た所に溜まる
            Vector2 aim = (to - from).sqrMagnitude > 1f ? (to - from).normalized : Vector2.left;
            Vector2 muzzle = from + aim * 46f;

            var audio = AudioManager.Instance;

            // ---- ① 暗転 ----
            for (float t = 0f; t < DimSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Sin(Mathf.Clamp01(t / DimSeconds) * Mathf.PI * 0.5f);
                SetStage(dim, casterCopy, casterColor, targetCopy, targetColor, p);
                yield return null;
            }
            SetStage(dim, casterCopy, casterColor, targetCopy, targetColor, 1f);

            // ---- ② 溜める。血が湧いて玉になり、撃つ前に一瞬だけ縮む ----
            if (audio != null) audio.PlayHeartbeat(HeartbeatStrength.Strong, HeartbeatSpacing.Compact);
            for (float t = 0f; t < ChargeSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / ChargeSeconds);
                // 前の8割でふくらみ、残りで縮む（引き金を引く溜め）
                float r = p < 0.8f
                    ? Mathf.Lerp(0f, ChargeRadius, Mathf.Sin(p / 0.8f * Mathf.PI * 0.5f))
                    : Mathf.Lerp(ChargeRadius, ShotRadius, (p - 0.8f) / 0.2f);
                shapes.Begin();
                // 血の表示から玉へ、粒が吸い寄せられる
                for (int k = 0; k < 5 && p <= 0.8f; k++)
                {
                    float u = Mathf.Repeat(p * 2.2f + k * 0.2f, 1f);
                    Vector2 side = new Vector2(-aim.y, aim.x) * Mathf.Sin(k * 2.1f) * 18f * (1f - u);
                    AssaultMarkUI.DrawDrop(shapes, Vector2.Lerp(from, muzzle, u) + side, 5f * (1f - u * 0.5f), 0.85f);
                }
                // 締まっていく輪。狙いを付ける間
                Color close = AssaultMarkUI.BloodShine;
                close.a = p * 0.8f;
                shapes.Ring(muzzle, Mathf.Lerp(70f, ChargeRadius + 6f, Mathf.SmoothStep(0f, 1f, p)), 3f, close);
                AssaultMarkUI.DrawDrop(shapes, muzzle, r, 1f);
                shapes.End();
                yield return null;
            }

            // ---- ③ 撃つ。筋を引いて、相手へ一直線 ----
            if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Noise, 620f, 180f, 0.16f, 0.9f);
            ScreenQuake.Play(5f, 0.1f);                       // 撃った反動
            for (float t = 0f; t < ShotSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / ShotSeconds);
                shapes.Begin();
                DrawStreak(shapes, muzzle, to, p, ShotRadius, ShotRadius * 0.8f, 1f);
                shapes.End();
                yield return null;
            }

            // ---- ④ 当たる。相手が跳ね、札が叩きつけられる。印が残る ----
            if (onHit != null) onHit();
            ScreenQuake.Play(14f, 0.22f);
            if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Noise, 200f, 60f, 0.24f, 1.0f);
            AssaultMarkUI.Show(targetHpAnchor);

            // 当たった所から輪が2つ広がり、しぶきが散る。札は大きい所から叩きつけられる。
            // 印（AssaultMarkUI）は暗転より奥に居て、白く光るまで見えない。そのあいだはここで同じ札を描く
            for (float t = 0f; t < HoldBeforeFlash; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / 0.32f);
                float slam = Mathf.Clamp01(t / SlamSeconds);

                shapes.Begin();
                for (int k = 0; k < 2; k++)
                {
                    float q = Mathf.Clamp01(p - k * 0.25f);
                    if (q <= 0f) continue;
                    Color c = Color.Lerp(AssaultMarkUI.BloodShine, AssaultMarkUI.Blood, q);
                    c.a = 1f - q;
                    shapes.Ring(to, AssaultMarkUI.Radius + 6f + q * 58f, 5f, c);
                }

                for (int k = 0; k < SplashCount; k++)
                {
                    float a = (k + 0.5f) * Mathf.PI * 2f / SplashCount;
                    float reach = (k % 2 == 0 ? 58f : 38f) * Mathf.Sin(p * Mathf.PI * 0.5f);
                    // 撃ってきた向きの先へ多く飛ぶ
                    Vector2 at = to + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach + aim * 16f * p;
                    at.y -= 30f * p * p;                      // 少し垂れる
                    AssaultMarkUI.DrawDrop(shapes, at, 5f * (1f - p), 1f - p);
                }

                AssaultMarkUI.DrawTag(shapes, to, Mathf.Lerp(2.6f, 1f, slam * slam), 1f);
                shapes.End();
                yield return null;
            }

            // ---- ⑤ 白く光って暗転が消える。心音と BGM の戻りは待たない ----
            Action clear = () =>
            {
                if (this == null) return;
                SetStage(dim, casterCopy, casterColor, targetCopy, targetColor, 0f);
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
    }
}
