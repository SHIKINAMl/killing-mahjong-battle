using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("HandRoleConditions")]
    public class HandRoleConditionsTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private GameObject root;
        private Component roles;
        private static Type T(string name) => Type.GetType("KillingMahjong.UI." + name + ", Assembly-CSharp", true);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
        private static object Get(object target, string name) => target.GetType().GetField(name, F).GetValue(target);
        private static object Prop(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        private static string Text(Transform target)
        {
            var text = target.GetComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));
            return (string)Prop(text, "text");
        }
        private static readonly Color Bg = new Color32(37, 27, 35, 238);
        private static string[] Four => new[] { "平和", "清一色", "一盃口", "立直" };
        private CanvasGroup[] Groups => ((IEnumerable)Get(roles, "groups")).Cast<CanvasGroup>().ToArray();

        [SetUp]
        public void Setup()
        {
            T("UIFrameSelection").GetMethod("Reset").Invoke(null, null);
            root = new GameObject("RoleConditionTests", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            roles = (Component)T("HandRoleConditionsUI").GetMethod("Ensure").Invoke(null, new object[] { root.transform });
            Call(roles, "SetRoles", Four, Bg);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root);
            T("UIFrameSelection").GetMethod("Reset").Invoke(null, null);
            yield return null;
        }
        [Test]
        public void StartsEmptyThenRevealsOneRolePerStagger()
        {
            Call(roles, "SetHovered", true); Assert.That(Groups.All(g => g.alpha == 0), Is.True);
            Call(roles, "RenderElapsed", .06f); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(1));
            Call(roles, "RenderElapsed", .16f); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(2));
            Call(roles, "RenderElapsed", .26f); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(3));
            Call(roles, "RenderElapsed", .5f); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(4));
        }
        [Test]
        public void ExitClearsEveryCardInTheSameCall()
        {
            Call(roles, "SetHovered", true); Call(roles, "RenderElapsed", .5f);
            Call(roles, "SetHovered", false);
            Assert.That(Prop(roles, "VisibleCardCount"), Is.Zero);
            Assert.That(roles.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(Groups.All(g => g.alpha == 0), Is.True);
        }
        [Test]
        public void InterruptedReplayAlwaysRestartsAtTheFirstRole()
        {
            Call(roles, "SetHovered", true); Call(roles, "RenderElapsed", .16f);
            Call(roles, "SetHovered", false); Call(roles, "SetHovered", true);
            Assert.That(Groups.All(g => g.alpha == 0), Is.True);
            Call(roles, "RenderElapsed", .06f); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(1));
        }
        [Test]
        public void StayingHoveredDoesNotRestartAnimation()
        {
            Call(roles, "SetHovered", true); Call(roles, "RenderElapsed", .5f);
            Call(roles, "SetHovered", true); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(4));
        }
        [Test]
        public void ChangedWaitReplacesTheRolesAndClosesOldCards()
        {
            Call(roles, "SetHovered", true); Call(roles, "RenderElapsed", .5f);
            Call(roles, "SetRoles", new[] { "立直" }, Bg);
            Assert.That(Prop(roles, "CardCount"), Is.EqualTo(1)); Assert.That(Prop(roles, "VisibleCardCount"), Is.Zero);
            Assert.That(Prop(roles, "IsHovered"), Is.False);
        }
        [Test]
        public void BoostedNamesAndDuplicateDoraUseCanonicalDescriptions()
        {
            Call(roles, "SetRoles", new[] { "断么九+1", "ドラ", "ドラ" }, Bg);
            Assert.That(Prop(roles, "CardCount"), Is.EqualTo(2));
            var names = (System.Collections.Generic.List<string>)Get(roles, "names");
            Assert.That(names[0], Is.EqualTo("断幺九+1")); Assert.That(names[1], Is.EqualTo("ドラ×2"));
            Assert.That(Text(roles.transform.Find("RoleCard0/Condition")), Is.EqualTo("1・9・字牌を一枚も使わない"));
        }
        [Test]
        public void NullOrEmptyRolesNeverCreateAFakeRole()
        {
            Call(roles, "SetRoles", null, Bg); Call(roles, "SetHovered", true);
            Assert.That(Prop(roles, "CardCount"), Is.Zero); Assert.That(Prop(roles, "IsHovered"), Is.False);
        }
        [Test]
        public void RepeatedDataReusesThePoolAndDoesNotRestart()
        {
            int count = roles.transform.childCount;
            Call(roles, "SetHovered", true); Call(roles, "RenderElapsed", .5f);
            Call(roles, "SetRoles", Four, Bg);
            Assert.That(roles.transform.childCount, Is.EqualTo(count)); Assert.That(Prop(roles, "VisibleCardCount"), Is.EqualTo(4));
        }
        [Test]
        public void HidingTheOwnerStopsThePresentation()
        {
            Call(roles, "SetHovered", true); root.SetActive(false); Call(roles, "SetHovered", true);
            Assert.That(Prop(roles, "IsHovered"), Is.False); Assert.That(roles.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }
        [Test]
        public void CardGraphicsNeverInterceptClicks()
        {
            Assert.That(roles.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            foreach (var group in Groups) Assert.That(group.blocksRaycasts, Is.False);
            foreach (var g in roles.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) Assert.That(g.raycastTarget, Is.False);
        }
        [Test]
        public void BackgroundCenterHasOneTranslucentQuadAndNoOpaqueUnderlay()
        {
            var graphic = roles.transform.Find("RoleCard0").GetComponent(T("Effects.PixelShapeGraphic"));
            var quads = (IEnumerable)Get(graphic, "_quads"); int covering = 0;
            foreach (object quad in quads)
            {
                var a = (Vector2)Get(quad, "A"); var c = (Vector2)Get(quad, "C");
                if (220 > a.x && 220 < c.x && -17 > a.y && -17 < c.y)
                { covering++; Assert.That(((Color32)Get(quad, "Color")).a, Is.EqualTo(238)); }
            }
            Assert.That(covering, Is.EqualTo(1));
        }
        [Test]
        public void FrameIdIsRegisteredAndCanFollowAPlayerOverride()
        {
            Assert.That(T("UIFrameCatalog").GetMethod("Contains").Invoke(null, new object[] { "ui.frame.role_condition_slant" }), Is.True);
            Assert.That(Prop(roles, "FrameId"), Is.EqualTo("ui.frame.role_condition_slant"));
            var style = ScriptableObject.CreateInstance(T("UIFrameStyle"));
            string id = "tests.role_conditions_theme_" + Guid.NewGuid().ToString("N");
            style.GetType().GetField("id", F).SetValue(style, id);
            T("UIFrameCatalog").GetMethod("Register").Invoke(null, new object[] { style });
            T("UIFrameSelection").GetMethod("SetForPlayer").Invoke(null, new object[] { "local", id });
            Assert.That(Prop(roles, "FrameId"), Is.EqualTo(id));
            UnityEngine.Object.DestroyImmediate(style);
        }
        [Test]
        public void EightRolesStayInsideTheCanvasAndKeepTheBottomFixed()
        {
            Call(roles, "SetRoles", new[] { "立直", "断么九", "平和", "一盃口", "東", "西", "ドラ", "赤ドラ" }, Bg);
            var rect = (RectTransform)roles.transform;
            Assert.That(-rect.anchoredPosition.y + rect.sizeDelta.y, Is.EqualTo(418));
            Assert.That(-rect.anchoredPosition.y, Is.GreaterThanOrEqualTo(0));
            Assert.That(Prop(roles, "CardCount"), Is.EqualTo(8));
        }
    }
}
