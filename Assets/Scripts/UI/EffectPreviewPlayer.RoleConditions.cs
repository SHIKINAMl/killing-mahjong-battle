using System.Collections;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PreviewRoleConditions()
        {
            var reveal = HandWaitRevealUI.Ensure(rig.transform);
            var data = new[] {
                new WaitData { tile=1, han=9, mangan_or_more=true, multiplier=2, multiplier_label="倍満", yaku=new[]{"平和","清一色","一盃口","立直"} },
                new WaitData { tile=4, han=7, mangan_or_more=true, multiplier=1.5f, multiplier_label="跳満", yaku=new[]{"清一色","立直"} }
            };
            reveal.Show(data, rig.tiles, false);
            reveal.enabled = false; // 試写はマウス位置でなく時刻で出入りを示す。同じ表示実装を呼ぶ。
            var roles = reveal.RoleConditions;
            scope.AddCleanup(() => { if (reveal != null) { roles.HideImmediate(); reveal.Hide(); } });
            yield return new WaitForSeconds(.6f);
            roles.SetHovered(true); yield return new WaitForSeconds(2f);
            roles.SetHovered(false); yield return new WaitForSeconds(.6f);
            roles.SetHovered(true); yield return new WaitForSeconds(2f);
            roles.SetHovered(false); yield return new WaitForSeconds(.6f);
            roles.SetHovered(true); yield return new WaitForSeconds(.04f);
            roles.SetHovered(false); yield return new WaitForSeconds(.6f);
            roles.SetHovered(true); yield return new WaitForSeconds(1.5f);
            roles.SetHovered(false);
        }
    }
}
