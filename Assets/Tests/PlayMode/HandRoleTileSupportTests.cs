using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KillingMahjong.Tests
{
    [Category("HandRoleTileSupport")]
    public class HandRoleTileSupportTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject root;
        private Image[] images;
        private Image candidate;
        private Array targets;
        private object wait, presenter;
        private static Type T(string name) => Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
        private static object Get(object obj, string name) => obj.GetType().GetField(name, F).GetValue(obj);
        private static void Set(object obj, string name, object value) => obj.GetType().GetField(name, F).SetValue(obj, value);
        private static object Prop(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private int Active => (int)Prop(presenter, "ActiveTileCount");
        private Component Glow(Image image) => image.GetComponentInChildren(T("UI.Effects.TileRoleGlow"), true);
        private object Target(Image image, int index, int id) => Activator.CreateInstance(T("UI.RoleTileTarget"), new object[] { image, index, id });
        private Image Image(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(root.transform, false);
            var image = go.GetComponent<Image>(); image.rectTransform.sizeDelta = new Vector2(45, 40);
            return image;
        }
        [SetUp]
        public void Setup()
        {
            root = new GameObject("RoleTileSupportTests", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var ids = (int[])T("UI.RoleTileSupportPreviewData").GetProperty("Hand").GetValue(null);
            images = new Image[13]; targets = Array.CreateInstance(T("UI.RoleTileTarget"), 13);
            for (int i = 0; i < 13; i++) { images[i] = Image("Tile" + i); images[i].rectTransform.anchoredPosition = new Vector2(i * 25 - 150, -235); targets.SetValue(Target(images[i], i, ids[i]), i); }
            candidate = Image("Candidate");
            wait = T("UI.RoleTileSupportPreviewData").GetMethod("CreateWait").Invoke(null, null);
            presenter = Activator.CreateInstance(T("UI.HandRoleTileSupport"));
            Call(presenter, "Bind", wait, targets, candidate);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Call(presenter, "Clear"); UnityEngine.Object.Destroy(root); yield return null;
        }
        private void Hover(string role) => Call(presenter, "SetHoveredRole", role, targets);
        private bool Resolve(string role, out int[] indexes, out bool includeWait)
        {
            var args = new[] { wait, role, targets, null, (object)false };
            bool found = (bool)T("UI.HandRoleSupportData").GetMethod("TryResolve").Invoke(null, args);
            indexes = ((IEnumerable<int>)args[3]).OrderBy(i => i).ToArray(); includeWait = (bool)args[4]; return found;
        }
        [Test]
        public void RepeatedTileTypesUseTheActualWallIndexes()
        {
            Assert.That(Resolve("一盃口", out int[] indexes, out bool withWait), Is.True);
            Assert.That(indexes, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 })); Assert.That(withWait, Is.False);
            Hover("一盃口"); Assert.That(Active, Is.EqualTo(6)); Assert.That(Glow(images[6]) == null, Is.True);
        }
        [Test]
        public void LegacyPayloadHasNoFabricatedSupport()
        {
            var legacy = JsonUtility.FromJson("{\"tile\":8,\"han\":6,\"yaku\":[\"一盃口\"]}", T("EngineData.WaitData"));
            Assert.That(Get(legacy, "source_wall_indexes"), Is.Null); Assert.That(Get(legacy, "yaku_supports"), Is.Null);
            Call(presenter, "Bind", legacy, targets, candidate); Hover("一盃口");
            Assert.That(Active, Is.Zero); Assert.That(root.GetComponentsInChildren(T("UI.Effects.TileRoleGlow"), true).Length, Is.Zero);
        }
        [Test]
        public void OptionalSupportSurvivesJsonDeserialization()
        {
            var parsed = JsonUtility.FromJson(JsonUtility.ToJson(wait), T("EngineData.WaitData"));
            Assert.That(((int[])Get(parsed, "source_wall_indexes")).Length, Is.EqualTo(13));
            Assert.That(((Array)Get(parsed, "yaku_supports")).Length, Is.EqualTo(4));
            Call(presenter, "Bind", parsed, targets, candidate); Hover("東"); Assert.That(Active, Is.EqualTo(3));
        }
        [Test]
        public void RoleSwitchClearsOldTilesInTheSameCall()
        {
            Hover("一盃口"); Hover("東"); Assert.That(Active, Is.EqualTo(3));
            foreach (var image in images.Take(6)) Assert.That(Prop(Glow(image), "Alpha"), Is.EqualTo(0f));
            Hover(null); Assert.That(Active, Is.Zero);
        }
        [Test]
        public void WholeHandUsesTheExplicitWaitFlagAndContextUsesNoTiles()
        {
            Hover("混一色"); Assert.That(Active, Is.EqualTo(14)); Assert.That(Prop(Glow(candidate), "IsHighlighted"), Is.True);
            Hover("立直"); Assert.That(Active, Is.Zero);
        }
        [UnityTest]
        public IEnumerator StayingOnTheSameRoleDoesNotResetTheFade()
        {
            Hover("一盃口"); yield return new WaitForSecondsRealtime(.12f);
            Hover("一盃口"); Call(Glow(images[0]), "RenderAt", Time.unscaledTime);
            Assert.That((float)Prop(Glow(images[0]), "Alpha"), Is.GreaterThan(.95f));
        }
        [Test]
        public void GlowKeepsTheSourcePositionScaleColorAndDoesNotDrawAFrame()
        {
            var positions = images.Select(i => i.rectTransform.anchoredPosition).ToArray();
            var colors = images.Select(i => i.color).ToArray(); Hover("混一色");
            for (int i = 0; i < images.Length; i++)
            {
                var glow = Glow(images[i]); Call(glow, "RenderAt", Time.unscaledTime + 1);
                Assert.That(images[i].rectTransform.anchoredPosition, Is.EqualTo(positions[i]));
                Assert.That(images[i].rectTransform.localScale, Is.EqualTo(Vector3.one)); Assert.That(images[i].color, Is.EqualTo(colors[i]));
                Assert.That(glow.GetComponentsInChildren(T("UI.Effects.PixelShapeGraphic")).Length, Is.Zero);
                Assert.That(glow.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
                Assert.That(glow.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                var light = glow.transform.Find("SurfaceLight").GetComponent<Image>();
                Assert.That(light.material.shader.name, Is.EqualTo("KillingMahjong/NeonKeyAdd"));
            }
        }
        [Test]
        public void WrongOrDuplicatedSelectionSnapshotCannotLightAnything()
        {
            Set(wait, "source_wall_indexes", Enumerable.Repeat(0, 13).ToArray()); Hover("一盃口"); Assert.That(Active, Is.Zero);
            Set(wait, "source_wall_indexes", Enumerable.Range(1, 13).ToArray()); Hover("一盃口"); Assert.That(Active, Is.Zero);
        }
        [Test]
        public void InvalidGroupDoesNotLeakAPartialWitness()
        {
            var support = ((Array)Get(wait, "yaku_supports")).GetValue(0);
            var groups = (Array)Get(support, "groups"); Set(groups.GetValue(1), "wall_indexes", new[] { 99 });
            Assert.That(Resolve("一盃口", out var indexes, out var withWait), Is.False);
            Assert.That(indexes, Is.Empty); Assert.That(withWait, Is.False); Hover("一盃口"); Assert.That(Active, Is.Zero);
        }
        [Test]
        public void AmbiguousDuplicateRoleEntriesAndUnreportedRolesAreRejected()
        {
            var supports = (Array)Get(wait, "yaku_supports"); var duplicate = Array.CreateInstance(T("EngineData.YakuSupportData"), 2);
            duplicate.SetValue(supports.GetValue(0), 0); duplicate.SetValue(supports.GetValue(0), 1);
            Set(wait, "yaku_supports", duplicate); Hover("一盃口"); Assert.That(Active, Is.Zero);
            Hover("存在しない役"); Assert.That(Active, Is.Zero);
        }
        [Test]
        public void ChangedTileValueAtTheSameWallIndexInvalidatesTheBoundData()
        {
            Hover("一盃口"); targets.SetValue(Target(images[0], 0, 30), 0); Hover("一盃口"); Assert.That(Active, Is.Zero);
        }
        [Test]
        public void TwoIndexesCannotAliasOneImage()
        {
            targets.SetValue(Target(images[0], 1, 1), 1); Hover("一盃口"); Assert.That(Active, Is.Zero);
        }
        [Test]
        public void BoostAndFontAliasesOnlyNormalizeTheNameNotTheTiles()
        {
            Set(wait, "yaku", new[] { "断幺九+1" });
            var support = ((Array)Get(wait, "yaku_supports")).GetValue(0); Set(support, "name", "断么九");
            Hover("断幺九+1"); Assert.That(Active, Is.EqualTo(6));
        }
        [UnityTest]
        public IEnumerator RebuiltImagesWithTheSameSnapshotReceiveTheSameSupport()
        {
            Hover("一盃口"); UnityEngine.Object.Destroy(images[0].gameObject); yield return null;
            images[0] = Image("Rebuilt0"); targets.SetValue(Target(images[0], 0, 1), 0);
            Hover("一盃口"); Assert.That(Active, Is.EqualTo(6)); Assert.That(Glow(images[0]) != null, Is.True);
        }
        [UnityTest]
        public IEnumerator ClearingReleasesGlowObjectsAndMaterials()
        {
            Hover("混一色"); var material = Glow(images[0]).transform.Find("SurfaceLight").GetComponent<Image>().material;
            Call(presenter, "Clear"); Assert.That(Active, Is.Zero); yield return null;
            Assert.That(material == null, Is.True); Assert.That(root.GetComponentsInChildren(T("UI.Effects.TileRoleGlow"), true).Length, Is.Zero);
        }
        private Component Reveal()
        {
            var reveal = (Component)T("UI.HandWaitRevealUI").GetMethod("Ensure").Invoke(null, new object[] { root.transform });
            ((Behaviour)reveal).enabled = false; Call(reveal, "BindRoleTileTargets", targets);
            var rig = Resources.Load<GameObject>("Presentation/EffectPreviewRig").GetComponent(T("UI.EffectPreviewRig"));
            var waits = Array.CreateInstance(T("EngineData.WaitData"), 1); waits.SetValue(wait, 0);
            Call(reveal, "Show", waits, Get(rig, "tiles"), false); Canvas.ForceUpdateCanvases(); return reveal;
        }
        private static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        private static Vector2 FirstLeftRole(Component reveal)
        {
            var text = reveal.transform.Find("StrengthPanel/Yaku").GetComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));
            text.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) }).Invoke(text, new object[] { true, false });
            return Center((RectTransform)text.transform) + new Vector2(-50, 32);
        }
        [Test]
        public void LeftAndSlantedCardHoverUseTheSameImplementationAndExitImmediately()
        {
            var reveal = Reveal(); Call(reveal, "RefreshHoverVisibility", FirstLeftRole(reveal));
            Assert.That(Prop(reveal, "ActiveRoleTileCount"), Is.EqualTo(6));
            var roles = (Component)Prop(reveal, "RoleConditions"); Call(roles, "RenderElapsed", .5f);
            Call(reveal, "RefreshHoverVisibility", Center((RectTransform)roles.transform.Find("RoleCard1")));
            Assert.That(Prop(reveal, "ActiveRoleTileCount"), Is.EqualTo(3)); Assert.That(Prop(roles, "IsHovered"), Is.True);
            Call(reveal, "RefreshHoverVisibility", new Vector2(-100000, -100000));
            Assert.That(Prop(reveal, "ActiveRoleTileCount"), Is.Zero); Assert.That(Prop(roles, "IsHovered"), Is.False);
        }
        [Test]
        public void ForcedConfirmationAndHideClearTheGlow()
        {
            var reveal = Reveal(); Call(reveal, "RefreshHoverVisibility", FirstLeftRole(reveal));
            Assert.That(Prop(reveal, "ActiveRoleTileCount"), Is.EqualTo(6));
            Call(reveal, "SetForcedVisible", true); Assert.That(Prop(reveal, "ActiveRoleTileCount"), Is.Zero);
            Call(reveal, "SetForcedVisible", false); Call(reveal, "RefreshHoverVisibility", FirstLeftRole(reveal));
            Call(reveal, "Hide"); Assert.That(Prop(reveal, "ActiveRoleTileCount"), Is.Zero);
        }
    }
}
