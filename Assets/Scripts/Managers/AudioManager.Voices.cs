using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // --- Voice Control ---
        public void PlayVoice(AudioClip clip)
        {
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, voiceVolume * masterVolume);
            }
        }

        /// <summary>
        /// 役名→ファイル名のマッピング辞書を初期化し、Audio/Voice/ からロードする
        /// </summary>
        private void InitializeYakuVoices()
        {
            // 役名 → ファイル名（拡張子なし）
            var yakuFileMap = new Dictionary<string, string>
            {
                // 1飜
                { "立直", "yaku_riichi" },
                { "断么九", "yaku_tanyao" },
                { "平和", "yaku_pinfu" },
                { "一盃口", "yaku_iipeikou" },
                { "東", "yaku_ton" },
                { "西", "yaku_shaa" },
                { "ドラ", "yaku_dora" },
                { "赤ドラ", "yaku_akadora" },
                { "一発", "yaku_ippatsu" },
                { "河底撈魚", "yaku_houtei" },
                // 2飜
                { "三色同順", "yaku_sanshoku" },
                { "三色同刻", "yaku_sanshoku_doukou" },
                { "三暗刻", "yaku_sanankou" },
                { "対々和", "yaku_toitoi" },
                { "混老頭", "yaku_honroutou" },
                { "混全帯么九", "yaku_chanta" },
                { "七対子", "yaku_chiitoi" },
                { "一気通貫", "yaku_ikkitsuukan" },
                // 3飜
                { "二盃口", "yaku_ryanpeikou" },
                { "混一色", "yaku_honitsu" },
                { "純全帯么九", "yaku_junchan" },
                // 6飜
                { "清一色", "yaku_chinitsu" },
                // 役満
                { "九蓮宝燈", "yaku_chuuren" },
                { "緑一色", "yaku_ryuuiisou" },
                { "清老頭", "yaku_chinroutou" },
                { "四暗刻", "yaku_suuankou" },
                // ダブル役満
                { "純正九蓮宝燈", "yaku_junsei_chuuren" },
            };

            yakuVoiceClips = new Dictionary<string, AudioClip>();
            foreach (var kvp in yakuFileMap)
            {
                // Audio/Voice/Yaku/ はResourcesフォルダ外なので、事前にロードしておく必要がある
                // → Addressables等が無い場合は手動でInspectorからセットするか、
                //    ファイルをResourcesフォルダに移動する必要がある。
                //    ここではResourcesに無い前提で、パスベースのロードを試みる。
                var clip = Resources.Load<AudioClip>("Voice/Yaku/" + kvp.Value);
                if (clip != null)
                {
                    yakuVoiceClips[kvp.Key] = clip;
                }
            }

            // ランク名 → ファイル名
            var rankFileMap = new Dictionary<string, string>
            {
                { "満貫", "rank_mangan" },
                { "跳満", "rank_haneman" },
                { "倍満", "rank_baiman" },
                { "三倍満", "rank_sanbaiman" },
                { "役満", "rank_yakuman" },
                { "ダブル役満", "rank_double_yakuman" },
            };

            rankVoiceClips = new Dictionary<string, AudioClip>();
            foreach (var kvp in rankFileMap)
            {
                var clip = Resources.Load<AudioClip>("Voice/rank/" + kvp.Value);
                if (clip != null)
                {
                    rankVoiceClips[kvp.Key] = clip;
                }
            }

            // ロンボイス
            ronVoiceClip = Resources.Load<AudioClip>("Voice/Yaku/ron");
        }

        /// <summary>
        /// ロン宣言ボイスを再生する
        /// </summary>
        public void PlayRonVoice()
        {
            if (yakuVoiceClips == null) InitializeYakuVoices();

            // 新しいronボイスがあればそちら、なければInspectorの旧ronVoice
            var clip = ronVoiceClip != null ? ronVoiceClip : ronVoice;
            if (clip != null) PlayVoice(clip);
        }

        /// <summary>
        /// 役名ボイスを再生する（例：「タンヤオ」「ピンフ」等）
        ///
        /// **渡されるのが素の役名とは限らない。** サーバーは強化回数を連結した
        /// `断么九+1` の形で送ってくるし、表示側はさらに `ドラ×3` のようにまとめる。
        /// 辞書は素の役名しかキーに持っていないので、そのまま引くと**外れて無音になる**。
        /// 2026-08-27 の調査では 27役×強化0〜3回＝99通りのうち **72通り（72.7%）が無音**で、
        /// しかもサーバーが全対局の開始時に強化を1件配るため、**狙って育てた役ほど黙っていた**。
        ///
        /// 素の名前で引けたときはそれを使い、外れたときだけ崩して引き直す。
        /// **素の役名を渡す既存の呼び出しは、今までと同じ経路を通る。**
        /// </summary>
        public void PlayYakuVoice(string yakuName)
        {
            if (yakuVoiceClips == null) InitializeYakuVoices();
            if (string.IsNullOrEmpty(yakuName)) return;

            AudioClip clip;
            if (!yakuVoiceClips.TryGetValue(yakuName, out clip))
            {
                string key = KillingMahjong.Common.YakuNameUtil.ToVoiceKey(yakuName);
                if (key == yakuName) return; // 崩しても同じなら、本当に持っていない役
                if (!yakuVoiceClips.TryGetValue(key, out clip)) return;
            }

            PlayVoice(clip);
        }

        /// <summary>
        /// ランク名ボイスを再生する（例：「満貫！」「跳満！」等）
        /// </summary>
        public void PlayRankVoice(string rankName)
        {
            if (rankVoiceClips == null) InitializeYakuVoices();

            if (!string.IsNullOrEmpty(rankName) && rankVoiceClips.TryGetValue(rankName, out AudioClip clip))
            {
                PlayVoice(clip);
            }
        }
    }
}

