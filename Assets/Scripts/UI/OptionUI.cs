using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// オプション（設定）画面のUIを制御するクラス
    /// </summary>
    public class OptionUI : MonoBehaviour
    {
        [Header("Audio Settings (Sliders)")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider seSlider;
        [SerializeField] private Slider voiceSlider;

        [Header("Game Settings (Toggles)")]
        [SerializeField] private Toggle highSpeedToggle;

        [Header("System Settings (Toggles)")]
        [SerializeField] private Toggle effectToggle;

        [Header("Buttons")]
        [SerializeField] private Button closeButton; // 保存せずに閉じる
        [SerializeField] private Button saveAndCloseButton; // 保存して閉じる

        private void Start()
        {
            InitializeUI();

            // --- スライダーのイベント登録 ---
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (seSlider != null) seSlider.onValueChanged.AddListener(OnSeChanged);
            if (voiceSlider != null) voiceSlider.onValueChanged.AddListener(OnVoiceChanged);
            
            // --- トグルのイベント登録 ---
            if (highSpeedToggle != null) highSpeedToggle.onValueChanged.AddListener(OnHighSpeedChanged);
            if (effectToggle != null) effectToggle.onValueChanged.AddListener(OnEffectChanged);

            // --- ボタンのイベント登録 ---
            if (closeButton != null) closeButton.onClick.AddListener(CloseWithoutSave);
            if (saveAndCloseButton != null) saveAndCloseButton.onClick.AddListener(SaveAndClose);
        }

        private void OnEnable()
        {
            // 画面が開かれるたびに、現在の設定値をUIに反映させる
            InitializeUI();
        }

        /// <summary>
        /// SettingsManagerが持っている現在の設定値をUI（スライダーなど）に反映する
        /// </summary>
        private void InitializeUI()
        {
            if (Core.SettingsManager.Instance != null)
            {
                var settings = Core.SettingsManager.Instance;
                
                if (bgmSlider != null) bgmSlider.value = settings.BgmVolume;
                if (seSlider != null) seSlider.value = settings.SeVolume;
                if (voiceSlider != null) voiceSlider.value = settings.VoiceVolume;
                
                if (highSpeedToggle != null) highSpeedToggle.isOn = settings.IsHighSpeedMode;
                if (effectToggle != null) effectToggle.isOn = settings.IsEffectEnabled;
            }
        }

        // --- 値が変更された時に呼ばれる処理（SettingsManagerの仮の値を更新） ---
        private void OnBgmChanged(float value)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetBgmVolume(value);
        }

        private void OnSeChanged(float value)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetSeVolume(value);
        }

        private void OnVoiceChanged(float value)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetVoiceVolume(value);
        }

        private void OnHighSpeedChanged(bool isOn)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetHighSpeedMode(isOn);
        }

        private void OnEffectChanged(bool isOn)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetEffectEnabled(isOn);
        }

        // --- ボタン処理 ---
        public void SaveAndClose()
        {
            if (Core.SettingsManager.Instance != null)
            {
                Core.SettingsManager.Instance.SaveSettings();
            }
            gameObject.SetActive(false);
        }

        public void CloseWithoutSave()
        {
            // キャンセルして閉じる場合は、変更前の値を再ロードして元に戻す
            if (Core.SettingsManager.Instance != null)
            {
                Core.SettingsManager.Instance.LoadSettings();
            }
            gameObject.SetActive(false);
        }
    }
}
