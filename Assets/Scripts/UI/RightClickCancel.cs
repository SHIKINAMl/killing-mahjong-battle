using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 右クリックで1つ戻る（2026-10-11 のユーザー指示）。
    ///
    /// 「キャンセルボタンや戻るボタンがあるとき、右クリックすると1つ戻るようにしてほしい」。
    ///
    /// **やっていることは、いま押せる「やめる・戻る・閉じる」のボタンを代わりに押すだけ。**
    /// 画面ごとに戻り方を書き足さなくて済むよう、ボタンを名前と文字で見分けている。
    /// 押せるボタンしか押さないので、右クリックでできることは、左クリックでできることと同じ。
    ///
    /// 候補が複数あるときは、**いちばん手前の画面のもの**を押す（Canvas の sortingOrder が大きいほう。
    /// 同じなら、あとに描かれるほう）。設定の上に確認が出ていれば、確認のほうが閉じる。
    ///
    /// **押さないもの**
    ///   ・タイトルへ戻るボタン、設定の「デバッグ部屋へ」 … 「1つ戻る」ではなく対局を抜けてしまう
    ///   ・選びなおすボタン     … 確定した手牌を取り下げる操作で、戻るとは違う
    ///   ・ゲームをやめるボタン … 部屋の「やめる」（RoomMenu_Exit）はゲームを終わらせる。文字は同じでも戻るではない
    ///   ・いま実際には押せないボタン … 画面の外へしまってある物、上に別の画面が被さっている物。
    ///     ボタンの真ん中へ左クリックしたとき、そのボタンに当たるかどうかで見る（<see cref="IsClickable"/>）
    ///   ・牌の上での右クリック … 打牌フェイズでは「選んで即打牌」に使っている（TileInteraction）
    ///
    /// シーンには置かない。どの画面でも効かせたいので、起動時に1つ作って残す
    /// （PhaseTransitionDirector と同じやり方）。
    /// </summary>
    public class RightClickCancel : MonoBehaviour
    {
        /// <summary>ボタンの名前に入っていたら「戻る」系とみなす言葉（小文字で比べる）。</summary>
        private static readonly string[] NameWords = { "close", "cancel", "back", "return", "nobutton", "no_button" };

        /// <summary>ボタンの文字がこれなら「戻る」系とみなす（前後の空白は除いて、全体が一致）。</summary>
        private static readonly string[] LabelWords =
        {
            "やめる", "戻る", "もどる", "キャンセル", "閉じる", "とじる", "いいえ",
            // コレクションの演出の試写の「一覧へ戻る」（EffectPreviewPlayer）。再生中に右クリックで一覧へ戻れる
            "一覧へ戻る",
            "×", "x", "X", "✕", "▼", "Close", "Cancel", "Back", "No",
        };

        /// <summary>名前か文字に入っていたら、押さない言葉（小文字で比べる）。</summary>
        // 「デバッグ部屋」… 設定の「デバッグ部屋へ」（2026-10-23。ボタン名が ReturnButton なので、外さないと右クリックで対局を抜ける）
        private static readonly string[] SkipWords = { "title", "タイトル", "デバッグ部屋", "reselect", "選びなお", "選び直", "exit", "quit" };

        private static RightClickCancel _instance;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfNeeded()
        {
            if (_instance != null) return;
            var go = new GameObject(nameof(RightClickCancel));
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RightClickCancel>();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;
            Button target = FindCancelButtonAt(mouse.position.ReadValue());
            if (target == null) return;

            Debug.Log("[RightClickCancel] 右クリックで '" + target.name + "' を押しました");
            target.onClick.Invoke();
        }

        /// <summary>カーソルの案内と実際の右クリックが同じ条件を見る。</summary>
        public static Button FindCancelButtonAt(Vector2 screenPosition)
        {
            if (_instance == null || !_instance.isActiveAndEnabled || _instance.IsPointerOverTile(screenPosition))
                return null;
            return FindCancelButton();
        }

        /// <summary>いま押せる「戻る」系のボタンのうち、いちばん手前のもの。無ければ null。</summary>
        public static Button FindCancelButton()
        {
            Button best = null;
            int bestOrder = int.MinValue;
            int bestDepth = int.MinValue;

            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) continue;
                if (!IsCancelButton(button)) continue;
                if (!IsClickable(button)) continue;

                var canvas = button.GetComponentInParent<Canvas>();
                if (canvas == null) continue;
                int order = canvas.rootCanvas == canvas || canvas.overrideSorting
                    ? canvas.sortingOrder
                    : canvas.rootCanvas.sortingOrder;
                int depth = HierarchyDepthOrder(button.transform);

                if (order > bestOrder || (order == bestOrder && depth > bestDepth))
                {
                    best = button;
                    bestOrder = order;
                    bestDepth = depth;
                }
            }
            return best;
        }

        private static bool IsCancelButton(Button button)
        {
            string name = button.name.ToLowerInvariant();
            string label = LabelOf(button);
            string labelLower = label.ToLowerInvariant();

            foreach (var skip in SkipWords)
            {
                if (name.Contains(skip) || labelLower.Contains(skip)) return false;
            }
            foreach (var word in NameWords)
            {
                if (name.Contains(word)) return true;
            }
            foreach (var word in LabelWords)
            {
                if (label == word) return true;
            }
            return false;
        }

        private static string LabelOf(Button button)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>(false);
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text.Trim();
            var legacy = button.GetComponentInChildren<Text>(false);
            if (legacy != null && !string.IsNullOrEmpty(legacy.text)) return legacy.text.Trim();
            return string.Empty;
        }

        private static readonly List<RaycastResult> ClickHits = new List<RaycastResult>();

        /// <summary>
        /// いま実際に押せるか。**ボタンの真ん中を左クリックしたら、そのボタンに当たるか**で見る。
        /// 画面の外へしまってあるパネルのボタン（能力一覧の閉じるボタンは、閉じていても有効のまま）や、
        /// 上に別の画面が被さっているボタンは、ここで外れる。透明にして隠してある画面も外す。
        /// </summary>
        private static bool IsClickable(Button button)
        {
            for (Transform t = button.transform; t != null; t = t.parent)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && (group.alpha <= 0.01f || !group.blocksRaycasts)) return false;
                var parentCanvas = t.GetComponent<Canvas>();
                if (parentCanvas != null && !parentCanvas.enabled) return false;
            }

            var eventSystem = EventSystem.current;
            var rect = button.transform as RectTransform;
            var canvas = button.GetComponentInParent<Canvas>();
            if (eventSystem == null || rect == null || canvas == null) return false;

            Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            if (center.x < 0f || center.y < 0f || center.x > Screen.width || center.y > Screen.height) return false;

            var pointer = new PointerEventData(eventSystem) { position = center };
            ClickHits.Clear();
            eventSystem.RaycastAll(pointer, ClickHits);
            if (ClickHits.Count == 0 || ClickHits[0].gameObject == null) return false;
            return ClickHits[0].gameObject.transform.IsChildOf(button.transform);
        }

        /// <summary>同じ Canvas の中で、あとに描かれる物ほど大きくなる数。</summary>
        private static int HierarchyDepthOrder(Transform t)
        {
            int order = 0;
            int weight = 1;
            // 親に近い階層ほど重く効かせたいが、深さは画面ごとに違うので、兄弟順を浅いほうから足し込む
            var chain = new List<Transform>();
            for (Transform p = t; p != null; p = p.parent) chain.Add(p);
            for (int i = chain.Count - 1; i >= 0 && weight < 100000000; i--)
            {
                order = order * 32 + Mathf.Min(31, chain[i].GetSiblingIndex());
                weight *= 32;
            }
            return order;
        }

        /// <summary>マウスの下に牌があるか。打牌フェイズの右クリック（選んで即打牌）を横取りしない。</summary>
        private bool IsPointerOverTile(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            _hits.Clear();
            eventSystem.RaycastAll(pointer, _hits);
            if (_hits.Count == 0) return false;
            return _hits[0].gameObject != null && _hits[0].gameObject.GetComponentInParent<TileInteraction>() != null;
        }
    }
}
