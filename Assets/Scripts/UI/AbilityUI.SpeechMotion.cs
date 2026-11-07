using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private Image speechHead;
        private Sprite speechFrame;
        private Sprite speechOriginalFrame;
        private Vector2 speechHeadOrigin;
        private float speechStartedAt;
        private static readonly int[] SpeechHeadOffsets = { 0, 2, -2, 2, 0, -2 };

        private void LateUpdate()
        {
            RefreshTransitionSuppression();
            bool speaking = _descText != null && _descText.gameObject.activeInHierarchy
                && !string.IsNullOrEmpty(_descText.text) && _descText.text != DescPlaceholder;
            if (!speaking)
            {
                RestoreSpeechHead();
                return;
            }

            var frame = ResolveFrameImage();
            if (frame == null || frame.sprite == null) return;
            if (speechHead == null)
            {
                speechFrame = Resources.Load<Sprite>("UI/AbilitySpeechFrame");
                var head = Resources.Load<Sprite>("UI/AbilitySpeechHead");
                if (speechFrame == null || head == null) return;
                var go = new GameObject("SpeakingHead", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(frame.transform, false);
                speechHead = go.GetComponent<Image>();
                speechHead.sprite = head;
                speechHead.raycastTarget = false;
                speechHead.gameObject.SetActive(false);
            }

            if (!speechHead.gameObject.activeSelf)
            {
                speechOriginalFrame = frame.sprite;
                frame.sprite = speechFrame;
                var bounds = frame.GetPixelAdjustedRect();
                var headRect = speechHead.rectTransform;
                headRect.anchorMin = headRect.anchorMax = headRect.pivot = new Vector2(0f, 1f);
                headRect.sizeDelta = new Vector2(bounds.width * 260f / 1010f, bounds.height * 210f / 1570f);
                speechHeadOrigin = new Vector2(bounds.width * 410f / 1010f, -bounds.height * 40f / 1570f);
                speechStartedAt = Time.unscaledTime;
                speechHead.gameObject.SetActive(true);
            }

            int step = Mathf.FloorToInt((Time.unscaledTime - speechStartedAt) * 18f) % SpeechHeadOffsets.Length;
            speechHead.rectTransform.anchoredPosition = speechHeadOrigin + new Vector2(SpeechHeadOffsets[step], 0f);
            speechHead.color = frame.color;
        }

        private void RestoreSpeechHead()
        {
            if (speechHead == null || !speechHead.gameObject.activeSelf) return;
            speechHead.rectTransform.anchoredPosition = speechHeadOrigin;
            speechHead.gameObject.SetActive(false);
            // 閉じるコマ送りが始まっていたら、そのコマを優先する。
            if (windowFrameImage != null && windowFrameImage.sprite == speechFrame)
                windowFrameImage.sprite = speechOriginalFrame;
        }

        private void OnDisable()
        {
            RestoreSpeechHead();
        }
    }
}
