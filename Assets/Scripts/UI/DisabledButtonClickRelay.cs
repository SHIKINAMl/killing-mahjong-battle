using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 押せない（interactable = false の）ボタンが押されたことを知らせる。
    /// Button は押せないとき onClick を呼ばないので、「なぜ押せないか」を言わせたい場面で使う
    /// （2026-10-29 要望「強化できない役を強化しようとした時にセリフを字幕でしゃべらせたい」）。
    /// </summary>
    public sealed class DisabledButtonClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action OnDisabledClick;

        private Button button;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (button == null) button = GetComponent<Button>();
            if (button != null && !button.interactable) OnDisabledClick?.Invoke();
        }
    }
}
