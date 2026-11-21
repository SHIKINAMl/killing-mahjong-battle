using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>ログの操作を、全画面のセリフ送りに奪わせない。</summary>
    public sealed class DialogueAdvanceRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        public DialogueUI Owner { get; set; }
        public Button LogButton { get; set; }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (OptionUI.AnyOpen) return false;
            if (Owner != null && Owner.IsLogOpen) return false;
            if (LogButton == null || !LogButton.isActiveAndEnabled || !LogButton.IsInteractable()) return true;
            var canvas = LogButton.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            return !RectTransformUtility.RectangleContainsScreenPoint(
                (RectTransform)LogButton.transform, screenPoint, camera);
        }
    }
}
