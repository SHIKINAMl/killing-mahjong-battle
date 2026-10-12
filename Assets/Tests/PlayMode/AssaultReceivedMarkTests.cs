using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("AssaultReceivedMark")]
    public class AssaultReceivedMarkTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root;
        private Component ui, board, skill, network, player, enemy;
        private RectTransform selfGauge, enemyGauge;
        private Delegate receive;
        private bool background;
        private static Type Main(string name) => Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        private static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
        private static object Get(object target, string name) => target.GetType().GetProperty(name, Members).GetValue(target);
        private static void MarkCall(string name, params object[] args) => Main("UI.Effects.AssaultMarkUI").GetMethod(name).Invoke(null, args);
        private Component[] Marks() => root == null ? new Component[0] : Array.ConvertAll(
            UnityEngine.Object.FindObjectsByType(Main("UI.Effects.AssaultMarkUI"), FindObjectsSortMode.None), o => (Component)o);
        private Component MarkFor(RectTransform target) => Array.Find(Marks(), m => (RectTransform)Field(m, "_target") == target);
        private Vector2 Position(Component mark)
        {
            return (Vector2)mark.GetType().GetMethod("MarkPosition", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { Field(mark, "_stage"), Field(mark, "_target"), Field(mark, "_attachToLeft") });
        }
        private int Quads(Component mark) => ((IList)Field(Field(mark, "_shapes"), "_quads")).Count;
        private GameObject Child(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            return go;
        }
        private Component Info(string name, string type, Vector2 position, Vector2 size, out RectTransform gauge)
        {
            var go = Child(name);
            var info = go.AddComponent(Main(type));
            ((Behaviour)info).enabled = false;
            var art = Child(name + "Gauge");
            gauge = (RectTransform)art.transform;
            gauge.SetParent(go.transform, false);
            gauge.sizeDelta = size;
            gauge.anchoredPosition = position;
            var image = art.AddComponent(Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true));
            Set(info, "hpFillImage", image);
            return info;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            background = Application.runInBackground;
            Application.runInBackground = true;
            Assert.That(Main("Managers.BoardStateManager").GetProperty("Instance").GetValue(null) == null, Is.True);
            Assert.That(Main("Network.NetworkMessageHandler").GetProperty("Instance").GetValue(null) == null, Is.True);
            MarkCall("Clear");
            root = new GameObject("AssaultReceivedMarkTest", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.SetActive(false);
            ui = root.AddComponent(Main("UI.GameUIManager"));
            ((Behaviour)ui).enabled = false;
            board = root.AddComponent(Main("Managers.BoardStateManager"));
            network = root.AddComponent(Main("Network.NetworkMessageHandler"));
            ((Behaviour)network).enabled = false;
            skill = root.AddComponent(Main("UI.GameUISkillController"));
            player = Info("Self", "UI.PlayerInfoUI", new Vector2(340, -100), new Vector2(80, 140), out selfGauge);
            enemy = Info("Enemy", "UI.EnemyInfoUI", new Vector2(-170, 100), new Vector2(80, 120), out enemyGauge);
            Set(ui, "playerInfoUI", player);
            Set(ui, "enemyInfoUI", enemy);
            Call(skill, "Setup", ui);
            root.SetActive(true);
            Call(network, "SetLocalPlayerId", "mark-test-local");
            var dataType = network.GetType().GetEvent("OnSkillCasted").EventHandlerType.GetGenericArguments()[0];
            receive = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(dataType), skill, skill.GetType().GetMethod("HandleSkillCasted"));
            network.GetType().GetEvent("OnSkillCasted").AddEventHandler(network, receive);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var previewType = Main("UI.EffectPreviewPlayer");
            var preview = previewType.GetProperty("Current").GetValue(null);
            if (preview != null) Call(preview, "Close");
            if (network != null && receive != null) network.GetType().GetEvent("OnSkillCasted").RemoveEventHandler(network, receive);
            MarkCall("Clear");
            if (root != null) UnityEngine.Object.Destroy(root);
            Application.runInBackground = background;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReceivedServerMessageMarksSelfWithoutChargingSelfOrUsingLocalAssault()
        {
            // 送信用クライアントは持たない隔離試験。既存ハンドラのstatus要求だけを想定する。
            LogAssert.Expect(LogType.Error, new Regex("\\[Network\\].*action 'status'.*"));
            Call(network, "ProcessServerMessage", "{\"type\":\"skill_casted\",\"data\":{\"player_id\":\"mark-test-enemy\",\"skillType\":\"assault\",\"health\":18800,\"cost\":1200}}");
            float deadline = Time.realtimeSinceStartup + 8;
            while ((MarkFor(selfGauge) == null || (bool)Get(ui, "IsTransitioning")) && Time.realtimeSinceStartup < deadline) yield return null;
            var mark = MarkFor(selfGauge);
            Assert.That(mark, Is.Not.Null);
            Assert.That(Field(mark, "_attachToLeft"), Is.True);
            Assert.That(MarkFor(enemyGauge), Is.Null);
            Assert.That(Get(board, "LocalPlayerHp"), Is.EqualTo(20000));
            Assert.That(Get(board, "EnemyPlayerHp"), Is.EqualTo(18800));
            Assert.That(Get(board, "LocalAssaultUsedThisRound"), Is.False);
            Assert.That(Get(ui, "IsTransitioning"), Is.False);
            Assert.That(Quads(mark), Is.GreaterThan(0));

            // 続いて自分も強襲する。既存の攻撃演出が相手の札を出しても、受けた札を消さない。
            var dataType = network.GetType().GetEvent("OnSkillCasted").EventHandlerType.GetGenericArguments()[0];
            var localCast = JsonUtility.FromJson("{\"player_id\":\"mark-test-local\",\"skillType\":\"assault\",\"health\":18800,\"cost\":1200}", dataType);
            Call(skill, "HandleSkillCasted", localCast);
            deadline = Time.realtimeSinceStartup + 10;
            while ((MarkFor(enemyGauge) == null || (bool)Get(ui, "IsTransitioning")) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(MarkFor(selfGauge), Is.SameAs(mark));
            Assert.That(MarkFor(enemyGauge), Is.Not.Null);
            Assert.That(Field(MarkFor(enemyGauge), "_attachToLeft"), Is.False);
            Assert.That(Get(board, "LocalAssaultUsedThisRound"), Is.True);
            Assert.That(Marks().Length, Is.EqualTo(2));
        }

        [Test]
        public void BothTargetsKeepTheirMarksAndDuplicateNotificationsDoNotStack()
        {
            MarkCall("Show", selfGauge, true);
            MarkCall("Show", enemyGauge, false);
            MarkCall("Show", selfGauge, true);
            Assert.That(Marks().Length, Is.EqualTo(2));
            Assert.That(Field(MarkFor(selfGauge), "_attachToLeft"), Is.True);
            Assert.That(Field(MarkFor(enemyGauge), "_attachToLeft"), Is.False);
            MarkCall("ClearTarget", selfGauge);
            Assert.That(MarkFor(enemyGauge), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MarkFollowsHpAndHidesAndReappearsWithItsTarget()
        {
            MarkCall("Show", selfGauge, true);
            var mark = MarkFor(selfGauge);
            Vector2 before = Position(mark);
            selfGauge.anchoredPosition += new Vector2(-20, 12);
            Canvas.ForceUpdateCanvases();
            Assert.That(Position(mark) - before, Is.EqualTo(new Vector2(-20, 12)));
            selfGauge.gameObject.SetActive(false);
            yield return null;
            Assert.That(Quads(mark), Is.Zero);
            selfGauge.gameObject.SetActive(true);
            yield return null;
            Assert.That(Quads(mark), Is.GreaterThan(0));
        }

        [Test]
        public void RoundResetClearsBothMarksAndResetsLocalUseFlag()
        {
            MarkCall("Show", selfGauge, true);
            MarkCall("Show", enemyGauge, false);
            Call(board, "MarkLocalAssaultUsed");
            Call(board, "ClearAllBoardData");
            var active = (IList)Main("UI.Effects.AssaultMarkUI").GetField("ActiveMarks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(active.Count, Is.Zero);
            Assert.That(Get(board, "LocalAssaultUsedThisRound"), Is.False);
        }

        [UnityTest]
        public IEnumerator CatalogRepeatAndCancelKeepExistingMatchMark()
        {
            MarkCall("Show", enemyGauge, false);
            var matchMark = MarkFor(enemyGauge);
            var type = Main("UI.EffectPreviewPlayer");
            var preview = type.GetMethod("Open").Invoke(null, new object[] { "skill.assault_received_mark", true, null });
            float deadline = Time.realtimeSinceStartup + 15;
            while ((int)Get(preview, "CompletedPlays") < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That((int)Get(preview, "CompletedPlays"), Is.GreaterThanOrEqualTo(2));
            Assert.That(MarkFor(enemyGauge), Is.SameAs(matchMark));
            Call(preview, "Replay");
            deadline = Time.realtimeSinceStartup + 5;
            while (((string)Get(preview, "State") != "playing" || Marks().Length < 2) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Marks().Length, Is.EqualTo(2));
            Call(preview, "Close");
            yield return null;
            Assert.That(MarkFor(enemyGauge), Is.SameAs(matchMark));
            Assert.That(Marks().Length, Is.EqualTo(1));
            Assert.That(Get(board, "LocalPlayerHp"), Is.EqualTo(20000));
        }
    }
}
