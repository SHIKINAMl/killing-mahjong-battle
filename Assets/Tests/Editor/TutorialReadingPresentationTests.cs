using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.Tests
{
    public class TutorialReadingPresentationTests
    {
        [Test]
        public void ReadingFocusRestoresOtherOwnersAndPreservesSettings()
        {
            var go = new GameObject("ReadingAudioTest");
            go.SetActive(false);
            var first = new GameObject("FirstReader");
            var second = new GameObject("SecondReader");
            try
            {
                var type = Type.GetType("KillingMahjong.Managers.AudioManager, Assembly-CSharp", true);
                var audio = go.AddComponent(type);
                var volume = type.GetField("bgmVolume");
                object original = volume.GetValue(audio);
                var set = type.GetMethod("SetReadingFocus");
                var release = type.GetMethod("ReleaseReadingFocus");
                var gain = type.GetProperty("ReadingFocusGain");
                set.Invoke(audio, new object[] { first, .55f });
                set.Invoke(audio, new object[] { second, .7f });
                Assert.That(gain.GetValue(audio), Is.EqualTo(.55f));
                release.Invoke(audio, new object[] { first });
                Assert.That(gain.GetValue(audio), Is.EqualTo(.7f));
                release.Invoke(audio, new object[] { second });
                Assert.That(gain.GetValue(audio), Is.EqualTo(1f));
                Assert.That(volume.GetValue(audio), Is.EqualTo(original));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CancellingDocumentRestoresMusicAndClearsItsOverlay()
        {
            var audioRoot = new GameObject("ReadingCancelAudioTest");
            audioRoot.SetActive(false);
            var root = new GameObject("ReadingCancelTest", typeof(RectTransform));
            try
            {
                var audioType = Type.GetType("KillingMahjong.Managers.AudioManager, Assembly-CSharp", true);
                var audio = audioRoot.AddComponent(audioType);
                var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
                scrim.transform.SetParent(root.transform, false);
                new GameObject("Document", typeof(RectTransform)).transform.SetParent(root.transform, false);
                var type = Type.GetType("KillingMahjong.UI.TutorialReadingPresentation, Assembly-CSharp", true);
                var effect = (Component)type.GetMethod("Prepare").Invoke(null, new object[] { root.transform });
                type.GetField("audioOwner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(effect, audio);
                audioType.GetMethod("SetReadingFocus").Invoke(audio, new object[] { effect, .55f });
                type.GetMethod("Cancel").Invoke(effect, null);
                type.GetMethod("Cancel").Invoke(effect, null);
                Assert.That(audioType.GetProperty("ReadingFocusGain").GetValue(audio), Is.EqualTo(1f));
                Assert.That(scrim.GetComponent<Image>().color.a, Is.EqualTo(0f));
                Assert.That(type.GetProperty("IsReading").GetValue(effect), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(audioRoot);
            }
        }
    }
}
