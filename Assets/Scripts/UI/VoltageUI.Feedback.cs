using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class VoltageUI
    {
        /// <summary>試写専用の表示インスタンス。実際のボルテージには書き込まない。</summary>
        internal static VoltageUI CreatePresentation(RectTransform parent)
        {
            var ui = Attach(parent, false);
            Managers.VoltageSystem.Changed -= ui.Refresh;
            return ui;
        }

        internal void PresentFeedback(int level, bool broken, int points, int from, int to, float multiplier)
        {
            RenderFeedbackState(level, broken, points, from, to, multiplier);
        }
    }
}
