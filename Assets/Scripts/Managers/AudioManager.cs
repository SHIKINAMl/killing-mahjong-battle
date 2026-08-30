using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager : MonoBehaviour
    {
        // partial の責務:
        // - AudioManager.Initialization.cs: AudioSourceとAudioSynthの準備
        // - AudioManager.Filter.cs: 対局中のBGMフィルター遷移
        // - AudioManager.Playback.cs: BGM・通常SE・打牌SEの再生
        // - AudioManager.Gameplay.cs: 対局結果とスキルの合成SE
        // - AudioManager.Voices.cs: 役名・ランク・ロンのボイス

        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource voiceSource;
        private AudioSource discardSeSource; // 打牌専用のAudioSource

        private AudioLowPassFilter bgmLowPassFilter; // BGMのこもり（重低音）表現用フィルター
        private Coroutine filterFadeCoroutine;

        [Header("Discard Pitch Settings")]
        [Tooltip("打牌1回あたり何半音ピッチを上げるか。1.0=半音ずつ、0.5=四分音ずつ（ゆっくり）")]
        [SerializeField, Range(0.1f, 2f)] private float discardPitchStepSemitones = 0.5f;

        private float currentDiscardPitch = 1.0f;
        private float lastDiscardTime = 0f;
        private const float PITCH_RESET_TIME = 2.5f; // 2.5秒間打牌がなければピッチリセット
        private const float MAX_PITCH = 2.0f; // 1オクターブ上まで

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float seVolume = 1f;
        [Range(0f, 1f)] public float voiceVolume = 1f;

        [Header("Audio Clips - Setup in Inspector")]
        [Tooltip("デフォルトのBGM（タイトルや日常会話など）")]
        public AudioClip defaultBgm;
        [Tooltip("対局中のBGM")]
        public AudioClip battleBgm;
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

        [Header("シーン遷移時のBGM")]
        [Tooltip("このシーンへ移ったら BGM を自動で止める。タイトルへ戻ったとき対局のBGMが鳴り続けるのを防ぐ")]
        [SerializeField] private string[] stopBgmOnScenes = { "タイトルシーン" };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSources();
                // AudioManager は DontDestroyOnLoad で生き残るため、
                // シーンを変えても BGM は鳴り続ける。タイトルへ戻る導線は
                // TutorialManager / InGameMenuUI / OptionUI / TitleUIManager と複数あるので、
                // 各所で止めると必ず漏れる。ここで一括して面倒を見る。
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (stopBgmOnScenes == null) return;
            foreach (var n in stopBgmOnScenes)
            {
                if (!string.IsNullOrEmpty(n) && scene.name == n)
                {
                    StopBGM();
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
                // 破棄済みオブジェクトを指したままにしない
                Instance = null;
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

        private AudioSynth synth; // 追加

        public void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (seSource != null) seSource.volume = seVolume * masterVolume;
            if (voiceSource != null) voiceSource.volume = voiceVolume * masterVolume;
            if (discardSeSource != null) discardSeSource.volume = seVolume * masterVolume;
        }

        // --- BGM Filter Control ---
        //
        // 打牌フェイズだけ BGM のこもりを外して前に出す。
        //
        // **カットオフは Hz で直線に動かしてはいけない（2026-08-24）。**
        // 1000→22000 を直線で 1.5 秒かけても、0.15 秒で 3100Hz、0.4 秒で 6600Hz まで開く。
        // こもりが取れて聞こえるのはこのあたりまでなので、**残りの1秒は耳に何も起きない**。
        // 結果、「いきなりクリアになった」という聞こえ方になる。
        //
        // 人の耳は周波数を対数（オクターブ）で聞くので、**log 空間で補間する**。
        // 1000→22000 は約 4.5 オクターブで、これを均等に配れば端から端まで動き続けて聞こえる。

        /// <summary>こもった状態のカットオフ。</summary>
        private const float MuffledCutoff = 1000f;

        /// <summary>開き切った状態。事実上フィルターオフ（48kHz のナイキストは 24000）。</summary>
        private const float OpenCutoff = 22000f;

        /// <summary>打牌フェイズに入るとき、こもりが取れるまでの秒数。</summary>
        public const float FilterOpenDuration = 2.0f;

        /// <summary>打牌フェイズを抜けるとき、こもるまでの秒数。</summary>
        public const float FilterMuffleDuration = 1.5f;

        /// <summary>いま向かっている先。同じ行き先の再依頼を無視するために持つ。</summary>
        private float _filterTargetFreq = float.NaN;

        // --- 役名ボイス・ランクボイス ---
        private Dictionary<string, AudioClip> yakuVoiceClips;
        private Dictionary<string, AudioClip> rankVoiceClips;
        private AudioClip ronVoiceClip;

    }
}
