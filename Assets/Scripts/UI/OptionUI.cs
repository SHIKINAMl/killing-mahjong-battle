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
        // **いまは選択欄のひな形としてだけ使う（2026-09-27）。**
        // 解像度の切り替え自体は「画面サイズ」へ移した。この欄は画面には出さないが、
        // 見た目をそろえるための複製元なので消さないこと。
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Buttons")]
        [SerializeField] private Button closeButton; // 保存せずに閉じる
        [SerializeField] private Button saveAndCloseButton; // 保存して閉じる
        [SerializeField] private Button returnToTitleButton; // タイトル（または別シーン）に戻る
        [SerializeField] private Button quitButton; // ゲーム終了

        // シーンを編集せず、既存の保存ボタンをひな形にして実行時に追加する。
        private Button tutorialArchiveButton;
        private TutorialArchiveUI tutorialArchiveUI;

        // 対局BGMの選択欄。**解像度の選択欄をひな形にして実行時に作る。**
        // シーンに置くと、対局シーンとOpeningSceneの2つを同じように直す必要があり、
        // 片方だけ古いままになる（セリフの影で実際に起きた）。
        private TMP_Dropdown matchBgmDropdown;
        private TMP_Dropdown textSpeedDropdown;
        private TMP_Dropdown screenModeDropdown;

        /// <summary>
        /// 選択欄の行の位置（anchoredPosition の y）。
        ///
        /// 音量3つが 195 / 140 / 80 に並んでいるので、その下へ 55 刻みで続ける。
        /// 画面の上からの位置に直すと 265 / 320 / 375px。
        /// </summary>
        private const float RowMatchBgm = 35f;
        private const float RowTextSpeed = -20f;
        private const float RowScreenMode = -75f;

        [Header("Scene Transition Settings")]
        [Tooltip("このシーンで『戻る』ボタンを表示するかどうか")]
        [SerializeField] private bool showReturnButton = true;
        [Tooltip("『戻る』ボタンを押したときに遷移するシーン名")]
        [SerializeField] private string returnSceneName = "タイトルシーン";

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;

        // --- チュートリアルの誘導用（2026-09-24、フロー図シート3） ---
        //
        // 資料を読んでいる間 `OpenTutorialArchive` が自分を SetActive(false) するので、
        // **activeSelf では「閉じた」と「資料を読んでいる」が見分けられない。**
        // 開いたか閉じたかは自分で覚えておく。

        private bool _isOpen;

        /// <summary>オプション画面が開いているか。資料を読んでいる間も開いている扱い。</summary>
        public bool IsOpen => _isOpen;

        /// <summary>資料を読んでいる最中か。</summary>
        public bool IsTutorialArchiveOpen =>
            tutorialArchiveUI != null && tutorialArchiveUI.gameObject.activeInHierarchy;

        /// <summary>「チュートリアル資料」ボタン。実行時に作るので外から取れるようにしておく。</summary>
        public RectTransform TutorialArchiveButtonRect =>
            tutorialArchiveButton != null ? tutorialArchiveButton.transform as RectTransform : null;

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

            // ひな形の Button にはまだ Start 内のクリック処理が入っていない段階で複製する。
            // こうして資料ボタンへ「保存して閉じる」の処理が混ざるのを防ぐ。
            CreateTutorialArchiveButton();
            BuildSettingRows();

            // --- スライダーのイベント登録 ---
            if (bgmSlider != null) bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            if (seSlider != null) seSlider.onValueChanged.AddListener(OnSeChanged);
            if (voiceSlider != null) voiceSlider.onValueChanged.AddListener(OnVoiceChanged);
            
            // --- トグルのイベント登録 ---
            if (highSpeedToggle != null) highSpeedToggle.onValueChanged.AddListener(OnHighSpeedChanged);
            if (effectToggle != null) effectToggle.onValueChanged.AddListener(OnEffectChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            // --- ドロップダウンのイベント登録 ---

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

        private void OnDestroy()
        {
            // 資料は独立した Overlay Canvas なので、オプションだけが破棄された場合にも残さない。
            if (tutorialArchiveUI != null)
            {
                Destroy(tutorialArchiveUI.gameObject);
            }
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
            _isOpen = true;
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
            _isOpen = false;
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

                // **通知を止めてから入れる。** そのまま代入すると onValueChanged が走り、
                // 画面を開いただけで曲が鳴り直したり、画面サイズが切り替わったりする
                SetDropdown(matchBgmDropdown, settings.MatchBgmSet);
                SetDropdown(textSpeedDropdown, settings.TextSpeed);
                SetDropdown(screenModeDropdown, settings.ScreenMode);
            }
        }

        /// <summary>
        /// 選択欄に値を入れる。**表示の更新まで面倒を見る。**
        /// `SetValueWithoutNotify` だけだと、複製直後は見出しの文字がひな形のまま残る。
        /// </summary>
        private static void SetDropdown(TMP_Dropdown dropdown, int value)
        {
            if (dropdown == null) return;
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, dropdown.options.Count - 1)));
            dropdown.RefreshShownValue();
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


        /// <summary>
        /// 設定画面を組み直す（2026-09-27）。
        ///
        /// **読む所が無い設定を画面から下ろし、実際に効くものだけ並べる。**
        /// 下ろしたのは High Speed Mode / Show Effects（どちらも値を持つだけで
        /// 誰も読んでいなかった）と Window Resolution（選択肢が 800x600 の1つだけで、
        /// しかも ApplyResolution が値を無視していた）。Fullscreen は「画面サイズ」に統合した。
        ///
        /// **保存値（PlayerPrefs）は消していない。** あとで機能を作ったときに
        /// そのまま復活できるようにしてある。
        ///
        /// 参考にしたのは NEEDY GIRL OVERDOSE の設定で、あちらは BGM / SE / 解像度 /
        /// 進行の速さ / 言語 の5項目だけだった。絞る方向に倣っている。
        /// </summary>
        private void BuildSettingRows()
        {
            if (resolutionDropdown == null)
            {
                Debug.LogWarning("[OptionUI] 選択欄のひな形（解像度の欄）が見つかりません。");
                return;
            }

            // **ひな形を隠すのは、複製を作り終えてから。** 先に消すと複製元が無くなる
            // **いまの設定をここで入れる。** 画面を開いたときの当て直し（LoadSettingsToUI）は
            // この欄を作るより前に走るので、そちらだけに任せると常に先頭のまま出る
            // （実機で「文字送り」が既定の「ふつう」ではなく「ゆっくり」と表示された）。
            var current = Core.SettingsManager.Instance;
            matchBgmDropdown = CreateDropdownRow("MatchBgm", "対局BGM",
                Core.SettingsManager.MatchBgmSetLabels, RowMatchBgm,
                current != null ? current.MatchBgmSet : 0, OnMatchBgmChanged);
            textSpeedDropdown = CreateDropdownRow("TextSpeed", "文字送り",
                Core.SettingsManager.TextSpeedLabels, RowTextSpeed,
                current != null ? current.TextSpeed : 1, OnTextSpeedChanged);
            screenModeDropdown = CreateDropdownRow("ScreenMode", "画面サイズ",
                Core.SettingsManager.ScreenModeLabels, RowScreenMode,
                current != null ? current.ScreenMode : 0, OnScreenModeChanged);

            HideRetiredRows();
            ArrangeSystemButtons();
            MakeBackdropOpaque();
        }

        /// <summary>
        /// 見出しと選択欄が1つずつの行を作る。
        /// **解像度の欄を複製する。** 同じ見た目・同じ大きさになるので、自前で組むより早く、
        /// あとでデザインが変わっても勝手に追従する。
        /// </summary>
        private TMP_Dropdown CreateDropdownRow(string name, string label, string[] options,
                                               float anchoredY, int initialValue,
                                               UnityEngine.Events.UnityAction<int> onChanged)
        {
            var go = Instantiate(resolutionDropdown.gameObject, resolutionDropdown.transform.parent, false);
            go.name = name + "Dropdown";
            var dd = go.GetComponent<TMP_Dropdown>();
            dd.onValueChanged.RemoveAllListeners();
            dd.ClearOptions();
            dd.AddOptions(new System.Collections.Generic.List<string>(options));

            var src = resolutionDropdown.transform as RectTransform;
            var rt = go.transform as RectTransform;
            if (src != null && rt != null) rt.anchoredPosition = new Vector2(src.anchoredPosition.x, anchoredY);

            var srcLabel = FindSiblingLabel(resolutionDropdown.transform);
            if (srcLabel != null)
            {
                var lgo = Instantiate(srcLabel.gameObject, srcLabel.transform.parent, false);
                lgo.name = name + "Label";
                var txt = lgo.GetComponent<TMP_Text>();
                if (txt != null) txt.text = label;
                var lsrc = srcLabel.transform as RectTransform;
                var lrt = lgo.transform as RectTransform;
                if (lsrc != null && lrt != null) lrt.anchoredPosition = new Vector2(lsrc.anchoredPosition.x, anchoredY);
            }

            // **値を入れてから listener を付ける。** 逆にすると、作った瞬間に
            // 設定変更として走ってしまう（画面サイズなら勝手に切り替わる）
            SetDropdown(dd, initialValue);
            dd.onValueChanged.AddListener(onChanged);
            return dd;
        }

        /// <summary>画面から下ろした行を隠す。**消さずに隠す**ので、戻すのは1行で済む。</summary>
        private void HideRetiredRows()
        {
            foreach (var n in new string[] { "SpeedText", "HighSpeedToggle", "EffectText", "EffectToggle",
                                             "FullscreenText", "FullscreenToggle", "ResolutionText" })
            {
                var t = FindChildByName(transform, n);
                if (t != null) t.gameObject.SetActive(false);
            }
            // ひな形にした解像度の欄は、複製が済んでから隠す
            if (resolutionDropdown != null) resolutionDropdown.gameObject.SetActive(false);
            // 「やめる」は不要との判断（2026-09-27）
            if (quitButton != null) quitButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// 背景を不透明にする。
        ///
        /// **0.60 のままだと後ろが透ける。** チュートリアル中に開くと契約書の本文と
        /// 「次へ」ボタンが浮かび上がって、設定のボタンと重なって見えていた。
        /// </summary>
        private void MakeBackdropOpaque()
        {
            var img = GetComponent<Image>();
            if (img == null) return;
            var c = img.color;
            c.a = 1f;
            img.color = c;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>選択欄の見出しらしき TMP_Text を、同じ親の中から探す。</summary>
        private static TMP_Text FindSiblingLabel(Transform dropdown)
        {
            var parent = dropdown.parent;
            if (parent == null) return null;
            var dRect = dropdown as RectTransform;
            if (dRect == null) return null;

            TMP_Text best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == dropdown) continue;
                if (child.GetComponentInParent<TMP_Dropdown>() != null) continue;
                var txt = child.GetComponent<TMP_Text>();
                if (txt == null) continue;
                var r = child as RectTransform;
                if (r == null) continue;
                float d = Mathf.Abs(r.anchoredPosition.y - dRect.anchoredPosition.y);
                if (d < bestDist) { bestDist = d; best = txt; }
            }
            return bestDist <= 40f ? best : null;
        }

        private void OnMatchBgmChanged(int index)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetMatchBgmSet(index);
        }

        private void OnTextSpeedChanged(int index)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetTextSpeed(index);
        }

        private void OnScreenModeChanged(int index)
        {
            if (Core.SettingsManager.Instance != null) Core.SettingsManager.Instance.SetScreenMode(index);
        }

        private void CreateTutorialArchiveButton()
        {
            if (tutorialArchiveButton != null) return;

            // 保存ボタンは長い日本語ラベルを収められる横幅なので、見た目のひな形に使う。
            Button template = saveAndCloseButton != null ? saveAndCloseButton : returnToTitleButton;
            if (template == null)
            {
                Debug.LogWarning("[OptionUI] チュートリアル資料のひな形ボタンが見つかりません。");
                return;
            }

            GameObject buttonObject = Instantiate(template.gameObject, template.transform.parent, false);
            buttonObject.name = "TutorialArchiveButton";
            tutorialArchiveButton = buttonObject.GetComponent<Button>();
            tutorialArchiveButton.onClick.RemoveAllListeners();

            TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "チュートリアル資料";

            if (buttonObject.GetComponent<UIButtonHoverEffect>() == null)
            {
                buttonObject.AddComponent<UIButtonHoverEffect>();
            }

            ArrangeSystemButtons();
            tutorialArchiveButton.onClick.AddListener(OpenTutorialArchive);
        }

        private void ArrangeSystemButtons()
        {
            // **「やめる」を下ろしたので3つ（2026-09-27）。**
            // 上段に「タイトルへ」と「チュートリアル資料」、下段の中央に「保存して閉じる」。
            // 保存がいちばん押す操作なので、単独で目立つ位置に置く。
            //
            // 以前は4つを2段2列に並べ、いちばん下が画面の端に張り付いていた。
            SetButtonPosition(returnToTitleButton, new Vector2(-135f, -160f));
            SetButtonPosition(tutorialArchiveButton, new Vector2(135f, -160f));
            SetButtonPosition(saveAndCloseButton, new Vector2(0f, -225f));
        }

        private static void SetButtonPosition(Button button, Vector2 position)
        {
            if (button == null) return;

            RectTransform rect = button.transform as RectTransform;
            if (rect != null) rect.anchoredPosition = position;
        }

        private void OpenTutorialArchive()
        {
            if (tutorialArchiveUI == null)
            {
                tutorialArchiveUI = TutorialArchiveUI.Create();
            }

            // 資料を読んでいる間は、背後の設定を誤って操作できないようオプション自体を伏せる。
            gameObject.SetActive(false);
            tutorialArchiveUI.Open(Open);
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
