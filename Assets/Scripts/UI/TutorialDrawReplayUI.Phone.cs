using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public sealed partial class TutorialDrawReplayUI
    {
        private const float PhoneZoomSeconds = .82f;
        private SpriteRenderer phoneCharacter;
        private RectTransform presentation;
        private CanvasGroup presentationGroup;
        private Image presentationDimmer;
        private Vector2 phoneOrigin;
        private float phoneScale = .4f;

        // Canvas本体の拡縮はScalerやマスクと競合するので、動画の内容だけを動かす。
        // 演出の途中状態を持つ一時コンテナ。終了・中断時は動画空間と一緒に破棄される。
        private void BuildPhonePresentation()
        {
            var children = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name == "Dimmer") presentationDimmer = child.GetComponent<Image>();
                else children.Add(child);
            }
            var group = new GameObject("PhoneVideoPresentation", typeof(RectTransform), typeof(CanvasGroup));
            group.transform.SetParent(transform, false);
            presentation = (RectTransform)group.transform;
            presentation.sizeDelta = new Vector2(800f, 600f);
            presentationGroup = group.GetComponent<CanvasGroup>();
            presentationGroup.alpha = 0f;
            foreach (var child in children) child.SetParent(presentation, false);
            if (presentationDimmer != null) presentationDimmer.color = Color.clear;
        }

        private void ResolvePhoneOrigin()
        {
            phoneOrigin = Vector2.zero;
            phoneScale = .4f;
            var sprite = phoneCharacter != null ? phoneCharacter.sprite : null;
            var camera = Camera.main;
            if (sprite == null || sprite.name != "tutorialexe_smartphone" || camera == null
                || !phoneCharacter.gameObject.activeInHierarchy) return;

            // 支給画像の黒い画面部分。座標はSprite内の左下原点、ピクセル単位。
            var screenPixels = new Rect(43f, 64f, 39f, 17f);
            var min = camera.WorldToScreenPoint(phoneCharacter.transform.TransformPoint(
                (new Vector3(screenPixels.xMin, screenPixels.yMin) - (Vector3)sprite.pivot) / sprite.pixelsPerUnit));
            var max = camera.WorldToScreenPoint(phoneCharacter.transform.TransformPoint(
                (new Vector3(screenPixels.xMax, screenPixels.yMax) - (Vector3)sprite.pivot) / sprite.pixelsPerUnit));
            var canvasRect = (RectTransform)transform;
            Vector2 localMin, localMax;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, min, null, out localMin)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, max, null, out localMax)) return;
            phoneOrigin = (localMin + localMax) * .5f;
            phoneScale = Mathf.Min(Mathf.Abs(localMax.x - localMin.x) / 800f,
                Mathf.Abs(localMax.y - localMin.y) / 600f);
            phoneScale = Mathf.Clamp(phoneScale, .001f, 1f);
        }

        private void SetPhoneZoom(float progress)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t < .5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * .5f;
            presentation.anchoredPosition = Vector2.Lerp(phoneOrigin, Vector2.zero, eased);
            presentation.localScale = Vector3.one * Mathf.Lerp(phoneScale, 1f, eased);
            if (presentationDimmer != null)
                presentationDimmer.color = new Color(0f, 0f, 0f, .85f * eased);
        }

        private IEnumerator OpenFromPhone()
        {
            Canvas.ForceUpdateCanvases();
            ResolvePhoneOrigin();
            presentationGroup.interactable = false;
            presentationGroup.blocksRaycasts = false;
            SetPhoneZoom(0f);
            presentationGroup.alpha = 1f;
            // スマホの中で映ったことが分かる短い間。
            yield return new WaitForSecondsRealtime(.18f);
            for (float elapsed = 0f; elapsed < PhoneZoomSeconds; elapsed += Time.unscaledDeltaTime)
            {
                SetPhoneZoom(PresentationTiming.Sample(elapsed) / PhoneZoomSeconds);
                yield return null;
            }
            SetPhoneZoom(1f);
            presentationGroup.interactable = true;
            presentationGroup.blocksRaycasts = true;
        }

        private IEnumerator ReturnToPhone()
        {
            presentationGroup.interactable = false;
            presentationGroup.blocksRaycasts = false;
            for (float elapsed = 0f; elapsed < .4f; elapsed += Time.unscaledDeltaTime)
            {
                SetPhoneZoom(1f - PresentationTiming.Sample(elapsed) / .4f);
                yield return null;
            }
            SetPhoneZoom(0f);
        }
    }
}
