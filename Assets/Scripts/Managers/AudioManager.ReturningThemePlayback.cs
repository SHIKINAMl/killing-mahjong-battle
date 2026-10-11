using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // 対戦中だけの曲ごとの栞。別のフェイズ中に時計を進めず、戻るときに続きを鳴らす。
        // 濃度1〜4は同じ時間軸なので、栞も共有する。他セットには適用しない。
        private struct ReturningThemePosition
        {
            public int Samples;
            public int Frequency;
            public double Seconds;
        }

        private readonly Dictionary<string, ReturningThemePosition> _rtPositions
            = new Dictionary<string, ReturningThemePosition>();
        private bool _rtHasPlayed;

        private static string RtPositionKey(string name)
        {
            if (name == null || !name.StartsWith(RtPrefix, StringComparison.Ordinal)) return null;
            return P4IsField(name) ? RtPrefix + "bgm_field" : name;
        }

        /// <summary>切り替えが実際に始まる小節頭の位置を保存する。予約待ちでは保存しない。</summary>
        private void RememberReturningThemePosition(string name, AudioClip clip, double seconds)
        {
            string key = RtPositionKey(name);
            if (key == null || clip == null || clip.samples <= 0 || clip.frequency <= 0
                || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0) return;

            _rtPositions[key] = new ReturningThemePosition
            {
                Samples = clip.samples,
                Frequency = clip.frequency,
                Seconds = PositiveMod(seconds, (double)clip.samples / clip.frequency)
            };
            _rtHasPlayed = true;
        }

        /// <summary>
        /// 最初だけ曲頭。以後はその曲の続きの小節頭へ戻す。
        /// 未再生の別曲も、確認済みの長尺版なら4小節の導入を飛ばす。
        /// </summary>
        private int ReturningThemeStartSample(string name, AudioClip clip)
        {
            string key = RtPositionKey(name);
            if (key == null || clip == null || clip.samples <= 0 || clip.frequency <= 0) return 0;

            double bar = ProposalBarSeconds(name, clip);
            double seconds = 0.0;
            ReturningThemePosition saved;
            if (_rtPositions.TryGetValue(key, out saved)
                && saved.Samples == clip.samples && saved.Frequency == clip.frequency)
            {
                seconds = saved.Seconds;
            }

            if (bar > 0.0)
            {
                // 実音の時計から保存した位置を小節頭へ寄せる。別曲の秒数は流用しない。
                seconds = Math.Ceiling(seconds / bar - 1e-7) * bar;
            }

            int sample = (int)(Math.Round(seconds * clip.frequency) % clip.samples);
            if (_rtHasPlayed)
            {
                // 途中で導入から離れた場合や、保存位置がループ先頭だった場合も再導入しない。
                sample = Math.Max(sample, ReturningThemeIntroEndSample(name, clip, bar));
            }
            return sample;
        }

        private static int ReturningThemeIntroEndSample(string name, AudioClip clip, double bar)
        {
            if (bar <= 0.0) return 0;
            double bars = Math.Round((double)clip.samples / clip.frequency / bar);
            // 現行82小節版と、場面別リメイクの長尺版には4小節の導入がある。
            // 短尺の掛け金・流局・結果・決定音には当てない。未知の曲にも推測で当てない。
            if (bars != 82.0) return 0;
            switch (name)
            {
                case "rt_bgm_field_1":
                case "rt_bgm_field_2":
                case "rt_bgm_field_3":
                case "rt_bgm_field_4":
                case "rt_bgm_phase_normal":
                case "rt_bgm_phase_turn":
                case "rt_bgm_prepare":
                case "rt_bgm_betting":
                case "rt_bgm_tension":
                case "rt_bgm_discard":
                case "rt_bgm_discard_hot":
                case "rt_bgm_ron":
                case "rt_bgm_draw_base":
                case "rt_bgm_result":
                case "rt_bgm_win":
                case "rt_bgm_lose":
                    return (int)Math.Round(4.0 * bar * clip.frequency);
                default:
                    return 0;
            }
        }

        private void ClearReturningThemePositions()
        {
            _rtPositions.Clear();
            _rtHasPlayed = false;
        }
    }
}
