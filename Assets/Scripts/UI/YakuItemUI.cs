using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class YakuItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI yakuNameText;
        // 配色は「自分＝青 / 敵＝赤」で統一している（スキルカットインの帯・影・血飛沫と同じ）。
        // 以前ここだけ逆になっていて、常時役一覧と全役一覧で自他の色が食い違っていた。
        [SerializeField] private TextMeshProUGUI localBoostText; // 自分の強化値（＋１など）。色は青
        [SerializeField] private TextMeshProUGUI enemyBoostText; // 敵の強化値（＋１など）。色は赤

        [Header("Optional")]
        [Tooltip("役の成立条件を出す専用テキスト。未設定なら役名の下に小さく差し込む。")]
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Tooltip("説明文の表示サイズ（役名に対する％）。専用テキスト未設定時のみ使う。")]
        [SerializeField, Range(30, 100)] private int inlineDescriptionSizePercent = 55;

        [Tooltip("役の説明文を常に併記する。OFF なら役名だけを並べ、説明はカーソルを合わせたときに出す")]
        [SerializeField] private bool showDescription = false;

        [Tooltip("カーソルを合わせたときに説明を吹き出しで出す")]
        [SerializeField] private bool showTooltipOnHover = true;

        /// <summary>ホバー時に出す説明文。一覧には出さないが保持しておく。</summary>
        private string _description;

        /// <summary>
        /// 役リストの1項目分をセットアップします
        /// </summary>
        /// <param name="yakuName">役の名前</param>
        /// <param name="localBoost">味方の強化値</param>
        /// <param name="enemyBoost">敵の強化値</param>
        public void Setup(string yakuName, int localBoost, int enemyBoost)
        {
            // 一覧は役名だけを並べる。説明はカーソルを合わせたときに吹き出しで出す。
            // showDescription を ON にすると、以前どおり常に併記する。
            _description = YakuInfo.GetDescription(yakuName);
            string description = showDescription ? _description : "";

            // ホバーを拾うには、この項目のどこかに raycastTarget が要る。
            // 背景画像が無いプレハブでも効くよう、透明な当たり判定を用意しておく。
            if (showTooltipOnHover) EnsureRaycastTarget();

            if (descriptionText != null)
            {
                descriptionText.text = description;
                // 説明を出さないときは行そのものを畳んで、名前だけが並ぶようにする
                descriptionText.gameObject.SetActive(showDescription);
                if (yakuNameText != null) yakuNameText.text = yakuName;
            }
            else if (yakuNameText != null)
            {
                // 専用テキストがPrefabに用意されていない場合でも読めるように、
                // リッチテキストで役名の下へ小さく差し込む（Prefab改修なしで機能させるため）。
                yakuNameText.text = string.IsNullOrEmpty(description)
                    ? yakuName
                    : $"{yakuName}\n<size={inlineDescriptionSizePercent}%>{description}</size>";
            }

            if (localBoostText != null)
            {
                // 0より大きければ "+1" のように表示し、0なら空文字（何も表示しない）
                localBoostText.text = localBoost > 0 ? $"+{localBoost}" : "";
            }

            if (enemyBoostText != null)
            {
                enemyBoostText.text = enemyBoost > 0 ? $"+{enemyBoost}" : "";
            }
        }

        /// <summary>
        /// ホバーを拾うための当たり判定を確保する。
        /// 既に raycastTarget な Graphic があれば何もしない。
        /// 無ければ、項目いっぱいに透明な Image を敷いて背面へ置く。
        /// </summary>
        private void EnsureRaycastTarget()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g.raycastTarget) return;
            }

            var existing = transform.Find("HoverArea");
            if (existing != null) return;

            var go = new GameObject("HoverArea", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // 完全透明でも raycast は拾える
            img.raycastTarget = true;
            rt.SetAsFirstSibling(); // 文字より背面へ
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!showTooltipOnHover || showDescription) return;
            YakuTooltip.Show(_description, yakuNameText != null ? yakuNameText.font : null);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!showTooltipOnHover) return;
            YakuTooltip.Hide();
        }

        private void OnDisable()
        {
            if (showTooltipOnHover) YakuTooltip.Hide();
        }
    }
}
