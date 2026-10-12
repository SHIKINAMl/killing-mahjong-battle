using UnityEngine;
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    /// <summary>シーン参照を維持する互換入口。共通フレームを使い、判定の位置・縮尺は動かさない。</summary>
    public class UIButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        // 旧シーンの設定値は保持。現在の見た目はUIFrameCatalogのIDで管理する。
        [SerializeField] private float hoverScale = 1.05f, clickScale = .95f, duration = .15f;
        [SerializeField] private DG.Tweening.Ease easeType = DG.Tweening.Ease.OutBack;
        private UIInteractionFeedback feedback;
        private void Awake() { feedback = UIInteractionFeedback.Attach(gameObject); }
        private void OnEnable() {
            if (feedback == null) feedback = UIInteractionFeedback.Attach(gameObject);
            feedback.Clear();
        }
        private void OnDisable() { if (feedback != null) feedback.Clear(); }
        public void OnPointerEnter(PointerEventData e) { if (feedback != null) feedback.OnPointerEnter(e); }
        public void OnPointerExit(PointerEventData e) { if (feedback != null) feedback.OnPointerExit(e); }
        public void OnPointerDown(PointerEventData e) { if (feedback != null) feedback.OnPointerDown(e); }
        public void OnPointerUp(PointerEventData e) { if (feedback != null) feedback.OnPointerUp(e); }
    }
}
