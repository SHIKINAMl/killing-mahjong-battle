using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ログを遡って読んでいるときに出す「一番下（最新）へ戻る」ボタン（2026-10-29 要望
    /// 「LOGに一番下まで戻るボタンがほしい　▼みたいなマークでお願いします。（文字はいらない）」）。
    ///
    /// 文字は置かず、ドットの ▼ だけを描く。最新の行が見えているあいだは隠す。
    /// 部品はシーンに置いてある物を使う（<see cref="SceneFirst"/>）。置き場所や色はシーンで直す。
    /// </summary>
    public sealed class LogLatestButtonUI : MonoBehaviour
    {
        public const string ObjectName = "LogLatestButton";

        [SerializeField] private ScrollRect scroll;
        [SerializeField] private GameObject visual;
        private DialogueUI owner;

        /// <summary>ログの枠（<paramref name="logPanel"/>）の直下から探す。無ければ作る。</summary>
        public static LogLatestButtonUI Ensure(Transform logPanel, ScrollRect scroll, DialogueUI owner)
        {
            var go = SceneFirst.Child(logPanel, ObjectName, out bool made, typeof(RectTransform), typeof(LogLatestButtonUI));
            var ui = go.GetComponent<LogLatestButtonUI>();
            if (made) { ui.Build(scroll); ui.Awake(); }
            ui.owner = owner;
            if (ui.scroll == null) ui.scroll = scroll;
            var button = go.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                button.onClick.RemoveListener(ui.JumpToLatest);
                button.onClick.AddListener(ui.JumpToLatest);
            }
            return ui;
        }

        /// <summary>シーンに無かったときだけ組み立てる。読む領域の右下、スクロールバーの右（閉じる×の下）に置く</summary>
        private void Build(ScrollRect target)
        {
            scroll = target;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(32, 32);
            rect.anchoredPosition = new Vector2(196, -250);

            visual = new GameObject("Mark", typeof(RectTransform), typeof(PixelShapeGraphic), typeof(Button));
            visual.transform.SetParent(transform, false);
            var markRect = (RectTransform)visual.transform;
            markRect.anchorMin = Vector2.zero; markRect.anchorMax = Vector2.one;
            markRect.offsetMin = markRect.offsetMax = Vector2.zero;

            visual.GetComponent<PixelShapeGraphic>().raycastTarget = true;

            var button = visual.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            visual.SetActive(false);
        }

        /// <summary>
        /// ▼の形を描く。PixelShapeGraphic の形はシーンに保存されないので、置いてある物も毎回ここで描く
        /// （位置や大きさはシーンの物のまま）。
        /// </summary>
        private void Awake()
        {
            var shape = visual != null ? visual.GetComponent<PixelShapeGraphic>() : null;
            if (shape == null) return;
            // 暗い角丸の台に、明るい ▼ を 2 単位（1ドット）刻みで積む
            var plate = new Color32(40, 16, 20, 230);
            var mark = new Color32(245, 245, 220, 255);
            shape.Begin();
            shape.Box(-14, -12, 14, 12, plate);
            shape.Box(-12, -14, 12, 14, plate);
            for (int row = 0; row < 6; row++)
            {
                float half = 10f - row * 2f;
                float top = 6f - row * 2f;
                shape.Box(-half, top - 2f, half, top, mark);
            }
            shape.End();
        }

        private void Update()
        {
            if (visual == null || scroll == null) return;
            bool show = !IsAtLatest();
            if (visual.activeSelf != show) visual.SetActive(show);
        }

        /// <summary>最新の行（一番下）が見えているか。DialogueUI の「新しい行を追いかける」判定と同じ幅で見る</summary>
        private bool IsAtLatest()
        {
            if (scroll.content == null) return true;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            float overflow = Mathf.Max(0, scroll.content.rect.height - viewport.rect.height);
            return overflow * scroll.verticalNormalizedPosition <= 16f;
        }

        private void JumpToLatest()
        {
            if (owner != null) owner.JumpToLatestLog();
            else if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 0f; }
        }
    }
}
