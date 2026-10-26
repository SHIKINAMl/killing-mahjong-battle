using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>読むための文字と、勝負を見せる文字を用途で選ぶ。</summary>
    public static class GameUiTypography
    {
        public static TMP_FontAsset Body => Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed")
            ?? TMP_Settings.defaultFontAsset;

        public static TMP_FontAsset Display => Resources.Load<TMP_FontAsset>("x8y12pxDenkiChip/x8y12pxDenkiChip_Dynamic")
            ?? Body;
    }
}
