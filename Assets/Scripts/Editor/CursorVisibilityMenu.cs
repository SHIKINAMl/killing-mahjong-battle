using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// エディタで再生中に OS のカーソルを表示したままにするかの切り替え。
    ///
    /// **既定は「表示する」。** `CustomCursor` はゲーム中 OS のカーソルを毎フレーム
    /// 消しにいくので、そのままだとゲームビューをクリックした瞬間に本物のカーソルが
    /// 消え、開発中はどこを指しているのか分からなくなる（2026-08-15 の要望）。
    ///
    /// **ビルドには影響しない。** 設定は `EditorPrefs` に持つのでプロジェクトにも入らない。
    /// 本番と同じ見た目（手の絵だけ）を確認したいときはここをオフにする。
    /// </summary>
    internal static class CursorVisibilityMenu
    {
        private const string MenuPath = "Tools/開発用/ゲーム中も OS のカーソルを表示";

        // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
        [MenuItem(MenuPath, priority = 100)]
#endif
        private static void Toggle()
        {
            bool next = !CustomCursor.KeepOsCursorInEditor;
            CustomCursor.KeepOsCursorInEditor = next;
            Menu.SetChecked(MenuPath, next);

            // 再生中に切り替えたら、その場で効かせる。次のフレームで
            // CustomCursor.Update が同じ値に揃えるので、ここは見た目の即応のため
            if (Application.isPlaying) Cursor.visible = next;

            Debug.Log(next
                ? "[Cursor] エディタ再生中も OS のカーソルを表示します（ビルドには影響しません）。"
                : "[Cursor] 本番と同じく、エディタ再生中も OS のカーソルを隠します。");
        }

        // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
        [MenuItem(MenuPath, validate = true)]
#endif
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, CustomCursor.KeepOsCursorInEditor);
            return true;
        }
    }
}
