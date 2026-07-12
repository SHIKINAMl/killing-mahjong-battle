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
        public float BgmVolume { get; private set; }
        public float SeVolume { get; private set; }
        public float VoiceVolume { get; private set; }

        // --- ゲームプレイ設定 ---
        public bool IsHighSpeedMode { get; private set; } // 打牌スピード（標準/高速）

        // --- 表示・システム設定 ---
        public bool IsEffectEnabled { get; private set; } // 背景エフェクトのON/OFF

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
            BgmVolume = PlayerPrefs.GetFloat("BgmVolume", 0.5f); // デフォルト音量は50%
            SeVolume = PlayerPrefs.GetFloat("SeVolume", 0.5f);
            VoiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 0.5f);
            
            IsHighSpeedMode = PlayerPrefs.GetInt("IsHighSpeedMode", 0) == 1; // 0 = false, 1 = true
            IsEffectEnabled = PlayerPrefs.GetInt("IsEffectEnabled", 1) == 1; // デフォルトはON

            ApplySettings();
        }

        /// <summary>
        /// 設定を保存し、ゲーム内に適用する
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("BgmVolume", BgmVolume);
            PlayerPrefs.SetFloat("SeVolume", SeVolume);
            PlayerPrefs.SetFloat("VoiceVolume", VoiceVolume);
            
            PlayerPrefs.SetInt("IsHighSpeedMode", IsHighSpeedMode ? 1 : 0);
            PlayerPrefs.SetInt("IsEffectEnabled", IsEffectEnabled ? 1 : 0);

            PlayerPrefs.Save();
            
            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        // --- 設定値の変更メソッド ---
        public void SetBgmVolume(float volume) { BgmVolume = volume; }
        public void SetSeVolume(float volume) { SeVolume = volume; }
        public void SetVoiceVolume(float volume) { VoiceVolume = volume; }
        public void SetHighSpeedMode(bool isHighSpeed) { IsHighSpeedMode = isHighSpeed; }
        public void SetEffectEnabled(bool isEnabled) { IsEffectEnabled = isEnabled; }

        /// <summary>
        /// 設定値を実際のゲーム内要素（音量など）に反映させる
        /// </summary>
        private void ApplySettings()
        {
            // 仮実装: AudioListenerの音量をBGM音量に合わせる（本来はAudioMixerやAudioManagerで個別制御する）
            AudioListener.volume = BgmVolume;
        }
    }
}
