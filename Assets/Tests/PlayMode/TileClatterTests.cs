using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("TileClatter")]
    public class TileClatterTests
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root;
        private Component effect;
        private float timeScale;
        private static Type T => Type.GetType("KillingMahjong.UI.Effects.TileClatterEffect, Assembly-CSharp", true);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, F).Invoke(target, args);
        private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private Component Attach() => (Component)T.GetMethod("Attach").Invoke(null, new object[] { (RectTransform)root.transform, 112f });
        private void Hide() => T.GetMethod("Hide").Invoke(null, new object[] { (RectTransform)root.transform });
        private RectTransform[] Tiles => effect.GetComponentsInChildren<RectTransform>().Where(r => r.name.StartsWith("Tile") && r != effect.transform).ToArray();
        private Vector2[] Positions => Tiles.Select(r => r.anchoredPosition).ToArray();
        private void Step(float dt, Vector2? pointer) => Call(effect, "PreviewStep", dt, pointer);
        private string SceneName => (string)Prop(effect, "PhysicsSceneName");
        private static int SceneCount => Enumerable.Range(0, SceneManager.sceneCount).Count(i => SceneManager.GetSceneAt(i).name.StartsWith("TileClatterPhysics_"));
        [SetUp]
        public void Setup()
        {
            timeScale = Time.timeScale;
            T.GetProperty("Art").SetValue(null, Enum.Parse(T.GetNestedType("TileArt"), "Discard"));
            root = new GameObject("TileClatterTests", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 600);
            effect = Attach(); Step(1f / 60, null);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root); Time.timeScale = timeScale;
            T.GetProperty("Art").SetValue(null, Enum.Parse(T.GetNestedType("TileArt"), "Discard"));
            yield return null; yield return null;
        }
        [Test]
        public void UsesExistingLaidDownArtAndRegisteredId()
        {
            Assert.That((int)Prop(effect, "TileCount"), Is.EqualTo(28));
            foreach (var tile in Tiles)
            {
                var image = tile.GetComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI"));
                Assert.That(((Sprite)Prop(image, "sprite")).name, Does.Contain("自身打"));
                Assert.That((bool)Prop(image, "raycastTarget"), Is.False);
            }
            var catalog = Type.GetType("KillingMahjong.UI.GameEffectCatalog, Assembly-CSharp", true);
            Assert.That(catalog.GetMethod("Find").Invoke(null, new object[] { "tile.clatter" }), Is.Not.Null);
        }
        [Test]
        public void RepeatedAttachReusesImagesAndPhysics()
        {
            var scene = SceneName; Assert.That(Attach(), Is.SameAs(effect)); Assert.That(SceneName, Is.EqualTo(scene));
            Assert.That(effect.transform.childCount, Is.EqualTo(28)); Assert.That(SceneCount, Is.EqualTo(1));
        }
        [Test]
        public void StartsWithoutOverlappingOrAutonomousMovement()
        {
            var start = Positions;
            for (int i = 0; i < 120; i++) Step(1f / 60, new Vector2(-340, 270));
            Assert.That((float)Prop(effect, "OpaqueOverlapPixels"), Is.LessThan(.05f));
            Assert.That(Positions.Select((p, i) => Vector2.Distance(p, start[i])).Max(), Is.LessThan(.001f));
        }
        [Test]
        public void FirstPointerInsideATileDoesNotEjectIt()
        {
            var start = Positions; var pointer = start[0] + new Vector2(0, 112);
            for (int i = 0; i < 60; i++) Step(1f / 60, pointer);
            Assert.That(Positions.Select((p, i) => Vector2.Distance(p, start[i])).Max(), Is.LessThan(.001f));
        }
        [Test]
        public void ActualSweepMovesAndRotatesWithoutOverlap()
        {
            var start = Positions; var angles = Tiles.Select(r => r.localEulerAngles.z).ToArray();
            Step(1f / 60, new Vector2(-310, 112)); float overlap = 0;
            for (int i = 0; i < 180; i++)
            { Step(1f / 60, new Vector2(Mathf.Lerp(-310, 310, i / 179f), 112)); overlap = Mathf.Max(overlap, (float)Prop(effect, "OpaqueOverlapPixels")); }
            Assert.That(overlap, Is.LessThan(.05f));
            Assert.That(Positions.Select((p, i) => Vector2.Distance(p, start[i])).Max(), Is.GreaterThan(10));
            Assert.That(Tiles.Select((r, i) => Mathf.Abs(Mathf.DeltaAngle(angles[i], r.localEulerAngles.z))).Max(), Is.GreaterThan(5));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ReversingHandDoesNotOverlapTiles(int fps)
        {
            Step(1f / fps, new Vector2(-310, 100)); float overlap = 0;
            for (int i = 0; i < fps * 5; i++)
            { float t = i / (float)fps; Step(1f / fps, new Vector2(Mathf.Sin(t * 4) * 280, 100 + Mathf.Sin(t * 2) * 48)); overlap = Mathf.Max(overlap, (float)Prop(effect, "OpaqueOverlapPixels")); }
            Assert.That(overlap, Is.LessThan(.05f));
        }
        [Test]
        public void SlidesThenSettlesInsteadOfReturningHome()
        {
            var start = Positions; Step(1f / 60, new Vector2(-310, 112));
            for (int i = 0; i < 120; i++) Step(1f / 60, new Vector2(Mathf.Lerp(-310, 290, i / 119f), 112));
            for (int i = 0; i < 240; i++) Step(1f / 60, null);
            var settled = Positions; for (int i = 0; i < 120; i++) Step(1f / 60, null);
            Assert.That(Positions.Select((p, i) => Vector2.Distance(p, settled[i])).Max(), Is.LessThan(.001f));
            Assert.That(settled.Select((p, i) => Vector2.Distance(p, start[i])).Max(), Is.GreaterThan(10));
        }
        [UnityTest]
        public IEnumerator HideDisposesSceneAndReattachKeepsPose()
        {
            var before = Positions; string oldScene = SceneName; Hide();
            Assert.That((bool)Prop(effect, "HasPhysics"), Is.False); Assert.That(effect.gameObject.activeSelf, Is.False);
            yield return null; yield return null;
            Assert.That(SceneManager.GetSceneByName(oldScene).IsValid(), Is.False);
            Assert.That(Attach(), Is.SameAs(effect)); Assert.That(SceneName, Is.Not.EqualTo(oldScene));
            Assert.That(Positions.Select((p, i) => Vector2.Distance(p, before[i])).Max(), Is.LessThan(.001f));
            Assert.That(SceneCount, Is.EqualTo(1));
        }
        [Test]
        public void HiddenStepCannotRestartPhysics()
        {
            Hide(); Step(1f / 60, new Vector2(0, 112));
            Assert.That((bool)Prop(effect, "HasPhysics"), Is.False);
        }
        [Test]
        public void ContinuesAtZeroGameTimeScale()
        {
            Time.timeScale = 0; var start = Positions; Step(1f / 60, new Vector2(-310, 112));
            for (int i = 0; i < 120; i++) Step(1f / 60, new Vector2(Mathf.Lerp(-310, 310, i / 119f), 112));
            Assert.That(Positions.Select((p, i) => Vector2.Distance(p, start[i])).Max(), Is.GreaterThan(10));
            Assert.That(Time.timeScale, Is.Zero);
        }
        [Test]
        public void PreservesGlobalPhysicsSettingsAndRandomSequence()
        {
            var gravity = Physics2D.gravity; int iterations = Physics2D.positionIterations;
            var mode = Physics2D.simulationMode;
            Hide(); var saved = UnityEngine.Random.state; float expected = UnityEngine.Random.value;
            UnityEngine.Random.state = saved; Attach(); Assert.That(UnityEngine.Random.value, Is.EqualTo(expected));
            Assert.That(Physics2D.gravity, Is.EqualTo(gravity)); Assert.That(Physics2D.positionIterations, Is.EqualTo(iterations)); Assert.That(Physics2D.simulationMode, Is.EqualTo(mode));
        }
        [UnityTest]
        public IEnumerator DestroyDoesNotLeaveSceneOrMaterial()
        {
            string name = SceneName; UnityEngine.Object.Destroy(root); root = null;
            yield return null; yield return null;
            Assert.That(SceneManager.GetSceneByName(name).IsValid(), Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<PhysicsMaterial2D>().Any(m => m.name == "TileClatterFelt"), Is.False);
        }
        [Test]
        public void DealingCleanupPreventsALatePile()
        {
            var phaseType = Type.GetType("KillingMahjong.UI.PhaseTransitionUI, Assembly-CSharp", true);
            var phase = root.AddComponent(phaseType);
            Call(phase, "PrepareRoundStartWait"); Call(phase, "MarkRoundStartCleanupDone");
            Call(phase, "ShowRoundStartWaitTiles");
            Assert.That(effect.gameObject.activeSelf, Is.False); Assert.That((bool)Prop(effect, "HasPhysics"), Is.False);
            Assert.That((bool)Prop(phase, "IsRoundStartCleanupDone"), Is.True);
        }
    }
}
