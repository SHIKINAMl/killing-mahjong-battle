using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // --- ゲームプレイ用の合成SE ---
        // 「削り合い」の要である被弾・スキル発動・賭け金操作がいずれも無音だったため、
        // 音声アセットを増やさずに AudioSynth の合成音で埋めている。
        // 差し替えたくなったら各メソッドの中身を PlaySE(clip) に置き換えるだけで済む。

        /// <summary>
        /// 被弾音。残HPの割合が低いほど低く歪んだ音になり、追い詰められている感を出す。
        /// </summary>
        /// <param name="hpRatio">被弾後の残HP割合（0〜1）</param>
        public void PlayDamageSE(float hpRatio)
        {
            hpRatio = Mathf.Clamp01(hpRatio);

            // 瀕死ほど開始周波数を下げる（600Hz → 260Hz）
            float startFreq = Mathf.Lerp(260f, 600f, hpRatio);
            float endFreq = startFreq * 0.25f;
            float duration = Mathf.Lerp(0.42f, 0.28f, hpRatio); // 瀕死ほど長く尾を引く

            PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Noise,
                startFreq, endFreq, duration, 1.0f);
        }

        /// <summary>
        /// 相手にダメージを与えた時の音。被弾音（PlayDamageSE）と違って手応えのある打撃音にする。
        /// とどめに近いほど低く重くなる。
        /// </summary>
        /// <param name="targetHpRatio">攻撃後の相手の残HP割合（0〜1）</param>
        public void PlayHitSE(float targetHpRatio)
        {
            targetHpRatio = Mathf.Clamp01(targetHpRatio);

            float startFreq = Mathf.Lerp(520f, 900f, targetHpRatio);
            PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Noise,
                startFreq, startFreq * 0.35f, 0.22f, 1.0f);
        }

        /// <summary>回復（＝相手から奪った）音。上昇するきらびやかな音。</summary>
        public void PlayHealSE()
        {
            PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Triangle,
                440f, 1320f, 0.35f, 0.9f);
        }

        /// <summary>スキル発動音。スキルごとに音色を変えて聴き分けられるようにする。</summary>
        public void PlaySkillSE(string skillType)
        {
            switch (skillType)
            {
                case KillingMahjong.Common.SkillNames.Mulligan:
                    // 牌を入れ替える：短くカラッとした二段の音
                    PlaySynthSoundDual(SynthWaveType.Triangle, SynthWaveType.Square,
                        880f, 1174f, 0.18f, 0.8f);
                    break;

                case KillingMahjong.Common.SkillNames.Perspective:
                    // 透視：ノイズ混じりの上昇音で「覗かれた」感じを出す
                    PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Noise,
                        300f, 1800f, 0.55f, 0.85f);
                    break;

                case KillingMahjong.Common.SkillNames.BoostHand:
                    // 役強化：どっしり上昇する重い音
                    PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Square,
                        160f, 640f, 0.6f, 1.0f);
                    break;

                case KillingMahjong.Common.SkillNames.Assault:
                    // 強襲：獲得を捨てて殴りにいく。短く鋭い、刺すような上昇音
                    PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Noise,
                        220f, 1760f, 0.28f, 1.0f);
                    break;

                case KillingMahjong.Common.SkillNames.SpecialVictory:
                    // 特殊勝利：一番長く不穏な下降音
                    PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Noise,
                        1200f, 90f, 0.9f, 1.0f);
                    break;

                default:
                    PlaySynthSound(SynthWaveType.Triangle, 660f, 990f, 0.2f, 0.8f);
                    break;
            }
        }

        /// <summary>
        /// 賭け金の増減音。賭け金が上限に近いほど高い音になり、額の大きさが耳でも分かるようにする。
        /// </summary>
        /// <param name="betRatio">現在の賭け金 ÷ 賭けられる上限（0〜1）</param>
        public void PlayBetTickSE(float betRatio)
        {
            betRatio = Mathf.Clamp01(betRatio);
            float freq = Mathf.Lerp(420f, 1100f, betRatio);
            PlaySynthSound(SynthWaveType.Triangle, freq, freq * 1.15f, 0.07f, 0.7f);
        }

        /// <summary>賭け金確定音。コインを置くような二段の決定音。</summary>
        public void PlayBetConfirmSE()
        {
            PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Sine,
                523f, 1046f, 0.28f, 0.95f);
        }

    }
}

