using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using KillingMahjong.Core;

namespace KillingMahjong.UI
{
    public enum VictoryType
    {
        NormalVictory,
        NormalDefeat,
        SpecialVictory,
        SpecialDefeat
    }

    [System.Serializable]
    public class VictoryConfig
    {
        public VictoryType victoryType;
        public Sprite image;
        [TextArea] public string text;
    }

    public class VictoryUI : MonoBehaviour
    {
        /// <summary>スコアが渡されなかったことを表す番兵。特殊勝利のように最終スコアが無い経路で使う。</summary>
        public const int UnknownScore = int.MinValue;

        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button titleButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Optional")]
        [Tooltip("最終スコアと通算成績を出す専用テキスト。未設定ならセリフ欄の下に追記する。")]
        [SerializeField] private TextMeshProUGUI summaryText;

        [Tooltip("再戦ボタン。未設定ならタイトルボタンを複製して実行時に生成する。")]
        [SerializeField] private Button rematchButton;

        [Tooltip("再戦ボタンをタイトルボタンから自動生成するときの位置ずらし量")]
        [SerializeField] private Vector2 autoRematchButtonOffset = new Vector2(0f, 110f);

        [Header("Configurations")]
        [SerializeField] private VictoryConfig[] configs;

        private bool resultRecorded = false;

        private void Awake()
        {
            if (titleButton != null)
            {
                titleButton.onClick.AddListener(OnTitleButtonClicked);
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>旧シグネチャ。スコア不明のまま結果を出す。</summary>
        public void PlayAnimation(VictoryType type)
        {
            PlayAnimation(type, UnknownScore, UnknownScore);
        }

        public void PlayAnimation(VictoryType type, int localScore, int enemyScore)
        {
            gameObject.SetActive(true);

            bool isWin = (type == VictoryType.NormalVictory || type == VictoryType.SpecialVictory);
            bool isSpecial = (type == VictoryType.SpecialVictory);

            // 対局の結果がこれまで一切保存されていなかったため、ここで通算戦績に記録する。
            // 呼び出し経路が複数あるので、1試合につき1回だけになるようガードする。
            if (!resultRecorded)
            {
                resultRecorded = true;
                PlayerStatsManager.RecordMatchResult(isWin, isSpecial);
            }

            // Apply config based on type
            if (configs != null)
            {
                foreach (var config in configs)
                {
                    if (config.victoryType == type)
                    {
                        if (backgroundImage != null && config.image != null)
                            backgroundImage.sprite = config.image;
                        if (dialogueText != null && !string.IsNullOrEmpty(config.text))
                            dialogueText.text = config.text;
                        break;
                    }
                }
            }

            ApplySummary(type, localScore, enemyScore);
            EnsureRematchButton();

            StartCoroutine(FadeInRoutine());
        }

        /// <summary>
        /// 「敗北...」の一言だけでは何が起きたのか分からないため、
        /// 最終スコアと通算成績を添える。
        /// </summary>
        private void ApplySummary(VictoryType type, int localScore, int enemyScore)
        {
            var sb = new System.Text.StringBuilder();

            if (localScore != UnknownScore && enemyScore != UnknownScore)
            {
                sb.Append($"最終HP  あなた {localScore}  /  相手 {enemyScore}");
            }
            else if (type == VictoryType.SpecialVictory)
            {
                sb.Append("特殊勝利による決着");
            }
            else if (type == VictoryType.SpecialDefeat)
            {
                sb.Append("相手の特殊勝利による決着");
            }

            if (sb.Length > 0) sb.Append('\n');
            sb.Append(PlayerStatsManager.BuildSummaryLine());

            string summary = sb.ToString();

            if (summaryText != null)
            {
                summaryText.text = summary;
            }
            else if (dialogueText != null)
            {
                // 専用テキストがInspectorで用意されていない場合でも見えるように追記する
                dialogueText.text = $"{dialogueText.text}\n<size=60%>{summary}</size>";
            }
        }

        /// <summary>
        /// 再戦ボタンがInspectorで割り当てられていない場合、タイトルボタンを複製して用意する。
        /// Prefab/シーンを触らなくても「もう一戦」の導線が出るようにするための措置。
        /// </summary>
        private void EnsureRematchButton()
        {
            if (rematchButton != null)
            {
                rematchButton.onClick.RemoveListener(OnRematchButtonClicked);
                rematchButton.onClick.AddListener(OnRematchButtonClicked);
                return;
            }

            if (titleButton == null) return;

            var clone = Instantiate(titleButton, titleButton.transform.parent);
            clone.name = "RematchButton";

            // 複製元のリスナー（タイトルへ戻る）を引き継がせない。
            // RemoveAllListeners は Inspector で設定された永続リスナーを消さないので個別に無効化する。
            clone.onClick.RemoveAllListeners();
            for (int i = 0; i < clone.onClick.GetPersistentEventCount(); i++)
            {
                clone.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
            }
            clone.onClick.AddListener(OnRematchButtonClicked);

            var cloneRt = clone.GetComponent<RectTransform>();
            var srcRt = titleButton.GetComponent<RectTransform>();
            if (cloneRt != null && srcRt != null)
            {
                cloneRt.anchoredPosition = srcRt.anchoredPosition + autoRematchButtonOffset;
            }

            SetButtonLabel(clone, "もう一戦");

            rematchButton = clone;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var tmp = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                tmp.text = label;
                return;
            }

            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
        }

        private IEnumerator FadeInRoutine()
        {
            if (canvasGroup == null) yield break;

            canvasGroup.blocksRaycasts = true;

            float elapsed = 0f;
            float duration = 0.5f; // Fast fade in for impact

            // Screen Shake effect parameters
            Vector3 originalPos = transform.localPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);

                // Shake
                transform.localPosition = originalPos + (Vector3)(Random.insideUnitCircle * 20f);

                yield return null;
            }

            transform.localPosition = originalPos;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
        }

        private void OnTitleButtonClicked()
        {
            // Reset timescale just in case
            Time.timeScale = 1f;
            SceneManager.LoadScene("タイトルシーン");
        }

        /// <summary>
        /// 同じ対戦シーンを読み込み直して即座に再戦する。
        /// 従来はタイトル経由の2回のシーン遷移が必要だった。
        /// </summary>
        private void OnRematchButtonClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
