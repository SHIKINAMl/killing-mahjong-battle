using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace KillingMahjong.Tests
{
    [Category("ReturningThemePlayback")]
    public class ReturningThemePlaybackTests
    {
        private GameObject _host;
        private Component _audio;
        private Type _type;
        private AudioClip _longClip;
        private AudioClip _shortClip;
        private double _bar;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [SetUp]
        public void SetUp()
        {
            // Assembly-CSharpへのasmdef参照はできないので、既存の本体を反射で検査する。
            _type = Type.GetType("KillingMahjong.Managers.AudioManager, Assembly-CSharp", true);
            _host = new GameObject("ReturningThemePlaybackTest");
            _host.SetActive(false); // Awake・常駐化・起動音楽を実行しない。
            _audio = _host.AddComponent(_type);
            _longClip = AudioClip.Create("rt_bgm_prepare", 6381529, 1, 44100, false);
            _shortClip = AudioClip.Create("rt_bgm_betting", 622588, 1, 44100, false);
            _bar = (double)_longClip.samples / _longClip.frequency / 82.0;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
            UnityEngine.Object.DestroyImmediate(_longClip);
            UnityEngine.Object.DestroyImmediate(_shortClip);
        }

        private object Invoke(string method, params object[] args)
        {
            return _type.GetMethod(method, Private).Invoke(_audio, args);
        }

        private int Start(string name, AudioClip clip = null)
        {
            return (int)Invoke("ReturningThemeStartSample", name, clip ?? _longClip);
        }

        private void Remember(string name, double bars)
        {
            Invoke("RememberReturningThemePosition", name, _longClip, bars * _bar);
        }

        private void AssertBar(int sample, double bars)
        {
            Assert.That(sample, Is.EqualTo(Math.Round(bars * _bar * 44100)).Within(1.0));
        }

        [Test]
        public void FirstPlaybackStartsWithIntro()
        {
            Assert.That(Start("rt_bgm_prepare"), Is.Zero);
        }

        [Test]
        public void MerelyRequestingASampleDoesNotConsumeIntro()
        {
            Start("rt_bgm_prepare");
            Assert.That(Start("rt_bgm_field_2"), Is.Zero);
        }

        [Test]
        public void ReturningPhaseResumesItsOwnBookmark()
        {
            Remember("rt_bgm_prepare", 10);
            Remember("rt_bgm_betting", 24);
            AssertBar(Start("rt_bgm_prepare"), 10);
            AssertBar(Start("rt_bgm_betting"), 24);
        }

        [Test]
        public void UnplayedLongPhaseSkipsRepeatedIntro()
        {
            Remember("rt_bgm_prepare", 10);
            AssertBar(Start("rt_bgm_ron"), 4);
        }

        [Test]
        public void InterruptedIntroIsNotReplayedOnReturn()
        {
            Remember("rt_bgm_prepare", 2);
            AssertBar(Start("rt_bgm_prepare"), 4);
        }

        [Test]
        public void AllFieldIntensitiesShareTheirBookmark()
        {
            Remember("rt_bgm_field_1", 22);
            AssertBar(Start("rt_bgm_field_4"), 22);
            Remember("rt_bgm_field_4", 30);
            AssertBar(Start("rt_bgm_field_2"), 30);
        }

        [Test]
        public void ResumeSnapsForwardToABarBoundary()
        {
            Remember("rt_bgm_prepare", 8.25);
            AssertBar(Start("rt_bgm_prepare"), 9);
        }

        [Test]
        public void WrappedBookmarkDoesNotReplayIntro()
        {
            Remember("rt_bgm_prepare", 82);
            AssertBar(Start("rt_bgm_prepare"), 4);
            Remember("rt_bgm_prepare", 92);
            AssertBar(Start("rt_bgm_prepare"), 10);
        }

        [Test]
        public void ShortRoleRemakeDoesNotLoseItsFirstFourBars()
        {
            Remember("rt_bgm_prepare", 10);
            Assert.That(Start("rt_bgm_betting", _shortClip), Is.Zero);
            double shortBar = (double)_shortClip.samples / 44100 / 8;
            Invoke("RememberReturningThemePosition", "rt_bgm_betting", _shortClip, 2 * shortBar);
            Assert.That(Start("rt_bgm_betting", _shortClip),
                Is.EqualTo(Math.Round(2 * shortBar * 44100)).Within(1));
        }

        [Test]
        public void ChangedClipLengthInvalidatesOldBookmark()
        {
            Remember("rt_bgm_betting", 24);
            Assert.That(Start("rt_bgm_betting", _shortClip), Is.Zero);
        }

        [Test]
        public void OtherSetsKeepTheirOriginalStartPolicy()
        {
            Remember("p4_bgm_prepare", 10);
            Assert.That(Start("p4_bgm_prepare"), Is.Zero);
            Assert.That(Start("rt_bgm_prepare"), Is.Zero);
        }

        [Test]
        public void UnknownTrackIsNotAssumedToHaveAnIntro()
        {
            Remember("rt_bgm_prepare", 10);
            Assert.That(Start("rt_unknown"), Is.Zero);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1.0)]
        public void InvalidPositionDoesNotConsumeIntro(double position)
        {
            Invoke("RememberReturningThemePosition", "rt_bgm_prepare", _longClip, position);
            Assert.That(Start("rt_bgm_prepare"), Is.Zero);
        }

        [Test]
        public void ExplicitStopClearsTheMatchBookmarks()
        {
            Remember("rt_bgm_prepare", 10);
            Invoke("StopProposal3");
            Assert.That(Start("rt_bgm_prepare"), Is.Zero);
            Assert.That(Start("rt_bgm_ron"), Is.Zero);
        }

        [Test]
        public void RonToSettlementWithTheSameTrackDoesNotScheduleARestart()
        {
            _type.GetField("UseReturningThemeBgm").SetValue(_audio, true);
            _type.GetField("_p3ClipName", Private).SetValue(_audio, "rt_bgm_ron");
            var phaseField = _type.GetField("currentBgmPhase", Private);
            phaseField.SetValue(_audio, Enum.Parse(phaseField.FieldType, "Liquidation"));
            var routine = (IEnumerator)Invoke("P3ChangeClip");
            Assert.That(routine.MoveNext(), Is.False);
        }

        private void SetTestVolumes(float bgm, float master = 1f, float sink = 1f)
        {
            _type.GetField("bgmVolume").SetValue(_audio, bgm);
            _type.GetField("masterVolume").SetValue(_audio, master);
            _type.GetField("_bgmSink", Private).SetValue(_audio, sink);
        }

        [TestCase("rt_bgm_prepare")]
        [TestCase("rt_bgm_betting")]
        [TestCase("rt_bgm_tension")]
        [TestCase("rt_bgm_field_2")]
        [TestCase("rt_bgm_draw_base")]
        public void ReturningThemePlaybackVolumeIsTwentyPercentHigher(string clip)
        {
            SetTestVolumes(0.5f);
            Assert.That((float)Invoke("ProposalMasterFor", clip), Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That((float)_type.GetField("bgmVolume").GetValue(_audio), Is.EqualTo(0.5f));
        }

        [TestCase("p4_bgm_field_2")]
        [TestCase("p3_bgm_prepare")]
        [TestCase(null)]
        public void OtherSetsDoNotReceiveReturningThemeGain(string clip)
        {
            _type.GetField("UseReturningThemeBgm").SetValue(_audio, true);
            SetTestVolumes(0.5f);
            Assert.That((float)Invoke("ProposalMasterFor", new object[] { clip }), Is.EqualTo(0.5f));
        }

        [Test]
        public void ReturningThemePlaybackVolumeRespectsAudioSourceCeiling()
        {
            SetTestVolumes(1f);
            Assert.That((float)Invoke("ProposalMasterFor", "rt_bgm_field_4"), Is.EqualTo(1f));
        }

        [Test]
        public void ReturningThemePlaybackStillRespectsMuteMasterAndDuck()
        {
            SetTestVolumes(0f);
            Assert.That((float)Invoke("ProposalMasterFor", "rt_bgm_field_2"), Is.Zero);
            SetTestVolumes(0.5f, 0.8f, 0.4f);
            Assert.That((float)Invoke("ProposalMasterFor", "rt_bgm_field_2"), Is.EqualTo(0.192f).Within(0.00001f));
        }

        [Test]
        public void ActiveDrawDeckAndMotifReceiveTheSameBoost()
        {
            var deck = _host.AddComponent<AudioSource>();
            var motif = _host.AddComponent<AudioSource>();
            _type.GetField("_p3Decks", Private).SetValue(_audio, new[] { deck, motif });
            _type.GetField("_p3Active", Private).SetValue(_audio, 0);
            _type.GetField("_p3ClipName", Private).SetValue(_audio, "rt_bgm_draw_base");
            _type.GetField("_p3Running", Private).SetValue(_audio, true);
            _type.GetField("_p4Motif", Private).SetValue(_audio, motif);
            _type.GetField("_p4MotifDeck", Private).SetValue(_audio, deck);
            _type.GetField("_p4MotifAlive", Private).SetValue(_audio, true);
            SetTestVolumes(0.717f);
            Invoke("ApplyProposal3Volumes");
            Assert.That(deck.volume, Is.EqualTo(0.8604f).Within(0.00001f));
            Assert.That(motif.volume, Is.EqualTo(deck.volume));
        }
    }
}
