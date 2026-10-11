using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 声（ロン・役名・満貫などの呼び上げ）の、素材ごとの音量の倍率（2026-10-11）。
    ///
    /// ユーザーの指摘「ロンとか、役の名前を呼んでいる音が小さい」。測ると、素材の音量がばらばらで、
    /// 小さい物は BGM に埋もれていた（ron は山が -13.7dB、一気通貫は -21.0dB。大きい物は -1dB 前後）。
    /// **素材は書き換えず**、鳴らすときに、山が -1dB になる倍率を掛けてそろえる（上げるのは +14dB まで）。
    ///
    /// 表は `Assets/Resources/Voice/` の素材を ffmpeg の volumedetect で測って作った。
    /// **素材を差し替えたら測り直すこと。** 表に無い名前は等倍で鳴る。
    /// </summary>
    public static class VoiceGain
    {
        private static readonly Dictionary<string, float> Gains = new Dictionary<string, float>
        {
            { "rank_baiman", 1.41f },   // 山 -4.0 dB
            { "rank_double_yakuman", 2.14f },   // 山 -7.6 dB
            { "rank_haneman", 1.51f },   // 山 -4.6 dB
            { "rank_mangan", 2.60f },   // 山 -9.3 dB
            { "rank_sanbaiman", 1.48f },   // 山 -4.4 dB
            { "rank_yakuman", 1.46f },   // 山 -4.3 dB
            { "ron", 4.32f },   // 山 -13.7 dB
            { "yaku_akadora", 1.29f },   // 山 -3.2 dB
            { "yaku_chanta", 1.88f },   // 山 -6.5 dB
            { "yaku_chiitoi", 2.88f },   // 山 -10.2 dB
            { "yaku_chinitsu", 5.01f },   // 山 -16.0 dB
            { "yaku_chinroutou", 2.34f },   // 山 -8.4 dB
            { "yaku_chuuren", 1.35f },   // 山 -3.6 dB
            { "yaku_dora", 1.58f },   // 山 -5.0 dB
            { "yaku_honitsu", 4.07f },   // 山 -13.2 dB
            { "yaku_honroutou", 1.40f },   // 山 -3.9 dB
            { "yaku_houtei", 1.00f },   // 山 -0.6 dB
            { "yaku_iipeikou", 2.21f },   // 山 -7.9 dB
            { "yaku_ikkitsuukan", 5.01f },   // 山 -21.0 dB
            { "yaku_ippatsu", 1.68f },   // 山 -5.5 dB
            { "yaku_junchan", 1.86f },   // 山 -6.4 dB
            { "yaku_junsei_chuuren", 1.35f },   // 山 -3.6 dB
            { "yaku_pinfu", 4.90f },   // 山 -14.8 dB
            { "yaku_riichi", 4.07f },   // 山 -13.2 dB
            { "yaku_ryanpeikou", 2.04f },   // 山 -7.2 dB
            { "yaku_ryuuiisou", 1.97f },   // 山 -6.9 dB
            { "yaku_sanankou", 2.26f },   // 山 -8.1 dB
            { "yaku_sanshoku", 2.02f },   // 山 -7.1 dB
            { "yaku_sanshoku_doukou", 1.97f },   // 山 -6.9 dB
            { "yaku_shaa", 1.88f },   // 山 -6.5 dB
            { "yaku_suuankou", 1.60f },   // 山 -5.1 dB
            { "yaku_tanyao", 1.51f },   // 山 -4.6 dB
            { "yaku_toitoi", 1.95f },   // 山 -6.8 dB
            { "yaku_ton", 3.27f },   // 山 -11.3 dB
        };

        /// <summary>この素材に掛ける倍率。表に無ければ 1。</summary>
        public static float Of(AudioClip clip)
        {
            if (clip == null) return 1f;
            float gain;
            return Gains.TryGetValue(clip.name, out gain) ? gain : 1f;
        }
    }
}
