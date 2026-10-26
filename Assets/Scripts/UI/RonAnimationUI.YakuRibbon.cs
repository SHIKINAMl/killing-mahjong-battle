using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class RonAnimationUI
    {
        private const float YakuRevealInterval = .8f;

        /// <summary>役の紹介は斜めの帯へ集約し、清算表では繰り返さない。</summary>
        private IEnumerator ShowYakuRibbon(RectTransform parent, List<string> fallbackNames, RonSettlementInfo settlement)
        {
            bool hasRows = settlement != null && settlement.Rows != null && settlement.Rows.Count > 0;
            int count = hasRows ? settlement.Rows.Count : fallbackNames != null ? fallbackNames.Count : 0;
            if (count == 0) yield break;

            var ribbon = new GameObject("YakuRibbon", typeof(RectTransform), typeof(Image));
            ribbon.transform.SetParent(parent, false);
            ribbon.GetComponent<Image>().color = new Color(.05f, .05f, .05f, .95f);
            ribbon.GetComponent<Image>().raycastTarget = false;
            var rect = (RectTransform)ribbon.transform;
            rect.anchorMin = new Vector2(0f, .40f);
            rect.anchorMax = new Vector2(1f, .40f);
            rect.sizeDelta = new Vector2(200f, 100f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 2f);

            var label = new GameObject("YakuText", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(ribbon.transform, false);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = new Vector2(-100f, 0f);
            text.font = GameUiTypography.Body;
            text.color = Color.white;
            text.fontSizeMin = Common.UITypography.BodySmall;
            text.fontSizeMax = 32f;
            text.enableAutoSizing = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            // 長い役の組合せでも文字を潰さず、1役ずつ名前・翻数・強化段数を読ませる。
            for (int i = 0; i < count; i++)
            {
                var row = hasRows ? settlement.Rows[i] : null;
                string name = hasRows ? row.Name : fallbackNames[i];
                text.text = name;
                if (row != null && row.Boost > 0)
                    text.text += " <color=#FFD700>+" + row.Boost + "段</color>";
                if (row != null && settlement.ShowPerRowHan)
                    text.text += "   " + row.Han + "翻";
                Managers.AudioManager.Instance?.PlayYakuVoice(name);
                yield return new WaitForSeconds(YakuRevealInterval);
            }

            // 表を出すフレームには帯を残さない。
            ribbon.SetActive(false);
            Destroy(ribbon);
        }
    }
}
