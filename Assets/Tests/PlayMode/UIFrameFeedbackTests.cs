using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("UIFrameFeedback")]
    public class UIFrameFeedbackTests
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private GameObject root, target;
        private Component feedback, button;
        private object pointer;
        private RectTransform rect;
        private Vector3 position, scale;
        private Vector2 size;
        private ScriptableObject custom;
        private static Type Main(string name) => Type.GetType("KillingMahjong.UI." + name + ", Assembly-CSharp", true);
        private static Type Ui(string name) => Type.GetType("UnityEngine." + name + ", UnityEngine.UI", true);
        private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Members).Invoke(obj, args);
        private static object Static(string type, string name, params object[] args) => Main(type).GetMethod(name, Members).Invoke(null, args);
        private bool State(string name) => (bool)feedback.GetType().GetProperty(name).GetValue(feedback);
        private void Point(Vector2 value) => pointer.GetType().GetProperty("position").SetValue(pointer, value);
        private void Geometry()
        {
            Assert.That(rect.localPosition, Is.EqualTo(position));
            Assert.That(rect.localScale, Is.EqualTo(scale));
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
        }

        [SetUp]
        public void SetUp()
        {
            Static("UIFrameSelection", "Reset");
            root = new GameObject("FrameTestRoot", typeof(RectTransform), typeof(Canvas));
            target = new GameObject("FrameTestButton", typeof(RectTransform));
            target.transform.SetParent(root.transform, false);
            rect = (RectTransform)target.transform; rect.sizeDelta = new Vector2(120, 40);
            rect.localScale = new Vector3(.8f, .8f, 1);
            target.AddComponent(Ui("UI.Image")); button = target.AddComponent(Ui("UI.Button"));
            feedback = (Component)Static("UIInteractionFeedback", "Attach", target);
            pointer = Activator.CreateInstance(Ui("EventSystems.PointerEventData"), new object[] {null});
            Point(Vector2.zero);
            position = rect.localPosition; scale = rect.localScale; size = rect.sizeDelta;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(root);
            Static("UIFrameSelection", "Reset");
            Main("UIFrameCatalog").GetMethod("Reset", Members).Invoke(null, null);
            if (custom != null) UnityEngine.Object.Destroy(custom);
            yield return null;
        }

        [Test]
        public void HoverPressExitOutsideReleaseDoesNotLeaveReactionOrMoveInput()
        {
            Call(feedback, "OnPointerEnter", pointer); Assert.That(State("IsHovered"), Is.True);
            Call(feedback, "OnPointerDown", pointer); Assert.That(State("IsPressed"), Is.True);
            Geometry(); Call(feedback, "OnPointerExit", pointer); Point(new Vector2(999, 999));
            Call(feedback, "OnPointerUp", pointer);
            Assert.That(State("IsHovered"), Is.False); Assert.That(State("IsPressed"), Is.False); Geometry();
        }

        [Test]
        public void ReleaseOutsideClearsHoverEvenIfExitWasNotDelivered()
        {
            Call(feedback, "OnPointerEnter", pointer); Call(feedback, "OnPointerDown", pointer);
            Point(new Vector2(999, 999)); Call(feedback, "OnPointerUp", pointer);
            Assert.That(State("IsHovered"), Is.False); Assert.That(State("IsPressed"), Is.False); Geometry();
        }

        [Test]
        public void SelectionRemainsWhenHoverLeavesButIsClearedWhenRecycled()
        {
            Call(feedback, "SetSelected", true); Call(feedback, "OnPointerEnter", pointer);
            Call(feedback, "OnPointerExit", pointer);
            Assert.That(State("IsSelected"), Is.True); Assert.That(State("IsHovered"), Is.False);
            target.SetActive(false); target.SetActive(true);
            Assert.That(State("IsSelected"), Is.False); Assert.That(State("IsPressed"), Is.False); Geometry();
        }

        [Test]
        public void BecomingNonInteractableClearsTransientReaction()
        {
            Call(feedback, "OnPointerEnter", pointer); Call(feedback, "OnPointerDown", pointer);
            button.GetType().GetProperty("interactable").SetValue(button, false); Call(feedback, "Refresh");
            Assert.That(State("IsHovered"), Is.False); Assert.That(State("IsPressed"), Is.False); Geometry();
        }

        [Test]
        public void ParentCanvasGroupBlocksHoverAndPress()
        {
            var group = root.AddComponent<CanvasGroup>(); group.interactable = false;
            Call(feedback, "OnCanvasGroupChanged"); Call(feedback, "OnPointerEnter", pointer);
            Call(feedback, "OnPointerDown", pointer);
            Assert.That(State("IsHovered"), Is.False); Assert.That(State("IsPressed"), Is.False); Geometry();
        }

        [Test]
        public void FrameGraphicsDoNotCaptureRaycasts()
        {
            foreach (var name in new[] {"InteractionFrame", "HoverFrameArt", "SelectedFrameArt"})
            {
                var child = target.transform.Find(name);
                Assert.That(child, Is.Not.Null);
                var graphic = child.GetComponent(Ui("UI.Graphic"));
                Assert.That((bool)graphic.GetType().GetProperty("raycastTarget").GetValue(graphic), Is.False);
            }
            Geometry();
        }

        [Test]
        public void UnknownFrameIdFallsBackToStableDefault()
        {
            Call(feedback, "SetFrameId", "not-installed");
            Assert.That(feedback.GetType().GetProperty("FrameId").GetValue(feedback), Is.EqualTo("ui.frame.pixel_standard"));
            Geometry();
        }

        [Test]
        public void PlayersCanEquipDifferentIdsWithoutChangingStateOrGeometry()
        {
            custom = ScriptableObject.CreateInstance(Main("UIFrameStyle"));
            Main("UIFrameStyle").GetField("id", Members).SetValue(custom, "ui.frame.test_other");
            Assert.That(Static("UIFrameCatalog", "Register", custom), Is.True);
            Assert.That(Static("UIFrameSelection", "SetForPlayer", "player-A", "ui.frame.test_other"), Is.True);
            Assert.That(Static("UIFrameSelection", "GetForPlayer", "player-B"), Is.EqualTo("ui.frame.pixel_standard"));
            var context = root.AddComponent(Main("UIFrameContext")); Call(context, "SetPlayer", "player-A");
            Call(feedback, "SetSelected", true);
            Assert.That(feedback.GetType().GetProperty("FrameId").GetValue(feedback), Is.EqualTo("ui.frame.test_other"));
            Assert.That(State("IsSelected"), Is.True); Geometry();
            Call(context, "SetPlayer", "player-B");
            Assert.That(feedback.GetType().GetProperty("FrameId").GetValue(feedback), Is.EqualTo("ui.frame.pixel_standard"));
            Assert.That(State("IsSelected"), Is.True); Geometry();
        }

        [Test]
        public void DefaultIdCannotBeOverwrittenByDifferentContent()
        {
            custom = ScriptableObject.CreateInstance(Main("UIFrameStyle"));
            Assert.That(Static("UIFrameCatalog", "Contains", "ui.frame.pixel_standard"), Is.True);
            Assert.That(Static("UIFrameCatalog", "Register", custom), Is.False);
        }

        [Test]
        public void RightClickDoesNotBecomeLeftPress()
        {
            Call(feedback, "OnPointerEnter", pointer);
            var property = pointer.GetType().GetProperty("button");
            property.SetValue(pointer, Enum.Parse(property.PropertyType, "Right"));
            Call(feedback, "OnPointerDown", pointer);
            Assert.That(State("IsPressed"), Is.False); Geometry();
        }

        [Test]
        public void LeavingDiscardClearsSelectionWithoutMovingInput()
        {
            var wall = root.AddComponent(Main("WallUI"));
            var slots = (IList)Call(wall, "GetWallSlots"); slots.Add(rect);
            Call(feedback, "SetSelected", true);
            Call(wall, "UpdateDiscardTurnIndicator", false, true);
            Assert.That(State("IsSelected"), Is.False); Geometry();
        }
    }
}
