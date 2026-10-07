using UnityEngine;

namespace KillingMahjong.UI
{
    public sealed partial class CollectionUI
    {
        /// <summary>
        /// 「没案」タブ。**演出の一覧表のうち、没案の印が付いた行だけを並べる**（2026-10-07）。
        ///
        /// 以前は赤い目の敗北演出1件だけを置いた専用の画面だった。演出を一覧表に
        /// まとめたときに没案もそちらへ入ったので、同じ一覧の作り
        /// （<see cref="BuildEffectList"/>）を借りて、没案だけをここへ分けている。
        /// 没案を増やすには、<see cref="GameEffectCatalog"/> の表でその行に
        /// `unused: true` を足すだけでよい（<see cref="GameEffectCatalog.Entry.IsUnused"/>）。
        /// </summary>
        private void BuildUnusedPage(Transform parent)
        {
            BuildEffectList(parent, "没案", "名前・分類・IDで検索", entry => entry.IsUnused, null);
        }
    }
}
