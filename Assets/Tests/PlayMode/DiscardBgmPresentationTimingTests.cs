using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("DiscardBgmPresentationTiming")]
    public class DiscardBgmPresentationTimingTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject host, phaseHost, boardHost, audioHost, listenerHost;
        private Component ui, controller, phase, audio, turnText;
        private RectTransform line;
        private bool wasRunningInBackground;

        private static Type MainType(string name)
        {
            return Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        }

        private static object Call(object target, string method, params object[] args)
        {
            return target.GetType().GetMethod(method, Members).Invoke(target, args);
        }

        private static object Field(object target, string name)
        {
            return target.GetType().GetField(name, Members).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, Members).SetValue(target, value);
        }

        private bool Held { get { return (bool)Field(ui, "holdDiscardBgmOpeningForBattleStart"); } }
        private string AudioPhase { get { return Field(audio, "currentBgmPhase").ToString(); } }
        private string Clip { get { return (string)audio.GetType().GetProperty("Proposal3ClipName").GetValue(audio); } }

        private void SetPhase(string name)
        {
            var method = ui.GetType().GetMethod("SetCurrentPhaseStatus");
            method.Invoke(ui, new[] { Enum.Parse(method.GetParameters()[0].ParameterType, name) });
        }

        private void Trigger()
        {
            var info = Activator.CreateInstance(MainType("EngineData.BettingCompletedInfo"));
            Call(controller, "TriggerBettingAnimationPhase", "Timing test", info);
        }

        private IEnumerator WaitForClip(string expected)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var decks = (AudioSource[])Field(audio, "_p3Decks");
                int active = (int)Field(audio, "_p3Active");
                bool switching = (bool)audio.GetType().GetProperty("IsProposal3Switching").GetValue(audio);
                if (Clip == expected && !switching && decks != null && decks[active].isPlaying) yield break;
                yield return null;
            }
            Assert.Fail("Timed out waiting for active playback of " + expected + "; actual=" + Clip);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            wasRunningInBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            {
                listenerHost = new GameObject("TimingTestListener");
                listenerHost.AddComponent<AudioListener>();
            }
            yield return null;
            var audioType = MainType("Managers.AudioManager");
            audio = (Component)audioType.GetProperty("Instance").GetValue(null);
            if (audio == null)
            {
                audioHost = new GameObject("TimingTestAudio");
                audio = audioHost.AddComponent(audioType);
            }
            var boardType = MainType("Managers.BoardStateManager");
            if ((Component)boardType.GetProperty("Instance").GetValue(null) == null)
            {
                boardHost = new GameObject("TimingTestBoard");
                boardHost.AddComponent(boardType);
            }
            Call(audio, "StopBGM");
            // 保存設定・音量・実際の対局状態は変更しない。
            Call(audio, "ApplyMatchBgmSet", 5);
            Call(audio, "SetBgmIntensity", 2);

            host = new GameObject("TimingTestUI");
            host.SetActive(false);
            ui = host.AddComponent(MainType("UI.GameUIManager"));
            controller = host.AddComponent(MainType("UI.GameUIPhaseController"));
            ((Behaviour)ui).enabled = false; // 本編Start・通信購読は実行しない。
            ((Behaviour)controller).enabled = false;
            Call(controller, "Setup", ui);
            host.SetActive(true);

            phaseHost = new GameObject("TimingTestPresentation", typeof(RectTransform));
            phase = phaseHost.AddComponent(MainType("UI.PhaseTransitionUI"));
            ((Behaviour)phase).enabled = false;
            var lineHost = new GameObject("TimingTestBand", typeof(RectTransform));
            lineHost.transform.SetParent(phaseHost.transform);
            line = (RectTransform)lineHost.transform;
            var textHost = new GameObject("TimingTestTurn", typeof(RectTransform));
            textHost.transform.SetParent(phaseHost.transform);
            turnText = textHost.AddComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));
            SetField(phase, "horizontalLineRt", line);
            SetField(phase, "centerText", turnText);
            SetField(phase, "lineInDuration", 0.1f);
            SetField(phase, "textWaitDuration", 0.3f);
            SetField(phase, "checkerFadeDuration", 0.1f);
            SetField(ui, "phaseTransitionUI", phase);
            SetPhase("TurnDecision");
            yield return WaitForClip("rt_bgm_tension");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (controller != null) Call(controller, "CancelPhasePresentations");
            if (audio != null) Call(audio, "StopBGM");
            if (phaseHost != null) UnityEngine.Object.Destroy(phaseHost);
            if (host != null) UnityEngine.Object.Destroy(host);
            if (boardHost != null) UnityEngine.Object.Destroy(boardHost);
            if (audioHost != null) UnityEngine.Object.Destroy(audioHost);
            if (listenerHost != null) UnityEngine.Object.Destroy(listenerHost);
            Application.runInBackground = wasRunningInBackground;
            yield return null;
        }

        private IEnumerator AssertPresentationThenDiscard()
        {
            bool sawTurnText = false;
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Held && Time.realtimeSinceStartup < deadline)
            {
                string text = (string)turnText.GetType().GetProperty("text").GetValue(turnText);
                if (turnText.gameObject.activeSelf && (text == "先攻" || text == "後攻")) sawTurnText = true;
                Assert.That(AudioPhase, Is.EqualTo("TurnDecision"), "Do not request Discard before the presentation completes.");
                Assert.That(Clip, Is.EqualTo("rt_bgm_tension"));
                yield return null;
            }
            Assert.That(Held, Is.False, "The presentation must release its BGM hold.");
            Assert.That(sawTurnText, Is.True);
            Assert.That(turnText.gameObject.activeSelf, Is.False);
            Assert.That(line.gameObject.activeSelf, Is.False, "The exiting band must also be gone.");
            Assert.That(AudioPhase, Is.EqualTo("Discard"));
            yield return WaitForClip("rt_bgm_field_2");
            TestContext.Out.WriteLine("Turn text and band hidden before rt_bgm_field_2 was requested.");
        }

        [UnityTest]
        public IEnumerator EarlyDiscardWaitsThroughTurnTextAndBandExit()
        {
            Trigger();
            SetPhase("Discard"); // 演出中に先行して届くサーバーフェイズを再現。
            yield return AssertPresentationThenDiscard();
        }

        [UnityTest]
        public IEnumerator QueuedPresentationAlsoHoldsAnOlderDeferredBgmAction()
        {
            var lease = (IDisposable)Call(ui, "BeginTransition", "timing-test");
            Trigger();
            SetPhase("Discard");
            // 先に登録されたBGM保留の実行を再現。待機中の演出を追い越さない。
            Call(ui, "ApplyBgmFilterForCurrentPhase");
            Assert.That(Held, Is.True);
            Assert.That(AudioPhase, Is.EqualTo("TurnDecision"));
            lease.Dispose();
            Call(controller, "PlayBettingTransition", Field(controller, "activeBettingTransition"));
            yield return AssertPresentationThenDiscard();
        }

        [UnityTest]
        public IEnumerator NewerDrawPhaseIsNotReplacedByStaleDiscardOnCompletion()
        {
            Trigger();
            SetPhase("Discard");
            SetPhase("Draw");
            Assert.That(AudioPhase, Is.EqualTo("Draw"));
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Held && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Held, Is.False);
            Assert.That(AudioPhase, Is.EqualTo("Draw"));
            yield return WaitForClip("rt_bgm_draw_base");
        }

        [UnityTest]
        public IEnumerator CancellationClearsHoldWithoutStartingDiscard()
        {
            Trigger();
            SetPhase("Discard");
            var stale = Field(controller, "activeBettingTransition");
            Call(controller, "CancelPhasePresentations");
            Assert.That(Held, Is.False);
            Assert.That(AudioPhase, Is.EqualTo("TurnDecision"));
            Call(controller, "CompleteBettingTransition", stale);
            Assert.That(AudioPhase, Is.EqualTo("TurnDecision"));
            SetPhase("Discard");
            Assert.That(AudioPhase, Is.EqualTo("Discard"), "A new entry must not remain held after cancellation.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletionWaitsForAnotherActiveTransition()
        {
            Call(ui, "HoldDiscardBgmOpeningForBattleStart");
            SetPhase("Discard");
            var lease = (IDisposable)Call(ui, "BeginTransition", "timing-test-other");
            Call(ui, "ReleaseDiscardBgmOpeningForBattleStart");
            Assert.That(Held, Is.False);
            Assert.That(AudioPhase, Is.EqualTo("TurnDecision"));
            lease.Dispose();
            // Updateを無効化した試験用UIなので、登録された実際の保留を手動で流す。
            foreach (var entry in (IEnumerable)Field(ui, "deferredActions"))
            {
                var type = entry.GetType();
                if ((string)type.GetProperty("Key").GetValue(entry) == "bgmFilter")
                    ((Action)type.GetProperty("Value").GetValue(entry))();
            }
            Assert.That(AudioPhase, Is.EqualTo("Discard"));
            yield return WaitForClip("rt_bgm_field_2");
        }

        [Test]
        public void TutorialDoesNotAcquireMainGameBgmHold()
        {
            ui.GetType().GetProperty("IsTutorialMode").SetValue(ui, true);
            Call(ui, "HoldDiscardBgmOpeningForBattleStart");
            SetPhase("Discard");
            Assert.That(Held, Is.False);
            Assert.That(AudioPhase, Is.EqualTo("TurnDecision"));
        }
    }
}
