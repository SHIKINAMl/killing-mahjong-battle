using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class VoltageUI
    {
        private bool feedbackReady, feedbackBroken;
        private int feedbackPoints, feedbackLevel;
        private Coroutine feedbackRoutine;
        private Vector2 feedbackBasePosition;

        private void PlayFeedback(bool broken, bool levelUp)
        {
            if (_multiplierText == null || !isActiveAndEnabled) return;
            CancelFeedback();
            feedbackBasePosition = _multiplierText.rectTransform.anchoredPosition;
            feedbackRoutine = StartCoroutine(FeedbackRoutine(broken, levelUp));
        }

        private IEnumerator FeedbackRoutine(bool broken, bool levelUp)
        {
            var rect = _multiplierText.rectTransform;
            for (float t = 0f; t < .28f; t += Time.unscaledDeltaTime)
            {
                float pulse = Mathf.Sin(Mathf.Clamp01(t / .28f) * Mathf.PI);
                rect.localScale = Vector3.one * (1f + pulse * (broken ? -.06f : levelUp ? .14f : .07f));
                rect.anchoredPosition = feedbackBasePosition + Vector2.down * (broken ? 3f * pulse : 0f);
                yield return null;
            }
            rect.localScale = Vector3.one;
            rect.anchoredPosition = feedbackBasePosition;
            feedbackRoutine = null;
        }

        private void CancelFeedback()
        {
            if (feedbackRoutine == null) return;
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
            if (_multiplierText == null) return;
            _multiplierText.rectTransform.localScale = Vector3.one;
            _multiplierText.rectTransform.anchoredPosition = feedbackBasePosition;
        }

        /// <summary>試写専用の表示インスタンス。実際のボルテージには書き込まない。</summary>
        internal static VoltageUI CreatePresentation(RectTransform parent)
        {
            var ui = Attach(parent, false);
            Managers.VoltageSystem.Changed -= ui.Refresh;
            return ui;
        }

        internal void PresentFeedback(int level, bool broken, int points, int from, int to, float multiplier)
        {
            RenderFeedbackState(level, broken, points, from, to, multiplier);
            PlayFeedback(broken, level > feedbackLevel);
            feedbackLevel = level;
        }
    }
}
