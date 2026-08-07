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
            Debug.Log($"[MatchmakingUI] ShowWaiting called with message: {message}");
            gameObject.SetActive(true);
            if (statusText != null)
            {
                statusText.text = message;
            }
            else
            {
                Debug.LogError("[MatchmakingUI] statusText is null!");
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
