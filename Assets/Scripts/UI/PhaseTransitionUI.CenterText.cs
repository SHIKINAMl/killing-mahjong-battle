using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator CenterTextAnimRoutine(string text, float duration, Action onComplete = null)
        {
            if (horizontalLineRt != null) PlayTransitionStinger("br_band_open");
            yield return OpenBand();
            yield return RevealBandText(text);

            yield return new WaitForSeconds(duration);
            yield return HideBandText();

            // 閉じる音は打点が終端にあるので、閉じ始めと同時に鳴らして帯が閉じ切る瞬間に合わせる
            PlayTransitionStinger("br_band_close");
            yield return CloseBand();
            onComplete?.Invoke();
        }
    }
}
