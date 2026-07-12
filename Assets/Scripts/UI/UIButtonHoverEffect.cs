using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace KillingMahjong.UI
{
    public class UIButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float clickScale = 0.95f;
        [SerializeField] private float duration = 0.15f;
        [SerializeField] private Ease easeType = Ease.OutBack;

        private Vector3 _originalScale;
        private RectTransform _rectTransform;
        private bool _isInteractable = true;
        
        // UI.Button コンポーネントがある場合、Interactable に連動させる
        private UnityEngine.UI.Button _button;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalScale = _rectTransform.localScale;
            _button = GetComponent<UnityEngine.UI.Button>();
        }

        private void OnEnable()
        {
            // アクティブになったときにサイズを元に戻しておく
            if (_rectTransform != null)
            {
                _rectTransform.localScale = _originalScale;
            }
        }

        private void OnDisable()
        {
            // 非アクティブになるとTweenが止まってしまうため、強制的にKillしてリセット
            if (_rectTransform != null)
            {
                _rectTransform.DOKill();
                _rectTransform.localScale = _originalScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            // フワッと拡大
            _rectTransform.DOKill();
            _rectTransform.DOScale(_originalScale * hoverScale, duration).SetEase(easeType).SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            // 元に戻る
            _rectTransform.DOKill();
            _rectTransform.DOScale(_originalScale, duration).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            // 押した時に少しへこむ
            _rectTransform.DOKill();
            _rectTransform.DOScale(_originalScale * clickScale, duration * 0.5f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            // 離した時にホバー状態（または通常状態）に戻る
            _rectTransform.DOKill();
            _rectTransform.DOScale(_originalScale * hoverScale, duration).SetEase(easeType).SetUpdate(true);
        }

        private bool IsInteractable()
        {
            if (_button != null) return _button.interactable;
            return _isInteractable;
        }
    }
}
