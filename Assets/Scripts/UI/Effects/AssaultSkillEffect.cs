using System;
using System.Collections;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 強襲を使ったあとの演出（2026-10-09 に作り直した）。
    ///
    /// **自分の血を抜いて、覚悟を決める**（ユーザーの指示）。
    /// 絵の作りは透視・牌交換に合わせてある。最初の版は、刻む動きと照準の線
    /// （四方から赤い線が絞り込む）で、「雰囲気が合っていない」と言われた。
    ///
    ///   舞台 … 透視のものを、赤い調子で借りる（<see cref="PerspectiveSkillEffect"/>）。
    ///           その瞬間の画面を暗い赤にして周りに重ね、暗く落とし、集中線を**血が集まる所**へ集める。
    ///           BGM が水の中のように沈み、終わりに白く光って、心音とともに戻る
    ///   中身 … 血の粒。自分の血の表示から1つずつ抜けて、画面の真ん中に集まる
    ///           （透視で牌を1枚ずつ返すのと同じ間合い。1つごとに心音）。
    ///           集まった血がぎゅっと縮み、相手の血の表示へ撃ち込まれて、印になって残る
    ///
    /// 流れ（約3.6秒。うち白く光るまでが約3.0秒）
    ///   ① 集中     … 赤い舞台が出る
    ///   ② 血を抜く … 血の粒が3つ、自分の血の表示から真ん中へ。1つごとに心音
    ///   ③ 覚悟     … 集まった血が脈を打ち、外から輪が締まってきて、縮む
    ///   ④ 撃ち込む … 筋を引いて相手の血の表示へ飛び、弾けて相手が跳ねる。印が残る（<see cref="AssaultMarkUI"/>）
    ///   ⑤ 解除     … 白く光って舞台が消える
    ///
    /// シーンには置かない。出しているのは実機の画面の複製と、図形（丸）だけ。
    /// </summary>
    public class AssaultSkillEffect : MonoBehaviour
    {
        // 透視（ExposedTileEffectPlayer）と同じ間合い
        private const float DropGap = 0.28f;
        private const float HoldBeforeFlash = 0.45f;

        private const int DropCount = 3;
        private const float DropFlySeconds = 0.40f;
        private const float DropRadius = 10f;

        /// <summary>集まった血の大きさ。粒が1つ届くごとに1段ふくらむ。</summary>
        private static readonly float[] PoolRadius = { 0f, 16f, 23f, 30f };

        private const float ResolveSeconds = 0.60f;   // 脈を打って縮む
        private const float ShootSeconds = 0.20f;     // 相手へ飛ぶ
        private const float ResolvedScale = 0.6f;     // 縮みきった大きさ（集まった血に対して）

        /// <summary>血の粒の後ろ（集中線の集まる先）を暗くする量。</summary>
        private const float FocusDim = 0.6f;

        private PerspectiveSkillEffect _backdrop;
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
            // 舞台は別の入れ物なので、一緒に消す（残すと画面が赤いまま・音が沈んだままになる）
            if (_backdrop != null) _backdrop.Dispose();
            _backdrop = null;
        }

        /// <param name="targetHpAnchor">狙われる側の血の表示。血はここへ撃ち込まれ、印が残る</param>
        /// <param name="casterHpAnchor">撃った側の血の表示。血の粒はここから抜ける</param>
        /// <param name="onHit">血が当たった瞬間に呼ぶ（相手の立ち絵を跳ねさせる、など）</param>
        public IEnumerator Play(RectTransform targetHpAnchor, RectTransform casterHpAnchor, Action onHit)
        {
            // 位置を測るための入れ物。まだ何も描かないので、舞台が撮る画面には写らない
            _stage = new SkillEffectStage("Content", UISortingOrders.SkillEffectContent, transform);
            var shapes = _stage.AddShapes("Blood");

            Rect caster = _stage.LocalVisibleRectOf(casterHpAnchor);
            Vector2 from = caster.width > 1f ? caster.center : new Vector2(300f, -120f);
            // 血が集まる所。画面の真ん中から少し自分寄り（相手へ撃ち込む距離を取るため）
            Vector2 pool = new Vector2(40f, -30f);
            Vector2 to = AssaultMarkUI.MarkPosition(_stage, targetHpAnchor);

            // ---- ① 集中。舞台は透視と同じで、色だけ赤。集中線は血が集まる所へ集める ----
            // 最初は自分の血の表示へ集めていたが、画面の右端に寄って、主役の血の粒が暗がりに沈んだ
            float unit = Screen.width / Mathf.Max(1f, _stage.Rect.rect.width);
            const float focusHalfW = 130f, focusHalfH = 50f;
            var focus = new Rect(Screen.width * 0.5f + (pool.x - focusHalfW) * unit,
                                 Screen.height * 0.5f + (pool.y - focusHalfH) * unit,
                                 focusHalfW * 2f * unit, focusHalfH * 2f * unit);
            _backdrop = PerspectiveSkillEffect.Create(PerspectiveSkillEffect.Tone.Red);
            if (_backdrop != null)
            {
                _backdrop.FocusDim = FocusDim;
                yield return _backdrop.Enter(focus);
            }

            var audio = AudioManager.Instance;

            // ---- ② 血を抜く。1つずつ、心音と一緒に ----
            // 粒は山なりに飛ぶ。飛び出す時刻は等間隔（透視で牌を返す間合いと同じ）
            float drawSeconds = DropGap * (DropCount - 1) + DropFlySeconds;
            int beats = 0;
            float poolPop = 0f;      // 粒が届いた瞬間のふくらみ（1 → 0 へ戻る）
            int arrived = 0;
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

            // ---- ③ 覚悟。脈を1つ打って、ぎゅっと縮む ----
            if (audio != null) audio.PlayHeartbeat(HeartbeatStrength.Strong, HeartbeatSpacing.Compact);
            float full = PoolRadius[DropCount];
            for (float t = 0f; t < ResolveSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / ResolveSeconds);
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

            // ---- ④ 撃ち込む ----
            float small = full * ResolvedScale;
            if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Noise, 240f, 70f, 0.22f, 0.9f);
            for (float t = 0f; t < ShootSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / ShootSeconds);
                float eased = p * Mathf.Sqrt(p);             // 出だしは溜めて、一気に届く
                shapes.Begin();
                DrawStreak(shapes, pool, to, eased, small, 1f);
                shapes.End();
                yield return null;
            }

            // 当たった。相手が跳ね、印が残る
            if (onHit != null) onHit();
            ScreenQuake.Play(10f, 0.2f);
            AssaultMarkUI.Show(targetHpAnchor);

            // 着弾。白く弾けて、輪が2つ広がり、しぶきが散る。
            // 印（AssaultMarkUI）は舞台より奥に居て、白く光るまで見えない。そのあいだはここで同じ粒を描く
            for (float t = 0f; t < HoldBeforeFlash; t += Time.deltaTime)
            {
                float p = Mathf.Clamp01(t / 0.35f);
                shapes.Begin();

                // 撃った跡の筋が、少しだけ残って消える
                float trail = 1f - Mathf.Clamp01(t / 0.12f);
                if (trail > 0f) DrawStreak(shapes, pool, to, 1f, small, trail);

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

                float burst = 1f - Mathf.Clamp01(t / 0.14f);
                if (burst > 0f)
                {
                    Color white = Color.white;
                    white.a = burst;
                    shapes.Disc(to, AssaultMarkUI.Radius + 20f * burst, white);
                }

                AssaultMarkUI.DrawDrop(shapes, to, AssaultMarkUI.Radius, 1f);
                shapes.End();
                yield return null;
            }
            shapes.Begin();
            shapes.End();

            // ---- ⑤ 白く光って舞台が消える。心音と BGM の戻りは待たない ----
            if (_backdrop != null)
            {
                var backdrop = _backdrop;
                _backdrop = null;          // ここから先は舞台が自分で片付く
                yield return backdrop.ReleaseQuick();
            }
            else
            {
                ScreenFlash.Play();
            }

            Dispose();
        }

        private const int SplashCount = 8;

        /// <summary>
        /// 撃ち込む血。頭の粒と、その後ろに伸びる筋。<paramref name="head"/> は 0（集まった所）〜 1（相手）。
        /// 筋は粒を詰めて並べて作る。後ろほど細く薄い。
        /// </summary>
        private static void DrawStreak(PixelShapeGraphic g, Vector2 from, Vector2 to, float head, float startRadius, float alpha)
        {
            const int steps = 12;
            const float length = 0.55f;                       // 筋の長さ（道のりに対して）
            float headRadius = Mathf.Lerp(startRadius, AssaultMarkUI.Radius, head);
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
