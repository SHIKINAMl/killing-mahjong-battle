using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class TutorialFinalSettlementTests
    {
        [TestCase(1)]
        [TestCase(19000)]
        [TestCase(22000)]
        public void FinalDefeatDisplaysActualRemainingHpLoss(int remainingHp)
        {
            var scenarioType = Type.GetType("KillingMahjong.Managers.TutorialScenario, Assembly-CSharp", true);
            var scenario = Resources.Load("Tutorial/TutorialScenario", scenarioType);
            var ron = scenarioType.GetField("defeatRon").GetValue(scenario);
            var managerType = Type.GetType("KillingMahjong.Managers.TutorialManager, Assembly-CSharp", true);
            var result = managerType.GetMethod("BuildScriptedDefeatSettlement", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { ron, remainingHp, 15000 });
            var type = result.GetType();
            Assert.That(type.GetField("ShowHpBalances").GetValue(result), Is.True);
            Assert.That(type.GetField("MyHpBefore").GetValue(result), Is.EqualTo(remainingHp));
            Assert.That(type.GetField("MyHpAfter").GetValue(result), Is.EqualTo(0));
            Assert.That(type.GetField("MyDelta").GetValue(result), Is.EqualTo(-remainingHp));
            Assert.That(type.GetProperty("MyShownDelta").GetValue(result), Is.EqualTo(-remainingHp));
            Assert.That(type.GetProperty("TheirShownDelta").GetValue(result), Is.EqualTo(0));
            Assert.That(type.GetField("TotalHan").GetValue(result), Is.EqualTo(13));
        }

        [Test]
        public void OrdinarySettlementsKeepMultiplierDisplayByDefault()
        {
            var type = Type.GetType("KillingMahjong.UI.RonSettlementInfo, Assembly-CSharp", true);
            Assert.That(type.GetField("ShowHpBalances").GetValue(Activator.CreateInstance(type)), Is.False);
        }
    }
}
