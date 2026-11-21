using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    public class MatchmakingUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIcon; // Optional spinner

        /// <summary>合言葉のときだけ文字の後ろに敷く暗い板の名前。シーンに置く（<see cref="SceneFirst"/>）</summary>
        public const string BackdropName = "PasscodeBackdrop";

        /// <summary>板を文字の外へ広げる量（左右合わせて・上下合わせて）</summary>
        private static readonly Vector2 BackdropPadding = new Vector2(64f, 36f);

        private Image backdrop;
        private Canvas layer;

        /// <param name="withBackdrop">
        /// 合言葉を見せるとき true（2026-10-23 ユーザー指示「部屋を作った時の合言葉がわかりにくいのでわかりやすくして」）。
        /// 文字の後ろに暗い板を敷き、相手の吹き出しより手前に出す。以前は卓と立ち絵の上に白い字をそのまま重ねていた
        /// </param>
        public void ShowWaiting(string message = "Waiting for Opponent\n対戦相手を待っています...", bool withBackdrop = false)
        {
            Debug.Log($"[MatchmakingUI] ShowWaiting called with message: {message}");
            gameObject.SetActive(true);
            if (statusText != null)
            {
                statusText.text = message;
            }
            else
            {
                Debug.LogError("[MatchmakingUI] statusText is null!");
            }
            ApplyBackdrop(withBackdrop);

            // **待っているあいだ牌をじゃらじゃらさせる（2026-09-15 のユーザー指示）。**
            // 文字だけで止まっている画面だったので、卓の上で牌をかき混ぜている
            // 見た目を足す。作るのは1回だけで、2回目以降は使い回す。
            var rect = transform as RectTransform;
            if (rect != null) Effects.TileClatterEffect.Attach(rect, ClatterOffsetY);
        }

        /// <summary>
        /// 牌を並べる高さ。**画面の下端から測る。**
        /// 待ち文字は 200x50 の枠に 80pt を流し込んでいて枠からはみ出すので、
        /// 文字を基準にすると必ず重なる（2026-09-15 に実際そうなった）。
        /// </summary>
        private const float ClatterOffsetY = 110f;

        public void Hide()
        {
            ApplyBackdrop(false);
            gameObject.SetActive(false);
        }

        /// <summary>**エディタ用。** 暗い板をシーンに置く（<c>SceneFirstBaker.BakeMatch</c> から呼ぶ）。置いた物の名前を返す</summary>
        public string BakeBackdropForEditor()
        {
            EnsureBackdrop();
            if (backdrop != null) backdrop.gameObject.SetActive(false);
            return SceneFirst.Report(nameof(MatchmakingUI));
        }

        private void EnsureBackdrop()
        {
            if (backdrop != null || statusText == null) return;
            var go = SceneFirst.Child(statusText.transform.parent, BackdropName, out bool made,
                typeof(RectTransform), typeof(Image));
            backdrop = go.GetComponent<Image>();
            if (made)
            {
                backdrop.color = new Color32(14, 7, 16, 235);
                backdrop.raycastTarget = false;
            }
        }

        /// <summary>板を出す・しまう。出すときは文字の大きさに合わせ、文字の1つ奥に並べ、吹き出しより手前に上げる</summary>
        private void ApplyBackdrop(bool show)
        {
            if (show) EnsureBackdrop();
            if (backdrop != null) backdrop.gameObject.SetActive(show);

            // 吹き出し(16)より手前へ。合言葉のときだけ上げ、ふつうの「待っています」は今までどおり
            if (layer == null && show)
            {
                layer = GetComponent<Canvas>();
                if (layer == null) layer = gameObject.AddComponent<Canvas>();
            }
            if (layer != null)
            {
                layer.overrideSorting = show;
                if (show) layer.sortingOrder = Common.UISortingOrders.MatchmakingPasscode;
            }
            if (!show || backdrop == null || statusText == null) return;

            int textIndex = statusText.transform.GetSiblingIndex();
            if (backdrop.transform.GetSiblingIndex() > textIndex) backdrop.transform.SetSiblingIndex(textIndex);

            // 板は文字の欄と同じ所に基準を置き、文字が実際に描かれた範囲（textBounds。欄の基準点から測る）に合わせる
            statusText.ForceMeshUpdate();
            Bounds bounds = statusText.textBounds;
            var tr = statusText.rectTransform;
            var br = backdrop.rectTransform;
            br.anchorMin = tr.anchorMin;
            br.anchorMax = tr.anchorMax;
            br.pivot = new Vector2(.5f, .5f);
            br.anchoredPosition = tr.anchoredPosition + (Vector2)bounds.center;
            br.sizeDelta = (Vector2)bounds.size + BackdropPadding;
        }
    }
}
