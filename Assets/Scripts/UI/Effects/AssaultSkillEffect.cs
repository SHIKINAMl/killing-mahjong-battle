using System;
using System.Collections;
using TMPro;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 強襲を使ったあとの演出（2026-10-08）。
    ///
    /// **それまで、強襲はカットインが出て血が減るだけだった。** 撃った手応えが無く、
    /// 効果が出るのは何巡も先なので、撃ったことを忘れる（ユーザーの指摘）。
    /// 案は Antigravity と出し合い、「相手の血に照準を合わせて固定する」に決めた
    /// （km-docs/research/skill_effects_boost_assault_20261008.md の強襲・案1）。
    ///
    /// 流れ（約2.5秒）
    ///   ① 沈む     … 画面が赤黒く落ち、狙う側の血の表示のまわりだけ残る
    ///   ② 絞る     … 画面の四方から赤い線が伸び、照準が3段で狭まる。段ごとにピッ
    ///   ③ 固定     … 照準が血の表示にぴたりと合い、一瞬白く光る。画面が揺れ、相手が跳ねる
    ///   ④ 代償     … 撃った側の血の表示に赤い斜線が入り、「獲得 0」が点滅する
    ///   ⑤ 残す     … 暗幕と線が引き、**照準だけが残る**（<see cref="AssaultMarkUI"/> に引き継ぐ）
    ///
    /// 動きは刻む（1/20 秒ごと、位置は1ドット単位）。出しているのは図形と文字だけ。
    ///
    /// **音は、透視と同じ段取りを掛ける**（<see cref="SkillTranceAudio"/>、2026-10-09 のユーザー指示）。
    /// ①で BGM が水の中のように沈み、⑤で白く光って抜け、そのあと心音とともに元へ戻る。
    /// 心音は演出が終わったあとも鳴り続ける（ゲームは待たせない）。
    /// </summary>
    public class AssaultSkillEffect : MonoBehaviour
    {
        private const float Step = 0.05f;

        private const float LinesAt = 0.20f;     // 線が画面の端から伸び始める
        private const float LockAt = 1.20f;      // 照準が固定される
        private const float CostAt = 1.70f;      // 代償（獲得0）を見せる
        private const float LeaveAt = 2.10f;     // 暗幕と線が引き始める
        private const float EndAt = 2.50f;

        /// <summary>照準が狭まる段。時刻と、最後の大きさからどれだけ外にいるか。</summary>
        private static readonly float[] CloseTimes = { 0.20f, 0.30f, 0.40f, 0.65f, 0.90f };
        private static readonly float[] ClosePads = { 150f, 110f, 70f, 40f, 18f };

        /// <summary>段ごとに上がっていく合図の音の高さ（狭まる3段ぶん）。</summary>
        private static readonly float[] BeepPitches = { 880f, 1040f, 1240f };

        private static readonly Color Dark = new Color(0f, 0f, 0f, 0.85f);
        private static readonly Color PlateFill = new Color(0.05f, 0f, 0f, 0.88f);

        private SkillEffectStage _stage;
        private SkillTranceAudio _trance;

        private void OnDestroy()
        {
            // 光る前に打ち切られたら、沈めた音を戻す。光ったあとは向こうが自分で鳴らしきる
            if (_trance != null && !_trance.IsReleased) _trance.Dispose();
            _trance = null;
        }

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

        /// <param name="targetHpAnchor">狙われる側の血の表示。照準はここに合う</param>
        /// <param name="casterHpAnchor">撃った側の血の表示。「獲得 0」はここに出る</param>
        /// <param name="onLock">照準が固定された瞬間に呼ぶ（相手の立ち絵を跳ねさせる、など）</param>
        public IEnumerator Play(RectTransform targetHpAnchor, RectTransform casterHpAnchor, Action onLock)
        {
            _stage = new SkillEffectStage("Stage", UISortingOrders.PerspectiveOverlay, transform);

            var darken = _stage.AddDarken("Darken", new Color(0.10f, 0f, 0f, 1f));
            var shapes = _stage.AddShapes("Shapes");
            var hitText = _stage.AddText("Hit", "直撃", 26f, AssaultMarkUI.Core);
            var costText = _stage.AddText("Cost", "獲得 0", 26f, AssaultMarkUI.Core);
            hitText.fontStyle = FontStyles.Bold;
            costText.fontStyle = FontStyles.Bold;
            hitText.gameObject.SetActive(false);
            costText.gameObject.SetActive(false);

            Rect lockRect = AssaultMarkUI.ReticleRect(_stage, targetHpAnchor);
            Rect caster = _stage.LocalVisibleRectOf(casterHpAnchor);
            bool hasCaster = caster.width > 1f;
            Rect screen = _stage.Rect.rect;

            darken.CenterLocal = lockRect.center;
            darken.RadiusX = lockRect.width * 0.5f + 80f;
            darken.RadiusY = lockRect.height * 0.5f + 60f;
            darken.MaxAlpha = 0.78f;

            // 文字は血の表示に重ねない。「直撃」は照準の下、「獲得 0」は撃った側の血の表示の上。
            // どちらも黒い札の上に出す（赤い壁の上にじかに置くと読めなかった）
            Vector2 hitPos = SkillEffectStage.Snap(new Vector2(lockRect.center.x, lockRect.yMin - 28f));
            Vector2 costPos = SkillEffectStage.Snap(new Vector2(
                Mathf.Clamp(caster.center.x, screen.xMin + 70f, screen.xMax - 70f), caster.yMax + 28f));
            hitText.rectTransform.anchoredPosition = hitPos;
            costText.rectTransform.anchoredPosition = costPos;

            var audio = AudioManager.Instance;

            // BGM を沈め、深く沈んだ音を流し始める（透視と同じ段取り）
            _trance = SkillTranceAudio.Begin(0.3f);

            ScreenQuake.Play(4f, 0.15f);
            if (audio != null) audio.PlaySynthSound(SynthWaveType.Sawtooth, 1200f, 1800f, 0.3f, 0.5f);

            int beeps = 0;
            bool lockCue = false, costCue = false, leaveCue = false;
            float clock = 0f;
            int lastStep = -1;

            while (clock < EndAt)
            {
                clock += Time.deltaTime;
                int step = Mathf.FloorToInt(clock / Step);
                if (step == lastStep) { yield return null; continue; }
                lastStep = step;
                float t = step * Step;

                // ---- 音と、1回きりの合図 ----
                // 狭まる3段（CloseTimes の後ろ3つ）に合わせて、だんだん高くなる合図を鳴らす
                while (beeps < BeepPitches.Length && t >= CloseTimes[CloseTimes.Length - BeepPitches.Length + beeps])
                {
                    if (audio != null) audio.PlaySynthSound(SynthWaveType.Square, BeepPitches[beeps], BeepPitches[beeps], 0.06f, 0.5f);
                    beeps++;
                }
                if (!lockCue && t >= LockAt)
                {
                    lockCue = true;
                    ScreenQuake.Play(12f, 0.22f);
                    if (onLock != null) onLock();
                    if (audio != null)
                    {
                        audio.PlaySynthSound(SynthWaveType.Square, 1560f, 1560f, 0.18f, 0.7f);
                        audio.PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Noise, 200f, 60f, 0.25f, 1.1f);
                    }
                }
                if (!costCue && t >= CostAt)
                {
                    costCue = true;
                    if (audio != null) audio.PlaySynthSound(SynthWaveType.Sawtooth, 150f, 90f, 0.3f, 0.8f);
                }
                if (!leaveCue && t >= LeaveAt)
                {
                    leaveCue = true;
                    // 白く光って、沈んでいた音が抜ける。心音と BGM の戻りは向こうが鳴らしきる
                    if (_trance != null) _trance.ReleaseDetached();
                }

                // ---- 暗幕 ----
                float leave = Mathf.Clamp01((t - LeaveAt) / (EndAt - LeaveAt - 0.10f));
                darken.Strength = Mathf.Clamp01(t / 0.25f) * (1f - leave);

                bool locked = t >= LockAt;
                bool flash = locked && t < LockAt + 0.10f;
                float fadeOut = 1f - Mathf.Clamp01((t - LeaveAt) / 0.25f);

                bool hitVisible = locked && t < LeaveAt;
                bool costVisible = hasCaster && t >= CostAt && fadeOut > 0f;
                // 「獲得 0」は点滅させる（2コマ点いて1コマ消える）。消え際は点けっぱなし
                bool costTextVisible = costVisible && (t >= LeaveAt || step % 3 != 2);

                // ---- 図形 ----
                shapes.Begin();

                if (t >= LinesAt)
                {
                    float pad = 0f;
                    if (!locked)
                    {
                        for (int i = 0; i < CloseTimes.Length; i++)
                        {
                            if (t >= CloseTimes[i]) pad = ClosePads[i];
                        }
                    }
                    Rect reticle = SkillEffectStage.Expand(lockRect, pad);

                    // 固定の瞬間だけ全部白。そのあとは、線の芯を2コマおきに暗くする（走査しているように）
                    Color rim = flash ? Color.white : AssaultMarkUI.Red;
                    Color lineCore = flash ? Color.white : ((step % 4 < 2) ? AssaultMarkUI.Core : AssaultMarkUI.CoreDim);
                    // 絞っている間は芯を暗めにしておき、固定で白く点く
                    Color reticleCore = locked ? AssaultMarkUI.Core : AssaultMarkUI.CoreDim;

                    // 引くときは、線だけが画面の外へ戻っていく。照準は残す
                    float retract = t >= LeaveAt ? Mathf.Clamp01((t - LeaveAt) / 0.25f) : 0f;
                    if (retract < 1f) DrawLines(shapes, reticle, screen, retract, lineCore, rim);

                    AssaultMarkUI.DrawReticle(shapes, reticle, reticleCore, rim);
                }

                // ---- 代償: 撃った側の血の表示に斜線 ----
                if (costVisible)
                {
                    Rect slash = SkillEffectStage.Expand(caster, 6f);
                    Color core = AssaultMarkUI.Core; core.a = fadeOut;
                    Color red = AssaultMarkUI.Red; red.a = fadeOut;
                    Color dark = Dark; dark.a *= fadeOut;
                    // 斜線は伸びながら入る（2段）
                    float reach = t < CostAt + 0.05f ? 0.5f : 1f;
                    Vector2 from = new Vector2(slash.xMin, slash.yMax);
                    Vector2 to = Vector2.Lerp(from, new Vector2(slash.xMax, slash.yMin), reach);
                    shapes.Line(from, to, 14f, dark);
                    shapes.Line(from, to, 10f, red);
                    shapes.Line(from, to, 4f, core);
                }

                // ---- 文字の後ろの札 ----
                if (hitVisible) DrawPlate(shapes, hitPos, 76f, 34f, flash ? Color.white : AssaultMarkUI.Red, 1f);
                if (costTextVisible) DrawPlate(shapes, costPos, 112f, 34f, AssaultMarkUI.Red, fadeOut);

                shapes.End();

                // ---- 文字 ----
                if (hitText.gameObject.activeSelf != hitVisible) hitText.gameObject.SetActive(hitVisible);
                if (costText.gameObject.activeSelf != costTextVisible) costText.gameObject.SetActive(costTextVisible);
                if (costTextVisible) costText.alpha = fadeOut;

                yield return null;
            }

            // 照準だけを残して、自分は消える
            AssaultMarkUI.Show(targetHpAnchor);
            Dispose();
        }

        /// <summary>
        /// 画面の四方から照準の辺へ伸びる線。<paramref name="retract"/> が 0 で照準まで届き、
        /// 1 で画面の端まで引っ込む。
        /// </summary>
        private static void DrawLines(PixelShapeGraphic g, Rect reticle, Rect screen, float retract, Color core, Color rim)
        {
            Vector2 c = reticle.center;
            Color dark = Dark;
            dark.a *= core.a;

            float left = SkillEffectStage.Snap(Mathf.Lerp(reticle.xMin, screen.xMin, retract));
            float right = SkillEffectStage.Snap(Mathf.Lerp(reticle.xMax, screen.xMax, retract));
            float bottom = SkillEffectStage.Snap(Mathf.Lerp(reticle.yMin, screen.yMin, retract));
            float top = SkillEffectStage.Snap(Mathf.Lerp(reticle.yMax, screen.yMax, retract));

            // 外から 黒 → 赤 → 芯 の3重。赤い壁の上でも緑の卓の上でも線が残るように
            for (int pass = 0; pass < 3; pass++)
            {
                float half = pass == 0 ? 5f : (pass == 1 ? 3f : 1f);
                Color col = pass == 0 ? dark : (pass == 1 ? rim : core);

                g.Box(screen.xMin, c.y - half, left, c.y + half, col);
                g.Box(right, c.y - half, screen.xMax, c.y + half, col);
                g.Box(c.x - half, screen.yMin, c.x + half, bottom, col);
                g.Box(c.x - half, top, c.x + half, screen.yMax, col);
            }
        }

        /// <summary>文字の後ろに敷く黒い札。赤い枠つき。</summary>
        private static void DrawPlate(PixelShapeGraphic g, Vector2 center, float width, float height, Color edge, float alpha)
        {
            Rect r = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            Color fill = PlateFill; fill.a *= alpha;
            edge.a = alpha;
            g.Box(r, fill);
            g.Frame(r, 2f, edge);
        }
    }
}
