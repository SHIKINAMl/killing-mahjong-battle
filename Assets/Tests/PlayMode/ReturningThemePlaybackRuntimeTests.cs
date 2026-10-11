using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("ReturningThemePlaybackRuntime")]
    public class ReturningThemePlaybackRuntimeTests
    {
        private Type _type;
        private Component _audio;
        private GameObject _listener;
        private bool _wasRunningInBackground;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private object Call(string name, params object[] args)
        {
            return _type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(_audio, args);
        }

        private object Property(string name)
        {
            return _type.GetProperty(name).GetValue(_audio, null);
        }

        private int AnchorSample { get { return (int)_type.GetField("_p3AnchorSample", Private).GetValue(_audio); } }

        private void Phase(string name)
        {
            var method = _type.GetMethod("SetPhaseBgm");
            method.Invoke(_audio, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, name) });
        }

        private IEnumerator WaitForTrack(string name)
        {
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until)
            {
                if ((string)Property("Proposal3ClipName") == name && !(bool)Property("IsProposal3Switching"))
                {
                    var decks = (AudioSource[])_type.GetField("_p3Decks", Private).GetValue(_audio);
                    int active = (int)_type.GetField("_p3Active", Private).GetValue(_audio);
                    if (decks != null && decks[active].isPlaying) yield break;
                }
                yield return null;
            }
            Assert.Fail("Timed out waiting for " + name + "; actual=" + Property("Proposal3ClipName"));
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _listener = new GameObject("ReturningThemePlaybackTestListener");
            _listener.AddComponent<AudioListener>();
            _type = Type.GetType("KillingMahjong.Managers.AudioManager, Assembly-CSharp", true);
            yield return null;
            _audio = (Component)_type.GetProperty("Instance").GetValue(null, null);
            Assert.That(_audio, Is.Not.Null);
            Call("StopBGM");
            // 設定保存APIは使わない。テスト用シーンのAudioManagerだけを切り替える。
            Call("ApplyMatchBgmSet", 5);
            Call("SetBgmIntensity", 2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_audio != null) Call("StopBGM");
            if (_listener != null) UnityEngine.Object.Destroy(_listener);
            Application.runInBackground = _wasRunningInBackground;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActualScheduledTransitionsResumeInsteadOfRestarting()
        {
            Phase("Discard");
            yield return WaitForTrack("rt_bgm_field_2");
            Assert.That(AnchorSample, Is.Zero, "The first entry should retain the intro.");
            yield return new WaitForSecondsRealtime(8f);

            Phase("Betting");
            yield return WaitForTrack("rt_bgm_betting");
            int introEnd = AnchorSample;
            Assert.That(introEnd, Is.GreaterThan(0), "An unplayed long phase should skip its intro.");
            TestContext.Out.WriteLine("First betting entry sample=" + introEnd);

            Phase("TurnDecision");
            yield return WaitForTrack("rt_bgm_tension");
            int fieldResume = (int)Call("ReturningThemeStartSample", "rt_bgm_field_2",
                Call("GetProposal3Clip", "rt_bgm_field_2"));
            Phase("Discard");
            yield return WaitForTrack("rt_bgm_field_2");
            Assert.That(AnchorSample, Is.EqualTo(fieldResume));
            Assert.That(AnchorSample, Is.GreaterThan(introEnd));
            TestContext.Out.WriteLine("Resumed field sample=" + AnchorSample);

            Call("SetBgmIntensity", 4);
            yield return WaitForTrack("rt_bgm_field_4");
            Assert.That(AnchorSample, Is.GreaterThan(fieldResume), "Intensity switching must keep advancing.");

            Phase("Betting");
            yield return WaitForTrack("rt_bgm_betting");
            Assert.That(AnchorSample, Is.GreaterThan(introEnd), "Betting must resume its own later bookmark.");
            TestContext.Out.WriteLine("Resumed betting sample=" + AnchorSample);

            Phase("Draw");
            yield return WaitForTrack("rt_bgm_draw_base");
            Assert.That((bool)Property("IsDrawMotifAlive"), Is.True);
            Phase("Ron");
            Assert.That((bool)Property("IsDrawMotifAlive"), Is.False, "Draw effects must be gated immediately.");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That((bool)Property("IsDrawMotifSourcePlaying"), Is.False);
            yield return WaitForTrack("rt_bgm_ron");
            int ronAnchor = AnchorSample;
            double ronDsp = (double)_type.GetField("_p3AnchorDsp", Private).GetValue(_audio);
            Phase("Liquidation");
            yield return null;
            Assert.That(AnchorSample, Is.EqualTo(ronAnchor));
            Assert.That((double)_type.GetField("_p3AnchorDsp", Private).GetValue(_audio), Is.EqualTo(ronDsp));

            // 小節予約前の連続変更で、途中のフェイズをあとから鳴らさない。
            Phase("Betting");
            Phase("HandSelection");
            Phase("Discard");
            yield return WaitForTrack("rt_bgm_field_4");
            Assert.That((string)Property("Proposal3ClipName"), Is.EqualTo("rt_bgm_field_4"));
            Call("StopBGM");
            Assert.That((bool)Property("IsProposal3Running"), Is.False);
            Assert.That((bool)Property("IsDrawMotifSourcePlaying"), Is.False);
            int afterStop = (int)Call("ReturningThemeStartSample", "rt_bgm_prepare",
                Call("GetProposal3Clip", "rt_bgm_prepare"));
            Assert.That(afterStop, Is.Zero, "A new match after explicit stop may start with its intro again.");
        }
    }
}
