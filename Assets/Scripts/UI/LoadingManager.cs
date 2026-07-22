using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class LoadingManager : MonoBehaviour
    {
        private static LoadingManager instance;
        public static LoadingManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("LoadingManager");
                    instance = go.AddComponent<LoadingManager>();
                    DontDestroyOnLoad(go);
                    instance.Initialize();
                }
                return instance;
            }
        }

        private GameObject loadingCanvasObj;
        private Transform spinnerTransform;
        private Image fadeBgImage; // 暗転用背景
        private GameObject containerObj; // スピナーとテキストのコンテナ
        private int showCount = 0;
        private Coroutine fadeCoroutine;

        private void Initialize()
        {
            // Canvas構築
            loadingCanvasObj = new GameObject("LoadingCanvas");
            loadingCanvasObj.transform.SetParent(this.transform);
            
            Canvas canvas = loadingCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.LoadingScreen;
            
            var scaler = loadingCanvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            loadingCanvasObj.AddComponent<GraphicRaycaster>();
            loadingCanvasObj.AddComponent<CanvasGroup>();
            
            // 暗転用背景（全画面の黒）
            GameObject fadeBgObj = new GameObject("FadeBackground");
            fadeBgObj.transform.SetParent(loadingCanvasObj.transform, false);
            RectTransform fadeBgRt = fadeBgObj.AddComponent<RectTransform>();
            fadeBgRt.anchorMin = Vector2.zero;
            fadeBgRt.anchorMax = Vector2.one;
            fadeBgRt.sizeDelta = Vector2.zero;
            fadeBgImage = fadeBgObj.AddComponent<Image>();
            fadeBgImage.color = new Color(0, 0, 0, 0); // 初期は透明
            
            // 右下コンテナ
            containerObj = new GameObject("LoadingContainer");
            containerObj.transform.SetParent(loadingCanvasObj.transform, false);
            RectTransform containerRt = containerObj.AddComponent<RectTransform>();
            containerRt.anchorMin = new Vector2(1, 0);
            containerRt.anchorMax = new Vector2(1, 0);
            containerRt.pivot = new Vector2(1, 0);
            containerRt.anchoredPosition = new Vector2(-40, 40); // 右下マージン
            containerRt.sizeDelta = new Vector2(250, 60);

            // 背景（角丸の黒半透明）
            Image bg = containerObj.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            // スピナー画像 (シンプルな四角を回転させる)
            GameObject spinnerObj = new GameObject("Spinner");
            spinnerObj.transform.SetParent(containerObj.transform, false);
            RectTransform spinnerRt = spinnerObj.AddComponent<RectTransform>();
            spinnerRt.anchorMin = new Vector2(0, 0.5f);
            spinnerRt.anchorMax = new Vector2(0, 0.5f);
            spinnerRt.pivot = new Vector2(0, 0.5f);
            spinnerRt.anchoredPosition = new Vector2(20, 0);
            spinnerRt.sizeDelta = new Vector2(40, 40);
            
            Image spinnerImg = spinnerObj.AddComponent<Image>();
            spinnerImg.color = Color.white;
            spinnerTransform = spinnerObj.transform;

            // テキスト
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(containerObj.transform, false);
            RectTransform textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(1, 0.5f);
            textRt.anchorMax = new Vector2(1, 0.5f);
            textRt.pivot = new Vector2(1, 0.5f);
            textRt.anchoredPosition = new Vector2(-10, 0);
            textRt.sizeDelta = new Vector2(160, 40);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (TMPro.TMP_Settings.defaultFontAsset != null) 
            {
                tmp.font = TMPro.TMP_Settings.defaultFontAsset;
            }
            tmp.text = "Now Loading...";
            tmp.fontSize = KillingMahjong.Common.UITypography.BodySmall;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left | TextAlignmentOptions.Midline;
            tmp.fontStyle = FontStyles.Bold;
            tmp.margin = new Vector4(10, 0, 0, 0); // 左マージン

            loadingCanvasObj.SetActive(false);
        }

        private void Update()
        {
            if (loadingCanvasObj != null && loadingCanvasObj.activeSelf && spinnerTransform != null)
            {
                spinnerTransform.Rotate(0, 0, -360 * Time.deltaTime); // 1秒に1回転
            }
        }

        public void Show()
        {
            showCount++;
            if (showCount > 0)
            {
                loadingCanvasObj.SetActive(true);
            }
        }

        public void Hide()
        {
            showCount--;
            if (showCount <= 0)
            {
                showCount = 0;
                loadingCanvasObj.SetActive(false);
            }
        }
        
        public void ForceHide()
        {
            showCount = 0;
            loadingCanvasObj.SetActive(false);
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
            fadeBgImage.color = new Color(0, 0, 0, 0);
            containerObj.SetActive(true);
        }

        // --- フェード機能 ---
        
        public void FadeOutScreen(System.Action onComplete = null)
        {
            loadingCanvasObj.SetActive(true);
            showCount++;
            
            // スピナーはフェード開始時は隠しておき、フェード後に表示
            containerObj.SetActive(false);
            
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeRoutine(0f, 1f, 0.5f, () => 
            {
                containerObj.SetActive(true); // 暗転完了後に右下のロードを出す
                onComplete?.Invoke();
            }));
        }
        
        public void FadeInScreen(System.Action onComplete = null)
        {
            // スピナーは先に消す
            containerObj.SetActive(false);
            
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeRoutine(1f, 0f, 0.5f, () => 
            {
                showCount = 0;
                loadingCanvasObj.SetActive(false);
                onComplete?.Invoke();
            }));
        }

        private System.Collections.IEnumerator FadeRoutine(float startAlpha, float endAlpha, float duration, System.Action onComplete)
        {
            float elapsed = 0f;
            Color c = fadeBgImage.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
                fadeBgImage.color = c;
                yield return null;
            }
            c.a = endAlpha;
            fadeBgImage.color = c;
            
            onComplete?.Invoke();
        }
    }
}
