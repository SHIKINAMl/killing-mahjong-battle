using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 牌交換で牌を選んでいる間に出す「場を見る」ボタン（2026-10-30 要望「場を見るボタンがあってもいいかも」
    /// 「押している間だけ消すタイプで」）。**押している間だけ**、案内の文字を消し、牌交換のあいだ伏せている
    /// 相手・体力・役一覧を出して、いつもの盤面を見せる。離すと牌交換の画面に戻る。
    ///
    /// 牌交換の画面（MulliganTextCanvas）の中に置くので、牌交換の間だけ出る。部品はシーンに置いてある物を使う
    /// （<see cref="SceneFirst"/>。置くのは MulliganPeekButtonBaker）。見た目は決定ボタンを写したもの。
    /// </summary>
    public sealed class MulliganPeekButtonUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const string ObjectName = "MulliganPeekButton";

        private GameUISkillController owner;
        private bool held;

        /// <summary>牌交換の画面 <paramref name="mulliganCanvas"/> の直下から探す。無ければ <paramref name="template"/> を写して作る。</summary>
        public static MulliganPeekButtonUI Ensure(Transform mulliganCanvas, Button template, GameUISkillController owner)
        {
            if (mulliganCanvas == null) return null;
            var found = SceneFirst.FindChild(mulliganCanvas, ObjectName);
            GameObject go;
            if (found != null) go = found;
            else
            {
                if (template == null) return null;
                go = Instantiate(template.gameObject, mulliganCanvas, false);
                go.name = ObjectName;
                var button = go.GetComponent<Button>();
                // 決定ボタンの押した時の処理（シーンに書いてあるもの）を引き継がない
                if (button != null) button.onClick = new Button.ButtonClickedEvent();
                foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>(true)) tmp.text = "場を見る";
                foreach (var text in go.GetComponentsInChildren<Text>(true)) text.text = "場を見る";
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.sizeDelta = new Vector2(120f, 36f);
                // 画面の左下。手牌と山牌の左の空いているところ
                rect.anchoredPosition = new Vector2(-312f, -250f);
                go.AddComponent<MulliganPeekButtonUI>();
            }
            var ui = go.GetComponent<MulliganPeekButtonUI>();
            if (ui == null) ui = go.AddComponent<MulliganPeekButtonUI>();
            ui.owner = owner;
            return ui;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || owner == null) return;
            held = true;
            owner.SetMulliganPeek(true);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void OnDisable() => Release();

        private void Release()
        {
            if (!held) return;
            held = false;
            if (owner != null) owner.SetMulliganPeek(false);
        }
    }
}
