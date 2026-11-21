using System.Globalization;
using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>通常のHP増減と精算で、数字の書体・符号・色をそろえる。</summary>
    public static class HpNumberStyle
    {
        public static Color32 ColorForDelta(int delta) => delta > 0
            ? new Color32(101, 173, 255, 255) : new Color32(255, 92, 92, 255);

        public static string FormatDelta(int delta) => (delta > 0 ? "+" : delta < 0 ? "-" : "")
            + System.Math.Abs((long)delta).ToString("N0", CultureInfo.InvariantCulture);

        public static void Apply(TMP_Text text, int delta)
        {
            text.font = GameUiTypography.Display;
            text.fontStyle = FontStyles.Normal;
            text.color = ColorForDelta(delta);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }
    }
}
