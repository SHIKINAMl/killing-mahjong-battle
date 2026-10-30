using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    public static class AbilityBellDropEffect
    {
        public const string EffectId = "ui.ability_bell_drop";
        public const float Rise = 420f;

        public static IEnumerator Play(RectTransform bell, Vector2 goal)
        {
            if (bell == null) yield break;
            bool landed = false;
            try
            {
                const float fall = .45f;
                for (float t = 0f; t < fall;)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / fall);
                    bell.anchoredPosition = goal + new Vector2(0f, Rise * (1f - u * u));
                    yield return null;
                }
                bell.anchoredPosition = goal;
                landed = true;
                WholeScreenShake.Play(.18f, 14f, new Vector2(0f, 1f));
            }
            finally
            {
                // 中断しても空中には置き去りにしない。着地前の中断で揺れは鳴らさない。
                if (!landed && bell != null) bell.anchoredPosition = goal;
            }
        }
    }
}
