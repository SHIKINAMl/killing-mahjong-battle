using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class TutorialScenarioCanonicalTests
    {
        private Type ScenarioType => Type.GetType("KillingMahjong.Managers.TutorialScenario, Assembly-CSharp", true);
        private ScriptableObject Canonical => (ScriptableObject)Resources.Load("Tutorial/TutorialScenario", ScenarioType);

        [Test]
        public void FirstRoundManganExampleIncludesHonorsAndMatchesDisplayedFiveHan()
        {
            var rounds = (IList)ScenarioType.GetField("rounds").GetValue(Canonical);
            var round = rounds[0];
            var type = round.GetType();
            var hand = (IList)type.GetField("manganHandBaseIds").GetValue(round);
            bool hasHonor = false;
            foreach (int tile in hand) hasHonor |= tile >= 27;
            Assert.That(hand.Count, Is.EqualTo(13));
            Assert.That(hasHonor, Is.True, "混一色の例を清一色・九蓮宝燈の牌姿に戻さない");

            var yaku = (IList)type.GetField("manganHandYaku").GetValue(round);
            var winYaku = (IList)type.GetField("yakuList").GetValue(round);
            CollectionAssert.AreEquivalent(yaku, winYaku);
            var rules = Type.GetType("KillingMahjong.GameRules, Assembly-CSharp", true);
            int han = 0;
            foreach (string name in yaku)
                han += (int)rules.GetMethod("GetBaseHan").Invoke(null, new object[] { name });
            Assert.That(han, Is.EqualTo(5));
            Assert.That(type.GetField("manganHandHan").GetValue(round), Is.EqualTo(han));
            Assert.That(type.GetField("rankText").GetValue(round), Is.EqualTo("満貫"));
            int bet = (int)type.GetField("betAmount").GetValue(round);
            Assert.That(rules.GetMethod("CalculateWinnerGain", new[] { typeof(int), typeof(int) })
                .Invoke(null, new object[] { bet, han }), Is.EqualTo(2000));
        }

        [Test]
        public void MissingSceneReferenceUsesIndependentCopyOfCanonicalScript()
        {
            var source = Canonical;
            Assert.That(source, Is.Not.Null);
            var copy = (ScriptableObject)ScenarioType.GetMethod("BuildDefault").Invoke(null, null);
            try
            {
                Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(JsonUtility.ToJson(source)));
                var rounds = (IList)ScenarioType.GetField("rounds").GetValue(copy);
                var lines = (IList)rounds[0].GetType().GetField("introLines").GetValue(rounds[0]);
                var text = lines[0].GetType().GetField("text");
                string original = (string)text.GetValue(lines[0]);
                text.SetValue(lines[0], "複製だけを編集");
                var sourceRounds = (IList)ScenarioType.GetField("rounds").GetValue(source);
                var sourceLines = (IList)sourceRounds[0].GetType().GetField("introLines").GetValue(sourceRounds[0]);
                Assert.That(text.GetValue(sourceLines[0]), Is.EqualTo(original));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void ExportAndResumeDoNotExposeRemovedLegacyRounds()
        {
            var source = Canonical;
            var rounds = (IList)ScenarioType.GetField("rounds").GetValue(source);
            Assert.That(rounds.Count, Is.EqualTo(2), "現行フローは第2局の分岐で完結する");
            var clamp = ScenarioType.GetMethod("ClampStartRound");
            foreach (int saved in new[] { 2, 3, 4, 5 })
                Assert.That(clamp.Invoke(null, new object[] { saved }), Is.EqualTo(1));

            var table = Type.GetType("KillingMahjong.Managers.Tutorial.TutorialLineTable, Assembly-CSharp", true);
            var rows = (IList)table.GetMethod("Dump", BindingFlags.Static | BindingFlags.Public)
                .Invoke(null, new object[] { source });
            foreach (var row in rows)
            {
                string id = (string)row.GetType().GetField("id").GetValue(row);
                Assert.That(id.StartsWith("r0.") || id.StartsWith("r1.") || id.StartsWith("revival."),
                    Is.True, "未使用の旧会話がTSVへ混ざっている: " + id);
            }

            var ron = ScenarioType.GetField("defeatRon").GetValue(source);
            Assert.That(((IList)ron.GetType().GetField("meldBaseIds").GetValue(ron)).Count, Is.EqualTo(12),
                "旧第3局から分離した、現行の敗北演出用の面子は残す");
        }
    }
}
