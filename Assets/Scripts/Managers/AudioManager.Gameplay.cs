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

        /// <summary>
        /// スキル発動音。スキルごとに音色を変えて聴き分けられるようにする。
        ///
        /// **録画で示された手触りに寄せた音に差し替えた（2026-09-27）。**
        /// 以前はここで波形を合成していたが、狙いの音（基音＋3倍音の「空洞」＋下降する尾）は
        /// `PlaySynthSoundDual` の2波形では作れないので、書き出した wav を鳴らす形にした。
        /// 素材は `km-docs/se_ui_click/`（make_ngo_style.js / make_accent.js）で作っている。
        /// 4種とも**頭が共通**で、尾だけが違う。発動の合図をそろえて統一感を出すため。
        /// 末尾に「っピ」（下降しながら断ち切られる一瞬）が付いている。
        /// </summary>
        public void PlaySkillSE(string skillType)
        {
            switch (skillType)
            {
                case KillingMahjong.Common.SkillNames.Mulligan:
                    PlayStinger("se_skill_swap");
                    break;

                case KillingMahjong.Common.SkillNames.Perspective:
                    PlayStinger("se_skill_peek");
                    break;

                case KillingMahjong.Common.SkillNames.BoostHand:
                    PlayStinger("se_skill_heavy");
                    break;

                case KillingMahjong.Common.SkillNames.Assault:
                    PlayStinger("se_skill_strike");
                    break;

                case KillingMahjong.Common.SkillNames.SpecialVictory:
                    // 特殊勝利だけは専用の音をまだ作っていない。いちばん重い「役強化」で代用する
                    PlayStinger("se_skill_heavy");
                    break;

                default:
                    PlayStinger("se_skill_swap");
                    break;
            }
        }

        /// <summary>
        /// 賭け金の増減音。賭け金が上限に近いほど高い音になり、額の大きさが耳でも分かるようにする。
        ///
        /// **3段の wav から選ぶ（2026-09-27）。** 録画で示された手触りに寄せた音で、
        /// 基音が 400Hz → 583Hz へ上がり、同時に短くなる（参考音も高い方ほど短かった）。
        ///
        /// **ここには「っピ」を足さない。** 連打される音なので、足すとくどくなる
        /// （ユーザーの判断。確定とスキルにだけ付ける）。
        /// </summary>
        /// <param name="betRatio">現在の賭け金 ÷ 賭けられる上限（0〜1）</param>
        public void PlayBetTickSE(float betRatio)
        {
            betRatio = Mathf.Clamp01(betRatio);
            // 0〜1 を 3段へ。境目は均等（1/3, 2/3）
            int step = betRatio < 0.3334f ? 1 : (betRatio < 0.6667f ? 2 : 3);
            PlayStinger("se_bet_tick_" + step);
        }

        /// <summary>
        /// 賭け金確定音。566Hz を鳴らしてから 3:2 上へ跳ね、末尾に「っピ」が付く。
        /// </summary>
        public void PlayBetConfirmSE()
        {
            PlayStinger("se_bet_confirm");
        }

    }
}

