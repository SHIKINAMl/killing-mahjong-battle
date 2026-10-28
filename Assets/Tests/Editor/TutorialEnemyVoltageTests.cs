using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class TutorialEnemyVoltageTests
    {
        private Type voltage;
        private object round;
        private MethodInfo pick;

        [SetUp]
        public void SetUp()
        {
            voltage = Type.GetType("KillingMahjong.Managers.VoltageSystem, Assembly-CSharp", true);
            voltage.GetMethod("ResetAll").Invoke(null, null);
            var scenarioType = Type.GetType("KillingMahjong.Managers.TutorialScenario, Assembly-CSharp", true);
            var scenario = Resources.Load("Tutorial/TutorialScenario", scenarioType);
            round = ((IList)scenarioType.GetField("rounds").GetValue(scenario))[1];
            pick = Type.GetType("KillingMahjong.Managers.TutorialManager, Assembly-CSharp", true)
                .GetMethod("PickEnemyDiscardForSecondBattle", BindingFlags.NonPublic | BindingFlags.Static);
        }

        [TearDown]
        public void TearDown() => voltage.GetMethod("ResetAll").Invoke(null, null);

        [TestCase(false)]
        [TestCase(true)]
        public void RebuiltBranchHistoryNeverRepeatsEnemyDiscard(bool delayedArrival)
        {
            var seen = new HashSet<int>();
            var waits = (List<int>)round.GetType().GetField("waitBaseIds").GetValue(round);
            int previousPoints = 0;
            for (int turn = 1; turn <= 13; turn++)
            {
                // 正体判明後の履歴再構築を再現。敵の履歴をローカル集合から失わせる。
                if (turn == 11) seen.Clear();
                int enemy = (int)pick.Invoke(null, new object[] { round, turn, seen });
                Assert.That(waits.Contains(enemy), Is.False);
                Assert.That(voltage.GetMethod("IsNewTileType").Invoke(null, new object[] { enemy }), Is.True,
                    "敵 " + turn + " 打目が既出牌");
                seen.Add(enemy);
                if (delayedArrival)
                    voltage.GetMethod("MarkSeen").Invoke(null, new object[] { enemy });
                else
                    voltage.GetMethod("NotifyDiscard").Invoke(null, new object[] { true, enemy });

                // プレイヤーが次の台本候補を先に捨てるケースも含める。
                int player = (int)pick.Invoke(null, new object[] { round, turn, seen });
                seen.Add(player);
                voltage.GetMethod("NotifyDiscard").Invoke(null, new object[] { false, player });
                if (delayedArrival)
                    voltage.GetMethod("ApplyDiscardResult").Invoke(null, new object[] { true, true });
                int points = (int)voltage.GetMethod("GetPoints").Invoke(null, new object[] { true });
                Assert.That(points, Is.GreaterThan(previousPoints));
                Assert.That(voltage.GetMethod("IsBroken").Invoke(null, new object[] { true }), Is.False);
                previousPoints = points;
            }
        }
    }
}
