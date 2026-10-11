using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("AbilityBellSound")]
    public class AbilityBellSoundTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root, listener;
        private Component audio, ability, button;
        private object pointer;
        private bool ownsAudio, originalEnabled, originalBackground;
        private float originalMaster, originalSe;
        private static Type Main(string name) => Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        private static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
        private AudioSource Bell => (AudioSource)Field(audio, "abilityBellSource");
        private bool IsOpen => (bool)ability.GetType().GetProperty("IsWindowOpen").GetValue(ability);
        private void Click() => Call(button, "OnPointerClick", pointer);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
                listener = new GameObject("AbilityBellListener", typeof(AudioListener));
            var audioType = Main("Managers.AudioManager");
            audio = (Component)audioType.GetProperty("Instance").GetValue(null);
            ownsAudio = audio == null;
            if (ownsAudio) audio = new GameObject("AbilityBellAudio").AddComponent(audioType);
            yield return null;
            originalEnabled = (bool)Field(audio, "AudioEnabled");
            originalMaster = (float)Field(audio, "masterVolume");
            originalSe = (float)Field(audio, "seVolume");
            Set(audio, "AudioEnabled", true);
            Set(audio, "masterVolume", .8f);
            Set(audio, "seVolume", .5f);
            Call(audio, "StopAbilityBellSE");

            root = new GameObject("AbilityBellUI", typeof(RectTransform));
            ability = root.AddComponent(Main("UI.AbilityUI"));
            ((Behaviour)ability).enabled = false;
            var window = new GameObject("Window", typeof(RectTransform));
            window.transform.SetParent(root.transform, false);
            window.SetActive(false);
            Set(ability, "abilityWindow", window.GetComponent<RectTransform>());
            var trigger = new GameObject("BellButton", typeof(RectTransform));
            trigger.transform.SetParent(root.transform, false);
            button = trigger.AddComponent(Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true));
            Set(ability, "triggerButton", button);
            var eventType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", true);
            var events = new GameObject("AbilityBellEvents");
            events.transform.SetParent(root.transform);
            var eventSystem = events.AddComponent(eventType);
            ((Behaviour)eventSystem).enabled = false;
            pointer = Activator.CreateInstance(Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI", true), new object[] { eventSystem });
            Call(ability, "Start"); // 本編と同じonClickの購読を使う。
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var previewType = Main("UI.EffectPreviewPlayer");
            var preview = previewType.GetProperty("Current").GetValue(null);
            if (preview != null) Call(preview, "Close");
            if (audio != null)
            {
                Call(audio, "StopAbilityBellSE");
                Set(audio, "AudioEnabled", originalEnabled);
                Set(audio, "masterVolume", originalMaster);
                Set(audio, "seVolume", originalSe);
                if (ownsAudio) UnityEngine.Object.Destroy(audio.gameObject);
            }
            if (root != null) UnityEngine.Object.Destroy(root);
            if (listener != null) UnityEngine.Object.Destroy(listener);
            Application.runInBackground = originalBackground;
            yield return null;
        }

        [Test]
        public void ApprovedClipKeepsOriginalLengthAndPeak()
        {
            var clip = Resources.Load<AudioClip>("Stingers/se_ability_bell");
            Assert.That(clip, Is.Not.Null);
            // WebGL向け取り込みでは44.1kHzに変換される場合もある。時間・音量・端点を確認する。
            Assert.That(clip.frequency, Is.EqualTo(44100).Or.EqualTo(48000));
            Assert.That(clip.length, Is.EqualTo(2.25f).Within(.0001f));
            Assert.That(clip.samples, Is.EqualTo(Mathf.RoundToInt(clip.frequency * 2.25f)));
            Assert.That(clip.channels, Is.EqualTo(1));
            Assert.That(clip.LoadAudioData(), Is.True);
            var data = new float[clip.samples];
            Assert.That(clip.GetData(data, 0), Is.True);
            float peak = 0;
            foreach (float x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
            Assert.That(peak, Is.InRange(.78f, .82f), "Resampling may move the sample peak slightly; do not normalize or clip the bell.");
            Assert.That(data[0], Is.EqualTo(0f).Within(.00001f));
            Assert.That(data[data.Length - 1], Is.EqualTo(0f).Within(.00001f));
        }

        [UnityTest]
        public IEnumerator ActualButtonOpensWithBellAndClosingDoesNotRestartIt()
        {
            Click();
            Assert.That(IsOpen, Is.True);
            Assert.That(Bell, Is.Not.Null);
            Assert.That(Bell.clip.name, Is.EqualTo("se_ability_bell"));
            Assert.That(Bell.isPlaying, Is.True);
            Assert.That(Bell.pitch, Is.EqualTo(1f));
            Assert.That(Bell.volume, Is.EqualTo(.4f).Within(.001f));
            yield return new WaitForSecondsRealtime(.15f);
            int before = Bell.timeSamples;
            Assert.That(before, Is.GreaterThan(0));
            Click();
            Assert.That(IsOpen, Is.False);
            Assert.That(Bell.timeSamples, Is.GreaterThanOrEqualTo(before));
            yield return new WaitForSecondsRealtime(.15f);
            Click();
            Assert.That(IsOpen, Is.True);
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Length, Is.GreaterThan(0));
            Assert.That(Bell.timeSamples, Is.LessThan(before));
        }

        [Test]
        public void AutomaticOpeningDisabledButtonAndMuteDoNotRing()
        {
            Call(ability, "OpenWindow");
            Assert.That(IsOpen, Is.True);
            Assert.That(Bell == null || !Bell.isPlaying, Is.True);
            Call(ability, "CloseWindow", true);
            button.GetType().GetProperty("interactable").SetValue(button, false);
            Click();
            Assert.That(IsOpen, Is.False);
            Assert.That(Bell == null || !Bell.isPlaying, Is.True);
            button.GetType().GetProperty("interactable").SetValue(button, true);
            Set(audio, "AudioEnabled", false);
            Click();
            Assert.That(IsOpen, Is.True);
            Assert.That(Bell == null || !Bell.isPlaying, Is.True);
        }

        [Test]
        public void BellStopDoesNotStopOrChangeOtherAudioSources()
        {
            var bgm = (AudioSource)Field(audio, "bgmSource");
            var se = (AudioSource)Field(audio, "seSource");
            bool bgmPlaying = bgm.isPlaying, sePlaying = se.isPlaying;
            var bgmClip = bgm.clip;
            float bgmVolume = bgm.volume, seVolume = se.volume;
            Click();
            Call(audio, "StopAbilityBellSE");
            Assert.That(Bell.isPlaying, Is.False);
            Assert.That(bgm.isPlaying, Is.EqualTo(bgmPlaying));
            Assert.That(se.isPlaying, Is.EqualTo(sePlaying));
            Assert.That(bgm.clip, Is.SameAs(bgmClip));
            Assert.That(bgm.volume, Is.EqualTo(bgmVolume));
            Assert.That(se.volume, Is.EqualTo(seVolume));
        }

        [UnityTest]
        public IEnumerator CatalogPreviewCompletesRepeatsAndStopsWithoutBoardChanges()
        {
            var boardType = Main("Managers.BoardStateManager");
            Assert.That(boardType.GetProperty("Instance").GetValue(null) == null, Is.True);
            var type = Main("UI.EffectPreviewPlayer");
            var player = type.GetMethod("Open").Invoke(null, new object[] { "ui.ability_bell", true, null });
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((int)type.GetProperty("CompletedPlays").GetValue(player) < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That((int)type.GetProperty("CompletedPlays").GetValue(player), Is.GreaterThanOrEqualTo(2));
            Call(player, "Replay");
            deadline = Time.realtimeSinceStartup + 5f;
            while ((Bell == null || !Bell.isPlaying) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Bell.isPlaying, Is.True);
            Call(player, "Close");
            Assert.That(Bell.isPlaying, Is.False);
            Assert.That(boardType.GetProperty("Instance").GetValue(null) == null, Is.True);
            yield return null;
            Assert.That(type.GetProperty("Current").GetValue(null) == null, Is.True);
        }
    }
}
