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
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;
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
        [SerializeField] private int resolutionIndex = 0; // デフォルトは1920x1080
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

        /// <summary>
        /// PlayerPrefsから設定を読み込む
        /// </summary>
        public void LoadSettings()
        {
            bgmVolume = PlayerPrefs.GetFloat("BgmVolume", bgmVolume);
            seVolume = PlayerPrefs.GetFloat("SeVolume", seVolume);
            voiceVolume = PlayerPrefs.GetFloat("VoiceVolume", voiceVolume);
            
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
        public void SetBgmVolume(float volume) { bgmVolume = volume; }
        public void SetSeVolume(float volume) { seVolume = volume; }
        public void SetVoiceVolume(float volume) { voiceVolume = volume; }
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
            int width = 1920;
            int height = 1080;
            switch(resolutionIndex)
            {
                case 0: width = 1920; height = 1080; break;
                case 1: width = 1600; height = 900; break;
                case 2: width = 1280; height = 720; break;
                case 3: width = 1024; height = 576; break;
                case 4: width = 800; height = 600; break;
                default: width = 1920; height = 1080; break;
            }
            Screen.SetResolution(width, height, isFullScreen);
        }
    }
}
