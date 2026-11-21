using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class BettingGaugeTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void GaugeTracksBetChangesEvenWhenTutorialHidesOutcomeCard(bool allowOutcomeCard)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var bettingType = Type.GetType("KillingMahjong.UI.BettingUI, Assembly-CSharp", true);
            var fxType = Type.GetType("KillingMahjong.UI.Effects.BetStakeFx, Assembly-CSharp", true);
            var go = new GameObject("BettingGaugeRegression", typeof(RectTransform));
            try
            {
                var betting = go.AddComponent(bettingType);
                var fx = go.AddComponent(fxType);
                bettingType.GetField("_stakeFx", flags).SetValue(betting, fx);
                bettingType.GetField("_outcomePreviewAllowed", flags).SetValue(betting, allowOutcomeCard);
                bettingType.GetField("currentMoney", flags).SetValue(betting, 20000);
                foreach (int bet in new[] { 200, 2000, 5000, 3000, 0 })
                {
                    bettingType.GetField("currentBet", flags).SetValue(betting, bet);
                    bettingType.GetMethod("UpdateOutcomePreview", flags).Invoke(betting, new object[] { 0, false, 1f });
                    Assert.That(fxType.GetField("weight", flags).GetValue(fx),
                        Is.EqualTo(Mathf.Clamp01(bet / 5000f)).Within(.0001f));
                    Assert.That(fxType.GetField("running", flags).GetValue(fx), Is.False,
                        "ゲージ更新だけで停止中の心音を再開しない");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
