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
                .Invoke(null, new object[] { ron, remainingHp, 15000, 200, 5000, 1.1f });
            var type = result.GetType();
            Assert.That(type.GetField("ShowHpBalances").GetValue(result), Is.False);
            Assert.That(type.GetField("MyHpBefore").GetValue(result), Is.EqualTo(remainingHp));
            Assert.That(type.GetField("MyHpAfter").GetValue(result), Is.EqualTo(0));
            Assert.That(type.GetField("MyDelta").GetValue(result), Is.EqualTo(-23600));
            Assert.That(type.GetProperty("MyShownDelta").GetValue(result), Is.EqualTo(-remainingHp));
            Assert.That(type.GetProperty("TheirShownDelta").GetValue(result), Is.EqualTo(5000), "儲けは0でも自分の掛け金は返還される");
            Assert.That(type.GetField("TotalHan").GetValue(result), Is.EqualTo(13));
            Assert.That(type.GetField("AssaultApplied").GetValue(result), Is.True);
            Assert.That(type.GetField("AssaultBonusDamage").GetValue(result), Is.EqualTo(22000));
        }

        [Test]
        public void OrdinarySettlementsKeepMultiplierDisplayByDefault()
        {
            var type = Type.GetType("KillingMahjong.UI.RonSettlementInfo, Assembly-CSharp", true);
            Assert.That(type.GetField("ShowHpBalances").GetValue(Activator.CreateInstance(type)), Is.False);
        }

        [TestCase(200)]
        [TestCase(1000)]
        [TestCase(3000)]
        [TestCase(5000)]
        public void FreePlayerBetStillReachesDefeatThroughEnemyAssault(int playerBet)
        {
            var scenarioType = Type.GetType("KillingMahjong.Managers.TutorialScenario, Assembly-CSharp", true);
            var scenario = Resources.Load("Tutorial/TutorialScenario", scenarioType);
            var rounds = (System.Collections.IList)scenarioType.GetField("rounds").GetValue(scenario);
            int enemyBet = (int)rounds[1].GetType().GetField("enemyBetAmount").GetValue(rounds[1]);
            var ron = scenarioType.GetField("defeatRon").GetValue(scenario);
            var managerType = Type.GetType("KillingMahjong.Managers.TutorialManager, Assembly-CSharp", true);
            var result = managerType.GetMethod("BuildScriptedDefeatSettlement", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { ron, 22000 - playerBet, 15000, playerBet, enemyBet, 1.1f });
            Assert.That(result.GetType().GetField("MyBet").GetValue(result), Is.EqualTo(playerBet));
            Assert.That(result.GetType().GetField("MyHpAfter").GetValue(result), Is.EqualTo(0));
            Assert.That(result.GetType().GetField("TheirDelta").GetValue(result), Is.EqualTo(0));
        }
    }
}
