using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
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
        [Tooltip("クリックで進める本文。未設定の場合は従来の text を1ページとして使う。")]
        [TextArea] public string[] dialoguePages;
        [Tooltip("本文終了後に表示する結果見出し（例：DEAD END）。")]
        public string resultTitle;
        [Tooltip("エンディング番号と名前（例：No.4：激烈借金地獄）。")]
        public string endingName;
    }

    public class VictoryUI : MonoBehaviour
    {
        public const int UnknownScore = int.MinValue;

        // 既存シーンの参照を維持。表示は共通の EndingSequenceUI が担当する。
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button titleButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private VictoryConfig[] configs;

        private bool resultRecorded;
        private EndingSequenceUI sequence;

        private void Awake()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void PlayAnimation(VictoryType type)
        {
            PlayAnimation(type, UnknownScore, UnknownScore);
        }

        public void PlayAnimation(VictoryType type, int localScore, int enemyScore)
        {
            gameObject.SetActive(true);
            if (!resultRecorded)
            {
                resultRecorded = true;
                PlayerStatsManager.RecordMatchResult(
                    type == VictoryType.NormalVictory || type == VictoryType.SpecialVictory,
                    type == VictoryType.SpecialVictory);
            }

            VictoryConfig selected = null;
            if (configs != null)
                foreach (var config in configs)
                    if (config != null && config.victoryType == type) { selected = config; break; }

            if (sequence == null)
            {
                var root = new GameObject("EndingSequence", typeof(RectTransform));
                // 独立した全画面 Canvas。旧結果パネルの矩形や透明度を引き継がない。
                sequence = root.AddComponent<EndingSequenceUI>();
            }
            sequence.Show(selected, dialogueText != null ? dialogueText.font : null, OnTitleButtonClicked);
        }

        private void OnDestroy()
        {
            if (sequence != null) Destroy(sequence.gameObject);
        }

        private void OnTitleButtonClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("タイトルシーン");
        }
    }
}
