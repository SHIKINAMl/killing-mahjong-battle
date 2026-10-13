using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KillingMahjong.Tests
{
    [Category("CpuMatchMenu")]
    public class CpuMatchMenuTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type T(string name) => Type.GetType("KillingMahjong." + name + ", Assembly-CSharp", true);
        private static object Invoke(Type t, object obj, string method, params object[] args) => t.GetMethod(method, F).Invoke(obj, args);
        private static object Cpu(int value) => Enum.ToObject(T("Network.CpuDifficulty"), value);
        private static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
        private static object Prop(object o, string field) => o.GetType().GetProperty(field).GetValue(o);
        private static Type Request => T("Network.MatchJoinRequest");
        private static void Public() => Invoke(Request, null, "RequestPublic");
        private static string Json() => (string)Invoke(Request, null, "BuildJoinJson");
        private static object Gate(int difficulty = 1, float now = 100) => Activator.CreateInstance(T("Network.CpuMatchJoinGate"), new[] { Cpu(difficulty), (object)now });
        private static void Call(object o, string method, params object[] args) => Invoke(o.GetType(), o, method, args);
        private static object Started(string mode = "cpu", string difficulty = "normal") => JsonUtility.FromJson(
            "{\"match_id\":\"M1\",\"mode\":\"" + mode + "\",\"players\":[{\"client_id\":\"P1\"}," +
            "{\"client_id\":\"CPU1\",\"is_cpu\":true,\"cpu\":{\"difficulty\":\"" + difficulty + "\"}}]}", T("EngineData.GameStartedData"));
        private static Component OpenCpuMenu(string name, out GameObject holder, out GameObject root)
        {
            holder = new GameObject(name);
            var menu = holder.AddComponent(T("UI.TitleMultiMenuUI"));
            Call(menu, "Open", null, null); root = (GameObject)Get(menu, "_root");
            Call(menu, "ShowCpuPanel");
            return menu;
        }
        private static void DisposeMenu(Component menu, GameObject holder, GameObject root)
        {
            Call(menu, "Close"); UnityEngine.Object.Destroy(holder); UnityEngine.Object.Destroy(root);
        }
        [SetUp] public void Begin() { Public(); }
        [TearDown] public void End() { Public(); }

        [TestCase(0, "easy", "やさしい")]
        [TestCase(1, "normal", "ふつう")]
        [TestCase(2, "hard", "つよい")]
        [TestCase(3, "special", "とくしゅ")]
        public void FourDifficultiesUseTheServerContract(int index, string key, string label)
        {
            Invoke(Request, null, "RequestCpu", Cpu(index));
            Assert.That(Json(), Is.EqualTo("{\"type\":\"join\",\"data\":{\"mode\":\"cpu\",\"difficulty\":\"" + key + "\"}}"));
            Assert.That(Invoke(T("Network.CpuDifficultyCatalog"), null, "Label", Cpu(index)), Is.EqualTo(label));
        }
        [TestCase("RequestPublic", "public")]
        [TestCase("RequestPrivateCreate", "private_create")]
        public void OtherModesClearCpuDifficulty(string method, string wire)
        {
            Invoke(Request, null, "RequestCpu", Cpu(3)); Invoke(Request, null, method);
            Assert.That(Json(), Is.EqualTo("{\"type\":\"join\",\"data\":{\"mode\":\"" + wire + "\"}}"));
            Assert.That(Request.GetProperty("Difficulty").GetValue(null).ToString(), Is.EqualTo("Normal"));
        }
        [Test] public void CpuAndPrivatePasswordsDoNotLeakAcrossModes()
        {
            Invoke(Request, null, "RequestPrivateJoin", " abc12 "); Invoke(Request, null, "RequestCpu", Cpu(2));
            Assert.That(Request.GetProperty("Password").GetValue(null), Is.EqualTo(""));
            Invoke(Request, null, "RequestPrivateJoin", " xyz34 ");
            Assert.That(Json(), Is.EqualTo("{\"type\":\"join\",\"data\":{\"mode\":\"private_join\",\"password\":\"XYZ34\"}}"));
            Assert.That(Request.GetProperty("Difficulty").GetValue(null).ToString(), Is.EqualTo("Normal"));
        }
        [Test] public void InvalidDifficultyCannotChangeRequestState()
        {
            Invoke(Request, null, "RequestPrivateCreate"); string before = Json();
            var error = Assert.Throws<TargetInvocationException>(() => Invoke(Request, null, "RequestCpu", Cpu(99)));
            Assert.That(error.InnerException, Is.TypeOf<ArgumentOutOfRangeException>()); Assert.That(Json(), Is.EqualTo(before));
        }
        [TestCase(0,"easy")][TestCase(1,"normal")][TestCase(2,"hard")][TestCase(3,"special")]
        public void ExactCpuSeatAndDifficultyAreAccepted(int difficulty, string key)
        {
            var gate = Gate(difficulty); Call(gate,"Accept", Started("cpu",key));
            Assert.That(Prop(gate,"State").ToString(), Is.EqualTo("Matched"));
        }
        [TestCase("public", "normal")][TestCase("cpu", "hard")][TestCase("", "normal")]
        public void WrongModeOrDifficultyNeverStartsHumanMatch(string mode, string difficulty)
        {
            var gate=Gate(); Call(gate,"Accept",Started(mode,difficulty));
            Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Failed"));
        }
        [Test] public void LegacyNotificationDoesNotPretendToBeCpu()
        {
            var gate=Gate(); var legacy=JsonUtility.FromJson("{\"match_id\":\"M1\",\"players\":[{\"client_id\":\"P1\"},{\"client_id\":\"P2\"}]}",T("EngineData.GameStartedData"));
            Call(gate,"Accept",legacy); Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Failed"));
        }
        [Test] public void MissingCpuSeatIsRejected()
        {
            var data=Started(); var players=(Array)Get(data,"players"); players.GetValue(1).GetType().GetField("is_cpu").SetValue(players.GetValue(1),false);
            var gate=Gate();Call(gate,"Accept",data); Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Failed"));
        }
        [Test] public void DuplicatePlayerIdentityIsRejected()
        {
            var data=Started();var players=(Array)Get(data,"players");players.GetValue(1).GetType().GetField("client_id").SetValue(players.GetValue(1),"P1");
            var gate=Gate();Call(gate,"Accept",data);Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Failed"));
        }
        [Test] public void TimeoutIsIndependentOfGameTimeAndTerminal()
        {
            var gate=Gate();Call(gate,"Tick",114.99f);Assert.That(Prop(gate,"IsPending"),Is.True);
            Call(gate,"Tick",115f);Call(gate,"Accept",Started());Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Failed"));
        }
        [Test] public void CancelledAndFailedAttemptsIgnoreLateStart()
        {
            var cancelled=Gate();Call(cancelled,"Cancel");Call(cancelled,"Accept",Started());Assert.That(Prop(cancelled,"State").ToString(),Is.EqualTo("Cancelled"));
            var failed=Gate();Call(failed,"Fail","unsupported");Call(failed,"Accept",Started());Assert.That(Prop(failed,"Error"),Is.EqualTo("unsupported"));
        }
        [Test] public void AcceptedStartCannotBeOverwrittenByConnectionClose()
        {
            var gate=Gate();Call(gate,"Accept",Started());Call(gate,"Fail","closed");Call(gate,"Cancel");Call(gate,"Tick",200f);
            Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Matched"));
        }
        [TestCase("Unsupported join mode: cpu","このサーバーはCPU対戦に未対応です")]
        [TestCase("Unsupported difficulty: special","このサーバーでは選んだ強さを使えません")]
        public void UnsupportedServerGivesActionableError(string raw,string expected)
        { Assert.That(Invoke(T("UI.TitleMultiMenuUI"),null,"TranslateCpuJoinError",raw),Is.EqualTo(expected)); }

        [UnityTest] public IEnumerator PreflightConnectionDoesNotAutostartAgain()
        {
            var go=new GameObject("CpuManualStartupTest");
            var type=Type.GetType("WebSocketGameClientSample, Assembly-CSharp",true);
            var client=go.AddComponent(type);
            Invoke(type,client,"UseManualStartup");
            Assert.That(type.GetField("autoConnectOnStart",F).GetValue(client),Is.False);
            yield return null;
            Assert.That(type.GetProperty("IsConnected").GetValue(client),Is.False);
            UnityEngine.Object.Destroy(go);yield return null;
        }

        [UnityTest] public IEnumerator MenuChoicesReturnAndReopenWithoutConnecting()
        {
            var holder=new GameObject("CpuMenuTests"); var menu=holder.AddComponent(T("UI.TitleMultiMenuUI"));
            Call(menu,"Open",null,null); var root=(GameObject)Get(menu,"_root");
            Call(menu,"ShowCpuPanel");Assert.That(((GameObject)Get(menu,"_cpuPanel")).activeSelf,Is.True);
            var choices=(Button[])Get(menu,"_cpuChoices");Assert.That(choices.Length,Is.EqualTo(4));
            for(int i=0;i<4;i++)
            {
                choices[i].onClick.Invoke(); Assert.That(Get(menu,"_cpuDifficulty").ToString(),Is.EqualTo(Cpu(i).ToString()));
                for(int j=0;j<4;j++)Assert.That(Prop(choices[j].GetComponent(T("UI.UIInteractionFeedback")),"IsSelected"),Is.EqualTo(i==j));
                Assert.That(((RectTransform)choices[i].transform).sizeDelta,Is.EqualTo(new Vector2(300,46)));
            }
            Call(menu,"OnCpuBackSelected");Assert.That(((GameObject)Get(menu,"_modePanel")).activeSelf,Is.True);
            Call(menu,"ShowCpuPanel");Assert.That(Get(menu,"_cpuDifficulty").ToString(),Is.EqualTo("Normal"));
            Call(menu,"Close");UnityEngine.Object.Destroy(holder);UnityEngine.Object.Destroy(root);yield return null;
        }

        [UnityTest] public IEnumerator AllFourChoicesRequireTwoClicksIncludingInitiallyHighlightedNormal()
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject holder, root;
                var menu = OpenCpuMenu("CpuTwoClickTest", out holder, out root);
                Assert.That(Get(menu, "_cpuChoiceArmed"), Is.False);
                ((Button[])Get(menu, "_cpuChoices"))[i].onClick.Invoke();
                Assert.That(Get(menu, "_cpuChoiceArmed"), Is.True);
                Assert.That(Get(menu, "_cpuAttempt"), Is.Null);
                Assert.That(Get(menu, "_cpuJoinClient"), Is.Null);
                Assert.That(Request.GetProperty("Mode").GetValue(null).ToString(), Is.EqualTo("Public"));
                // 確認ゲートのみを呼ぶ。実サーバー接続は別の実機検証で確認する。
                Assert.That(Invoke(menu.GetType(), menu, "TryConfirmCpuDifficulty", Cpu(i)), Is.True);
                Assert.That(Get(menu, "_cpuChoiceArmed"), Is.False);
                Assert.That(Invoke(menu.GetType(), menu, "TryConfirmCpuDifficulty", Cpu(i)), Is.False);
                DisposeMenu(menu, holder, root); yield return null;
            }
        }

        [UnityTest] public IEnumerator SwitchingChoiceAndReenteringResetTheConfirmation()
        {
            GameObject holder, root;
            var menu = OpenCpuMenu("CpuChoiceResetTest", out holder, out root);
            var choices = (Button[])Get(menu, "_cpuChoices");
            choices[0].onClick.Invoke(); choices[1].onClick.Invoke(); choices[0].onClick.Invoke();
            Assert.That(Get(menu, "_cpuAttempt"), Is.Null);
            Assert.That(Get(menu, "_cpuDifficulty").ToString(), Is.EqualTo("Easy"));
            Call(menu, "OnCpuBackSelected"); Assert.That(Get(menu, "_cpuChoiceArmed"), Is.False);
            Call(menu, "ShowCpuPanel"); Assert.That(Get(menu, "_cpuChoiceArmed"), Is.False);
            choices[1].onClick.Invoke(); Assert.That(Get(menu, "_cpuAttempt"), Is.Null);
            Call(menu, "Close"); Assert.That(Get(menu, "_cpuChoiceArmed"), Is.False);
            Call(menu, "Open", null, null); Call(menu, "ShowCpuPanel");
            choices[1].onClick.Invoke(); Assert.That(Get(menu, "_cpuAttempt"), Is.Null);
            Assert.That(Invoke(menu.GetType(), menu, "TryConfirmCpuDifficulty", Cpu(1)), Is.True);
            DisposeMenu(menu, holder, root); yield return null;
        }

        [UnityTest] public IEnumerator CpuLayoutHasNoStartActionAndHasAnInlineNonBlockingSpecialHint()
        {
            GameObject holder, root;
            var menu = OpenCpuMenu("CpuInlineHintTest", out holder, out root);
            var panel = ((GameObject)Get(menu, "_cpuPanel")).transform;
            var oldStart = panel.Find("対戦する");
            Assert.That(oldStart == null || !oldStart.gameObject.activeSelf, Is.True);
            Assert.That(((RectTransform)((Button)Get(menu, "_cpuBack")).transform).anchoredPosition, Is.EqualTo(new Vector2(0, -140)));
            var hintRect = (RectTransform)panel.Find("SpecialHint");
            Assert.That(hintRect.anchoredPosition, Is.EqualTo(new Vector2(70, -63)));
            Assert.That(hintRect.sizeDelta, Is.EqualTo(new Vector2(140, 22)));
            var hint = hintRect.GetComponent(Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true));
            Assert.That(Prop(hint, "alignment").ToString(), Is.EqualTo("Left"));
            Assert.That(Prop(hint, "raycastTarget"), Is.False);
            Call(menu, "OnCpuBackSelected");
            Assert.That(Invoke(menu.GetType(), menu, "TryConfirmCpuDifficulty", Cpu(0)), Is.False);
            DisposeMenu(menu, holder, root); yield return null;
        }

        [UnityTest] public IEnumerator PendingJoinBlocksDuplicateSubmissionAndCancels()
        {
            var holder=new GameObject("CpuMenuBusyTests"); var menu=holder.AddComponent(T("UI.TitleMultiMenuUI"));
            Call(menu,"Open",null,null);var root=(GameObject)Get(menu,"_root");Call(menu,"ShowCpuPanel");
            menu.GetType().GetField("_isCheckingRoom",F).SetValue(menu,true);
            var gate=Gate();menu.GetType().GetField("_cpuAttempt",F).SetValue(menu,gate);Call(menu,"SetCpuBusy",true);
            Call(menu,"OnCpuStartSelected");Call(menu,"OnCpuDifficultySelected",Cpu(3));Call(menu,"OnCpuDifficultySelected",Cpu(3));
            Assert.That(Get(menu,"_cpuAttempt"),Is.SameAs(gate));Assert.That(Get(menu,"_cpuDifficulty").ToString(),Is.EqualTo("Normal"));
            foreach(var choice in (Button[])Get(menu,"_cpuChoices"))Assert.That(choice.interactable,Is.False);
            Assert.That(((Button)Get(menu,"_cpuBack")).interactable,Is.True);
            Call(menu,"OnCpuBackSelected");Assert.That(Prop(gate,"State").ToString(),Is.EqualTo("Cancelled"));
            Assert.That(Get(menu,"_isCheckingRoom"),Is.False);Assert.That(Get(menu,"_cpuAttempt"),Is.Null);
            Assert.That(Get(menu,"_cpuChoiceArmed"),Is.False);
            Call(menu,"Close");UnityEngine.Object.Destroy(holder);UnityEngine.Object.Destroy(root);yield return null;
        }
    }
}
