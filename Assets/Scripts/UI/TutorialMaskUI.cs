using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(Canvas))]
    public class TutorialMaskUI : MonoBehaviour
    {
        [Header("Mask Panels")]
        [SerializeField] private Image topPanel;
        [SerializeField] private Image bottomPanel;
        [SerializeField] private Image leftPanel;
        [SerializeField] private Image rightPanel;
        
        [Header("Settings")]
        [SerializeField] private Color maskColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private float padding = 10f; // 切り抜く枠の余白

        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 900; // TutorialMaskの適当なオーダー。手前に表示させる
            
            // 初期のパネル設定
            if (topPanel != null) topPanel.color = maskColor;
            if (bottomPanel != null) bottomPanel.color = maskColor;
            if (leftPanel != null) leftPanel.color = maskColor;
            if (rightPanel != null) rightPanel.color = maskColor;
            
            Hide();
        }

        private RectTransform _currentTarget;

        public void Show(RectTransform targetRect)
        {
            gameObject.SetActive(true);

            if (targetRect == null)
            {
                SetFullScreenMask();
                _currentTarget = null;
                return;
            }

            _currentTarget = targetRect;
            UpdateMaskPosition();
        }

        private void Update()
        {
            if (_currentTarget != null)
            {
                UpdateMaskPosition();
            }
        }

        private void UpdateMaskPosition()
        {
            if (_currentTarget == null) return;

            // Canvas の RectTransform (親)
            RectTransform canvasRect = _canvas.GetComponent<RectTransform>();

            // ターゲットのワールド四隅を取得
            Vector3[] corners = new Vector3[4];
            _currentTarget.GetWorldCorners(corners);

            // 直接Canvasのローカル座標に変換する (CameraSpace/Overlay問わず確実)
            Vector3 bottomLeft3D = canvasRect.InverseTransformPoint(corners[0]);
            Vector3 topRight3D = canvasRect.InverseTransformPoint(corners[2]);

            Vector2 bottomLeft = new Vector2(bottomLeft3D.x - padding, bottomLeft3D.y - padding);
            Vector2 topRight = new Vector2(topRight3D.x + padding, topRight3D.y + padding);

            // キャンバスのサイズ
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;
            float minX = -canvasWidth / 2f;
            float maxX = canvasWidth / 2f;
            float minY = -canvasHeight / 2f;
            float maxY = canvasHeight / 2f;

            // 4つのパネルのアンカーとサイズを設定 (中央基準のローカル座標を前提とする)
            // Left Panel
            if (leftPanel != null)
            {
                RectTransform rt = leftPanel.rectTransform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 0.5f);
                rt.anchoredPosition = new Vector2(0, 0);
                rt.sizeDelta = new Vector2(bottomLeft.x - minX, 0);
            }

            // Right Panel
            if (rightPanel != null)
            {
                RectTransform rt = rightPanel.rectTransform;
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 0.5f);
                rt.anchoredPosition = new Vector2(0, 0);
                rt.sizeDelta = new Vector2(maxX - topRight.x, 0);
            }

            // Top Panel
            if (topPanel != null)
            {
                RectTransform rt = topPanel.rectTransform;
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0, 1);
                rt.anchoredPosition = new Vector2(bottomLeft.x - minX, 0);
                rt.sizeDelta = new Vector2(topRight.x - bottomLeft.x, maxY - topRight.y);
            }

            // Bottom Panel
            if (bottomPanel != null)
            {
                RectTransform rt = bottomPanel.rectTransform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.anchoredPosition = new Vector2(bottomLeft.x - minX, 0);
                rt.sizeDelta = new Vector2(topRight.x - bottomLeft.x, bottomLeft.y - minY);
            }
        }

        private void SetFullScreenMask()
        {
            if (topPanel != null)
            {
                RectTransform rt = topPanel.rectTransform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
            }
            if (bottomPanel != null) bottomPanel.rectTransform.sizeDelta = new Vector2(0, 0);
            if (leftPanel != null) leftPanel.rectTransform.sizeDelta = new Vector2(0, 0);
            if (rightPanel != null) rightPanel.rectTransform.sizeDelta = new Vector2(0, 0);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
