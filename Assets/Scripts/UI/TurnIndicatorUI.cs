using UnityEngine;
using TMPro;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public class TurnIndicatorUI : MonoBehaviour
    {
        private TextMeshProUGUI textMesh;
        private RectTransform rectTransform;

        private float minAlpha = 0.3f;
        private float maxAlpha = 1.0f;
        private float blinkSpeed = 3.0f;

        private Color localTurnColor = new Color(0f, 1f, 1f, 1f); // 水色（シアン）
        private Color enemyTurnColor = new Color(1f, 0.2f, 0.2f, 1f); // 赤

        private bool isLocalTurn = false;
        private bool isInitialized = false;

        private void Awake()
        {
            rectTransform = gameObject.GetComponent<RectTransform>();
            textMesh = gameObject.GetComponent<TextMeshProUGUI>();
            
            if (textMesh == null)
            {
                Debug.LogWarning("[TurnIndicatorUI] TextMeshProUGUI component is missing. Please add it to the prefab.");
            }
        }

        private void Start()
        {
            if (textMesh == null)
            {
                return;
            }

            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnTurnChanged += HandleTurnChanged;
                isLocalTurn = BoardStateManager.Instance.IsLocalTurn;
            }

            isInitialized = true;
            UpdateText();
        }

        private void OnDestroy()
        {
            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnTurnChanged -= HandleTurnChanged;
            }
        }

        private void HandleTurnChanged(bool isLocal)
        {
            isLocalTurn = isLocal;
            UpdateText();
        }

        public void SetVisible(bool isVisible)
        {
            if (gameObject != null)
            {
                gameObject.SetActive(isVisible);
                if (isVisible && BoardStateManager.Instance != null)
                {
                    isLocalTurn = BoardStateManager.Instance.IsLocalTurn;
                    UpdateText();
                }
            }
        }

        private void UpdateText()
        {
            if (textMesh == null) return;
            
            if (isLocalTurn)
            {
                textMesh.text = "YOUR TURN";
            }
            else
            {
                textMesh.text = "ENEMY TURN";
            }
        }

        private void Update()
        {
            if (!isInitialized || textMesh == null) return;

            // ネオンのように明滅させるアニメーション（サイン波）
            float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f; // 0.0 ~ 1.0
            float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);

            Color targetColor = isLocalTurn ? localTurnColor : enemyTurnColor;
            targetColor.a = currentAlpha;
            textMesh.color = targetColor;
        }
    }
}
