using UnityEngine;

namespace KillingMahjong.Common
{
    /// <summary>
    /// Defines a standardized typographic scale for the game's UI.
    /// This ensures consistent font sizes across all screens and components.
    /// </summary>
    public static class UITypography
    {
        public const float Giant = 250f;     // 画面全体を覆う特大テキスト（ロン！、最終スコアなど）
        public const float Huge = 150f;      // 非常に目立つテキスト（役満などのランク、マリガン開始文字など）
        public const float Title = 90f;      // 大見出し（フェイズ遷移など）
        public const float Header = 60f;     // 中見出し（サブテキスト、計算式など）
        public const float BodyLarge = 40f;  // 会話テキスト、大きめのボタン、矢印などのUIアイコン
        public const float Body = 30f;       // 一般的なボタン、オプションテキスト、ダイアログ本文
        public const float BodySmall = 22f;  // リストの項目（役一覧）、ロード画面、注釈
    }
}
