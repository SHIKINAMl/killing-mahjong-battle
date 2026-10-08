using System.Collections;
using TMPro;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 役強化を使ったあとの演出（2026-10-08）。
    ///
    /// **それまで、役強化はカットインが出て血が減るだけだった。** いちばん血を払う能力なのに、
    /// 何が起きたのかが絵で伝わらなかった（ユーザーの指摘）。
    /// 案は Antigravity と出し合い、「払った血を注いで、役を黄金に焼き直す」に決めた
    /// （km-docs/research/skill_effects_boost_assault_20261008.md の役強化・案1）。
    ///
    /// 流れ（約3秒）
    ///   ① 血が抜ける   … 自分の血の表示から赤い粒が飛び出し、画面の中央へ吸い込まれる。画面が赤く沈む
    ///   ② 役名が浮かぶ … まわりを暗く落として黒い帯を開き、強める役の名前を赤く出す。心音
    ///   ③ 焼き直す     … 役名の下から金色の光の柱が立ち、役名が赤から金へ変わる。金の粒が散る
    ///   ④ 刻印         … 役名の横に「+1翻」がドスンと落ちてくる。画面が揺れる
    ///   ⑤ 戻る         … 白く光って暗幕が消え、役名は手牌のほうへ縮みながら消える
    ///
    /// **動きは刻む。** 1/20 秒ごとにしか絵を進めず、位置は1ドット単位に置く（ドット絵の画面なので）。
    ///
    /// シーンには置かない。呼ばれるたびに自前の舞台を作り、終わったら自分を消す。
    /// 出しているのは図形（四角）と文字だけで、**AIで作った絵は使っていない。**
    /// </summary>
    public class BoostHandSkillEffect : MonoBehaviour
    {
        private const float Step = 0.05f;

        // 段取りの時刻（秒）
        private const float NameAt = 0.40f;        // 役名が浮かぶ
        private const float GoldAt = 1.20f;        // 光の柱が立ち、金へ変わり始める
        private const float StampAt = 2.00f;       // 「+1翻」が落ちる
        private const float FlashAt = 2.40f;       // 白く光って戻り始める
        private const float EndAt = 3.00f;

        private const int BloodCount = 40;
        private const int SparkCount = 26;

        private static readonly Color BloodBright = new Color32(0xE0, 0x24, 0x30, 0xFF);
        private static readonly Color BloodMid = new Color32(0xC0, 0x18, 0x26, 0xFF);
        private static readonly Color BloodDark = new Color32(0x8A, 0x10, 0x1C, 0xFF);
        private static readonly Color BloodOutline = new Color32(0x28, 0x04, 0x08, 0xFF);
        private static readonly Color BloodShine = new Color32(0xFF, 0xB0, 0xB8, 0xFF);
        private static readonly Color NameRed = new Color32(0xFF, 0x4A, 0x58, 0xFF);
        private static readonly Color Gold = new Color32(0xFF, 0xD7, 0x00, 0xFF);
        private static readonly Color GoldBright = new Color32(0xFF, 0xF0, 0x8A, 0xFF);
        private static readonly Color PlateFill = new Color32(0x24, 0x0C, 0x08, 0xE6);
        private static readonly Color BandFill = new Color(0f, 0f, 0f, 0.80f);
        private static readonly Color BandEdgeRed = new Color32(0xB0, 0x18, 0x24, 0xFF);

        /// <summary>赤 → 金へ焼き変わるときの色の段。なめらかに混ぜず、4段で切り替える。</summary>
        private static readonly Color[] HeatSteps =
        {
            new Color32(0xFF, 0x6E, 0x3C, 0xFF),
            new Color32(0xFF, 0x96, 0x28, 0xFF),
            new Color32(0xFF, 0xBE, 0x14, 0xFF),
            new Color32(0xFF, 0xD7, 0x00, 0xFF),
        };

        private SkillEffectStage _stage;
        private bool _tintSet;

        public static BoostHandSkillEffect Create()
        {
            if (!Application.isPlaying) return null;
            var go = new GameObject("BoostHandSkillEffect");
            return go.AddComponent<BoostHandSkillEffect>();
        }

        /// <summary>途中で止めたいときに。画面の赤みも戻す。</summary>
        public void Dispose()
        {
            if (this == null || gameObject == null) return;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 赤い色かぶりを出したまま消えると、画面が赤いまま残る
            if (_tintSet) ScreenTint.Clear(0.2f);
            _tintSet = false;
        }

        /// <param name="yakuName">強めた役の名前。分からなければ空でよい（その場合は「役強化」と出す）</param>
        /// <param name="casterHpAnchor">使った側の血の表示。血の粒がここから出る</param>
        public IEnumerator Play(string yakuName, RectTransform casterHpAnchor)
        {
            if (string.IsNullOrEmpty(yakuName)) yakuName = "役強化";

            _stage = new SkillEffectStage("Stage", UISortingOrders.PerspectiveOverlay, transform);

            var darken = _stage.AddDarken("Darken", new Color(0.02f, 0f, 0f, 1f));
            var back = _stage.AddShapes("Back");        // 光の柱・帯・血の粒（文字より奥）
            var nameText = _stage.AddText("YakuName", yakuName, 52f, NameRed);
            var front = _stage.AddShapes("Front");      // 刻印の板・金の粒（文字より手前）
            var stampText = _stage.AddText("Stamp", "+1翻", 40f, Gold);

            nameText.fontStyle = FontStyles.Bold;
            stampText.fontStyle = FontStyles.Bold;
            nameText.gameObject.SetActive(false);
            stampText.gameObject.SetActive(false);

            // 役名を出す場所。画面の中央やや上（相手の立ち絵の胸のあたり）
            Vector2 center = new Vector2(0f, 44f);
            Rect hp = _stage.LocalVisibleRectOf(casterHpAnchor);
            Vector2 source = hp.width > 1f ? hp.center : new Vector2(300f, -120f);

            darken.CenterLocal = center;
            darken.RadiusX = 190f;
            darken.RadiusY = 74f;
            darken.MaxAlpha = 0.74f;

            nameText.rectTransform.anchoredPosition = center;
            float nameHalfWidth = Mathf.Min(260f, nameText.GetPreferredValues(yakuName).x * 0.5f);
            Vector2 stampPos = SkillEffectStage.Snap(new Vector2(center.x + nameHalfWidth + 84f, center.y));
            // 役名の後ろに敷く黒い帯の半分の幅。刻印が横に並ぶときは、そこまで覆う
            float bandHalfWidth = SkillEffectStage.Snap(stampPos.x - center.x + 78f);
            // 役名が長いと刻印が画面の右へはみ出すので、そのときは役名の下に置く
            if (stampPos.x > 300f)
            {
                stampPos = SkillEffectStage.Snap(new Vector2(center.x, center.y - 70f));
                bandHalfWidth = SkillEffectStage.Snap(nameHalfWidth + 40f);
            }
            stampText.rectTransform.anchoredPosition = stampPos;

            // 粒の動きは最初に全部決めておく（コマごとに乱数を引くと、粒が震えて見える）
            var blood = BuildBlood(source, center);
            var sparks = BuildSparks(center, nameHalfWidth);

            var audio = AudioManager.Instance;

            // ① 画面を赤く沈める。血が抜ける音
            ScreenTint.Set(new Color(0.55f, 0f, 0.02f), 0.22f, 0.15f);
            _tintSet = true;
            if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Noise, 260f, 70f, 0.4f, 0.9f);

            bool tintOff = false, beat1 = false, beat2 = false, goldCue = false, stampCue = false, flashCue = false;
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
                // **赤い色かぶりは役名が出る前に引く。** 色かぶりの板は演出より手前にあり、
                // 出したままだと役名にも赤が乗って、赤い壁と見分けがつかなくなる（最初の試作で読めなかった）
                if (!tintOff && t >= NameAt - 0.05f) { tintOff = true; ScreenTint.Clear(0.15f); _tintSet = false; }
                if (!beat1 && t >= 0.50f) { beat1 = true; if (audio != null) audio.PlayHeartbeat(HeartbeatStrength.Medium, HeartbeatSpacing.Compact); }
                if (!beat2 && t >= 0.90f) { beat2 = true; if (audio != null) audio.PlayHeartbeat(HeartbeatStrength.Strong, HeartbeatSpacing.Compact); }
                if (!goldCue && t >= GoldAt)
                {
                    goldCue = true;
                    if (audio != null)
                    {
                        audio.PlaySynthSound(SynthWaveType.Sine, 660f, 1320f, 0.7f, 0.8f);
                        audio.PlaySynthSound(SynthWaveType.Triangle, 1320f, 1980f, 0.5f, 0.4f);
                    }
                }
                if (!stampCue && t >= StampAt)
                {
                    stampCue = true;
                    ScreenQuake.Play(10f, 0.2f);
                    if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Noise, 180f, 55f, 0.28f, 1.2f);
                }
                if (!flashCue && t >= FlashAt)
                {
                    flashCue = true;
                    ScreenFlash.Play(0.3f, 0.75f, playSound: false);
                }

                // ---- 暗幕 ----
                float leave = Mathf.Clamp01((t - (FlashAt + 0.05f)) / 0.15f);
                darken.Strength = Mathf.Clamp01((t - 0.15f) / 0.25f) * (1f - leave);

                // ---- 戻るときの動き（役名・刻印・帯で共通）: 手牌のほうへ縮みながら消える ----
                float fade = 1f;
                float shrink = 1f;
                Vector2 drift = Vector2.zero;
                if (t >= FlashAt + 0.05f)
                {
                    float p = Mathf.Clamp01((t - (FlashAt + 0.05f)) / (EndAt - FlashAt - 0.05f));
                    float eased = p * p;
                    fade = 1f - p;
                    shrink = Mathf.Lerp(1f, 0.5f, eased);
                    drift = SkillEffectStage.Snap(new Vector2(0f, -180f) * eased);
                }
                Vector2 namePos = center + drift;
                Vector2 stampNow = SkillEffectStage.Snap(namePos + (stampPos - center) * shrink);

                // ---- 奥の図形: 光の柱 → 帯 → 血の粒 の順に重ねる ----
                back.Begin();
                if (t >= GoldAt && t < FlashAt + 0.05f) DrawPillar(back, center, t, step);
                if (t >= NameAt - 0.05f && fade > 0f)
                {
                    // 帯は2段で左右へ開く
                    float open = t < NameAt ? 0.4f : 1f;
                    DrawBand(back, namePos, bandHalfWidth * open * shrink, 38f * shrink,
                             t >= GoldAt ? Gold : BandEdgeRed, fade);
                }
                DrawBlood(back, blood, t);
                back.End();

                // ---- 役名 ----
                bool nameVisible = t >= NameAt;
                if (nameText.gameObject.activeSelf != nameVisible) nameText.gameObject.SetActive(nameVisible);
                if (nameVisible)
                {
                    float nameScale = shrink;
                    if (t < GoldAt)
                    {
                        // 鼓動するように、少しだけ伸び縮みさせる
                        nameText.color = NameRed;
                        nameScale = (step % 6 < 3) ? 1.0f : 1.05f;
                    }
                    else if (t < FlashAt)
                    {
                        int heat = Mathf.Clamp(Mathf.FloorToInt((t - GoldAt) / 0.10f), 0, HeatSteps.Length - 1);
                        // 刻印が落ちた瞬間だけ白く光らせる
                        bool hit = t >= StampAt && t < StampAt + 0.10f;
                        nameText.color = hit ? Color.white : HeatSteps[heat];
                    }
                    else
                    {
                        nameText.color = Gold;
                    }
                    nameText.rectTransform.anchoredPosition = namePos;
                    nameText.rectTransform.localScale = Vector3.one * nameScale;
                    nameText.alpha = fade;
                }

                // ---- 刻印「+1翻」 ----
                bool stampVisible = t >= StampAt;
                if (stampText.gameObject.activeSelf != stampVisible) stampText.gameObject.SetActive(stampVisible);
                float stampScale = shrink;
                if (stampVisible)
                {
                    float into = t - StampAt;
                    if (into < 0.05f) stampScale = 2.6f;          // 奥から落ちてくる
                    else if (into < 0.10f) stampScale = 1.5f;
                    stampText.rectTransform.anchoredPosition = stampNow;
                    stampText.rectTransform.localScale = Vector3.one * stampScale;
                    stampText.color = into < 0.15f ? GoldBright : Gold;
                    stampText.alpha = fade;
                }

                // ---- 手前の図形: 刻印の板と金の粒 ----
                front.Begin();
                if (stampVisible && fade > 0f) DrawStampPlate(front, stampNow, stampScale, fade);
                if (t >= GoldAt) DrawSparks(front, sparks, t - GoldAt, fade);
                front.End();

                yield return null;
            }

            Dispose();
        }

        // ------------------------------------------------------------
        //  血の粒
        // ------------------------------------------------------------

        private struct BloodDrop
        {
            public Vector2 From, To;
            public float Delay, Duration, Arc, Size;
            public bool Dark;
        }

        private static BloodDrop[] BuildBlood(Vector2 source, Vector2 center)
        {
            var rng = new System.Random(20261008);
            var drops = new BloodDrop[BloodCount];
            for (int i = 0; i < drops.Length; i++)
            {
                drops[i] = new BloodDrop
                {
                    From = source + new Vector2(Rand(rng, -14f, 14f), Rand(rng, -20f, 20f)),
                    To = center + new Vector2(Rand(rng, -30f, 30f), Rand(rng, -12f, 12f)),
                    Delay = Rand(rng, 0f, 0.30f),
                    Duration = Rand(rng, 0.30f, 0.45f),
                    Arc = Rand(rng, 40f, 120f),
                    Size = rng.Next(0, 3) == 0 ? 10f : 8f,
                    Dark = rng.Next(0, 3) == 0,
                };
            }
            return drops;
        }

        private static void DrawBlood(PixelShapeGraphic g, BloodDrop[] drops, float t)
        {
            for (int i = 0; i < drops.Length; i++)
            {
                BloodDrop d = drops[i];
                float u = (t - d.Delay) / d.Duration;
                if (u < 0f || u > 1f) continue;   // まだ出ていない／もう吸い込まれた

                // 尾を2つ引く。粒だけだと赤い壁の上で見えない（最初の試作で見えなかった）
                g.BoxCentered(BloodAt(d, u - 0.16f), d.Size - 4f, d.Size - 4f, BloodDark);
                g.BoxCentered(BloodAt(d, u - 0.08f), d.Size - 2f, d.Size - 2f, BloodDark);

                Vector2 p = BloodAt(d, u);
                g.BoxCentered(p, d.Size + 4f, d.Size + 4f, BloodOutline);
                g.BoxCentered(p, d.Size, d.Size, d.Dark ? BloodMid : BloodBright);
                // 左上に1ドットの照り
                g.BoxCentered(p + new Vector2(-d.Size * 0.5f + 2f, d.Size * 0.5f - 2f), 2f, 2f, BloodShine);
            }
        }

        /// <summary>山なりに飛ぶ粒の位置。最後に向かって速くなる（吸い込まれる感じ）。</summary>
        private static Vector2 BloodAt(BloodDrop d, float u)
        {
            u = Mathf.Clamp01(u);
            Vector2 p = Vector2.Lerp(d.From, d.To, u * u);
            p.y += d.Arc * 4f * u * (1f - u);
            return SkillEffectStage.Snap(p);
        }

        // ------------------------------------------------------------
        //  役名の後ろの帯
        // ------------------------------------------------------------

        /// <summary>
        /// 役名の後ろに敷く黒い帯。**これが無いと、立ち絵や光の柱の上で役名が読めない。**
        /// 両端は階段状に細くする（四角のままだと「黒い板が乗っている」ように見える）。
        /// </summary>
        private static void DrawBand(PixelShapeGraphic g, Vector2 center, float halfWidth, float halfHeight, Color edge, float fade)
        {
            halfWidth = SkillEffectStage.Snap(halfWidth);
            halfHeight = SkillEffectStage.Snap(halfHeight);
            if (halfWidth < 8f || halfHeight < 4f) return;

            Color fill = BandFill; fill.a *= fade;
            edge.a = fade;

            g.Box(center.x - halfWidth, center.y - halfHeight, center.x + halfWidth, center.y + halfHeight, fill);
            // 両端の階段（外へ行くほど低くなる）
            for (int i = 1; i <= 3; i++)
            {
                float h = SkillEffectStage.Snap(halfHeight * (1f - i * 0.25f));
                float x0 = halfWidth + (i - 1) * 8f, x1 = halfWidth + i * 8f;
                g.Box(center.x + x0, center.y - h, center.x + x1, center.y + h, fill);
                g.Box(center.x - x1, center.y - h, center.x - x0, center.y + h, fill);
            }
            // 上下の細い線
            g.Box(center.x - halfWidth, center.y + halfHeight - 2f, center.x + halfWidth, center.y + halfHeight, edge);
            g.Box(center.x - halfWidth, center.y - halfHeight, center.x + halfWidth, center.y - halfHeight + 2f, edge);
        }

        // ------------------------------------------------------------
        //  光の柱
        // ------------------------------------------------------------

        private static void DrawPillar(PixelShapeGraphic g, Vector2 center, float t, int step)
        {
            float bottom = center.y - 44f;
            float rise = Mathf.Clamp01((t - GoldAt) / 0.25f);
            float eased = 1f - (1f - rise) * (1f - rise);
            float top = SkillEffectStage.Snap(Mathf.Lerp(bottom, 320f, eased));
            if (top <= bottom) return;

            // 蛍光灯のように、コマごとに少しだけ明るさを変える（なめらかには動かさない）
            float flicker = (step % 3 == 0) ? 0.82f : 1f;

            Color outer = Gold; outer.a = 0.16f * flicker;
            Color mid = Gold; mid.a = 0.34f * flicker;
            Color core = GoldBright; core.a = 0.80f * flicker;

            g.Box(center.x - 56f, bottom, center.x + 56f, top, outer);
            g.Box(center.x - 32f, bottom, center.x + 32f, top, mid);
            g.Box(center.x - 14f, bottom, center.x + 14f, top, core);

            // 根元の光だまり
            Color pool = GoldBright; pool.a = 0.55f * flicker;
            g.Box(center.x - 84f, bottom - 6f, center.x + 84f, bottom, pool);
        }

        // ------------------------------------------------------------
        //  金の粒
        // ------------------------------------------------------------

        private struct Spark
        {
            public Vector2 From, Velocity;
            public float Delay, Life, Size;
        }

        private static Spark[] BuildSparks(Vector2 center, float nameHalfWidth)
        {
            var rng = new System.Random(8808);
            var sparks = new Spark[SparkCount];
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = new Spark
                {
                    From = center + new Vector2(Rand(rng, -nameHalfWidth - 20f, nameHalfWidth + 20f), Rand(rng, -26f, 26f)),
                    Velocity = new Vector2(Rand(rng, -40f, 40f), Rand(rng, 60f, 170f)),
                    Delay = Rand(rng, 0f, 0.9f),
                    Life = Rand(rng, 0.45f, 0.85f),
                    Size = rng.Next(0, 2) == 0 ? 6f : 4f,
                };
            }
            return sparks;
        }

        private static void DrawSparks(PixelShapeGraphic g, Spark[] sparks, float sinceGold, float fade)
        {
            if (fade <= 0f) return;
            for (int i = 0; i < sparks.Length; i++)
            {
                Spark s = sparks[i];
                float age = sinceGold - s.Delay;
                if (age < 0f || age > s.Life) continue;

                float u = age / s.Life;
                Vector2 p = SkillEffectStage.Snap(s.From + s.Velocity * age);
                Color c = u < 0.5f ? GoldBright : Gold;
                c.a = (u < 0.7f ? 1f : 0.5f) * fade;   // 消える手前で1段だけ薄くする
                g.BoxCentered(p, s.Size, s.Size, c);
            }
        }

        // ------------------------------------------------------------
        //  刻印の板
        // ------------------------------------------------------------

        private static void DrawStampPlate(PixelShapeGraphic g, Vector2 pos, float scale, float fade)
        {
            float w = SkillEffectStage.Snap(128f * scale), h = SkillEffectStage.Snap(56f * scale);
            Rect r = new Rect(pos.x - w * 0.5f, pos.y - h * 0.5f, w, h);

            Color fill = PlateFill; fill.a *= fade;
            Color edge = Gold; edge.a = fade;
            Color dark = new Color(0f, 0f, 0f, 0.9f * fade);

            g.Box(SkillEffectStage.Expand(r, 2f), dark);   // いちばん外の黒い縁
            g.Box(r, fill);
            g.Frame(r, 4f, edge);
        }

        private static float Rand(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
