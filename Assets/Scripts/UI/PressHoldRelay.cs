using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 押している間だけ true、離したら false を知らせる（押している間だけ何かを見せるボタン用）。
    /// 押したままボタンの外へ出たときと、ボタンが消えたときも離したことにする。
    /// </summary>
    public sealed class PressHoldRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Action<bool> OnHoldChanged;
        private bool held;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || held) return;
            held = true;
            OnHoldChanged?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();
        private void OnDisable() => Release();

        private void Release()
        {
            if (!held) return;
            held = false;
            OnHoldChanged?.Invoke(false);
        }
    }
}
