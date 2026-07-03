using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class CutinAnimationUI : MonoBehaviour
    {
        public void PlayCutin(Sprite characterSprite, Sprite faceSprite, TMP_FontAsset font, string cutinText, System.Action onComplete)
        {
            StartCoroutine(CutinRoutine(characterSprite, faceSprite, font, cutinText, onComplete));
        }

        private IEnumerator CutinRoutine(Sprite characterSprite, Sprite faceSprite, TMP_FontAsset font, string cutinText, System.Action onComplete)
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
            canvas.sortingOrder = UISortingOrders.CutinAnimation;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;

            // 2. 背景（半透明の黒）
            GameObject bgObj = new GameObject("BgDimmer");
            bgObj.transform.SetParent(rootRt, false);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.7f);
            RectTransform bgRt = bgObj.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // 2.5 RevealMask（右上から左下へマスク解除するためのコンテナ）
            // 全体を覆う十分なサイズを持たせ、15度傾けることで斜めにフィルされるようにする
            GameObject revealObj = new GameObject("RevealMask");
            revealObj.transform.SetParent(rootRt, false);
            RectTransform revealRt = revealObj.AddComponent<RectTransform>();
            revealRt.anchorMin = new Vector2(0.5f, 0.5f);
            revealRt.anchorMax = new Vector2(0.5f, 0.5f);
            revealRt.sizeDelta = new Vector2(4000f, 4000f);
            revealRt.localRotation = Quaternion.Euler(0, 0, 15f); // 全体を15度傾ける

            Image revealImg = revealObj.AddComponent<Image>();
            revealImg.type = Image.Type.Filled;
            revealImg.fillMethod = Image.FillMethod.Horizontal;
            revealImg.fillOrigin = (int)Image.OriginHorizontal.Right; // 右側からフィル
            revealImg.fillAmount = 0f; // 初期状態では中身が見えない

            Mask revealMask = revealObj.AddComponent<Mask>();
            revealMask.showMaskGraphic = false;

            // 3. 斜めの帯を生成
            GameObject stripeObj = new GameObject("StripeContainer");
            stripeObj.transform.SetParent(revealRt, false);
            RectTransform stripeRt = stripeObj.AddComponent<RectTransform>();
            stripeRt.anchorMin = new Vector2(0.5f, 0.5f);
            stripeRt.anchorMax = new Vector2(0.5f, 0.5f);
            stripeRt.sizeDelta = new Vector2(3500f, 600f);
            stripeRt.anchoredPosition = Vector2.zero;
            stripeRt.localRotation = Quaternion.Euler(0, 0, 0f); // 親が15度傾いているのでこちらは0度

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

            // 4.5 顔パーツ画像（身体の上に乗せる）
            if (faceSprite != null)
            {
                GameObject faceObj = new GameObject("FaceImage");
                faceObj.transform.SetParent(charRt, false); // 身体の子要素にする
                Image faceImg = faceObj.AddComponent<Image>();
                faceImg.sprite = faceSprite;
                faceImg.preserveAspect = true;
                RectTransform faceRt = faceObj.GetComponent<RectTransform>();
                faceRt.anchorMin = Vector2.zero;
                faceRt.anchorMax = Vector2.one;
                faceRt.sizeDelta = Vector2.zero;
                faceRt.anchoredPosition = Vector2.zero;
            }

            // 5. テキスト
            GameObject textObj = new GameObject("RonText");
            textObj.transform.SetParent(revealRt, false); // revealRtの子にする
            TextMeshProUGUI textMesh = textObj.AddComponent<TextMeshProUGUI>();
            textMesh.text = string.IsNullOrEmpty(cutinText) ? "ロン！" : cutinText;
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
            textRt.localRotation = Quaternion.Euler(0, 0, 0f); // 親が15度傾いているので0度
            textRt.anchoredPosition = new Vector2(300f, 0); // 右側に寄せる

            // 6. アニメーション
            stripeRt.anchoredPosition = Vector2.zero;
            textRt.anchoredPosition = new Vector2(300f, 0);

            // ① 右上から左下へマスクを解除して表示していく
            float t = 0;
            float inDuration = 0.2f;
            while (t < inDuration)
            {
                float progress = t / inDuration;
                float eased = 1f - Mathf.Pow(1f - progress, 3f); // EaseOutCubic
                revealImg.fillAmount = eased;
                t += Time.deltaTime;
                yield return null;
            }
            revealImg.fillAmount = 1f;

            // ② タメ（文字も動かさない）
            float shakeTime = 0.8f;
            yield return new WaitForSeconds(shakeTime);

            // ③ 全体がフェードアウトして消える
            t = 0;
            float outDuration = 0.15f;
            while (t < outDuration)
            {
                float progress = t / outDuration;
                canvasGroup.alpha = 1f - progress;
                t += Time.deltaTime;
                yield return null;
            }

            Destroy(root);
            onComplete?.Invoke();
        }
    }
}
