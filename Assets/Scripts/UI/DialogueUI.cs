using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("Main Dialogue")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject dialoguePanel;

        [Header("Log")]
        [SerializeField] private GameObject logPanel;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private Button toggleLogButton;

        private List<string> dialogueHistory = new List<string>();

        private void Start()
        {
            toggleLogButton.onClick.AddListener(ToggleLog);
            logPanel.SetActive(false);
        }

        public void ShowText(string text)
        {
            if (dialogueText != null)
                dialogueText.text = text;
            
            AddToLog(text);
            dialoguePanel.SetActive(true);
        }

        private void AddToLog(string text)
        {
            dialogueHistory.Add(text);
            if (logText != null)
            {
                logText.text += text + "\n";
            }
        }

        public void ToggleLog()
        {
            logPanel.SetActive(!logPanel.activeSelf);
        }
    }
}
