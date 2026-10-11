using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    public class PerspectiveActionSerializationTests
    {
        private Type PayloadType => Type.GetType("KillingMahjong.Network.ActionPayload, Assembly-CSharp", true);
        private Type HandlerType => Type.GetType("KillingMahjong.Network.NetworkMessageHandler, Assembly-CSharp", true);

        private object Payload(string skill, string suit)
        {
            var payload = Activator.CreateInstance(PayloadType);
            PayloadType.GetField("skill_type").SetValue(payload, skill);
            PayloadType.GetField("suit").SetValue(payload, suit);
            return payload;
        }

        private string Serialize(string action, object payload)
        {
            return (string)HandlerType.GetMethod("SerializeAction", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new[] { action, payload });
        }

        private string OriginalJson(string action, object payload)
        {
            var wrapperType = Type.GetType("KillingMahjong.Network.ActionPayloadWrapper, Assembly-CSharp", true);
            var messageType = Type.GetType("KillingMahjong.Network.ActionMessage, Assembly-CSharp", true);
            var wrapper = Activator.CreateInstance(wrapperType);
            wrapperType.GetField("action").SetValue(wrapper, action);
            wrapperType.GetField("data").SetValue(wrapper, payload);
            var message = Activator.CreateInstance(messageType);
            messageType.GetField("data").SetValue(message, wrapper);
            return JsonUtility.ToJson(message);
        }

        [Serializable] private class Packet { public string type; public Wrapper data; }
        [Serializable] private class Wrapper { public string action; public Data data; }
        [Serializable] private class Data
        {
            public string skill_type;
            public string suit;
            public List<int> target_hand_indexes;
            public List<int> choices;
            public int bet_amount;
        }

        [TestCase("manzu")]
        [TestCase("pinzu")]
        [TestCase("souzu")]
        [TestCase("jihai")]
        public void ChosenSuitIsAddedToTheActualActionEnvelope(string suit)
        {
            var payload = Payload("perspective", suit);
            var packet = JsonUtility.FromJson<Packet>(Serialize("skill", payload));
            Assert.That(packet.type, Is.EqualTo("action"));
            Assert.That(packet.data.action, Is.EqualTo("skill"));
            Assert.That(packet.data.data.skill_type, Is.EqualTo("perspective"));
            Assert.That(packet.data.data.suit, Is.EqualTo(suit));
            Assert.That(PayloadType.GetField("suit").GetValue(payload), Is.EqualTo(suit));
        }

        [TestCase(null)]
        [TestCase("")]
        public void RandomPerspectiveOmitsSuitAndKeepsTheExistingPacket(string suit)
        {
            var payload = Payload("perspective", suit);
            var json = Serialize("skill", payload);
            Assert.That(json, Does.Not.Contain("\"suit\""));
            Assert.That(json, Is.EqualTo(OriginalJson("skill", payload)));
        }

        [TestCase("skill", "assault")]
        [TestCase("bet", "perspective")]
        [TestCase("mulligan_select", "mulligan")]
        public void OtherActionsStayByteForByteCompatible(string action, string skill)
        {
            var payload = Payload(skill, "manzu");
            PayloadType.GetField("target_hand_indexes").SetValue(payload, new List<int> { 1, 3, 5 });
            PayloadType.GetField("choices").SetValue(payload, new List<int> { 0, 2, 4 });
            PayloadType.GetField("bet_amount").SetValue(payload, 1234);
            var json = Serialize(action, payload);
            Assert.That(json, Is.EqualTo(OriginalJson(action, payload)));
            Assert.That(json, Does.Not.Contain("\"suit\""));
            var data = JsonUtility.FromJson<Packet>(json).data.data;
            Assert.That(data.target_hand_indexes, Is.EqualTo(new[] { 1, 3, 5 }));
            Assert.That(data.choices, Is.EqualTo(new[] { 0, 2, 4 }));
            Assert.That(data.bet_amount, Is.EqualTo(1234));
        }
    }
}
