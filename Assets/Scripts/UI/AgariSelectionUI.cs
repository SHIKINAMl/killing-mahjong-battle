using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    public class AgariSelectionUI : MonoBehaviour
    {
        private Button ronButton;
        private Button skipButton;

        private System.Action onRon;
        private System.Action onSkip;

        private void Start()
        {
            var ronObj = new GameObject("RonButton");
            ronObj.transform.SetParent(transform, false);
            ronButton = ronObj.AddComponent<Button>();
            var ronImg = ronObj.AddComponent<Image>();
            ronImg.color = Color.red;
            var ronRt = ronObj.GetComponent<RectTransform>();
            ronRt.sizeDelta = new Vector2(200, 100);
            ronRt.anchoredPosition = new Vector2(-150, 0);

            var ronTextObj = new GameObject("Text");
            ronTextObj.transform.SetParent(ronObj.transform, false);
            var ronText = ronTextObj.AddComponent<TextMeshProUGUI>();
            ronText.text = "ロン！";
            ronText.color = Color.white;
            ronText.alignment = TextAlignmentOptions.Center;
            var ronTextRt = ronTextObj.GetComponent<RectTransform>();
            ronTextRt.sizeDelta = new Vector2(200, 100);

            var skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(transform, false);
            skipButton = skipObj.AddComponent<Button>();
            var skipImg = skipObj.AddComponent<Image>();
            skipImg.color = Color.gray;
            var skipRt = skipObj.GetComponent<RectTransform>();
            skipRt.sizeDelta = new Vector2(200, 100);
            skipRt.anchoredPosition = new Vector2(150, 0);

            var skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);
            var skipText = skipTextObj.AddComponent<TextMeshProUGUI>();
            skipText.text = "見逃す";
            skipText.color = Color.white;
            skipText.alignment = TextAlignmentOptions.Center;
            var skipTextRt = skipTextObj.GetComponent<RectTransform>();
            skipTextRt.sizeDelta = new Vector2(200, 100);

            ronButton.onClick.AddListener(() => { onRon?.Invoke(); Hide(); });
            skipButton.onClick.AddListener(() => { onSkip?.Invoke(); Hide(); });
        }

        public void Show(System.Action onRonCallback, System.Action onSkipCallback)
        {
            this.onRon = onRonCallback;
            this.onSkip = onSkipCallback;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
