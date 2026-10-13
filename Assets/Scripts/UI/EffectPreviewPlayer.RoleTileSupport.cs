using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    /// <summary>コレクション専用の固定例。本編の役判定・対応情報の代わりには使わない。</summary>
    public static class RoleTileSupportPreviewData
    {
        public static int[] Hand => new[] { 1, 1, 2, 2, 3, 3, 3, 4, 5, 8, 27, 27, 27 };
        private static YakuSupportGroup Group(int[] indexes, bool wait = false) =>
            new YakuSupportGroup { wall_indexes = indexes, include_wait_tile = wait };
        public static WaitData CreateWait() => new WaitData {
            tile = 8, han = 6, mangan_or_more = true, multiplier = 1.5f, multiplier_label = "跳満",
            yaku = new[] { "一盃口", "東", "混一色", "立直" },
            source_wall_indexes = Enumerable.Range(0, 13).ToArray(),
            yaku_supports = new[] {
                new YakuSupportData { name = "一盃口", kind = "tiles", groups = new[] { Group(new[]{0,2,4}), Group(new[]{1,3,5}) } },
                new YakuSupportData { name = "東", kind = "tiles", groups = new[] { Group(new[]{10,11,12}) } },
                new YakuSupportData { name = "混一色", kind = "whole_hand", groups = new[] { Group(Enumerable.Range(0,13).ToArray(), true) } },
                new YakuSupportData { name = "立直", kind = "context", groups = new YakuSupportGroup[0] }
            }
        };
    }
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PreviewRoleTileSupport()
        {
            var targets = new RoleTileTarget[13]; var ids = RoleTileSupportPreviewData.Hand;
            for (int i = 0; i < targets.Length; i++)
            {
                var image = rig.handTiles[i].GetComponent<Image>();
                image.sprite = rig.tiles.GetTileSprite(ids[i]); targets[i] = new RoleTileTarget(image, i, ids[i]);
            }
            var reveal = HandWaitRevealUI.Ensure(rig.transform);
            reveal.enabled = false; // 同じ表示実装へ、試写の時刻でポインタ位置を渡す。
            reveal.BindRoleTileTargets(targets);
            reveal.Show(new[] { RoleTileSupportPreviewData.CreateWait() }, rig.tiles, false);
            scope.AddCleanup(() => { if (reveal != null) { reveal.Hide(); reveal.BindRoleTileTargets(null); } });
            Canvas.ForceUpdateCanvases();
            var label = reveal.transform.Find("StrengthPanel/Yaku").GetComponent<TMP_Text>(); label.ForceMeshUpdate();
            yield return new WaitForSecondsRealtime(.6f);
            reveal.RefreshHoverVisibility(RolePreviewLeftPoint(label, 0)); yield return new WaitForSecondsRealtime(1.4f);
            reveal.RefreshHoverVisibility(RolePreviewLeftPoint(label, 1)); yield return new WaitForSecondsRealtime(1.2f);
            for (int i = 0; i < 4; i++)
            {
                var card = (RectTransform)reveal.RoleConditions.transform.Find("RoleCard" + i);
                reveal.RefreshHoverVisibility(RectTransformUtility.WorldToScreenPoint(null, card.TransformPoint(card.rect.center)));
                yield return new WaitForSecondsRealtime(1.2f);
            }
            reveal.RefreshHoverVisibility(new Vector2(-100000, -100000)); yield return new WaitForSecondsRealtime(.6f);
            reveal.RefreshHoverVisibility(RolePreviewLeftPoint(label, 0)); yield return new WaitForSecondsRealtime(1.2f);
            reveal.SetForcedVisible(true); yield return new WaitForSecondsRealtime(.6f);
            reveal.Hide(); yield return null;
        }
        private static Vector2 RolePreviewLeftPoint(TMP_Text label, int index)
        {
            var line = label.textInfo.lineInfo[index];
            return RectTransformUtility.WorldToScreenPoint(null, label.rectTransform.TransformPoint(
                new Vector2((line.lineExtents.min.x + line.lineExtents.max.x) * .5f, (line.ascender + line.descender) * .5f)));
        }
    }
}
