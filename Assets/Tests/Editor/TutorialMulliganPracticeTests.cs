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
        [Test]
        public void ConfirmedExchangeSurvivesIntroCleanupAndAutoMangan()
        {
            var boardType = Type.GetType("KillingMahjong.Managers.BoardStateManager, Assembly-CSharp", true);
            var managerType = Type.GetType("KillingMahjong.Managers.TutorialManager, Assembly-CSharp", true);
            var scenarioType = Type.GetType("KillingMahjong.Managers.TutorialScenario, Assembly-CSharp", true);
            var scenario = Resources.Load("Tutorial/TutorialScenario", scenarioType);
            var round = ((IList)scenarioType.GetField("rounds").GetValue(scenario))[1];
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var instance = boardType.GetProperty("Instance");
            object previous = instance.GetValue(null);
            var boardObject = new GameObject("Mulligan board test");
            var managerObject = new GameObject("Mulligan tutorial test");
            try
            {
                var board = boardObject.AddComponent(boardType);
                instance.SetValue(null, board);
                var manager = managerObject.AddComponent(managerType);
                managerType.GetField("_round", flags).SetValue(manager, round);
                var wall = new List<int>((List<int>)round.GetType().GetField("wallBaseIds").GetValue(round));
                boardType.GetMethod("SetLocalState").Invoke(board, new object[] { wall, new List<int>(), new List<int>(), null });
                var cleanup = (Action)managerType.GetMethod("CaptureIntroDemoBoard", flags).Invoke(manager, null);
                var reserved = (HashSet<int>)managerType.GetMethod("GetManganReservedWallIndexes",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { round });
                int index = 0;
                while (reserved.Contains(index)) index++;
                int replacement = 19;
                Assert.That(wall[index], Is.Not.EqualTo(replacement));
                boardType.GetMethod("ReplaceWallTileAt").Invoke(board, new object[] { index, replacement });
                cleanup();
                Assert.That(((List<int>)boardType.GetProperty("OriginalWallTiles").GetValue(board))[index], Is.EqualTo(replacement),
                    "能力紹介の終了で確定した交換が取り消されている");
                managerType.GetMethod("ApplyMockAutoMangan").Invoke(manager, null);
                Assert.That(((List<int>)boardType.GetProperty("OriginalWallTiles").GetValue(board))[index], Is.EqualTo(replacement),
                    "おまかせで配牌し直し、交換が取り消されている");
                Assert.That(((List<int>)boardType.GetProperty("CurrentHandTiles").GetValue(board)).Count, Is.EqualTo(13));
                Assert.That(((List<int>)boardType.GetProperty("CurrentWallTiles").GetValue(board)).Count, Is.EqualTo(21));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(boardObject);
                instance.SetValue(null, previous);
            }
        }

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
