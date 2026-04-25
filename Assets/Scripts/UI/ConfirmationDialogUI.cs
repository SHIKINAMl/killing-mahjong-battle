using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace KillingMahjong.UI
{
    public class ConfirmationDialogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button noButton;

        private Action onConfirmAction;
        private Action onCancelAction;

        private void Awake()
        {
            if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
            if (noButton != null) noButton.onClick.AddListener(OnNoClicked);
        }

        public void ShowDialog(string message, Action onConfirm, Action onCancel)
        {
            if (messageText != null) messageText.text = message;
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;
            gameObject.SetActive(true);
        }

        private void OnOkClicked()
        {
            gameObject.SetActive(false);
            onConfirmAction?.Invoke();
        }

        private void OnNoClicked()
        {
            gameObject.SetActive(false);
            onCancelAction?.Invoke();
        }
    }
}
