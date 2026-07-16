using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

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
            ronRt.anchorMin = new Vector2(0.5f, 0.5f);
            ronRt.anchorMax = new Vector2(0.5f, 0.5f);
            ronRt.anchoredPosition = new Vector2(-150, 0);

            var ronTextObj = new GameObject("Text");
            ronTextObj.transform.SetParent(ronObj.transform, false);
            var ronText = ronTextObj.AddComponent<Text>(); // Use standard Text
            ronText.text = "ロン！";
            ronText.color = Color.white;
            ronText.alignment = TextAnchor.MiddleCenter;
            ronText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ronText.fontSize = (int)KillingMahjong.Common.UITypography.BodyLarge;
            var ronTextRt = ronTextObj.GetComponent<RectTransform>();
            ronTextRt.anchorMin = Vector2.zero;
            ronTextRt.anchorMax = Vector2.one;
            ronTextRt.sizeDelta = Vector2.zero;

            var skipObj = new GameObject("SkipButton");
            skipObj.transform.SetParent(transform, false);
            skipButton = skipObj.AddComponent<Button>();
            var skipImg = skipObj.AddComponent<Image>();
            skipImg.color = Color.gray;
            var skipRt = skipObj.GetComponent<RectTransform>();
            skipRt.sizeDelta = new Vector2(200, 100);
            skipRt.anchorMin = new Vector2(0.5f, 0.5f);
            skipRt.anchorMax = new Vector2(0.5f, 0.5f);
            skipRt.anchoredPosition = new Vector2(150, 0);

            var skipTextObj = new GameObject("Text");
            skipTextObj.transform.SetParent(skipObj.transform, false);
            var skipText = skipTextObj.AddComponent<Text>(); // Use standard Text
            skipText.text = "見逃す";
            skipText.color = Color.white;
            skipText.alignment = TextAnchor.MiddleCenter;
            skipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            skipText.fontSize = (int)KillingMahjong.Common.UITypography.BodyLarge;
            var skipTextRt = skipTextObj.GetComponent<RectTransform>();
            skipTextRt.anchorMin = Vector2.zero;
            skipTextRt.anchorMax = Vector2.one;
            skipTextRt.sizeDelta = Vector2.zero;

            ronButton.onClick.AddListener(() => { onRon?.Invoke(); Hide(); });
            skipButton.onClick.AddListener(() => { onSkip?.Invoke(); Hide(); });
        }

        public void Show(System.Action onRonCallback, System.Action onSkipCallback)
        {
            this.onRon = onRonCallback;
            this.onSkip = onSkipCallback;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; // Force screen space
                canvas.sortingOrder = UISortingOrders.AgariSelectionMax;
                
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
