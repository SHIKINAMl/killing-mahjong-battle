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

        // ==================== 行の見た目（2026-10-14） ====================
        //
        // プレハブの文字は 200x50 の矩形に大きな margin を足して位置を決めていて、
        // 役名が中央揃えで行ごとに左端がばらつき、敵の「+1」が役名の右端に食い込んでいた。
        // **矩形を行（100x25）に合わせて切り直し、margin は 0 にする。**
        // 寸法は強化チップ（YakuListUI）と同じく 1ドット＝2UI単位の格子に乗せる。
        //
        // プレハブではなくここで直すのは、YakuListUI のグリッド寄せと同じ理由。
        // 位置だけを決め、フォント・マテリアルには触らない。

        /// <summary>行の縁。1ドット。根元の黒い Image がそのまま縁になる。</summary>
        private const float RowBorder = 2f;

        /// <summary>役名の左の余白。縁から2ドット空ける。</summary>
        private const float NamePaddingLeft = 4f;

        /// <summary>強化値1つぶんの幅。「+1」〜「+9」が収まる。</summary>
        private const float BoostSlotWidth = 15f;

        /// <summary>役名と強化値の間。</summary>
        private const float NameBoostGap = 2f;

        private const float NameFontSize = 13f;
        private const float NameMinFontSize = 8f;
        private const float BoostFontSize = 12f;

        /// <summary>縁の内側に敷く面。真っ黒より一段明るくして、行の区切りを見せる。</summary>
        private static readonly Color RowFillColor = new Color32(0x1A, 0x1E, 0x2E, 0xFF);

        /// <summary>カーソルを合わせた行の面。どの行の説明を見ているかが分かる程度に上げる。</summary>
        private static readonly Color RowFillHoverColor = new Color32(0x2C, 0x33, 0x52, 0xFF);

        /// <summary>役名。紙に近い生成り（プレハブの色を引き継ぐ）。</summary>
        private static readonly Color NameColor = new Color32(0xF5, 0xF5, 0xDC, 0xFF);

        // 自分＝青 / 敵＝赤 は変えない。面が暗いので、プレハブの色より少しだけ明るくして読みやすくする
        private static readonly Color LocalBoostColor = new Color32(0x5C, 0xAE, 0xFF, 0xFF);
        private static readonly Color EnemyBoostColor = new Color32(0xFF, 0x5A, 0x4E, 0xFF);

        private const string RowFillName = "RowFill";

        private Image _rowFill;
        private bool _styled;

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

            ApplyRowStyle();

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

        /// <summary>
        /// 行を「縁・面・役名・敵の強化値・自分の強化値」の並びに整える。最初の1回だけ。
        /// 役名は左揃えにして、行をまたいで左端をそろえる。強化値は右端に固定幅で並べ、
        /// 値が無い行でも列がずれないようにする（敵が左・自分が右の順はプレハブのまま）。
        /// </summary>
        private void ApplyRowStyle()
        {
            if (_styled) return;
            _styled = true;

            // 面（縁の内側）。根元の Image を縁として残す
            if (GetComponent<Image>() != null)
            {
                var existing = transform.Find(RowFillName);
                _rowFill = existing != null ? existing.GetComponent<Image>() : null;
                if (_rowFill == null)
                {
                    var go = new GameObject(RowFillName, typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(transform, false);
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = new Vector2(RowBorder, RowBorder);
                    rt.offsetMax = new Vector2(-RowBorder, -RowBorder);
                    _rowFill = go.GetComponent<Image>();
                    _rowFill.raycastTarget = false;
                }
                _rowFill.color = RowFillColor;
                _rowFill.rectTransform.SetAsFirstSibling(); // 文字より背面へ
            }

            float boostsWidth = BoostSlotWidth * 2f;

            if (yakuNameText != null)
            {
                SetStretchRect(yakuNameText.rectTransform,
                    new Vector2(RowBorder + NamePaddingLeft, RowBorder),
                    new Vector2(-(RowBorder + boostsWidth + NameBoostGap), -RowBorder));
                yakuNameText.margin = Vector4.zero;
                yakuNameText.alignment = TextAlignmentOptions.Left;
                yakuNameText.color = NameColor;
                // 「純正九蓮宝燈」のような長い名前は1行のまま縮める
                yakuNameText.enableAutoSizing = true;
                yakuNameText.fontSizeMax = NameFontSize;
                yakuNameText.fontSizeMin = NameMinFontSize;
                yakuNameText.textWrappingMode = TextWrappingModes.NoWrap;
                yakuNameText.overflowMode = TextOverflowModes.Overflow;
            }

            StyleBoostText(enemyBoostText, EnemyBoostColor, RowBorder + boostsWidth, RowBorder + BoostSlotWidth);
            StyleBoostText(localBoostText, LocalBoostColor, RowBorder + BoostSlotWidth, RowBorder);
        }

        /// <summary>強化値を右端から <paramref name="leftFromRight"/>〜<paramref name="rightFromRight"/> の枠に置く。</summary>
        private static void StyleBoostText(TextMeshProUGUI text, Color color, float leftFromRight, float rightFromRight)
        {
            if (text == null) return;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(-leftFromRight, RowBorder);
            rt.offsetMax = new Vector2(-rightFromRight, -RowBorder);

            text.margin = Vector4.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.enableAutoSizing = false;
            text.fontSize = BoostFontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }

        private static void SetStretchRect(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private void SetHover(bool hover)
        {
            if (_rowFill != null) _rowFill.color = hover ? RowFillHoverColor : RowFillColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(true);
            if (!showTooltipOnHover || showDescription) return;
            YakuTooltip.Show(_description, yakuNameText != null ? yakuNameText.font : null);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
            if (!showTooltipOnHover) return;
            YakuTooltip.Hide();
        }

        private void OnDisable()
        {
            // 開いたまま閉じると Exit が来ないので、ここで戻す
            SetHover(false);
            if (showTooltipOnHover) YakuTooltip.Hide();
        }
    }
}
