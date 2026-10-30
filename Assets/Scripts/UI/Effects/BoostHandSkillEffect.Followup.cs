using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    public partial class BoostHandSkillEffect
    {
        private Image confirmationGlow;

        /// <summary>通常対局のカットイン直後は、強化対象の枠だけを短く強調する。</summary>
        public IEnumerator PlayAfterCutin(string yakuName, YakuListUI board)
        {
            _heldBoard = board;
            try
            {
                if (board == null) yield break;
                board.ReleaseLocalBoostChip();
                _heldBoard = null;
                board.SpotlightLocalBoost(yakuName, 1.1f);
                var chip = board.LocalBoostChipOf(yakuName);
                if (chip == null) yield break;
                _poppedChip = chip;
                _chipScale = chip.localScale;

                var glow = new GameObject("BoostConfirmation", typeof(RectTransform), typeof(Image));
                glow.transform.SetParent(chip, false);
                confirmationGlow = glow.GetComponent<Image>();
                confirmationGlow.raycastTarget = false;
                var rect = confirmationGlow.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                AudioManager.Instance?.PlayBoostSE(1.12f, .35f);

                const float seconds = .55f;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    float sample = PresentationTiming.Sample(t);
                    float pop = sample < .18f ? Mathf.Sin(sample / .18f * Mathf.PI) : 0f;
                    chip.localScale = _chipScale * (1f + .035f * pop);
                    confirmationGlow.color = new Color(1f, .9f, .6f,
                        .32f * (1f - Mathf.Clamp01(sample / .3f)));
                    yield return null;
                }
            }
            finally
            {
                ClearFollowup();
                if (_poppedChip != null) _poppedChip.localScale = _chipScale;
                _poppedChip = null;
                if (_heldBoard != null) _heldBoard.ReleaseLocalBoostChip();
                _heldBoard = null;
                Destroy(gameObject);
            }
        }

        private void ClearFollowup()
        {
            if (confirmationGlow != null) Destroy(confirmationGlow.gameObject);
            confirmationGlow = null;
        }
    }
}
