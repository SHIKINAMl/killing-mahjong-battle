using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class TutorialMulliganPracticeTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void EveryChoiceKeepsTheManganExampleAvailable(int roundIndex)
        {
            var scenarioType = Type.GetType("KillingMahjong.Managers.TutorialScenario, Assembly-CSharp", true);
            var scenario = Resources.Load("Tutorial/TutorialScenario", scenarioType);
            var round = ((IList)scenarioType.GetField("rounds").GetValue(scenario))[roundIndex];
            var roundType = round.GetType();
            var wall = (List<int>)roundType.GetField("wallBaseIds").GetValue(round);
            var hand = (List<int>)roundType.GetField("manganHandBaseIds").GetValue(round);
            var replacements = (List<int>)roundType.GetField("mulliganReplacementBaseIds").GetValue(round);
            var managerType = Type.GetType("KillingMahjong.Managers.TutorialManager, Assembly-CSharp", true);
            var reserved = (HashSet<int>)managerType.GetMethod("GetManganReservedWallIndexes",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { round });
            Assert.That(reserved.Count, Is.EqualTo(13));
            Assert.That(replacements.Count, Is.EqualTo(3));
            Assert.That(roundType.GetField("manganHandHan").GetValue(round), Is.EqualTo(5));
            var allowed = new List<int>();
            for (int i = 0; i < wall.Count; i++) if (!reserved.Contains(i)) allowed.Add(i);
            Assert.That(allowed.Count, Is.EqualTo(21));

            // 交換に出せる全3枚と、候補6枚から残す全3枚の組み合わせを確認する。
            for (int a = 0; a < allowed.Count - 2; a++)
            for (int b = a + 1; b < allowed.Count - 1; b++)
            for (int c = b + 1; c < allowed.Count; c++)
            {
                var outgoing = new[] { allowed[a], allowed[b], allowed[c] };
                var options = new List<int> { wall[outgoing[0]], wall[outgoing[1]], wall[outgoing[2]] };
                options.AddRange(replacements);
                for (int x = 0; x < 4; x++)
                for (int y = x + 1; y < 5; y++)
                for (int z = y + 1; z < 6; z++)
                {
                    var after = new List<int>(wall);
                    after[outgoing[0]] = options[x];
                    after[outgoing[1]] = options[y];
                    after[outgoing[2]] = options[z];
                    foreach (int tile in hand) Assert.That(after.Remove(tile), Is.True);
                }
            }
        }
    }
}
