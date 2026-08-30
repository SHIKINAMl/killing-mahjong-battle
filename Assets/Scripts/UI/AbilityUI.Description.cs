using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private TMPro.TextMeshProUGUI EnsureDescriptionBox()
        {
            if (_descText != null) return _descText;
            if (abilityWindow == null) return null;

            var boxTr = abilityWindow.Find(DescBoxName) as RectTransform;
            if (boxTr == null)
            {
                var go = new GameObject(DescBoxName, typeof(RectTransform), typeof(Image));
                boxTr = (RectTransform)go.transform;
                boxTr.SetParent(abilityWindow, false);
            }

            // 縁は箱そのものの地の色。内側に面を1枚敷く（行のタイルと同じ作り）
            var boxImg = boxTr.GetComponent<Image>();
            boxImg.sprite = null;
            boxImg.color = DescBorderColor;
            boxImg.raycastTarget = false;

            var fillTr = boxTr.Find(DescFillName) as RectTransform;
            if (fillTr == null)
            {
                var fillGo = new GameObject(DescFillName, typeof(RectTransform), typeof(Image));
                fillTr = (RectTransform)fillGo.transform;
                fillTr.SetParent(boxTr, false);
            }
            var fillImg = fillTr.GetComponent<Image>();
            fillImg.sprite = null;
            fillImg.color = DescBoxColor;
            fillImg.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(DescBorder, DescBorder);
            fillTr.offsetMax = new Vector2(-DescBorder, -DescBorder);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();

            boxTr.anchorMin = new Vector2(0.5f, 0.5f);
            boxTr.anchorMax = new Vector2(0.5f, 0.5f);
            boxTr.pivot = new Vector2(0.5f, 0f);
            boxTr.sizeDelta = new Vector2(RowWidth, DescBoxHeight);
            boxTr.anchoredPosition = new Vector2(
                PanelInnerCenter.x,
                PanelInnerCenter.y - PanelInnerHeight * 0.5f + ListTopMargin);
            boxTr.localScale = Vector3.one;

            var textTr = boxTr.Find(DescTextName) as RectTransform;
            TMPro.TextMeshProUGUI text;
            if (textTr == null)
            {
                var go = new GameObject(DescTextName, typeof(RectTransform));
                textTr = (RectTransform)go.transform;
                textTr.SetParent(boxTr, false);
                text = go.AddComponent<TMPro.TextMeshProUGUI>();
                if (tooltipText != null && tooltipText.font != null) text.font = tooltipText.font;
            }
            else
            {
                text = textTr.GetComponent<TMPro.TextMeshProUGUI>();
            }

            textTr.anchorMin = Vector2.zero;
            textTr.anchorMax = Vector2.one;
            textTr.offsetMin = new Vector2(4f, 3f);
            textTr.offsetMax = new Vector2(-4f, -3f);
            textTr.localScale = Vector3.one;

            // **折り返して全文を出す。** 46文字の「強襲」が最長で、
            // 128 幅・44 高に 9px で 4 行なら収まる。収まらない分は自動で縮む。
            //
            // **`Overflow` にしないこと。** 自動縮小の下限(7px)でも入らない文言を
            // 足したとき、はみ出した行が巻物の枠を突き抜けて盤面に出てしまう。
            // `Truncate` なら最悪でも欄の中で切れて止まる。
            text.margin = Vector4.zero;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.overflowMode = TMPro.TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMax = 10f;
            text.fontSizeMin = 7f;
            text.raycastTarget = false;

            _descText = text;
            return _descText;
        }
        private void HideLegacyTooltip()
        {
            if (tooltipText != null && !string.IsNullOrEmpty(tooltipText.text)) tooltipText.text = "";
            if (tooltipPanel != null && tooltipPanel.activeSelf) tooltipPanel.SetActive(false);
        }
        private void StyleCloseButton()
        {
            if (closeButton == null) return;
            var img = closeButton.GetComponent<Image>();
            if (img == null) return;

            img.sprite = null;
            img.color = CloseBorderColor;

            var rt = img.rectTransform;
            var fillTr = rt.Find(CloseFillName) as RectTransform;
            if (fillTr == null)
            {
                var go = new GameObject(CloseFillName, typeof(RectTransform), typeof(Image));
                fillTr = (RectTransform)go.transform;
                fillTr.SetParent(rt, false);
            }
            var fill = fillTr.GetComponent<Image>();
            fill.sprite = null;
            fill.color = DescBoxColor;
            fill.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(DescBorder, DescBorder);
            fillTr.offsetMax = new Vector2(-DescBorder, -DescBorder);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();
        }
    }
}
