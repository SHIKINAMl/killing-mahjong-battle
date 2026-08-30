using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        public void SetBgmFilter(bool isMuffled, float fadeDuration = -1f)
        {
            if (bgmLowPassFilter == null) return;

            float targetFreq = isMuffled ? MuffledCutoff : OpenCutoff;
            if (fadeDuration < 0f) fadeDuration = isMuffled ? FilterMuffleDuration : FilterOpenDuration;

            // 同じ行き先へ改めて頼まれただけなら、進行中のフェードを潰さない。
            // 潰すと**フェードが毎回やり直しになって伸びる**（フェーズ変更で2か所から呼ばれていた）
            if (filterFadeCoroutine != null)
            {
                if (Mathf.Approximately(_filterTargetFreq, targetFreq)) return;
                StopCoroutine(filterFadeCoroutine);
            }

            filterFadeCoroutine = StartCoroutine(FilterFadeRoutine(targetFreq, fadeDuration));
        }

        private System.Collections.IEnumerator FilterFadeRoutine(float targetFreq, float duration)
        {
            _filterTargetFreq = targetFreq;

            float startFreq = Mathf.Max(bgmLowPassFilter.enabled ? bgmLowPassFilter.cutoffFrequency : OpenCutoff, 20f);
            bgmLowPassFilter.enabled = true; // 確実にオンにする

            float logStart = Mathf.Log(startFreq);
            float logTarget = Mathf.Log(targetFreq);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 端をなめらかにする。始まりと終わりの「動き出し・止まり」が耳に付きにくくなる
                t = t * t * (3f - 2f * t);
                bgmLowPassFilter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(logStart, logTarget, t));
                yield return null;
            }

            bgmLowPassFilter.cutoffFrequency = targetFreq;
            filterFadeCoroutine = null;

            // 全帯域まで開いたらフィルター自体をオフにして負荷軽減＆音質劣化防止
            if (targetFreq >= OpenCutoff)
            {
                bgmLowPassFilter.enabled = false;
            }
        }

    }
}

