using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;
using TMPro;

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

        [Header("Window Settings")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Buttons")]
        [SerializeField] private Button closeButton; // 保存せずに閉じる
        [SerializeField] private Button saveAndCloseButton; // 保存して閉じる
        [SerializeField] private Button returnToTitleButton; // タイトル（または別シーン）に戻る
        [SerializeField] private Button quitButton; // ゲーム終了

        [Header("Scene Transition Settings")]
        [Tooltip("このシーンで『戻る』ボタンを表示するかどうか")]
        [SerializeField] private bool showReturnButton = true;
        [Tooltip("『戻る』ボタンを押したときに遷移するシーン名")]
        [SerializeField] private string returnSceneName = "タイトルシーン";

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
            _rectTransform = GetComponent<RectTransform>();

            // 配下のすべてのボタンにホバーエフェクトを自動追加
            Button[] allButtons = GetComponentsInChildren<Button>(true);
            foreach (var btn in allButtons)
            {
                if (btn.GetComponent<UIButtonHoverEffect>() == null)
                {
                    btn.gameObject.AddComponent<UIButtonHoverEffect>();
                }
            }
        }

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
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            // --- ドロップダウンのイベント登録 ---
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

            // --- ボタンのイベント登録 ---
            if (closeButton != null) closeButton.onClick.AddListener(CloseWithoutSave);
            if (saveAndCloseButton != null) saveAndCloseButton.onClick.AddListener(SaveAndClose);
            
            if (returnToTitleButton != null) 
            {
                returnToTitleButton.gameObject.SetActive(showReturnButton);
                returnToTitleButton.onClick.AddListener(ReturnToScene);
            }
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        }

        private void OnEnable()
        {
            InitializeUI();
        }

        /// <summary>
        /// アニメーション付きでオプション画面を開く
        /// </summary>
        public void Open()
        {
            Debug.Log("[OptionUI] Open() が呼ばれました。");
            gameObject.SetActive(true);
            
            if (_canvasGroup == null) 
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                    Debug.Log("[OptionUI] CanvasGroupを自動追加しました。");
                }
            }            
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            InitializeUI();

            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            Debug.Log($"[OptionUI] アニメーション開始。現在位置: {_rectTransform.anchoredPosition}, 親: {transform.parent?.name}");

            _rectTransform.DOKill();
            _canvasGroup.DOKill();
            
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayPaperSlideSE();
            }

            // 確実に画面中央に配置
            _rectTransform.anchoredPosition = Vector2.zero;

            // アニメーションの初期状態を明示的にセット
            _rectTransform.localScale = Vector3.one * 0.9f;
            _canvasGroup.alpha = 0f;

            // 目標値（スケール1、アルファ1）に向かってアニメーション
            _rectTransform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
            _canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true).OnComplete(() => {
                // アニメーション完了後に再度確実に入力を有効化
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            });
        }

        public void Close()
        {
            if (_canvasGroup == null) return;

            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            _rectTransform.DOKill();
            _canvasGroup.DOKill();
            
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayPaperSlideSE();
            }

            _rectTransform.DOScale(Vector3.one * 0.9f, 0.15f).SetEase(Ease.InQuad).SetUpdate(true);
            _canvasGroup.DOFade(0f, 0.15f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() => 
            {
                gameObject.SetActive(false);
            });
        }

        /// <summary>
        /// SettingsManagerが持っている現在の設定値をUI（スライダーなど）に反映する
        /// </summary>
        private void InitializeUI()
        {
            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(new System.Collections.Generic.List<string> { "800x600" });
            }

            if (Core.SettingsManager.Instance != null)
            {
                var settings = Core.SettingsManager.Instance;
                
                if (bgmSlider != null) bgmSlider.value = settings.BgmVolume;
                if (seSlider != null) seSlider.value = settings.SeVolume;
                if (voiceSlider != null) voiceSlider.value = settings.VoiceVolume;
                
                if (highSpeedToggle != null) highSpeedToggle.isOn = settings.IsHighSpeedMode;
                if (effectToggle != null) effectToggle.isOn = settings.IsEffectEnabled;
                
                if (resolutionDropdown != null) resolutionDropdown.value = 0;
                if (fullscreenToggle != null) fullscreenToggle.isOn = settings.IsFullScreen;
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

        private void OnFullscreenChanged(bool isOn)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetFullScreen(isOn);
        }

        private void OnResolutionChanged(int index)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetResolutionIndex(index);
        }

        // --- ボタン処理 ---
        public void SaveAndClose()
        {
            if (Core.SettingsManager.Instance != null)
            {
                Core.SettingsManager.Instance.SaveSettings();
            }
            Close();
        }

        public void CloseWithoutSave()
        {
            // キャンセルして閉じる場合は、変更前の値を再ロードして元に戻す
            if (Core.SettingsManager.Instance != null)
            {
                Core.SettingsManager.Instance.LoadSettings();
            }
            Close();
        }

        public void ReturnToScene()
        {
            SaveAndClose();
            if (!string.IsNullOrEmpty(returnSceneName))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(returnSceneName);
            }
        }

        public void QuitGame()
        {
            SaveAndClose();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
