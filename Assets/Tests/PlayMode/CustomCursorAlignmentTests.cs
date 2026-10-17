using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("CustomCursorAlignment")]
    public class CustomCursorAlignmentTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private GameObject owner;
        private Component cursor;
        private Type cursorType;
        private RectTransform rect;
        private bool originallyVisible;

        private object Get(string name) => cursorType.GetField(name, Members).GetValue(cursor);
        private void Set(string name, object value) => cursorType.GetField(name, Members).SetValue(cursor, value);
        private void Apply(Vector2 position, bool pressed) =>
            cursorType.GetMethod("ApplyPointerState", Members).Invoke(cursor, new object[] { position, pressed });

        [SetUp]
        public void SetUp()
        {
            originallyVisible = Cursor.visible;
            cursorType = Type.GetType("CustomCursor, Assembly-CSharp", true);
            owner = new GameObject("CustomCursorAlignmentTest");
            owner.SetActive(false);
            cursor = owner.AddComponent(cursorType);
            ((Behaviour)cursor).enabled = false;
            var texture = Resources.Load<Texture2D>("マウスカーソル");
            Assert.That(texture, Is.Not.Null);
            Assert.That(new Vector2(texture.width, texture.height), Is.EqualTo(new Vector2(128, 128)));
            Set("handCursor", texture);
            Set("clickRotationAngle", 8f);
            cursorType.GetMethod("CreateUICursor", Members).Invoke(cursor, null);
            rect = (RectTransform)Get("cursorRect");
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (cursor != null)
            {
                cursorType.GetMethod("ResetCursor", Members).Invoke(cursor, null);
                var image = rect != null ? rect.GetComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true)) : null;
                var sprite = image != null ? image.GetType().GetProperty("sprite").GetValue(image) as Sprite : null;
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
            }
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            Cursor.visible = originallyVisible;
        }

        private void AssertTip(Vector2 pointer)
        {
            // Local coordinates of pixel (26,24), measured from the image's top left.
            var pixel01 = new Vector2(26f / 128f, 1f - 24f / 128f);
            var localTip = Vector2.Scale(pixel01 - rect.pivot, rect.rect.size);
            var screenTip = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(localTip));
            Assert.That(Vector2.Distance(screenTip, pointer), Is.LessThan(0.001f));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(64f, 64f)), "Do not resize the approved image.");
        }

        [Test]
        public void DefaultHotspotIsTheApprovedFingerTip()
        {
            Assert.That((Vector2)Get("hotspot"), Is.EqualTo(new Vector2(26, 24)));
            Assert.That(rect.pivot.x, Is.EqualTo(26f / 128f).Within(0.00001f));
            Assert.That(rect.pivot.y, Is.EqualTo(1f - 24f / 128f).Within(0.00001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IdleAndPressedTipMatchesPointer(bool pressed)
        {
            var pointer = new Vector2(400, 300);
            Apply(pointer, pressed);
            AssertTip(pointer);
            Assert.That(Mathf.DeltaAngle(rect.localEulerAngles.z, pressed ? 8f : 0f), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void RepeatedPressReleaseAndMoveDoesNotAccumulateOffset()
        {
            for (int i = 0; i < 40; i++)
            {
                var pointer = new Vector2(90 + i * 12, 150 + i * 5);
                Apply(pointer, true);
                AssertTip(pointer);
                Apply(pointer, false);
                AssertTip(pointer);
            }
        }

        [TestCase(-12f)]
        [TestCase(0f)]
        [TestCase(8f)]
        public void PreservesConfiguredTiltWithoutMovingTip(float angle)
        {
            Set("clickRotationAngle", angle);
            Apply(new Vector2(700, 520), true);
            AssertTip(new Vector2(700, 520));
            Assert.That(Mathf.DeltaAngle(rect.localEulerAngles.z, angle), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void CursorGraphicDoesNotBlockUiRaycasts()
        {
            var canvas = (GameObject)Get("cursorCanvasObj");
            var raycaster = (Behaviour)canvas.GetComponent(Type.GetType("UnityEngine.UI.GraphicRaycaster, UnityEngine.UI", true));
            var image = rect.GetComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true));
            Assert.That(raycaster.enabled, Is.False);
            Assert.That((bool)image.GetType().GetProperty("raycastTarget").GetValue(image), Is.False);
        }

        [UnityTest]
        public IEnumerator HoverFeedbackRespectsDisabledButtonsAndFrontmostBlockers()
        {
            var eventType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", true);
            var canvasType = typeof(Canvas);
            var imageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
            var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
            var raycasterType = Type.GetType("UnityEngine.UI.GraphicRaycaster, UnityEngine.UI", true);
            var eventOwner = new GameObject("CursorFeedbackTestEvents", eventType);
            var panel = new GameObject("CursorFeedbackTestCanvas", typeof(RectTransform), canvasType, raycasterType);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var target = new GameObject("TestClickable", typeof(RectTransform), imageType, buttonType);
            target.transform.SetParent(panel.transform, false);
            var targetRect = target.GetComponent<RectTransform>();
            targetRect.sizeDelta = new Vector2(180, 80);
            var button = target.GetComponent(buttonType);
            var interactable = buttonType.GetProperty("interactable");
            var method = cursorType.GetMethod("IsClickableAt", Members);
            try
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
                Assert.That((bool)method.Invoke(cursor, new object[] { center }), Is.True);
                interactable.SetValue(button, false);
                Assert.That((bool)method.Invoke(cursor, new object[] { center }), Is.False, "Disabled controls must not glow.");
                interactable.SetValue(button, true);
                var blocker = new GameObject("TestBlockingPanel", typeof(RectTransform), imageType);
                blocker.transform.SetParent(panel.transform, false);
                blocker.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That((bool)method.Invoke(cursor, new object[] { center }), Is.False, "Do not glow through a foreground panel.");
            }
            finally
            {
                UnityEngine.Object.Destroy(panel);
                UnityEngine.Object.Destroy(eventOwner);
            }
        }
    }
}
