using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class MatchmakingUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject loadingIcon; // Optional spinner

        public void ShowWaiting(string message = "Waiting for Opponent\n対戦相手を待っています...")
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
            gameObject.SetActive(false);
        }
    }
}
