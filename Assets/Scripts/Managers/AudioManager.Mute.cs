using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 音を丸ごと止める栓（2026-09-12）。
    ///
    /// **いまは止めてある。** BGMの出来に納得がいかず、音の作業を後回しにしたため
    /// （ユーザーの指示 2026-09-12）。**作ったものは消していない。**
    /// <see cref="AudioEnabled"/> を true にすれば、そのまま全部鳴り出す。
    ///
    /// **`AudioListener.volume` は使えない。**
    /// `SettingsManager.Awake` が毎回 `AudioListener.volume = 1f` を強制している
    /// （0 で固まって二度と鳴らなくなった事故の対策で、意図的に入っているもの）。
    /// あそこを触ると、その事故を呼び戻すことになる。
    ///
    /// **音量スライダーも使わない。** `SettingsManager` が保存値を
    /// `AudioManager.bgmVolume` へ押し込んでくるので、0 にしても起動のたびに戻る。
    ///
    /// そこで**この栓だけで止める。** 入口で弾いたうえ、毎フレーム
    /// 配下の AudioSource を黙らせる。一発物は使い捨ての AudioSource を
    /// 作って鳴らす経路があるので、入口のガードだけでは取りこぼす。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>
        /// false のあいだ、このマネージャが出す音は**すべて**鳴らない。
        /// BGM・SE・ボイス・スティンガー・層、どれも止まる。
        ///
        /// **音を戻すときはここを true にするだけ。** 他に消した場所は無い。
        /// </summary>
        [Header("音の栓")]
        [Tooltip("切ると、このマネージャが出す音がすべて止まる。音の作業を後回しにしているあいだ false")]
        public bool AudioEnabled = true;   // 2026-09-19 にユーザーの指示で再開（09-12 から止めていた）

        /// <summary>入口で使う。鳴らしてよいか。</summary>
        private bool CanPlay { get { return AudioEnabled; } }

        /// <summary>
        /// 掃除の間隔[秒]。**毎フレームやる必要は無い。**
        /// 入口はガードで塞いであるので、ここに引っかかるのは
        /// playOnAwake や外から作られた取りこぼしだけ。
        /// `GetComponentsInChildren` は毎回配列を作るため、毎フレーム呼ぶとゴミが出る。
        /// </summary>
        private const float MuteSweepInterval = 0.25f;

        private float _nextMuteSweep;

        /// <summary>直前に見た栓の状態。true に戻った瞬間を拾うために持つ。</summary>
        private bool _lastAudioEnabled;

        /// <summary>
        /// 配下の AudioSource を黙らせる。
        /// **一発物は再生のたびに AudioSource を作る**ので、入口のガードだけでは取りこぼす。
        ///
        /// 栓を true に戻したときは、**音量を保存値へ戻して**この掃除をやめる。
        /// ここで戻さないと、黙らせたときの 0 がそのまま残って無音が続く。
        /// </summary>
        private void EnforceMute()
        {
            if (AudioEnabled)
            {
                if (!_lastAudioEnabled)
                {
                    _lastAudioEnabled = true;
                    ApplyVolumes();          // 0 にしたぶんを保存値へ戻す
                    ApplyLayerVolumes();
                }
                return;
            }

            _lastAudioEnabled = false;

            if (Time.unscaledTime < _nextMuteSweep) return;
            _nextMuteSweep = Time.unscaledTime + MuteSweepInterval;

            var sources = GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                if (sources[i].isPlaying) sources[i].Stop();
                sources[i].volume = 0f;
            }
        }

    }
}
