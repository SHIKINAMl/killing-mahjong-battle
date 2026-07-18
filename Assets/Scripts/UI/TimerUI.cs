using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace KillingMahjong.UI
{
    public class TimerUI : MonoBehaviour
    {
        private Image backgroundImage;
        private Image fillImage;
        private RectTransform needleRect;
        private Image needleImage;

        private float timeLimit;
        private float currentTime;
        private bool isRunning;
        private bool isTimeout;

        private Coroutine flashCoroutine;

        public void Initialize()
        {
            if (backgroundImage != null) return; // 既に初期化済み

            // --- 背景（時計の枠）の作成 ---
            backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // --- ゲージ（残り時間）の作成 ---
            GameObject fillObj = new GameObject("FillImage");
            fillObj.transform.SetParent(transform, false);
            fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.1f, 0.8f, 0.9f, 0.8f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false; // 反時計回りに減るようにする

            RectTransform fillRt = fillImage.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            fillRt.offsetMin = new Vector2(4, 4);
            fillRt.offsetMax = new Vector2(-4, -4);

            // --- 針の作成 ---
            GameObject needleObj = new GameObject("Needle");
            needleObj.transform.SetParent(transform, false);
            needleRect = needleObj.AddComponent<RectTransform>();
            needleImage = needleObj.AddComponent<Image>();
            needleImage.color = Color.white;

            // 針のピボットを下中央にし、上に向かって伸びるようにする
            needleRect.pivot = new Vector2(0.5f, 0f);
            needleRect.anchorMin = new Vector2(0.5f, 0.5f);
            needleRect.anchorMax = new Vector2(0.5f, 0.5f);
            needleRect.anchoredPosition = Vector2.zero;
            
            // 標準のSpriteを探して設定（あれば）
#if UNITY_EDITOR
            Sprite knobSprite = UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (knobSprite != null)
            {
                backgroundImage.sprite = knobSprite;
                fillImage.sprite = knobSprite;
            }
#endif

            gameObject.SetActive(false);
        }

        public void SetSize(float size)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(size, size);
            }
            if (needleRect != null)
            {
                needleRect.sizeDelta = new Vector2(size * 0.05f, size * 0.45f); // 針の太さと長さ
            }
        }

        public void StartTimer(float duration)
        {
            if (backgroundImage == null) Initialize();

            timeLimit = duration;
            currentTime = duration;
            isRunning = true;
            isTimeout = false;
            
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }
            
            // 色をリセット
            fillImage.color = new Color(0.1f, 0.8f, 0.9f, 0.8f);
            needleImage.color = Color.white;
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            gameObject.SetActive(true);
        }

        public void StopTimer()
        {
            isRunning = false;
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isRunning) return;

            if (currentTime > 0)
            {
                currentTime -= Time.deltaTime;
                
                // ゲージの更新
                fillImage.fillAmount = currentTime / timeLimit;

                // 針の回転（360度）
                float angle = (1.0f - (currentTime / timeLimit)) * 360f;
                needleRect.localRotation = Quaternion.Euler(0, 0, -angle);

                if (currentTime <= 0)
                {
                    currentTime = 0;
                    isRunning = false;
                    isTimeout = true;
                    fillImage.fillAmount = 0;
                    
                    // タイムアウト時の点滅を開始
                    flashCoroutine = StartCoroutine(FlashRoutine());
                }
            }
        }

        private IEnumerator FlashRoutine()
        {
            bool isRed = false;
            while (true)
            {
                if (isRed)
                {
                    backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    needleImage.color = Color.white;
                }
                else
                {
                    backgroundImage.color = new Color(0.8f, 0.1f, 0.1f, 0.8f); // 赤色
                    needleImage.color = Color.red;
                }
                isRed = !isRed;
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
