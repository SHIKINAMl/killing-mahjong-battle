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
                tmpText.outlineWidth = 0.2f; // アウトラインで文字を見やすく
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
