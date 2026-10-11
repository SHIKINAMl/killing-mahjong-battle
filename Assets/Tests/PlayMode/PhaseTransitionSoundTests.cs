using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("PhaseTransitionSound")]
    public class PhaseTransitionSoundTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject audioHost, phaseHost, listenerHost;
        private Component audio, phase, centerText;
        private bool wasRunningInBackground;
        private bool ownsAudioHost;

        private static Type MainType(string name)
        {
            return Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        }

        private static object Field(object target, string name)
        {
            return target.GetType().GetField(name, Members).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, Members).SetValue(target, value);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            {
                listenerHost = new GameObject("TransitionSoundTestListener");
                listenerHost.AddComponent<AudioListener>();
            }
            var audioType = MainType("Managers.AudioManager");
            // Bootstrapは隔離した試験シーンにもAudioManagerを生成する。
            audio = (Component)audioType.GetProperty("Instance").GetValue(null);
            ownsAudioHost = audio == null;
            if (ownsAudioHost)
            {
                audioHost = new GameObject("TransitionSoundTestAudio");
                audio = audioHost.AddComponent(audioType);
            }
            else audioHost = audio.gameObject;
            yield return null; // 起動時のStartを終えてから試験用BGMに切り替える。
            audioType.GetMethod("StopBGM").Invoke(audio, null);
            // 保存値は変更せず、試験用AudioManagerだけで夜卓の流局曲を開始する。
            audioType.GetMethod("ApplyMatchBgmSet").Invoke(audio, new object[] { 5 });
            var setPhase = audioType.GetMethod("SetPhaseBgm");
            setPhase.Invoke(audio, new[] { Enum.Parse(setPhase.GetParameters()[0].ParameterType, "Draw") });

            phaseHost = new GameObject("TransitionSoundTestPhase", typeof(RectTransform));
            phase = phaseHost.AddComponent(MainType("UI.PhaseTransitionUI"));
            ((Behaviour)phase).enabled = false; // テスト用部品を設定し、本編Startは実行しない。
            var line = new GameObject("TransitionSoundTestBand", typeof(RectTransform));
            line.transform.SetParent(phaseHost.transform, false);
            SetField(phase, "horizontalLineRt", line.GetComponent<RectTransform>());
            var text = new GameObject("TransitionSoundTestText", typeof(RectTransform));
            text.transform.SetParent(phaseHost.transform, false);
            centerText = text.AddComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));
            SetField(phase, "centerText", centerText);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (audio != null) audio.GetType().GetMethod("StopBGM").Invoke(audio, null);
            if (phaseHost != null) UnityEngine.Object.Destroy(phaseHost);
            if (ownsAudioHost && audioHost != null) UnityEngine.Object.Destroy(audioHost);
            if (listenerHost != null) UnityEngine.Object.Destroy(listenerHost);
            Application.runInBackground = wasRunningInBackground;
            yield return null;
        }

        [TestCase("br_band_open", 37485)]
        [TestCase("br_band_close", 41895)]
        [TestCase("br_fall", 66150)]
        [TestCase("br_riser", 59535)]
        public void ApprovedClipsLoadAtTheExistingLength(string name, int frames)
        {
            var clip = Resources.Load<AudioClip>("Stingers/" + name);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.frequency, Is.EqualTo(44100));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.samples, Is.EqualTo(frames));
            Assert.That(clip.LoadAudioData(), Is.True);
            var data = new float[frames];
            Assert.That(clip.GetData(data, 0), Is.True);
            float peak = 0f;
            foreach (float value in data)
            {
                Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            Assert.That(peak, Is.InRange(0.3f, 0.54f), "The approved four clips have restrained peaks, not the old loud sweeps.");
        }

        [UnityTest]
        public IEnumerator DrawOmitsDedicatedCueAndKeepsBgmAndCallbacks()
        {
            int savedSet = PlayerPrefs.GetInt("MatchBgmSet", -1);
            float savedVolume = PlayerPrefs.GetFloat("BgmVolume", -1);
            var audioType = audio.GetType();
            float deadline = Time.realtimeSinceStartup + 10f;
            AudioSource deck = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var decks = (AudioSource[])Field(audio, "_p3Decks");
                int active = (int)Field(audio, "_p3Active");
                bool switching = (bool)audioType.GetProperty("IsProposal3Switching").GetValue(audio);
                if (!switching && decks != null && decks[active].isPlaying
                    && decks[active].clip.name == "rt_bgm_draw_base")
                {
                    deck = decks[active];
                    break;
                }
                yield return null;
            }
            Assert.That(deck, Is.Not.Null, "The actual night-theme draw BGM must be playing.");
            var clip = deck.clip;
            float volume = deck.volume;
            var cache = (IDictionary)Field(audio, "stingerClips");
            Assert.That(cache.Contains("st_draw"), Is.False);
            int midpoint = 0, complete = 0;
            float began = Time.realtimeSinceStartup;
            Action mid = () => { Assert.That(complete, Is.Zero); midpoint++; };
            Action end = () => { Assert.That(midpoint, Is.EqualTo(1)); complete++; };
            phase.GetType().GetMethod("PlayDrawTransition").Invoke(phase, new object[] { mid, end });
            deadline = began + 10f;
            bool sawDrawText = false;
            bool sawBandAudio = false, sawFallAudio = false;
            while (complete == 0 && Time.realtimeSinceStartup < deadline)
            {
                if (centerText.gameObject.activeSelf)
                {
                    sawDrawText |= (string)centerText.GetType().GetProperty("text").GetValue(centerText) == "流局";
                }
                foreach (var source in audioHost.GetComponentsInChildren<AudioSource>())
                {
                    Assert.That(source.clip == null || source.clip.name != "st_draw", Is.True,
                        "No dedicated draw cue may be scheduled or playing.");
                    if (source.isPlaying && source.clip != null)
                    {
                        sawBandAudio |= source.clip.name == "br_band_open";
                        sawFallAudio |= source.clip.name == "br_fall";
                    }
                }
                Assert.That(cache.Contains("st_draw"), Is.False);
                Assert.That(deck.clip, Is.SameAs(clip));
                Assert.That(deck.isPlaying, Is.True);
                Assert.That(deck.volume, Is.EqualTo(volume).Within(.00001f));
                yield return null;
            }
            Assert.That(complete, Is.EqualTo(1));
            Assert.That(midpoint, Is.EqualTo(1));
            Assert.That(sawDrawText, Is.True);
            Assert.That(sawBandAudio && sawFallAudio, Is.True, "Keep the adopted transition sounds.");
            Assert.That(Time.realtimeSinceStartup - began, Is.GreaterThanOrEqualTo(3.45f),
                "Keep the default 0.5 s band, 1 s text, 1 s fade and 1 s dark hold.");
            Assert.That((bool)Field(phase, "isDarkened"), Is.True);
            Assert.That(PlayerPrefs.GetInt("MatchBgmSet", -1), Is.EqualTo(savedSet));
            Assert.That(PlayerPrefs.GetFloat("BgmVolume", -1), Is.EqualTo(savedVolume));
            TestContext.Out.WriteLine("Draw: br_band_open + br_fall only; rt_bgm_draw_base kept playing; midpoint then completion; no st_draw.");
        }
    }
}
