using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource voiceSource;

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float seVolume = 1f;
        [Range(0f, 1f)] public float voiceVolume = 1f;

        [Header("Audio Clips - Setup in Inspector")]
        [Tooltip("デフォルトのBGM")]
        public AudioClip defaultBgm;
        [Tooltip("打牌した時のSE")]
        public AudioClip discardSE;
        [Tooltip("牌を選択・移動した時のSE")]
        public AudioClip selectTileSE;
        [Tooltip("ロンした時のボイス（声）")]
        public AudioClip ronVoice;

        // 今後の「音ハメ」拡張用プロパティ
        public float CurrentBgmTime => bgmSource != null ? bgmSource.time : 0f;
        public int CurrentBgmTimeSamples => bgmSource != null ? bgmSource.timeSamples : 0;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSources();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // ゲーム開始時にBGMが設定されていれば再生する
            if (defaultBgm != null && !IsBgmPlaying)
            {
                PlayBGM(defaultBgm);
            }
        }

        private void InitializeSources()
        {
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
            if (seSource == null) seSource = gameObject.AddComponent<AudioSource>();
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop = true;
            ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (seSource != null) seSource.volume = seVolume * masterVolume;
            if (voiceSource != null) voiceSource.volume = voiceVolume * masterVolume;
        }

        // --- BGM Control ---
        // 将来的にBGMのBPM（テンポ）同期やビート検知処理をここに追加できます
        public void PlayBGM(AudioClip clip = null, bool restartIfSame = false)
        {
            if (bgmSource == null) return;
            if (clip == null) clip = defaultBgm;
            if (clip == null) return;

            if (bgmSource.clip == clip && bgmSource.isPlaying && !restartIfSame) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        // --- SE Control ---
        public void PlaySE(AudioClip clip)
        {
            if (clip != null && seSource != null)
            {
                seSource.PlayOneShot(clip, seVolume * masterVolume);
            }
        }

        // --- Voice Control ---
        public void PlayVoice(AudioClip clip)
        {
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, voiceVolume * masterVolume);
            }
        }
    }
}
