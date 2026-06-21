using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace KillingMahjong.UI
{
    public class CutinAnimationUI : MonoBehaviour
    {
        public void PlayCutin(Sprite characterSprite, TMP_FontAsset font, System.Action onComplete)
        {
            StartCoroutine(CutinRoutine(characterSprite, font, onComplete));
        }

        private IEnumerator CutinRoutine(Sprite characterSprite, TMP_FontAsset font, System.Action onComplete)
        {
            // 1. ルートコンテナの作成
            GameObject root = new GameObject("CutinRoot");
            root.transform.SetParent(transform, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;
            
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 32000; // 最前面

            // 2. 背景（半透明の黒）
            GameObject bgObj = new GameObject("BgDimmer");
            bgObj.transform.SetParent(rootRt, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.7f);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // 3. 斜めの帯を生成
            GameObject stripeObj = new GameObject("StripeContainer");
            stripeObj.transform.SetParent(rootRt, false);
            RectTransform stripeRt = stripeObj.AddComponent<RectTransform>();
            stripeRt.anchorMin = new Vector2(0.5f, 0.5f);
            stripeRt.anchorMax = new Vector2(0.5f, 0.5f);
            stripeRt.sizeDelta = new Vector2(3500f, 600f);
            stripeRt.anchoredPosition = Vector2.zero;
            stripeRt.localRotation = Quaternion.Euler(0, 0, 15f);

            // 帯の背景色
            Image stripeImg = stripeObj.AddComponent<Image>();
            stripeImg.color = new Color(0.85f, 0f, 0.5f, 1f); // 派手なピンク系
            Mask stripeMask = stripeObj.AddComponent<Mask>();
            stripeMask.showMaskGraphic = true;

            // 4. キャラクター画像（帯の中でマスクされる）
            GameObject charObj = new GameObject("CharacterImage");
            charObj.transform.SetParent(stripeRt, false);
            Image charImg = charObj.AddComponent<Image>();
            charImg.sprite = characterSprite;
            charImg.preserveAspect = true;
            RectTransform charRt = charObj.GetComponent<RectTransform>();
            charRt.anchorMin = new Vector2(0.5f, 0.5f);
            charRt.anchorMax = new Vector2(0.5f, 0.5f);
            charRt.sizeDelta = new Vector2(1000f, 1000f);
            charRt.localRotation = Quaternion.Euler(0, 0, -15f); // キャラは傾きをキャンセルしてまっすぐに
            charRt.anchoredPosition = new Vector2(-200f, 0); // 左側に寄せる

            // 5. テキスト「ロン！」
            GameObject textObj = new GameObject("RonText");
            textObj.transform.SetParent(rootRt, false);
            TextMeshProUGUI textMesh = textObj.AddComponent<TextMeshProUGUI>();
            textMesh.text = "ロン！";
            if (font != null) textMesh.font = font;
            textMesh.color = Color.white;
            textMesh.fontSize = 250;
            textMesh.fontStyle = FontStyles.Bold | FontStyles.Italic;
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.outlineWidth = 0.2f;
            textMesh.outlineColor = Color.black;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = new Vector2(1000f, 400f);
            textRt.localRotation = Quaternion.Euler(0, 0, 15f);
            textRt.anchoredPosition = new Vector2(300f, 0); // 右側に寄せる

            // 6. アニメーション
            // 初期状態は画面外
            Vector2 startPos = new Vector2(2500f, -1000f);
            Vector2 endPos = new Vector2(-2500f, 1000f);
            
            stripeRt.anchoredPosition = startPos;
            textRt.anchoredPosition = startPos;

            // スライドイン（高速）
            float t = 0;
            float inDuration = 0.15f;
            while (t < inDuration)
            {
                float progress = t / inDuration;
                float eased = 1f - Mathf.Pow(1f - progress, 3f); // EaseOutCubic
                
                stripeRt.anchoredPosition = Vector2.Lerp(startPos, Vector2.zero, eased);
                textRt.anchoredPosition = Vector2.Lerp(startPos, new Vector2(300f, 0), eased);
                
                t += Time.deltaTime;
                yield return null;
            }

            stripeRt.anchoredPosition = Vector2.zero;
            textRt.anchoredPosition = new Vector2(300f, 0);

            // 画面揺らし（タメ）
            float shakeTime = 0.8f;
            t = 0;
            while (t < shakeTime)
            {
                float x = Random.Range(-1f, 1f) * 15f;
                float y = Random.Range(-1f, 1f) * 15f;
                rootRt.anchoredPosition = new Vector2(x, y);

                float scale = 1f + Mathf.Sin(t * 30f) * 0.05f;
                textRt.localScale = new Vector3(scale, scale, 1f);

                t += Time.deltaTime;
                yield return null;
            }
            rootRt.anchoredPosition = Vector2.zero;

            // スライドアウト（高速）
            t = 0;
            float outDuration = 0.15f;
            while (t < outDuration)
            {
                float progress = t / outDuration;
                float eased = progress * progress * progress; // EaseInCubic

                stripeRt.anchoredPosition = Vector2.Lerp(Vector2.zero, endPos, eased);
                textRt.anchoredPosition = Vector2.Lerp(new Vector2(300f, 0), endPos, eased);

                t += Time.deltaTime;
                yield return null;
            }

            Destroy(root);
            onComplete?.Invoke();
        }
    }
}
