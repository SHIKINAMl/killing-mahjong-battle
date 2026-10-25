using UnityEngine;
using TMPro;
using System.Collections;

namespace KillingMahjong.UI
{
    public class DamagePopupUI : MonoBehaviour
    {
        private TextMeshProUGUI tmpText;
        private float floatSpeed = 100f; // 上昇速度
        private float fadeDuration = 1.5f; // 消えるまでの時間

        public void Setup(int amount, Color color)
        {
            tmpText = GetComponent<TextMeshProUGUI>();
            if (tmpText == null)
            {
                tmpText = gameObject.AddComponent<TextMeshProUGUI>();
                tmpText.fontSize = 60;
                tmpText.alignment = TextAlignmentOptions.Center;
                tmpText.fontStyle = FontStyles.Bold;
                // フォールバック生成ではTMPの既定フォントが未設定のことがある。
                // 材質が無いまま縁を付けると、スキル支払いの途中で例外になる。
                if (tmpText.font == null)
                    tmpText.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed") ?? TMP_Settings.defaultFontAsset;
                if (tmpText.isActiveAndEnabled && tmpText.fontSharedMaterial != null) tmpText.outlineWidth = 0.2f;
            }

            string prefix = amount > 0 ? "+" : "";
            tmpText.text = $"{prefix}{amount}";
            tmpText.color = color;
            
            // 初期のアルファ値を1にする
            Color c = tmpText.color;
            c.a = 1f;
            tmpText.color = c;

            StartCoroutine(AnimatePopup());
        }

        private IEnumerator AnimatePopup()
        {
            RectTransform rt = GetComponent<RectTransform>();
            Vector3 startPos = rt.anchoredPosition;

            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / fadeDuration;

                // 上昇
                if (rt != null)
                {
                    rt.anchoredPosition = startPos + new Vector3(0, floatSpeed * progress, 0);
                }

                // 後半でフェードアウト
                if (tmpText != null && progress > 0.5f)
                {
                    float fadeProgress = (progress - 0.5f) * 2f; // 0~1
                    Color c = tmpText.color;
                    c.a = 1f - fadeProgress;
                    tmpText.color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
