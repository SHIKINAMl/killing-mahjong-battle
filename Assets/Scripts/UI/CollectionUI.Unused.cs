using UnityEngine;

namespace KillingMahjong.UI
{
    public sealed partial class CollectionUI
    {
        /// <summary>
        /// 「没案」タブ。**演出の一覧表のうち、没案の分類が付いた行だけを並べる**（2026-10-07）。
        ///
        /// 以前は赤い目の敗北演出1件だけを置いた専用の画面だった。演出を一覧表に
        /// まとめたときに没案もそちらへ入ったので、同じ一覧の作り
        /// （<see cref="BuildEffectList"/>）を借りて、没案だけをここへ分けている。
        /// 没案を増やすには、<see cref="GameEffectCatalog"/> の表で分類を
        /// <see cref="GameEffectCatalog.UnusedGroup"/> にするだけでよい。
        /// </summary>
        private void BuildUnusedPage(Transform parent)
        {
            BuildEffectList(parent, "没案", "名前・IDで検索", entry => entry.IsUnused, null);
        }
    }
}
