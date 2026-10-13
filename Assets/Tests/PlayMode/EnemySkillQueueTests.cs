using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("EnemySkillQueue")]
    public class EnemySkillQueueTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private GameObject root;
        private Component ui, board, network, skill, phase, queue;
        private GameObject checker;
        private bool background;
        private static Type Main(string name) => Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Members).SetValue(target, value);
        private static object Get(object target, string property) => target.GetType().GetProperty(property, Members).GetValue(target);
        private static object Field(object target, string field) => target.GetType().GetField(field, Members).GetValue(target);
        private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Members).Invoke(target, args);
        private object Cast(string type, int hp, string player = "enemy", params int[] exposed)
        {
            var cast = Activator.CreateInstance(Main("Network.SkillCastedData"));
            Set(cast, "player_id", player); Set(cast, "skillType", type); Set(cast, "health", hp);
            Set(cast, "exposedHandIndexes", new System.Collections.Generic.List<int>(exposed));
            return cast;
        }
        private void Send(object cast) => Call(skill, "HandleSkillCasted", cast);
        private void Enqueue(Func<IEnumerator> body) => Call(queue, "Enqueue", "test-boundary", body, false, null, null);
        private static IEnumerator Flag(Action action) { action(); yield break; }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            background = Application.runInBackground; Application.runInBackground = true;
            root = new GameObject("EnemySkillQueueTest"); root.SetActive(false);
            board = root.AddComponent(Main("Managers.BoardStateManager"));
            network = root.AddComponent(Main("Network.NetworkMessageHandler")); ((Behaviour)network).enabled = false;
            ui = root.AddComponent(Main("UI.GameUIManager"));
            ui.GetType().GetProperty("IsTutorialMode").SetValue(ui, true); // 通信を始めずにUIだけ初期化する。
            skill = root.AddComponent(Main("UI.GameUISkillController"));
            var rig = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Presentation/EffectPreviewRig"), root.transform);
            rig.SetActive(true);
            var refs = rig.GetComponent(Main("UI.EffectPreviewRig"));
            phase = (Component)Field(refs, "phase");
            Set(ui, "phaseTransitionUI", phase); Set(ui, "playerInfoUI", Field(refs, "player")); Set(ui, "enemyInfoUI", Field(refs, "enemy"));
            var confirmation = new GameObject("Confirmation").AddComponent(Main("UI.ConfirmationDialogUI"));
            confirmation.transform.SetParent(root.transform); ((Behaviour)confirmation).enabled = false;
            Set(ui, "confirmationDialogUI", confirmation);
            root.SetActive(true);
            Call(network, "SetLocalPlayerId", "local");
            yield return null;
            yield return null;
            phase.gameObject.SetActive(true);
            ((Component)Field(refs, "player")).gameObject.SetActive(true);
            ((Component)Field(refs, "enemy")).gameObject.SetActive(true);
            yield return null;
            ui.GetType().GetProperty("IsTutorialMode").SetValue(ui, false);
            Call(phase, "CancelTransitions");
            checker = ((Component)Field(phase, "fullScreenCheckerImage")).gameObject;
            queue = (Component)Main("UI.Effects.EffectQueue").GetProperty("Instance").GetValue(null);
            Call(queue, "Clear"); Set(queue, "_ui", ui);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var preview = Main("UI.EffectPreviewPlayer").GetProperty("Current").GetValue(null);
            if (preview != null) Call(preview, "Close");
            if (queue != null) Call(queue, "Clear");
            if (skill != null) Call(skill, "CancelActiveTransitions");
            if (root != null) UnityEngine.Object.Destroy(root);
            Application.runInBackground = background;
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullBlackAndFadeOutDoNotReleaseAfterTwentySeconds()
        {
            checker.SetActive(true); Set(phase, "isDarkened", true);
            bool ran = false;
            Enqueue(() => Flag(() => ran = true));
            yield return new WaitForSecondsRealtime(20.2f);
            Assert.That(ran, Is.False, "黒幕待機は従来の20秒上限で飛び越さない");
            Assert.That(Get(queue, "PendingCount"), Is.EqualTo(1));
            bool faded = false;
            Call(phase, "PlayRoundStartFadeOut", (Action)(() => faded = true));
            while (!faded)
            {
                Assert.That(ran, Is.False, "フェードアウトの途中でも出さない");
                yield return null;
            }
            yield return null;
            Assert.That(ran, Is.True);
        }

        [Test]
        public void InterleavedWorkKeepsEnemyBatchesSeparate()
        {
            checker.SetActive(true);
            Send(Cast("perspective", 19000)); Send(Cast("perspective", 18000));
            Assert.That(Get(queue, "PendingCount"), Is.EqualTo(1));
            Enqueue(() => Flag(() => { }));
            Send(Cast("mulligan", 17000));
            Assert.That(Get(queue, "PendingCount"), Is.EqualTo(3), "別の演出を追い越してまとめない");
            Send(Cast("perspective", 16000, "local"));
            Send(Cast("perspective", 15000));
            Assert.That(Get(queue, "PendingCount"), Is.EqualTo(5), "自分のスキルの前後も別々");
        }

        [UnityTest]
        public IEnumerator OneCutinPreservesEveryServerEffectAndFinalHealth()
        {
            checker.SetActive(true);
            var enemyWall = (IList)Get(board, "CurrentEnemyWallTiles"); enemyWall.Add(9);
            Send(Cast("perspective", 18800, "enemy", 1, 3));
            var swap = Cast("mulligan", 18500);
            var result = Activator.CreateInstance(Main("Network.MulliganResultData"));
            Set(result, "oldTile", 9); Set(result, "newTile", 10); Set(swap, "mulliganResult", result);
            Send(swap); Send(Cast("perspective", 18200, "enemy", 4));
            Assert.That(Get(queue, "PendingCount"), Is.EqualTo(1));
            checker.SetActive(false);
            int cutins = 0, previousId = 0;
            string label = null;
            float deadline = Time.realtimeSinceStartup + 12;
            do
            {
                var current = GameObject.Find("DeathGameCutinContainer");
                if (current != null && current.GetInstanceID() != previousId)
                {
                    previousId = current.GetInstanceID(); cutins++;
                    var text = current.transform.Find("MainText").GetComponent(Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true));
                    label = (string)Get(text, "text");
                }
                yield return null;
            } while (((int)Get(queue, "PendingCount") > 0 || Get(queue, "CurrentName") != null) && Time.realtimeSinceStartup < deadline);
            Assert.That(cutins, Is.EqualTo(1));
            Assert.That(label, Is.EqualTo("透視 × 2\n牌交換"));
            Assert.That(Get(board, "EnemyPlayerHp"), Is.EqualTo(18200));
            Assert.That(Get(board, "LocalPlayerHp"), Is.EqualTo(20000));
            Assert.That(enemyWall.Contains(10), Is.True); Assert.That(enemyWall.Contains(9), Is.False);
            var exposed = (System.Collections.Generic.HashSet<int>)Get(board, "ExposedLocalHandWallIndexes");
            Assert.That(exposed.SetEquals(new[] { 1, 3, 4 }), Is.True);
            Assert.That(Get(queue, "CurrentName"), Is.Null);
            Assert.That(Get(ui, "IsTransitioning"), Is.False);
        }

        [UnityTest]
        public IEnumerator CancelWhileWaitingDropsOldBatch()
        {
            checker.SetActive(true); Send(Cast("perspective", 18000)); Send(Cast("mulligan", 17000));
            Call(skill, "CancelActiveTransitions"); checker.SetActive(false);
            yield return null; yield return null; yield return null;
            Assert.That(Get(board, "EnemyPlayerHp"), Is.EqualTo(20000));
            Assert.That(GameObject.Find("DeathGameCutinContainer"), Is.Null);
            Assert.That(Get(queue, "PendingCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator CollectionRepeatsAndStopsWithoutChangingMatch()
        {
            var type = Main("UI.EffectPreviewPlayer");
            var preview = type.GetMethod("Open").Invoke(null, new object[] { "skill.cutin.enemy_batch", true, null });
            float deadline = Time.realtimeSinceStartup + 10;
            while ((int)Get(preview, "CompletedPlays") < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(Get(preview, "State"), Is.Not.EqualTo("error"));
            Assert.That((int)Get(preview, "CompletedPlays"), Is.GreaterThanOrEqualTo(2));
            Call(preview, "Replay"); yield return null; Call(preview, "Close"); yield return null;
            Assert.That(Get(board, "LocalPlayerHp"), Is.EqualTo(20000));
            Assert.That(Get(board, "EnemyPlayerHp"), Is.EqualTo(20000));
            Assert.That(((IList)Get(board, "CurrentHandTiles")).Count, Is.Zero);
            Assert.That(GameObject.Find("DeathGameCutinContainer"), Is.Null);
        }
    }
}
