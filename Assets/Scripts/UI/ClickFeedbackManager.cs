using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class ClickFeedbackManager : MonoBehaviour
    {
        private static ClickFeedbackManager instance;
        private Canvas targetCanvas;
        private GameObject dotPrefab;
        private float fadeDuration = 0.4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("ClickFeedbackManager");
                instance = obj.AddComponent<ClickFeedbackManager>();
                DontDestroyOnLoad(obj);
            }
        }

        private void Awake()
        {
            CreateCanvasAndPrefab();
        }

        private void CreateCanvasAndPrefab()
        {
            // Canvas setup
            GameObject canvasObj = new GameObject("ClickFeedbackCanvas");
            canvasObj.transform.SetParent(transform);
            targetCanvas = canvasObj.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = UISortingOrders.ClickFeedback;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // Dot prefab setup
            dotPrefab = new GameObject("ClickDotPrefab");
            dotPrefab.SetActive(false);
            dotPrefab.transform.SetParent(transform);
            
            var img = dotPrefab.AddComponent<Image>();
            img.sprite = CreateCircleSprite();
            img.color = new Color(1f, 0.9f, 0.2f, 0.8f); // やや黄色みがかった白（見やすくするため）
            img.raycastTarget = false; // クリックを阻害しないようにする

            RectTransform rt = dotPrefab.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(40, 40); // 40x40 ピクセル
        }

        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius)
                    {
                        // 中心ほど濃く、端に行くほど薄くなるグラデーション
                        float alpha = 1f - Mathf.Pow(dist / radius, 2f);
                        colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        colors[y * size + x] = Color.clear;
                    }
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            bool isClicked = false;
            Vector2 clickPos = Vector2.zero;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isClicked = true;
                clickPos = Mouse.current.position.ReadValue();
            }
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                isClicked = true;
                clickPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }

            if (isClicked)
            {
                SpawnDot(clickPos);
            }
        }

        private void SpawnDot(Vector2 screenPos)
        {
            GameObject dot = Instantiate(dotPrefab, targetCanvas.transform);
            dot.SetActive(true);

            RectTransform rt = dot.GetComponent<RectTransform>();
            
            // ScreenSpaceOverlay の Canvas での画面座標への変換
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(targetCanvas.transform as RectTransform, screenPos, null, out localPoint);
            rt.anchoredPosition = localPoint;

            StartCoroutine(AnimateDot(dot, rt));
        }

        private IEnumerator AnimateDot(GameObject dot, RectTransform rt)
        {
            Image img = dot.GetComponent<Image>();
            Color startColor = img.color;
            Vector3 startScale = Vector3.one * 1.5f; // 最初は少し大きく
            Vector3 endScale = Vector3.one * 0.2f;   // 小さくなりながら消える

            rt.localScale = startScale;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                
                // イージング (EaseOutQuad)
                float easeT = 1f - (1f - t) * (1f - t);

                // フェードアウト
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, easeT);
                img.color = c;

                // スケール
                rt.localScale = Vector3.Lerp(startScale, endScale, easeT);

                yield return null;
            }

            Destroy(dot);
        }
    }
}
