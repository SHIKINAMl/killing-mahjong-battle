using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>心音の強さ。打牌なら弱、ボルテージが溜まれば中、段が上がれば強。</summary>
    public enum HeartbeatStrength
    {
        Weak,
        Medium,
        Strong
    }

    /// <summary>心音の間合い。<see cref="Compact"/> は詰まった1拍、<see cref="Spaced"/> は間を置いた2拍。</summary>
    public enum HeartbeatSpacing
    {
        Compact,
        Spaced
    }

    public partial class AudioManager
    {
        // ------------------------------------------------------------
        //  心音SE（2026-09-29）
        //
        //  **`PlayDiscardSE` を通してはいけない。** あちらは打牌が続くほど
        //  ピッチを半音ずつ上げていく専用の経路で、心音に掛かると
        //  打つたびに鼓動が甲高くなっていく。心音は固定ピッチで鳴らす。
        //
        //  **`Resources.Load` も使わない。** 素材は `Assets/Se/Heartbeat/` に
        //  置いたまま Inspector で挿す。`Resources` へ移すとビルドに
        //  必ず載ってしまい、容量を削っている今の方針と逆を向く。
        //
        //  **低HPのときの心音（`HeartbeatEffect`）とは別物。**
        //  あちらは赤黒のビネットを出す常時の演出で、ここの素材は流用しない。
        // ------------------------------------------------------------

        [Header("心音SE（Assets/Se/Heartbeat）")]
        [Tooltip("弱・詰まった1拍。打牌のたびに鳴る")]
        [SerializeField] private AudioClip heartbeatWeakCompact;
        [Tooltip("弱・間を置いた2拍。いまは使っていない（聴き比べ用に残す）")]
        [SerializeField] private AudioClip heartbeatWeakSpaced;
        [Tooltip("中・詰まった1拍。いまは使っていない（聴き比べ用に残す）")]
        [SerializeField] private AudioClip heartbeatMediumCompact;
        [Tooltip("中・間を置いた2拍。ボルテージのポイントが増えたときに鳴る")]
        [SerializeField] private AudioClip heartbeatMediumSpaced;
        [Tooltip("強・詰まった1拍。いまは使っていない（聴き比べ用に残す）")]
        [SerializeField] private AudioClip heartbeatStrongCompact;
        [Tooltip("強・間を置いた2拍。ボルテージの段が上がったときに鳴る")]
        [SerializeField] private AudioClip heartbeatStrongSpaced;

        /// <summary>
        /// 心音を1回鳴らす。
        ///
        /// **音量の掛け算も間引きもここでは足さない。** `PlaySE` がそのまま
        /// `seVolume × masterVolume` で鳴らす。ランダムなピッチ揺らぎも入れない
        /// （`AudioManager.Variation.cs` で揺らしているのは何十回も鳴る音。
        /// 心音は1打に1回なので、揺らすと逆に不安定に聞こえる）。
        /// </summary>
        public void PlayHeartbeat(HeartbeatStrength strength, HeartbeatSpacing spacing)
        {
            PlaySE(GetHeartbeatClip(strength, spacing));
        }

        /// <summary>打牌のたびに鳴らす心音。弱・詰まった1拍。</summary>
        public void PlayDiscardHeartbeat()
        {
            PlayHeartbeat(HeartbeatStrength.Weak, HeartbeatSpacing.Compact);
        }

        /// <summary>
        /// 未出牌の光がゲージに着いて、ポイントが増えたときの心音。
        /// 段が上がったかどうかで強さを分ける。
        /// </summary>
        public void PlayVoltageHeartbeat(bool leveledUp)
        {
            PlayHeartbeat(leveledUp ? HeartbeatStrength.Strong : HeartbeatStrength.Medium,
                          HeartbeatSpacing.Spaced);
        }

        private AudioClip GetHeartbeatClip(HeartbeatStrength strength, HeartbeatSpacing spacing)
        {
            bool spaced = spacing == HeartbeatSpacing.Spaced;
            switch (strength)
            {
                case HeartbeatStrength.Strong: return spaced ? heartbeatStrongSpaced : heartbeatStrongCompact;
                case HeartbeatStrength.Medium: return spaced ? heartbeatMediumSpaced : heartbeatMediumCompact;
                default:                       return spaced ? heartbeatWeakSpaced   : heartbeatWeakCompact;
            }
        }
    }
}
