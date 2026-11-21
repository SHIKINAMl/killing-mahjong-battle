using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>資料を読む時間への切り替え。入力の覆いは既存の説明Canvasを使う。</summary>
    public sealed class TutorialReadingPresentation : MonoBehaviour
    {
        public const string EffectId = "tutorial.reading";
        private const float FocusSeconds = .18f;
        private const float RiseSeconds = .24f;
        private const float SettleSeconds = .06f;
        private const float ExitSeconds = .18f;
        private const float DimAlpha = .5f;
        private const float MusicGain = .55f;
        private RectTransform content;
        private CanvasGroup contentGroup;
        private Image scrim;
        private AudioManager audioOwner;
        private bool cancelled;
        public bool IsReading { get; private set; }

        public static TutorialReadingPresentation Prepare(Transform root)
        {
            var effect = root.gameObject.AddComponent<TutorialReadingPresentation>();
            effect.scrim = root.Find("Scrim").GetComponent<Image>();
            var children = new List<Transform>();
            foreach (Transform child in root)
                if (child != effect.scrim.transform) children.Add(child);
            effect.content = new GameObject("ReadingContent", typeof(RectTransform), typeof(CanvasGroup))
                .GetComponent<RectTransform>();
            effect.content.SetParent(root, false);
            effect.content.anchorMin = Vector2.zero;
            effect.content.anchorMax = Vector2.one;
            effect.content.offsetMin = effect.content.offsetMax = Vector2.zero;
            foreach (var child in children) child.SetParent(effect.content, false);
            effect.contentGroup = effect.content.GetComponent<CanvasGroup>();
            effect.contentGroup.alpha = 0f;
            effect.contentGroup.interactable = false;
            effect.SetFocus(0f);
            return effect;
        }

        private void SetFocus(float strength)
        {
            if (scrim != null) scrim.color = new Color(0, 0, 0, DimAlpha * strength);
            if (audioOwner != null) audioOwner.SetReadingFocus(this, Mathf.Lerp(1f, MusicGain, strength));
        }

        public IEnumerator Enter()
        {
            cancelled = false;
            audioOwner = AudioManager.Instance;
            for (float t = 0; t < FocusSeconds && !cancelled; t += Time.unscaledDeltaTime)
            {
                SetFocus(Mathf.Clamp01(t / FocusSeconds));
                yield return null;
            }
            if (cancelled) yield break;
            SetFocus(1f);
            if (audioOwner != null) audioOwner.PlayPaperSlideSE();
            contentGroup.alpha = 1f;
            for (float t = 0; t < RiseSeconds && !cancelled; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / RiseSeconds);
                float ease = 1f - Mathf.Pow(1f - u, 3f);
                content.anchoredPosition = new Vector2(0, Mathf.Round(Mathf.Lerp(-80f, 4f, ease)));
                yield return null;
            }
            if (cancelled) yield break;
            if (audioOwner != null) audioOwner.PlayReadingBoardPlaceSE();
            for (float t = 0; t < SettleSeconds && !cancelled; t += Time.unscaledDeltaTime)
            {
                content.anchoredPosition = new Vector2(0, Mathf.Round(Mathf.Lerp(4f, 0f, t / SettleSeconds)));
                yield return null;
            }
            if (cancelled) yield break;
            content.anchoredPosition = Vector2.zero;
            contentGroup.interactable = true;
            IsReading = true;
        }

        public IEnumerator Exit()
        {
            IsReading = false;
            contentGroup.interactable = false;
            for (float t = 0; t < ExitSeconds && !cancelled; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / ExitSeconds);
                content.anchoredPosition = new Vector2(0, Mathf.Round(-60f * u * u));
                contentGroup.alpha = 1f - u;
                SetFocus(1f - u);
                yield return null;
            }
            Cancel();
        }

        public void Cancel()
        {
            cancelled = true;
            IsReading = false;
            if (audioOwner != null) audioOwner.ReleaseReadingFocus(this);
            audioOwner = null;
            if (contentGroup != null) { contentGroup.alpha = 0; contentGroup.interactable = false; }
            if (scrim != null) scrim.color = Color.clear;
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
