using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 「押している間だけ」を扱うボタン部品。
    ///
    /// Button.onClick は離した瞬間にしか来ないため、押しっぱなしを見るには
    /// ポインタの押下・解放を直接拾う必要がある。
    ///
    /// **押したまま指を外して離される場合がある。** その時 OnPointerUp は
    /// このオブジェクトに来ないので、OnPointerExit でも解放扱いにしないと
    /// 押しっぱなし状態のまま戻らなくなる（＝手牌が出たまま固まる）。
    /// </summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public UnityEvent onHoldStart = new UnityEvent();
        public UnityEvent onHoldEnd = new UnityEvent();

        private bool _held;

        public bool IsHeld => _held;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_held) return;
            _held = true;
            onHoldStart.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // 押したまま外へ出た場合。ここで離さないと戻せなくなる
            Release();
        }

        private void OnDisable()
        {
            // フェイズが変わってボタンごと消えるときも、必ず解放して状態を戻す
            Release();
        }

        private void Release()
        {
            if (!_held) return;
            _held = false;
            onHoldEnd.Invoke();
        }
    }
}
