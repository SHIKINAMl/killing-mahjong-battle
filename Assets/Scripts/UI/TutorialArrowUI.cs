using UnityEngine;

namespace KillingMahjong.UI
{
    public class TutorialArrowUI : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float bobbingAmount = 20f; // 上下に揺れる幅
        [SerializeField] private float bobbingSpeed = 5f;   // 揺れる速度
        [SerializeField] private Vector2 offset = new Vector2(0, 100f); // 牌の中心からのオフセット（上に表示）

        [Header("Visual Settings")]
        [SerializeField] private Sprite arrowSprite; // ここに矢印画像を設定できます

        private RectTransform myRectTransform;
        private RectTransform targetRectTransform;
        private Vector2 basePosition;

        private void Awake()
        {
            myRectTransform = GetComponent<RectTransform>();
            
            UnityEngine.UI.Image image = GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<UnityEngine.UI.Image>();
            }

            if (arrowSprite != null)
            {
                image.sprite = arrowSprite;
            }

            if (myRectTransform != null)
            {
                myRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                myRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                myRectTransform.pivot = new Vector2(0.5f, 0.5f);
                
                // どんな画像でも一定の大きさに収まるように固定サイズにする
                myRectTransform.sizeDelta = new Vector2(80, 80);
                
                if (image != null)
                {
                    // アスペクト比（縦横比）を維持する
                    image.preserveAspect = true;
                }
            }

            // 矢印を最前面に表示するためのCanvas設定
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;

            gameObject.SetActive(false);
        }

        private Vector2? customOffset = null;

        public void ShowAt(RectTransform target)
        {
            targetRectTransform = target;
            customOffset = null;
            gameObject.SetActive(true);
            UpdatePosition();
        }

        public void ShowAt(RectTransform target, Vector2 overrideOffset)
        {
            targetRectTransform = target;
            customOffset = overrideOffset;
            gameObject.SetActive(true);
            UpdatePosition();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            targetRectTransform = null;
        }

        private void Update()
        {
            if (targetRectTransform == null) return;
            UpdatePosition();
            Animate();
        }

        private void UpdatePosition()
        {
            if (targetRectTransform == null) return;

            Vector3[] corners = new Vector3[4];
            targetRectTransform.GetWorldCorners(corners);
            
            Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;
            
            Vector2 currentOffset = customOffset.HasValue ? customOffset.Value : offset;

            Canvas canvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                RectTransform canvasRect = canvas.GetComponent<RectTransform>();
                Vector3 localPos = canvasRect.InverseTransformPoint(topCenterWorld);
                basePosition = (Vector2)localPos + currentOffset;
            }
            else
            {
                basePosition = (Vector2)topCenterWorld + currentOffset;
            }
        }

        private void Animate()
        {
            float yOffset = Mathf.Sin(Time.time * bobbingSpeed) * bobbingAmount;
            myRectTransform.anchoredPosition = basePosition + new Vector2(0, yOffset);
        }
    }
}
