using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 場に出ている血（賭け金プール）の表示。
    ///
    /// 賭け金は確定した時点で両者の血から引かれ、決着するまで場に積まれたままになる。
    /// 流局では決着しないので次の局へ持ち越され、その局でも同額が賭けられて積み増される。
    /// プレイヤーからは「いくら血が場に出ているのか」が見えないと、
    /// 流局で血が減ったこととロンで戻ってくることが結びつかないため常時表示する。
    ///
    /// 額の管理はしない（表示専用）。積算は TutorialManager / GameUIPhaseController 側が持つ。
    /// </summary>
    public class BetPotUI : MonoBehaviour
    {
        [Header("表示先。未設定なら Awake で簡易ラベルを自動生成する")]
        [SerializeField, Tooltip("場の総額。{0} に総額が入る")] private TextMeshProUGUI totalText;
        [SerializeField, Tooltip("自分が場に出している額。{0} に額が入る")] private TextMeshProUGUI playerStakeText;
        [SerializeField, Tooltip("相手が場に出している額。{0} に額が入る")] private TextMeshProUGUI enemyStakeText;

        [Header("書式")]
        [SerializeField] private string totalFormat = "場の血 {0}";
        [SerializeField] private string playerStakeFormat = "自分 {0}";
        [SerializeField] private string enemyStakeFormat = "相手 {0}";

        [Header("挙動")]
        [SerializeField, Tooltip("総額が0のときは表示を隠す")] private bool hideWhenEmpty = true;
        [SerializeField, Tooltip("自動生成したラベルの位置（画面上端中央からのオフセット）")]
        private Vector2 fallbackOffset = new Vector2(0f, -40f);

        private CanvasGroup group;
        private int playerStake;
        private int enemyStake;
        private bool boardVisible = true;

        /// <summary>自分が場に出している額。</summary>
        public int PlayerStake => playerStake;

        /// <summary>相手が場に出している額。</summary>
        public int EnemyStake => enemyStake;

        /// <summary>場に出ている血の総額。</summary>
        public int Total => playerStake + enemyStake;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();

            // 場の血の上をタップして打牌が吸われないようにする
            group.blocksRaycasts = false;
            group.interactable = false;

            if (totalText == null && playerStakeText == null && enemyStakeText == null)
            {
                totalText = BuildFallbackLabel();
            }

            Refresh();
        }

        /// <summary>場に出ている額を設定し直す。</summary>
        public void SetStakes(int player, int enemy)
        {
            playerStake = Mathf.Max(0, player);
            enemyStake = Mathf.Max(0, enemy);
            Refresh();
        }

        /// <summary>賭け金が確定したときに呼ぶ。既に場に出ている分へ積み増す。</summary>
        public void AddStakes(int player, int enemy)
        {
            SetStakes(playerStake + player, enemyStake + enemy);
        }

        /// <summary>決着して場の血が動いたときに呼ぶ。</summary>
        public void Clear()
        {
            SetStakes(0, 0);
        }

        /// <summary>
        /// 盤面ごと隠す場面（立ち絵とセリフだけの演出中など）で使う。
        /// 額は保持したまま表示だけ消すので、盤面が戻れば持ち越し分もそのまま出る。
        /// </summary>
        public void SetVisible(bool visible)
        {
            boardVisible = visible;
            Refresh();
        }

        private void Refresh()
        {
            int total = Total;

            if (totalText != null) totalText.text = string.Format(totalFormat, total);
            if (playerStakeText != null) playerStakeText.text = string.Format(playerStakeFormat, playerStake);
            if (enemyStakeText != null) enemyStakeText.text = string.Format(enemyStakeFormat, enemyStake);

            if (group != null)
            {
                bool hidden = !boardVisible || (hideWhenEmpty && total <= 0);
                group.alpha = hidden ? 0f : 1f;
            }
        }

        /// <summary>
        /// インスペクタ未設定でも額が見えるようにする保険。
        /// 見た目を作り込む場合はシーン側でラベルを割り当てること（こちらは生成されなくなる）。
        /// </summary>
        private TextMeshProUGUI BuildFallbackLabel()
        {
            var go = new GameObject("PotLabel(auto)", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = fallbackOffset;
            rt.sizeDelta = new Vector2(420f, 60f);

            var label = go.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 40f;
            label.raycastTarget = false;
            label.color = Color.white;

            return label;
        }
    }
}
