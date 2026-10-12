using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("HandWaitReveal")]
    public class HandWaitRevealTests
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root, boxObject;
        private Component manager, selection, board, reveal, box;
        private Array waits;
        private static Type T(string name) => Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        private static object Get(object obj, string name) => obj.GetType().GetField(name, F).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, F).SetValue(obj, value);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
        private static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            Assert.That(T("Managers.BoardStateManager").GetProperty("Instance").GetValue(null) == null, Is.True);
            root = new GameObject("HandWaitRevealTest", typeof(RectTransform)); root.SetActive(false);
            manager = root.AddComponent(T("UI.GameUIManager")); ((Behaviour)manager).enabled = false;
            board = root.AddComponent(T("Managers.BoardStateManager"));
            selection = root.AddComponent(T("UI.GameUIHandSelectionController"));
            Set(manager, "currentPhaseStatus", Enum.Parse(T("EngineData.RoundStatus"), "HandSelection"));
            Call(selection, "Setup", manager);
            reveal = (Component)Get(selection, "_waitReveal");
            root.SetActive(true);
            var prefab = Resources.Load<GameObject>("Presentation/EffectPreviewRig");
            var rig = prefab.GetComponent(T("UI.EffectPreviewRig"));
            var tiles = (UnityEngine.Object)Get(rig, "tiles");
            Set(manager, "tileResourceManager", tiles);
            boxObject = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Presentation/HandConfirmationWaitBox"), root.transform);
            box = boxObject.GetComponent(T("UI.WaitUI"));
            Call(box, "BindTileResources", tiles);
            Set(manager, "waitUI", box);
            waits = Array.CreateInstance(T("EngineData.WaitData"), 5);
            for (int i = 0; i < 5; i++)
            {
                var w = Activator.CreateInstance(T("EngineData.WaitData"));
                Set(w, "tile", i + 1); Set(w, "han", 8); Set(w, "mangan_or_more", true);
                Set(w, "multiplier", 2f); Set(w, "multiplier_label", "倍満"); Set(w, "yaku", new[] { "清一色", "一盃口", "立直" });
                waits.SetValue(w, i);
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            T("UI.Effects.EffectQueue").GetMethod("ClearIfAny").Invoke(null, null);
            yield return null;
        }

        private void Show() => Call(reveal, "Show", waits, Get(manager, "tileResourceManager"), false);
        private object Data(Array data)
        {
            var obj = Activator.CreateInstance(T("EngineData.IsTenpaiData")); Set(obj, "waits", data); return obj;
        }
        private void EnqueueProbe(List<int> indexes, int version, bool confirmation)
        {
            var type = selection.GetType().GetNestedType("TenpaiProbe", BindingFlags.NonPublic);
            var probe = Activator.CreateInstance(type); Set(probe, "Indexes", indexes); Set(probe, "Version", version);
            Set(probe, "Generation", 0); Set(probe, "Confirmation", confirmation);
            Call(Get(selection, "tenpaiProbes"), "Enqueue", probe);
        }

        [UnityTest]
        public IEnumerator EveryArrivalRevealsItsTileInTheSameFrame()
        {
            Show();
            var job = ((MonoBehaviour)selection).StartCoroutine((IEnumerator)Call(reveal, "Transfer", box));
            int observed = 0;
            float deadline = Time.unscaledTime + 8;
            while ((bool)Prop(reveal, "IsTransferring") && Time.unscaledTime < deadline)
            {
                int count = (int)Prop(reveal, "ArrivalCount");
                if (count != observed)
                {
                    Assert.That(Prop(box, "ArrivalRevealFrame"), Is.EqualTo(Prop(reveal, "LastArrivalFrame")));
                    observed = count;
                }
                yield return null;
            }
            yield return job;
            Assert.That(Prop(reveal, "ArrivalCount"), Is.EqualTo(5));
            Assert.That(Prop(box, "IsArrivalInProgress"), Is.False);
            Assert.That(reveal.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator RebuildDoesNotShowTilesBeforeTheirArrival()
        {
            Show();
            var job = ((MonoBehaviour)selection).StartCoroutine((IEnumerator)Call(reveal, "Transfer", box));
            yield return new WaitForSeconds(.2f);
            Call(box, "DisplayWaits", new List<int> { 1, 2, 3, 4, 5 });
            var tile = (Component)Call(box, "GetArrivalTileRect", 0);
            var cg = tile.GetComponent<CanvasGroup>();
            Assert.That(cg.alpha, Is.Zero);
            yield return job;
        }

        [UnityTest]
        public IEnumerator CancelDuringFlightClearsParticlesAndIncompleteBox()
        {
            Show();
            var job = ((MonoBehaviour)selection).StartCoroutine((IEnumerator)Call(reveal, "Transfer", box));
            yield return new WaitForSeconds(.3f);
            Call(reveal, "Hide");
            yield return job;
            Assert.That(Prop(reveal, "IsTransferring"), Is.False);
            Assert.That(Prop(box, "IsArrivalInProgress"), Is.False);
            Assert.That(boxObject.activeSelf, Is.False);
            Assert.That(reveal.transform.Find("WaitFlightPixels") == null, Is.True);
            Show();
            yield return ((MonoBehaviour)selection).StartCoroutine((IEnumerator)Call(reveal, "Transfer", box));
            Assert.That(Prop(reveal, "ArrivalCount"), Is.EqualTo(5));
        }

        [Test]
        public void LateCancelledConfirmationResponseIsDiscarded()
        {
            EnqueueProbe(new List<int> { 1 }, 0, true);
            Set(selection, "submissionVersion", 1); Set(selection, "_confirming", true);
            Set(selection, "_pendingHandIndexes", new List<int> { 1 });
            Assert.That(Call(selection, "HandleTenpaiProbe", Data(waits)), Is.True);
            Assert.That(Get(selection, "_confirmationResolved"), Is.False);
            Assert.That(Get(selection, "_hasRankResult"), Is.False);
        }

        [Test]
        public void ChangedHandPreviewDoesNotResolveCurrentConfirmation()
        {
            EnqueueProbe(new List<int> { 1 }, 0, false);
            Set(selection, "_confirming", true); Set(selection, "_pendingHandIndexes", new List<int> { 2 });
            Call(selection, "HandleTenpaiProbe", Data(waits));
            Assert.That(Get(selection, "_confirmationResolved"), Is.False);
        }

        [Test]
        public void OutstandingPreviewForSameHandCanResolveConfirmationWithoutASecondRequest()
        {
            EnqueueProbe(new List<int> { 1 }, 0, false);
            Set(selection, "submissionVersion", 1); Set(selection, "_confirming", true);
            Set(selection, "_pendingHandIndexes", new List<int> { 1 });
            Call(selection, "HandleTenpaiProbe", Data(waits));
            Assert.That(Get(selection, "_confirmationResolved"), Is.True);
            Assert.That(((ICollection)Get(selection, "tenpaiProbes")).Count, Is.Zero);
        }

        [Test]
        public void CancelInvalidatesPendingCallbacksAndCandidateUI()
        {
            Show();
            int before = (int)Get(selection, "submissionVersion");
            Call(selection, "CancelPendingConfirmation");
            Assert.That(Get(selection, "submissionVersion"), Is.EqualTo(before + 1));
            Assert.That(Prop(selection, "IsConfirming"), Is.False);
            Assert.That(reveal.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void SingleWaitUsesTheSameRendererWithoutPaging()
        {
            Call(box, "PrepareArrival", new List<int> { 1 }, new List<int>());
            Call(box, "PrepareArrivalPage", 0);
            var tile = (Component)Call(box, "GetArrivalTileRect", 0);
            Assert.That(tile.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Call(box, "RevealArrivalTile", 0); Call(box, "FinishArrival");
            Assert.That(tile.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(Prop(box, "IsArrivalInProgress"), Is.False);
            Assert.That(Call(box, "GetArrivalTileRect", 1), Is.Null);
        }

        [Test]
        public void SkillChangedPreviewCannotResolveCurrentConfirmation()
        {
            var type = selection.GetType().GetNestedType("TenpaiProbe", BindingFlags.NonPublic);
            var probe = Activator.CreateInstance(type);
            Set(probe, "Indexes", new List<int> { 1 }); Set(probe, "Signature", "old-hand-or-boost");
            Call(Get(selection, "tenpaiProbes"), "Enqueue", probe);
            Set(selection, "_confirming", true); Set(selection, "_pendingHandIndexes", new List<int> { 1 });
            Call(selection, "HandleTenpaiProbe", Data(waits));
            Assert.That(Get(selection, "_confirmationResolved"), Is.False);
        }

        [Test]
        public void RepeatedDecideDoesNotStartASecondConfirmation()
        {
            var hand = (List<int>)Prop(board, "CurrentHandTiles");
            var wall = (List<int>)Prop(board, "OriginalWallTiles");
            var indexes = (List<int>)Prop(board, "LocalHandIndexes");
            for (int i = 0; i < 13; i++) { hand.Add(i); wall.Add(i); indexes.Add(i); }
            Set(selection, "_hasRankResult", true); Set(selection, "_rankResultIndexes", new List<int>(indexes));
            Set(selection, "_rankSignature", Call(selection, "CurrentSelectionSignature"));
            Set(selection, "_rankWaits", Array.CreateInstance(T("EngineData.WaitData"), 0));
            Call(selection, "CompleteHandSelection");
            int version = (int)Get(selection, "submissionVersion");
            Assert.That(Prop(selection, "IsConfirming"), Is.True);
            Call(selection, "CompleteHandSelection");
            Assert.That(Get(selection, "submissionVersion"), Is.EqualTo(version));
            Call(selection, "CancelPendingConfirmation");
        }

        [Test]
        public void InactiveTutorialPhaseIsEnabledForConfirmationAndRestoredOnCancel()
        {
            var rig = Resources.Load<GameObject>("Presentation/EffectPreviewRig").GetComponent(T("UI.EffectPreviewRig"));
            var original = (Component)Get(rig, "phase");
            var phaseObject = UnityEngine.Object.Instantiate(original.gameObject, root.transform);
            var phase = phaseObject.GetComponent(T("UI.PhaseTransitionUI"));
            phaseObject.SetActive(false); Set(manager, "phaseTransitionUI", phase);
            Call(selection, "BeginImmediateConfirmation");
            Assert.That(phaseObject.activeSelf, Is.True);
            Assert.That(Prop(selection, "IsConfirming"), Is.True);
            Call(selection, "CancelPendingConfirmation");
            Assert.That(phaseObject.activeSelf, Is.False);
            Assert.That(Prop(selection, "IsConfirming"), Is.False);
        }

        [UnityTest]
        public IEnumerator PreviewCanReopenBeforeThePreviousSceneFinishesUnloading()
        {
            var type = T("UI.EffectPreviewPlayer");
            int scenes = UnityEngine.SceneManagement.SceneManager.sceneCount;
            type.GetMethod("Open").Invoke(null, new object[] { "hand.wait_transfer", false, null });
            var second = type.GetMethod("Open").Invoke(null, new object[] { "hand.wait_reveal", false, null });
            Assert.That(Prop(second, "EffectId"), Is.EqualTo("hand.wait_reveal"));
            yield return null;
            Call(second, "Close");
            float deadline = Time.unscaledTime + 3;
            while (UnityEngine.SceneManagement.SceneManager.sceneCount > scenes && Time.unscaledTime < deadline) yield return null;
            Assert.That(UnityEngine.SceneManagement.SceneManager.sceneCount, Is.EqualTo(scenes));
            Assert.That(type.GetProperty("Current").GetValue(null), Is.Null);
        }
    }
}
