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
        private AudioSource discardSeSource; // 打牌専用のAudioSource

        [Header("Discard Pitch Settings")]
        private float currentDiscardPitch = 1.0f;
        private float lastDiscardTime = 0f;
        private const float PITCH_RESET_TIME = 2.5f; // 2.5秒間打牌がなければピッチリセット
        private const float SEMITONE_RATIO = 1.059463094359295f; // 半音の周波数比率
        private const float MAX_PITCH = 2.0f; // 1オクターブ上まで

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
        [Tooltip("牌を選択・移動した時のSE（旧）")]
        public AudioClip selectTileSE;
        [Tooltip("ロンした時のボイス（声）")]
        public AudioClip ronVoice;

        [Header("ASMR Sound Clips")]
        [Tooltip("牌やボタンにカーソルを乗せた時の音（スッ…）")]
        public AudioClip hoverSE;
        [Tooltip("牌を選択（クリック）した時の音（カチャッ）")]
        public AudioClip pickTileSE;
        [Tooltip("山から牌をツモってきた時の音（スチャッ）")]
        public AudioClip drawTileSE;
        [Tooltip("UIやダイアログがポップアップした時の音（ポポンッ）")]
        public AudioClip uiPopupSE;
        
        [Header("Paper UI Sound Clips")]
        [Tooltip("紙のUIがスライドしてくる/戻る時の音")]
        public AudioClip paperSlideSE;

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
            if (discardSeSource == null) discardSeSource = gameObject.AddComponent<AudioSource>();

            bgmSource.loop = true;
            ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (seSource != null) seSource.volume = seVolume * masterVolume;
            if (voiceSource != null) voiceSource.volume = voiceVolume * masterVolume;
            if (discardSeSource != null) discardSeSource.volume = seVolume * masterVolume;
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

        // --- Discard SE Control ---
        public void PlayDiscardSE(AudioClip clip = null)
        {
            if (clip == null) clip = discardSE;
            if (clip == null || discardSeSource == null) return;

            // 一定時間経過していればピッチをリセット
            if (Time.time - lastDiscardTime > PITCH_RESET_TIME)
            {
                currentDiscardPitch = 1.0f;
            }
            else
            {
                // 半音上げる
                currentDiscardPitch *= SEMITONE_RATIO;
                if (currentDiscardPitch > MAX_PITCH)
                {
                    currentDiscardPitch = MAX_PITCH;
                }
            }

            discardSeSource.pitch = currentDiscardPitch;
            discardSeSource.volume = seVolume * masterVolume;
            discardSeSource.PlayOneShot(clip);

            lastDiscardTime = Time.time;
        }

        // --- ASMR Specific SE Control ---
        public void PlayHoverSE()
        {
            if (hoverSE != null) PlaySE(hoverSE);
        }

        public void PlayPickTileSE()
        {
            if (pickTileSE != null) PlaySE(pickTileSE);
            else if (selectTileSE != null) PlaySE(selectTileSE); // フォールバック
        }

        public void PlayDrawTileSE()
        {
            if (drawTileSE != null) PlaySE(drawTileSE);
        }

        public void PlayUIPopupSE()
        {
            if (uiPopupSE != null) PlaySE(uiPopupSE);
        }

        public void PlayPaperSlideSE()
        {
            if (paperSlideSE != null) PlaySE(paperSlideSE);
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
