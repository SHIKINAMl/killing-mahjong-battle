using UnityEngine;
using TMPro;
using System.Collections;
using System.Globalization;

namespace KillingMahjong.UI
{
    public class DamagePopupUI : MonoBehaviour
    {
        private const float FontSize = 36f;
        private const float Duration = 1.15f;
        private const float Rise = 18f;
        private CanvasGroup group;
        private GameObject visual;

        public void Setup(int amount)
        {
            StopAllCoroutines();
            if (visual != null)
            {
                visual.SetActive(false);
                Destroy(visual);
            }
            // 旧プレハブに文字が直付けされていても、二重に描かない。
            var legacy = GetComponent<TextMeshProUGUI>();
            if (legacy != null) legacy.enabled = false;
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            visual = new GameObject("HpNumberVisual", typeof(RectTransform));
            visual.transform.SetParent(transform, false);
            var font = Resources.Load<TMP_FontAsset>("x8y12pxDenkiChip/x8y12pxDenkiChip_Dynamic")
                ?? Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed") ?? TMP_Settings.defaultFontAsset;
            string sign = amount > 0 ? "+" : "-";
            string digits = System.Math.Abs((long)amount).ToString("N0", CultureInfo.InvariantCulture);
            string plain = sign + digits;
            var ink = new Color32(29, 13, 22, 255);
            CreateNumber("Shadow", font, plain, new Vector2(3f, -3f), ink);
            CreateNumber("EdgeLeft", font, plain, new Vector2(-2f, 0f), ink);
            CreateNumber("EdgeRight", font, plain, new Vector2(2f, 0f), ink);
            CreateNumber("EdgeTop", font, plain, new Vector2(0f, 2f), ink);
            CreateNumber("EdgeBottom", font, plain, new Vector2(0f, -2f), ink);
            string accent = amount > 0 ? "72DFC0" : "F26F7C";
            string face = amount > 0 ? "D9FFF3" : "FFE4DF";
            var number = CreateNumber("Number", font,
                "<color=#" + accent + ">" + sign + "</color><color=#" + face + ">" + digits + "</color>",
                Vector2.zero, Color.white);
            var size = number.GetPreferredValues(plain) + new Vector2(16f, 10f);
            ((RectTransform)transform).sizeDelta = size;
            ((RectTransform)visual.transform).sizeDelta = size;
            KeepInsideParent();
            StartCoroutine(AnimatePopup());
        }

        private TextMeshProUGUI CreateNumber(string name, TMP_FontAsset font, string text, Vector2 offset, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(visual.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = offset;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = FontSize;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.color = color;
            label.text = text;
            return label;
        }

        private void KeepInsideParent()
        {
            var rt = (RectTransform)transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return;
            // 呼び出し元が中央アンカーに置く。上昇後も画面端で数字が欠けないようにする。
            float x = Mathf.Max(0f, (parent.rect.width - rt.rect.width) * .5f - 8f);
            float y = Mathf.Max(0f, (parent.rect.height - rt.rect.height) * .5f - 8f);
            var position = rt.anchoredPosition;
            position.x = Mathf.Clamp(position.x, -x, x);
            position.y = Mathf.Clamp(position.y, -y + 6f, y - Rise);
            rt.anchoredPosition = position;
        }

        private IEnumerator AnimatePopup()
        {
            var rt = (RectTransform)transform;
            Vector2 startPos = rt.anchoredPosition;
            float timer = 0f;
            while (timer < Duration)
            {
                timer += Time.unscaledDeltaTime;
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / .10f));
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((timer - .45f) / .70f));
                rt.anchoredPosition = startPos + Vector2.up * Mathf.Round(-6f * (1f - enter) + Rise * rise);
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((timer - .80f) / .35f));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
