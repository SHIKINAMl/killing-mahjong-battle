using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

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
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private Button titleButton;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Configurations")]
        [SerializeField] private VictoryConfig[] configs;

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

        public void PlayAnimation(VictoryType type)
        {
            gameObject.SetActive(true);

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

            StartCoroutine(FadeInRoutine());
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
    }
}
