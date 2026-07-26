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
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.0f;
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
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadSettings()
        {
            // 過去のセーブデータを引き継ぐ（通常の挙動に戻す）
            // 初回起動時（キーがない場合）のみ、デフォルト値（BGM: 0.0f, SE: 0.5f）が採用されます
            bgmVolume = PlayerPrefs.GetFloat("BgmVolume", 0.0f); 
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
            else
            {
                AudioListener.volume = bgmVolume; // Fallback
            }

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
