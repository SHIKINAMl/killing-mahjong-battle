using System.Collections;
using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // ------------------------------------------------------------
        //  透視スキルのSE（2026-10-03）
        //
        //  素材はユーザーから受け取ったもの。**`Resources` には置かない。**
        //  `Assets/Se/Clairvoyance/` に置いたまま Inspector で挿す
        //  （心音SE と同じ扱い。`Resources` へ移すとビルドに必ず載る）。
        //
        //  **沈んでいる音のループは専用の AudioSource で鳴らす。**
        //  `PlaySE` の PlayOneShot ではループできず、止めるときに
        //  他のSEまで巻き込んで止めることになる。
        //
        //  **BGMのローパスは掛からない。** あちらは BGM の音源の置き場だけに付いている。
        //  このループは最初から沈んだ音として作られているので、掛ける必要も無い。
        //
        //  **透視だけのものではなくなった（2026-10-09）。** 自分が使うスキルの演出すべてで鳴らす
        //  （UI/Effects/SkillTranceAudio.cs）。名前は透視のまま残してある。
        // ------------------------------------------------------------

        [Header("透視スキルのSE（Assets/Se/Clairvoyance）")]
        [Tooltip("集中中に流し続ける、深く沈んだループ。透視開始でフェードインし、白フラッシュで消す")]
        [SerializeField] private AudioClip clairvoyanceLoop;
        [Tooltip("白フラッシュと同時に鳴る「さーーーーっ」。集中が抜ける音")]
        [SerializeField] private AudioClip clairvoyanceFlashReturn;
        [Tooltip("戻るときの心音「どくっ」×3。1本に3拍とも入っている")]
        [SerializeField] private AudioClip clairvoyanceHeartbeat;

        /// <summary>ループの鳴らしきりの音量。SE全体の音量に掛ける。</summary>
        private const float ClairvoyanceLoopVolume = 0.85f;

        private AudioSource clairvoyanceLoopSource;
        private Coroutine clairvoyanceFadeCoroutine;

        /// <summary>集中中のループを鳴らし始める。</summary>
        /// <param name="fadeIn">鳴りきるまでの秒数。0 なら即座に</param>
        public void StartClairvoyanceLoop(float fadeIn = 0.45f)
        {
            if (!CanPlay || clairvoyanceLoop == null) return;

            EnsureClairvoyanceSource();

            clairvoyanceLoopSource.clip = clairvoyanceLoop;
            clairvoyanceLoopSource.loop = true;
            if (!clairvoyanceLoopSource.isPlaying)
            {
                clairvoyanceLoopSource.volume = 0f;
                clairvoyanceLoopSource.Play();
            }

            StartClairvoyanceFade(ClairvoyanceLoopVolume * seVolume * masterVolume, fadeIn, false);
        }

        /// <summary>集中中のループを消す。</summary>
        /// <param name="fadeOut">消えきるまでの秒数。白フラッシュに合わせるので短くてよい</param>
        public void StopClairvoyanceLoop(float fadeOut = 0.18f)
        {
            if (clairvoyanceLoopSource == null) return;
            StartClairvoyanceFade(0f, fadeOut, true);
        }

        /// <summary>白フラッシュと同時に鳴らす「さーーーーっ」。</summary>
        public void PlayClairvoyanceFlashReturn()
        {
            PlaySE(clairvoyanceFlashReturn);
        }

        /// <summary>戻るときの心音3拍。</summary>
        public void PlayClairvoyanceHeartbeat()
        {
            PlaySE(clairvoyanceHeartbeat);
        }

        /// <summary>透視のSEが揃っているか。揃っていなければ合成音の段取りに落とす。</summary>
        public bool HasClairvoyanceSe => clairvoyanceLoop != null
                                      && clairvoyanceFlashReturn != null
                                      && clairvoyanceHeartbeat != null;

        private void EnsureClairvoyanceSource()
        {
            if (clairvoyanceLoopSource != null) return;

            clairvoyanceLoopSource = gameObject.AddComponent<AudioSource>();
            clairvoyanceLoopSource.playOnAwake = false;
            clairvoyanceLoopSource.loop = true;
            clairvoyanceLoopSource.volume = 0f;
        }

        private void StartClairvoyanceFade(float target, float duration, bool stopAtEnd)
        {
            if (clairvoyanceFadeCoroutine != null) StopCoroutine(clairvoyanceFadeCoroutine);
            clairvoyanceFadeCoroutine = StartCoroutine(ClairvoyanceFadeRoutine(target, duration, stopAtEnd));
        }

        private IEnumerator ClairvoyanceFadeRoutine(float target, float duration, bool stopAtEnd)
        {
            float start = clairvoyanceLoopSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                t = t * t * (3f - 2f * t);
                clairvoyanceLoopSource.volume = Mathf.Lerp(start, target, t);
                yield return null;
            }

            clairvoyanceLoopSource.volume = target;
            clairvoyanceFadeCoroutine = null;

            if (stopAtEnd) clairvoyanceLoopSource.Stop();
        }
    }
}
