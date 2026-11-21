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
        private GameObject confirmationCard;

        /// <summary>強化した役名を短く見せ、同じ名前が右上の強化枠へ収まる。</summary>
        public IEnumerator PlayAfterCutin(string yakuName, YakuListUI board)
        {
            _heldBoard = board;
            try
            {
                if (board == null) yield break;
                board.HoldLocalBoostChip(yakuName);
                board.SpotlightLocalBoost(yakuName, 2f);
                var chip = board.LocalBoostChipOf(yakuName);
                if (chip == null) yield break;

                _stage = new SkillEffectStage("RoleConfirmationStage", UISortingOrders.SkillEffectContent, transform);
                string shown = string.IsNullOrEmpty(yakuName) ? "役強化"
                    : YakuNameUtil.ToDisplayText(new YakuNameUtil.Entry { BaseName = yakuName, Boost = 0, Count = 1 });
                confirmationCard = new GameObject("BoostRoleCard", typeof(RectTransform), typeof(Image));
                var card = confirmationCard.GetComponent<RectTransform>();
                card.SetParent(_stage.Rect, false);
                card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
                card.sizeDelta = new Vector2(Mathf.Clamp(shown.Length * 40f + 32f, 180f, 320f), 88f);
                var backing = confirmationCard.GetComponent<Image>();
                backing.color = new Color(.12f, .08f, .09f, .96f);
                backing.raycastTarget = false;
                var name = _stage.AddText("BoostRoleName", shown, Mathf.Min(40f, 280f / Mathf.Max(1, shown.Length)), Color.white);
                name.rectTransform.SetParent(card, false);
                name.rectTransform.anchoredPosition = new Vector2(0f, 13f);
                var amount = _stage.AddText("BoostRoleAmount", "+1段", 24f, new Color(1f, .85f, .45f));
                amount.rectTransform.SetParent(card, false);
                amount.rectTransform.anchoredPosition = new Vector2(0f, -23f);
                Vector2 destination = _stage.LocalRectOf(chip).center;
                Vector2 from = destination + new Vector2(-150f, -85f);
                from.x = Mathf.Clamp(from.x, -388f + card.sizeDelta.x * .5f, 388f - card.sizeDelta.x * .5f);
                from.y = Mathf.Clamp(from.y, -244f, 244f);
                card.anchoredPosition = from;
                yield return new WaitForSeconds(.55f);
                for (float t = 0f; t < .28f; t += Time.deltaTime)
                {
                    float p = Mathf.Clamp01(PresentationTiming.Sample(t) / .28f);
                    card.anchoredPosition = Vector2.Lerp(from, destination, p * p);
                    card.localScale = Vector3.one * Mathf.Lerp(1f, .25f, p);
                    yield return null;
                }
                Destroy(confirmationCard);
                confirmationCard = null;
                board.ReleaseLocalBoostChip();
                _heldBoard = null;
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
            if (confirmationCard != null) Destroy(confirmationCard);
            confirmationCard = null;
        }
    }
}
