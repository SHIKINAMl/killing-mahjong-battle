using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("TileClickIdentity")]
    public class TileClickIdentityTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root;
        private Component board, manager, hand, wall, visual;
        private Component first, second;
        private object pointer;
        private static Type Main(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static void Set(Component target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Get(Component target, string name) => target.GetType().GetProperty(name, Members).GetValue(target);
        private static object Call(Component target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
        private GameObject Child(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            return go;
        }
        private object Phase(string name) => Enum.Parse(Main("KillingMahjong.EngineData.RoundStatus"), name);
        private List<int> Indexes => (List<int>)Get(board, "LocalHandIndexes");
        private List<RectTransform> Slots(Component target, string method) => (List<RectTransform>)Call(target, method);
        private RectTransform Rect(Component tile) => (RectTransform)tile.transform;
        private Component Tile(string name, int index)
        {
            var tile = Child(name).AddComponent(Main("KillingMahjong.UI.TileInteraction"));
            Call(tile, "Initialize", 0, false, manager, null);
            tile.GetType().GetProperty("WallIndex").SetValue(tile, index);
            Rect(tile).sizeDelta = new Vector2(45, 60);
            return tile;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Main("KillingMahjong.Managers.BoardStateManager").GetProperty("Instance").GetValue(null) == null, Is.True);
            root = new GameObject("TileClickIdentityTest", typeof(RectTransform));
            root.SetActive(false);
            manager = root.AddComponent(Main("KillingMahjong.UI.GameUIManager"));
            ((Behaviour)manager).enabled = false;
            board = root.AddComponent(Main("KillingMahjong.Managers.BoardStateManager"));
            hand = Child("Hand").AddComponent(Main("KillingMahjong.UI.HandUI"));
            ((Behaviour)hand).enabled = false;
            wall = Child("Wall").AddComponent(Main("KillingMahjong.UI.WallUI"));
            ((Behaviour)wall).enabled = false;
            Set(hand, "handSlotContainer", Child("HandTiles").transform);
            Set(hand, "discardPhaseContainer", Child("DiscardTiles").transform);
            Set(hand, "cursor", Child("HandCursor").transform);
            Set(wall, "wallContainer", Child("WallTiles").transform);
            Set(manager, "handUI", hand);
            Set(manager, "wallUI", wall);
            Set(manager, "currentPhaseStatus", Phase("HandSelection"));
            Call(hand, "Setup", manager);
            Call(wall, "Setup", manager);
            visual = root.AddComponent(Main("KillingMahjong.UI.GameUIVisualController"));
            ((Behaviour)visual).enabled = false;
            Set(visual, "uiManager", manager);
            Set(visual, "_moveAnimator", Activator.CreateInstance(Main("KillingMahjong.UI.TileMoveAnimator"), manager, visual));
            manager.GetType().GetProperty("VisualController").SetValue(manager, visual);
            var eventSystemType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", true);
            var eventSystem = Child("EventSystem").AddComponent(eventSystemType);
            ((Behaviour)eventSystem).enabled = false;
            pointer = Activator.CreateInstance(Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI", true), new object[] { eventSystem });
            root.SetActive(true);
            Call(board, "SetLocalState", new List<int> { 0, 1, 0 }, new List<int>(), null, null);
            first = Tile("DuplicateFirst", 0);
            second = Tile("DuplicateClicked", 2);
            Slots(wall, "GetWallSlots").Add(Rect(first));
            Slots(wall, "GetWallSlots").Add(Rect(second));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingSecondDuplicateMovesThatObjectAndReturnsThatObject()
        {
            Call(second, "OnPointerClick", pointer);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Slots(hand, "GetHandSlots"), Contains.Item(Rect(second)));
            Assert.That(Slots(wall, "GetWallSlots"), Contains.Item(Rect(first)));
            CollectionAssert.AreEqual(new[] { 2 }, Indexes);
            Call(first, "OnPointerClick", pointer);
            yield return new WaitForSecondsRealtime(.3f);
            Call(second, "OnPointerClick", pointer);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Slots(hand, "GetHandSlots"), Contains.Item(Rect(first)));
            Assert.That(Slots(wall, "GetWallSlots"), Contains.Item(Rect(second)));
            CollectionAssert.AreEqual(new[] { 0 }, Indexes);
        }

        [Test]
        public void ClickingAnotherEqualTileChangesSelectionInsteadOfDiscarding()
        {
            Set(manager, "currentPhaseStatus", Phase("Discard"));
            board.GetType().GetProperty("IsLocalTurn").SetValue(board, true);
            Call(first, "OnPointerClick", pointer);
            Assert.That(Get(board, "SelectedWallIndex"), Is.EqualTo(0));
            Call(second, "OnPointerClick", pointer);
            Assert.That(Get(board, "SelectedWallIndex"), Is.EqualTo(2));
            Assert.That(Get(board, "IsLocalTurn"), Is.True);
            Assert.That(Call(manager, "IsTileSelected", 0, first), Is.False);
            Assert.That(Call(manager, "IsTileSelected", 0, second), Is.True);
        }

        [Test]
        public void ServerIndexesSurviveStateSyncAndDoNotDiscardAHandDuplicate()
        {
            Call(board, "SetLocalState", new List<int> { 0, 1, 0 }, new List<int> { 0 }, null, new List<int> { 2 });
            CollectionAssert.AreEqual(new[] { 2 }, Indexes);
            Assert.That(Call(board, "FindAvailableWallIndex", 0), Is.EqualTo(0));
            Assert.That(Call(board, "MoveTileToHand", 0, 2), Is.False);
            Assert.That(Call(board, "MoveTileToWall", 0, 0), Is.False);
            Assert.That(Call(board, "MoveTileToWall", 0, 2), Is.True);
        }

        [Test]
        public void SubmittingThirteenTilesPreservesTheClickedDuplicateIndex()
        {
            var wallIds = new List<int> { 0 };
            var handIds = new List<int>();
            var exact = new List<int>();
            for (int i = 0; i < 13; i++) { wallIds.Add(i); handIds.Add(i); exact.Add(i + 1); }
            Call(board, "SetLocalState", wallIds, handIds, null, exact);
            var controller = root.AddComponent(Main("KillingMahjong.UI.GameUIHandSelectionController"));
            Call(controller, "Setup", manager);
            var args = new object[] { null, null };
            Assert.That(Call(controller, "TryCaptureCurrentHandSelection", args), Is.True);
            CollectionAssert.AreEqual(exact, (List<int>)args[0]);
            CollectionAssert.DoesNotContain((List<int>)args[0], 0);
        }

        [Test]
        public void StaleTileWrongTurnAndSubmittedHandCannotBeClicked()
        {
            second.GetType().GetProperty("WallIndex").SetValue(second, 1); // 種類が違う古いスロット
            Call(second, "OnPointerClick", pointer);
            Assert.That(Indexes, Is.Empty);
            second.GetType().GetProperty("WallIndex").SetValue(second, 2);
            Call(hand, "SetSubmittedState", true);
            Call(second, "OnPointerClick", pointer);
            Assert.That(Indexes, Is.Empty);
            Set(manager, "currentPhaseStatus", Phase("Discard"));
            board.GetType().GetProperty("IsLocalTurn").SetValue(board, false);
            Call(second, "OnPointerClick", pointer);
            Assert.That(Get(board, "SelectedWallIndex"), Is.EqualTo(-1));
        }

        [Test]
        public void LayoutRetainsOriginalIndexesAfterSortingAndRemovingAHandTile()
        {
            Call(wall, "LayoutWallTiles", new List<RectTransform> { Rect(first), Rect(second) },
                new List<int> { 0, 0 }, new List<int>(), false, new List<int> { 0, 2 });
            var clicked = (RectTransform)Call(wall, "GrabTileByWallIndex", 2);
            Assert.That(clicked, Is.Not.Null);
            Assert.That(Get(clicked.GetComponent(Main("KillingMahjong.UI.TileInteraction")), "WallIndex"), Is.EqualTo(2));
            Assert.That(Slots(wall, "GetWallSlots").Count, Is.EqualTo(1));
            Assert.That(Get(Slots(wall, "GetWallSlots")[0].GetComponent(Main("KillingMahjong.UI.TileInteraction")), "WallIndex"), Is.EqualTo(0));
        }

        [Test]
        public void FullRebuildKeepsIndexHolesAndNoticesAnEqualIdHandSwap()
        {
            var prefab = Child("TilePrefab");
            prefab.AddComponent(Main("KillingMahjong.UI.TileInteraction"));
            Set(manager, "tilePrefab", prefab);
            var pool = Child("TilePool").AddComponent(Main("KillingMahjong.UI.TilePoolManager"));
            Set(pool, "_container", Child("PoolSlots").transform);
            Call(pool, "InitializePool", manager);
            Set(visual, "poolManager", pool);
            Call(board, "SetLocalState", new List<int> { 0, 1, 0 }, new List<int> { 0 }, null, new List<int> { 2 });
            Call(board, "MarkWallIndexAsDiscarded", 1);
            ((List<int>)Get(board, "CurrentWallTiles")).Remove(1);
            var rebuild = visual.GetType().GetMethod("RebuildAllTilesFromState", new[] { typeof(List<int>), typeof(bool) });
            rebuild.Invoke(visual, new object[] { null, false });
            Assert.That(Get(Slots(hand, "GetHandSlots")[0].GetComponent(Main("KillingMahjong.UI.TileInteraction")), "WallIndex"), Is.EqualTo(2));
            Assert.That(Get(Slots(wall, "GetWallSlots")[0].GetComponent(Main("KillingMahjong.UI.TileInteraction")), "WallIndex"), Is.EqualTo(0));

            // 牌IDの構成は同じでも、選ばれた実物が変われば差分同期だけで済ませない。
            Call(board, "SetLocalState", new List<int> { 0, 1, 0 }, new List<int> { 0 }, null, new List<int> { 0 });
            rebuild.Invoke(visual, new object[] { null, false });
            Assert.That(Get(Slots(hand, "GetHandSlots")[0].GetComponent(Main("KillingMahjong.UI.TileInteraction")), "WallIndex"), Is.EqualTo(0));
            Assert.That(Get(Slots(wall, "GetWallSlots")[0].GetComponent(Main("KillingMahjong.UI.TileInteraction")), "WallIndex"), Is.EqualTo(2));

            // 牌交換後にも、別の同種牌を書き換えて古い位置を残さない。
            Assert.That(Call(board, "ReplaceWallTileAt", 2, 1), Is.True);
            rebuild.Invoke(visual, new object[] { null, false });
            var exchanged = Slots(wall, "GetWallSlots")[0].GetComponent(Main("KillingMahjong.UI.TileInteraction"));
            Assert.That(Get(exchanged, "WallIndex"), Is.EqualTo(2));
            Assert.That(Get(exchanged, "TileId"), Is.EqualTo(1));
            Assert.That(Call(manager, "IsCurrentTile", exchanged), Is.True);
        }
    }
}
