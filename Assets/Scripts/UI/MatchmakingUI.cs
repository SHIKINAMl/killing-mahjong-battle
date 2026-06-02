using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class MatchmakingUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIcon; // Optional spinner

        public void ShowWaiting(string message = "Waiting for Opponent\n対戦相手を待っています...")
        {
            gameObject.SetActive(true);
            if (statusText != null)
            {
                statusText.text = message;
            }
            if (loadingIcon != null) loadingIcon.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
