using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("HandActionButtonState")]
    public class HandActionButtonStateTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root;
        private Component manager, hand, board;
        private Type handType, managerType, buttonType;

        private static Type MainType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static object Get(Component component, string field) => component.GetType().GetField(field, Members).GetValue(component);
        private static void Set(Component component, string field, object value) => component.GetType().GetField(field, Members).SetValue(component, value);
        private static object Call(Component component, string method, params object[] args) =>
            component.GetType().GetMethod(method, Members).Invoke(component, args);

        private object Phase(string name) => Enum.Parse(MainType("KillingMahjong.EngineData.RoundStatus"), name);
        private void Layout(string phase)
        {
            Set(manager, "currentPhaseStatus", Phase(phase));
            Call(hand, "UpdateLayout", Phase(phase));
        }
        private Component Button(string field) => (Component)Get(hand, field);
        private bool CanPress(string field) => (bool)buttonType.GetMethod("IsInteractable").Invoke(Button(field), null);
        private void SetPressable(string field, bool value) => buttonType.GetProperty("interactable").SetValue(Button(field), value);

        private GameObject Child(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            return go;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            managerType = MainType("KillingMahjong.UI.GameUIManager");
            handType = MainType("KillingMahjong.UI.HandUI");
            buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
            var boardType = MainType("KillingMahjong.Managers.BoardStateManager");
            Assert.That(boardType.GetProperty("Instance").GetValue(null) == null, Is.True,
                "Use an isolated test scene without a live match; do not overwrite its board.");

            root = new GameObject("HandActionButtonStateTest", typeof(RectTransform));
            root.SetActive(false);
            manager = root.AddComponent(managerType);
            ((Behaviour)manager).enabled = false; // ネット接続や対局全体の初期化は行わない。
            board = root.AddComponent(boardType);
            hand = Child("Hand").AddComponent(handType);
            ((Behaviour)hand).enabled = false;
            Set(hand, "handSlotContainer", Child("SelectionTiles").transform);
            Set(hand, "discardPhaseContainer", Child("DiscardTiles").transform);
            Set(hand, "cursor", Child("Cursor").transform);
            Set(hand, "decideButton", Child("Decide").AddComponent(buttonType));
            Set(hand, "autoManganButton", Child("Auto").AddComponent(buttonType));
            Set(manager, "handUI", hand);
            Call(hand, "Setup", manager);
            root.SetActive(true);

            // 初期化順がManager→Handになった場合を再現。13枚未満なら決定だけが無効。
            Layout("HandSelection");
            Assert.That(CanPress("decideButton"), Is.False);
            Call(hand, "Start");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [Test]
        public void ClonedButtonsDoNotInheritTheDisabledDecideState()
        {
            Assert.That(CanPress("reselectButton"), Is.True);
            Assert.That(CanPress("peekButton"), Is.True);
        }

        [Test]
        public void DecideStillRequiresThirteenTiles()
        {
            Layout("HandSelection");
            Assert.That(CanPress("decideButton"), Is.False);
            var tiles = (List<int>)board.GetType().GetProperty("CurrentHandTiles").GetValue(board);
            for (int i = 0; i < 13; i++) tiles.Add(i);
            Layout("HandSelection");
            Assert.That(CanPress("decideButton"), Is.True);
            tiles.RemoveAt(0);
            Layout("HandSelection");
            Assert.That(CanPress("decideButton"), Is.False);
        }

        [Test]
        public void ReselectIsEnabledOnlyWhileTheSubmittedHandCanBeWithdrawn()
        {
            // 以前の無効状態が残っていても、用途に合う状態へ戻す。
            SetPressable("reselectButton", false);
            Call(hand, "SetSubmittedState", true);
            Assert.That(Button("reselectButton").gameObject.activeSelf, Is.True);
            Assert.That(CanPress("reselectButton"), Is.True);

            var phase = root.AddComponent(MainType("KillingMahjong.UI.GameUIPhaseController"));
            Call(phase, "Setup", manager);
            managerType.GetProperty("PhaseController").SetValue(manager, phase);
            var selection = root.AddComponent(MainType("KillingMahjong.UI.GameUIHandSelectionController"));
            Call(selection, "Setup", manager);
            managerType.GetProperty("HandSelectionController").SetValue(manager, selection);
            Set(phase, "_handSelectionReadyLocal", true);
            Set(phase, "_handSelectionReadyEnemy", true);
            Layout("HandSelection");
            Assert.That(Button("reselectButton").gameObject.activeSelf, Is.False);
            Assert.That(CanPress("reselectButton"), Is.False);

            Set(phase, "_handSelectionReadyEnemy", false);
            managerType.GetProperty("IsTutorialMode").SetValue(manager, true);
            Layout("HandSelection");
            Assert.That(Button("reselectButton").gameObject.activeSelf, Is.False);
            managerType.GetProperty("IsTutorialMode").SetValue(manager, false);
            Call(hand, "SetSubmittedState", false);
            Assert.That(Button("reselectButton").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PeekHasNormalButtonStateRegardlessOfWhoseTurnItIs()
        {
            foreach (bool localTurn in new[] { false, true })
            {
                board.GetType().GetProperty("IsLocalTurn").SetValue(board, localTurn);
                SetPressable("peekButton", false);
                Layout("Discard");
                Assert.That(Button("peekButton").gameObject.activeSelf, Is.True);
                Assert.That(CanPress("peekButton"), Is.True);
            }
            Layout("Betting");
            Assert.That(Button("peekButton").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void PeekHoldReleaseExitAndPhaseChangeRestoreHiddenHand()
        {
            Layout("Discard");
            var hold = Button("peekButton").GetComponent(MainType("KillingMahjong.UI.HoldButton"));
            var tiles = (Transform)Get(hand, "discardPhaseContainer");
            Call(hold, "OnPointerDown", new object[] { null });
            Assert.That(tiles.gameObject.activeSelf, Is.True);
            Call(hold, "OnPointerUp", new object[] { null });
            Assert.That(tiles.gameObject.activeSelf, Is.False);
            Call(hold, "OnPointerDown", new object[] { null });
            Call(hold, "OnPointerExit", new object[] { null });
            Assert.That(tiles.gameObject.activeSelf, Is.False);
            Call(hold, "OnPointerDown", new object[] { null });
            Layout("HandSelection");
            Assert.That((bool)Get(hand, "isPeeking"), Is.False);
            Assert.That(Button("peekButton").gameObject.activeSelf, Is.False);
        }
    }
}
