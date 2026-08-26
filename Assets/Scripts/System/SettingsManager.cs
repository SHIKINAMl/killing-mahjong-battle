using UnityEngine;
using System;

namespace KillingMahjong.Core
{
    /// <summary>
    /// ゲーム内の設定（音量・システム・ゲームプレイ）を管理し、保存・読み込みを行うクラス
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // --- オーディオ設定 ---
        [Header("Audio Settings")]
        // 既定値が 0.0f だったため初回起動のプレイヤーはBGMが鳴らない状態から始まっていた。
        // SEより控えめな 0.35f を既定にする。無音に戻したい場合はここと LoadSettings の
        // GetFloat 第2引数を 0.0f に戻す。
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float seVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.5f;

        public float BgmVolume => bgmVolume;
        public float SeVolume => seVolume;
        public float VoiceVolume => voiceVolume;

        // --- ゲームプレイ設定 ---
        [Header("Game Settings")]
        [SerializeField] private bool isHighSpeedMode = false;
        public bool IsHighSpeedMode => isHighSpeedMode; // 打牌スピード（標準/高速）

        // --- 表示・システム設定 ---
        [Header("System Settings")]
        [SerializeField] private bool isEffectEnabled = true;
        public bool IsEffectEnabled => isEffectEnabled; // 背景エフェクトのON/OFF

        // --- ウィンドウ（解像度）設定 ---
        [Header("Window Settings")]
        [SerializeField] private int resolutionIndex = 4; // デフォルトは 800x600
        [SerializeField] private bool isFullScreen = false;

        public int ResolutionIndex => resolutionIndex;
        public bool IsFullScreen => isFullScreen;

        // 設定が変更された時に呼ばれるイベント（UI側で受け取る用）
        public event Action OnSettingsChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // シーンをまたいでも破棄されないようにする

                // **グローバル音量は必ず全開に戻す（2026-08-26）。**
                // 以前はここの ApplySettings が AudioManager 不在時に
                // `AudioListener.volume = bgmVolume` を代用していた。0 が入ると
                // BGMだけでなくSEもボイスも全部消え、スライダーをいくら上げても
                // 誰も戻さないので二度と鳴らなかった。エディタでは Play をまたいで
                // 値が残ることがあるため、既に 0 で固まっている環境をここで治す。
                AudioListener.volume = 1f;

                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 設定を当て直す。
        ///
        /// **`Awake` の実行順は保証されない。** `AudioManager` が後に起きると
        /// `LoadSettings()` の時点では `AudioManager.Instance` が null で、
        /// 保存した音量がどこにも当たらないまま終わる。
        /// `Start` は全ての `Awake` の後に走るので、ここで必ず一度当て直す。
        /// </summary>
        private void Start()
        {
            ApplySettings();
        }

        private void OnDestroy()
        {
            // 破棄済みオブジェクトを指したままにしない
            if (Instance == this) Instance = null;
        }

        public void LoadSettings()
        {
            // 過去のセーブデータを引き継ぐ（通常の挙動に戻す）
            // 初回起動時（キーがない場合）のみ、デフォルト値（BGM: 0.35f, SE: 0.5f）が採用されます
            bgmVolume = PlayerPrefs.GetFloat("BgmVolume", 0.35f);
            seVolume = PlayerPrefs.GetFloat("SeVolume", 0.5f);
            voiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 0.5f);
            
            isHighSpeedMode = PlayerPrefs.GetInt("IsHighSpeedMode", isHighSpeedMode ? 1 : 0) == 1;
            isEffectEnabled = PlayerPrefs.GetInt("IsEffectEnabled", isEffectEnabled ? 1 : 0) == 1;

            resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", resolutionIndex);
            isFullScreen = PlayerPrefs.GetInt("IsFullScreen", isFullScreen ? 1 : 0) == 1;

            ApplySettings();
        }

        /// <summary>
        /// 設定を保存し、ゲーム内に適用する
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("BgmVolume", bgmVolume);
            PlayerPrefs.SetFloat("SeVolume", seVolume);
            PlayerPrefs.SetFloat("VoiceVolume", voiceVolume);
            
            PlayerPrefs.SetInt("IsHighSpeedMode", isHighSpeedMode ? 1 : 0);
            PlayerPrefs.SetInt("IsEffectEnabled", isEffectEnabled ? 1 : 0);

            PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
            PlayerPrefs.SetInt("IsFullScreen", isFullScreen ? 1 : 0);

            PlayerPrefs.Save();
            
            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        // --- 設定値の変更メソッド ---
        public void SetBgmVolume(float volume) 
        { 
            bgmVolume = volume; 
            if (KillingMahjong.Managers.AudioManager.Instance != null) 
            {
                KillingMahjong.Managers.AudioManager.Instance.bgmVolume = volume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
        }
        
        public void SetSeVolume(float volume) 
        { 
            seVolume = volume; 
            if (KillingMahjong.Managers.AudioManager.Instance != null) 
            {
                KillingMahjong.Managers.AudioManager.Instance.seVolume = volume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
        }
        
        public void SetVoiceVolume(float volume) 
        { 
            voiceVolume = volume; 
            if (KillingMahjong.Managers.AudioManager.Instance != null) 
            {
                KillingMahjong.Managers.AudioManager.Instance.voiceVolume = volume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
        }
        public void SetHighSpeedMode(bool isHighSpeed) { isHighSpeedMode = isHighSpeed; }
        public void SetEffectEnabled(bool isEnabled) { isEffectEnabled = isEnabled; }
        public void SetResolutionIndex(int index) { resolutionIndex = index; }
        public void SetFullScreen(bool isFull) { isFullScreen = isFull; }

        private void OnValidate()
        {
            // インスペクターで値を変えた時に、即座に適用されるようにする
            if (Application.isPlaying)
            {
                ApplySettings();
            }
        }

        /// <summary>
        /// 設定値を実際のゲーム内要素（音量など）に反映させる
        /// </summary>
        private void ApplySettings()
        {
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.bgmVolume = bgmVolume;
                KillingMahjong.Managers.AudioManager.Instance.seVolume = seVolume;
                KillingMahjong.Managers.AudioManager.Instance.voiceVolume = voiceVolume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
            // **`AudioListener.volume` を代用してはいけない。**
            // あれはゲーム全体のマスターで、BGMの値を入れるとSEもボイスも巻き添えになる。
            // 0 が入ると全ての音が消え、スライダー（SetBgmVolume）は AudioManager しか
            // 触らないので、上げ直しても二度と戻らなかった。
            // `AudioManager` がまだ居ないだけなら、`Start()` の当て直しで拾える。

            ApplyResolution();
        }

        private void ApplyResolution()
        {
            int width = 800;
            int height = 600;
#if !UNITY_WEBGL
            Screen.SetResolution(width, height, isFullScreen);
#endif
        }
    }
}
