using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class AgariSelectionUI : MonoBehaviour
    {
        private Button ronButton;

        private System.Action onRon;

        private void Start()
        {
            var ronObj = new GameObject("RonButton");
            ronObj.transform.SetParent(transform, false);
            ronButton = ronObj.AddComponent<Button>();
            var ronImg = ronObj.AddComponent<Image>();
            ronImg.color = Color.red;
            var ronRt = ronObj.GetComponent<RectTransform>();
            ronRt.sizeDelta = new Vector2(300, 120); // 少し大きくする
            ronRt.anchorMin = new Vector2(0.5f, 0.5f);
            ronRt.anchorMax = new Vector2(0.5f, 0.5f);
            ronRt.anchoredPosition = new Vector2(0, 0); // 中央配置

            var ronTextObj = new GameObject("Text");
            ronTextObj.transform.SetParent(ronObj.transform, false);
            var ronText = ronTextObj.AddComponent<Text>(); // Use standard Text
            ronText.text = "ロン！";
            ronText.color = Color.white;
            ronText.alignment = TextAnchor.MiddleCenter;
            ronText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ronText.fontSize = (int)KillingMahjong.Common.UITypography.Header; // 少し大きく
            var ronTextRt = ronTextObj.GetComponent<RectTransform>();
            ronTextRt.anchorMin = Vector2.zero;
            ronTextRt.anchorMax = Vector2.one;
            ronTextRt.sizeDelta = Vector2.zero;

            ronButton.onClick.AddListener(() => { onRon?.Invoke(); Hide(); });
        }

        public void Show(System.Action onRonCallback)
        {
            this.onRon = onRonCallback;
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
