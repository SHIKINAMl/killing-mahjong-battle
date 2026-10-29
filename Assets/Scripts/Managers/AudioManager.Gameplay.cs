using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // --- ゲームプレイ用のSE ---
        // 以前は AudioSynth の合成音で埋めていたが、「AIくさい」と言われたので
        // Freesound（CC0）のゲーム風の素材へ差し替えた（2026-10-19）。素材はユーザーが耳で選び、
        // 1つの場面で複数選んだものは重ねて1つにしてある。作り方は km-bridge/効果音/game_se_make.py。

        /// <summary>
        /// 被弾音。残HPの割合が低いほど低く鳴らし、追い詰められている感を出す。
        /// </summary>
        /// <param name="hpRatio">被弾後の残HP割合（0〜1）</param>
        public void PlayDamageSE(float hpRatio)
        {
            hpRatio = Mathf.Clamp01(hpRatio);
            // 瀕死ほど低く（満タンで素材のまま、瀕死で約3半音下）
            PlayStingerPitched("se_damage", Mathf.Lerp(0.84f, 1f, hpRatio), PitchJitter.VerySmall);
        }

        /// <summary>
        /// 相手にダメージを与えた時の音。被弾音（PlayDamageSE）と違って手応えのある打撃音にする。
        /// とどめに近いほど低く重くなる。
        /// </summary>
        /// <param name="targetHpRatio">攻撃後の相手の残HP割合（0〜1）</param>
        public void PlayHitSE(float targetHpRatio)
        {
            targetHpRatio = Mathf.Clamp01(targetHpRatio);
            PlayStingerPitched("se_hit", Mathf.Lerp(0.84f, 1f, targetHpRatio), PitchJitter.VerySmall);
        }

        /// <summary>回復（＝相手から奪った）音。</summary>
        public void PlayHealSE()
        {
            PlayStinger("se_heal", PitchJitter.VerySmall);
        }

        /// <summary>ロンの「ドン」。8bit の大砲と爆発を重ねた音。</summary>
        /// <summary>
        /// 動画の続きで、先輩が倒れる絵がフェードで出る直前の音。
        /// 2026-10-29 の要望は「ドンっていう鈍いSE」で se_impact を低くしていたが、聞いたプランナーから
        /// 「ちょっと鈍すぎるかも」「さーって血の気が引くような高音のものがいいかも」。
        /// 候補 A（br_fall_bar）・B（br_riser）は「論外」と言われたので、残った C の br_blackout（暗転の音）を仮に使う。
        /// 素材を差し替えるならここ
        /// </summary>
        public void PlaySeniorCollapseSE() => PlayStinger("br_blackout", PitchJitter.None);

        public void PlayImpactSE()
        {
            PlayStinger("se_impact", PitchJitter.VerySmall);
        }

        /// <summary>
        /// 役強化の演出の音。1つの素材を、場面ごとに高さと大きさを変えて使う。
        /// </summary>
        public void PlayBoostSE(float pitch, float volumeScale)
        {
            PlayStingerPitched("se_boost", pitch, PitchJitter.None, volumeScale);
        }

        /// <summary>強襲を撃つ音。</summary>
        public void PlayAssaultSE()
        {
            PlayStinger("se_assault", PitchJitter.VerySmall);
        }

        /// <summary>試合の決着のジングル。勝ちと負けで別の音。</summary>
        public void PlayMatchEndJingle(bool won)
        {
            PlayStinger(won ? "se_jingle_win" : "se_jingle_lose", PitchJitter.None);
        }

        /// <summary>
        /// スキル発動音。
        ///
        /// **4種とも同じ素材（Freesound の「Retro Spell SFX」）で、音程だけ変えてある（2026-10-19）。**
        /// 牌交換を素材のまま、透視を少し高く、強襲を少し低く、役強化をいちばん低くして聴き分けられるようにした。
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
        /// **3段の wav から選ぶ。** Freesound の短い電子音を3つ重ねた1つの音を、
        /// 段ごとに全音ずつ高くしてある（2026-10-19）。
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

        /// <summary>
        /// 手牌選択の「並べ直す」を押したとき（2026-10-15 のユーザーの要望）。
        /// 一度「選び直す」に付けたが、ユーザーが言っていたのは「並べ直す」だったので付け替えた。
        /// 短い3音を上へ駆け上がる「ピ・ピ・ピン」（F5→C6→F6、0.36秒）。自作の合成で、
        /// 作り方は km-docs/sound/make_reselect.py。2〜5kHz は 2.7%、5kHz 以上は 0%。
        /// </summary>
        public void PlayReselectSE()
        {
            PlayStinger("se_reselect");
        }

    }
}

