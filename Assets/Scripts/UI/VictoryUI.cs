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
        [Tooltip("勝利用の演出案。未設定の間は、対応する敗北演出の内容を仮に使用する。")]
        [SerializeField] private VictoryConfig[] victoryEndingConfigs;

        private bool resultRecorded;
        private EndingSequenceUI victorySequence;
        private EndingSequenceUI defeatSequence;

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

            if (type == VictoryType.NormalVictory || type == VictoryType.SpecialVictory)
                PlayVictoryEnding(type);
            else
                PlayDefeatEnding(type);
        }

        private void PlayVictoryEnding(VictoryType type)
        {
            var selected = FindConfig(victoryEndingConfigs, type);
            if (selected == null)
            {
                // 勝利案ができるまでは演出内容だけ借用し、勝利の記録・経路は維持する。
                var placeholderType = type == VictoryType.NormalVictory
                    ? VictoryType.NormalDefeat : VictoryType.SpecialDefeat;
                selected = FindConfig(configs, placeholderType) ?? FindConfig(configs, type);
            }
            if (defeatSequence != null) defeatSequence.gameObject.SetActive(false);
            ShowSequence(ref victorySequence, "VictoryEndingSequence", selected);
        }

        private void PlayDefeatEnding(VictoryType type)
        {
            if (victorySequence != null) victorySequence.gameObject.SetActive(false);
            ShowSequence(ref defeatSequence, "DefeatEndingSequence", FindConfig(configs, type));
        }

        private static VictoryConfig FindConfig(VictoryConfig[] source, VictoryType type)
        {
            if (source != null)
                foreach (var config in source)
                    if (config != null && config.victoryType == type) return config;
            return null;
        }

        private void ShowSequence(ref EndingSequenceUI sequence, string name, VictoryConfig selected)
        {
            if (sequence == null)
            {
                var root = new GameObject(name, typeof(RectTransform));
                // 独立した全画面 Canvas。旧結果パネルの矩形や透明度を引き継がない。
                sequence = root.AddComponent<EndingSequenceUI>();
            }
            sequence.Show(selected, dialogueText != null ? dialogueText.font : null, OnTitleButtonClicked);
        }

        private void OnDestroy()
        {
            if (victorySequence != null) Destroy(victorySequence.gameObject);
            if (defeatSequence != null) Destroy(defeatSequence.gameObject);
        }

        private void OnTitleButtonClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("タイトルシーン");
        }
    }
}
