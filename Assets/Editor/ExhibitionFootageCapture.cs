using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using KillingMahjong.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// 紹介動画用のEditor限定撮影。保存済みシーン・本番通信・判定コードは変更しない。
    /// スキルは別プロセスの実GameEngine/GameSessionから採取した通知を再生する。
    /// </summary>
    public static class ExhibitionFootageCapture
    {
        [Serializable] public class FixtureSet { public SkillFixture[] entries; }
        [Serializable] public class SkillFixture
        {
            public string skill, beforeStatus, cast, afterStatus, win;
            public List<int> localWall, localHand, enemyWall, enemyHand;
            public int hpBefore, enemyHpBefore, countBefore;
        }

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        public static string Status { get; private set; } = "idle";
        private static GameUIManager ui;
        private static TutorialManager tutorial;
        private static TutorialScenario scenario;
        private static string output;

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, Private).SetValue(target, value);
        }
        private static object Call(object target, string name, params object[] args)
        {
            return target.GetType().GetMethod(name, Private).Invoke(target, args);
        }

        private static void Prepare(string directory)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play mode is required");
            output = directory;
            Directory.CreateDirectory(output);
            ui = UnityEngine.Object.FindFirstObjectByType<GameUIManager>();
            tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialManager>();
            if (ui == null || tutorial == null) throw new InvalidOperationException("Open OpeningScene first");
            tutorial.StopAllCoroutines();
            var opening = UnityEngine.Object.FindFirstObjectByType<OpeningSequenceManager>();
            if (opening != null)
            {
                opening.StopAllCoroutines();
                foreach (var name in new[] { "paperObject", "largePaperUI" })
                {
                    var obj = opening.GetType().GetField(name, Private).GetValue(opening) as GameObject;
                    if (obj != null) obj.SetActive(false);
                }
                var enemy = opening.GetType().GetField("enemyCharacterObj", Private).GetValue(opening) as GameObject;
                if (enemy != null) enemy.SetActive(true);
            }
            LoadingManager.Instance?.ForceHide();
            scenario = TutorialScenario.BuildDefault();
            Set(tutorial, "_scenario", scenario);
            Set(tutorial, "_round", scenario.rounds[0]);
            Set(tutorial, "_firstRoundChromeHidden", false);
            Set(tutorial, "_isWaitingForLine", false);
            ui.IsTutorialMode = true;
            ui.TutorialManager = tutorial;
            var network = NetworkMessageHandler.Instance;
            network.SetLocalPlayerId("self");
            var debug = new GameObject("CaptureTransport").AddComponent<DebugWebSocketClient>();
            Set(network, "useDebugClient", true);
            Set(network, "debugWebSocketClient", debug);
            ui.DialogueUI.HideAdvanceOnAnyClick();
            ui.DialogueUI.gameObject.SetActive(false);
            Call(tutorial, "ClearGuide");
            foreach (var item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (item.name == "CursorImage") item.gameObject.SetActive(false);
        }

        public static string StartSkills(string directory, string fixturePath, string onlySkill = null)
        {
            Prepare(directory);
            var fixtures = JsonUtility.FromJson<FixtureSet>(File.ReadAllText(fixturePath));
            Status = "starting";
            ui.StartCoroutine(SkillsRoutine(fixtures, onlySkill));
            return Status;
        }

        private static IEnumerator SkillsRoutine(FixtureSet fixtures, string onlySkill)
        {
            foreach (var fixture in fixtures.entries)
            {
                // 特殊勝利は現行の能力一覧から廃止済み。展示動画へ混ぜない。
                if (fixture.skill == "special_victory") continue;
                if (onlySkill != null && fixture.skill != onlySkill) continue;
                Status = "preparing " + fixture.skill;
                ui.ClearAllTiles();
                var board = BoardStateManager.Instance;
                board.ClearAllBoardData();
                board.SetLocalState(fixture.localWall, fixture.localHand);
                board.SetEnemyState(fixture.enemyWall, fixture.enemyHand);
                Set(tutorial, "_playerHp", fixture.hpBefore);
                Set(tutorial, "_enemyHp", fixture.enemyHpBefore);
                Call(tutorial, "SetPhase", RoundStatus.HandSelection);
                Call(tutorial, "SetBoardVisible", true);
                NetworkMessageHandler.Instance.ProcessServerMessage(fixture.beforeStatus);
                ui.PhaseTransitionUI.gameObject.SetActive(true);
                ui.AbilityUI.gameObject.SetActive(true);
                ui.AbilityUI.IsDisplayOnly = false;
                ui.AbilityUI.SetAbilityHidden("assault", false);
                ui.VisualController.RebuildAllTilesFromState();
                ui.DialogueUI.gameObject.SetActive(false);
                yield return new WaitForSeconds(1f);
                var path = Path.Combine(output, fixture.skill + "_raw.mp4");
                string started = VideoCaptureTool.StartRecording(path, 800, 600, 60, 0f);
                if (!started.StartsWith("STARTED")) { Status = started; yield break; }
                Status = "recording " + fixture.skill;
                ui.AbilityUI.OpenWindow();
                var itemRect = ui.AbilityUI.GetAbilityItemRect(fixture.skill);
                var item = itemRect != null ? itemRect.GetComponent<AbilityItemUI>() : null;
                if (item != null) ui.AbilityUI.OnAbilitySelected(item);
                yield return new WaitForSeconds(3f);
                ui.AbilityUI.CloseWindow(false);
                NetworkMessageHandler.Instance.ProcessServerMessage(fixture.cast);
                // 役強化の演出は実statusを待つ。比較前の状態を残してから通知を渡す。
                yield return new WaitForSeconds(0.25f);
                if (fixture.skill == "boost_hand")
                    NetworkMessageHandler.Instance.ProcessServerMessage(fixture.afterStatus);
                float deadline = Time.time + 25f;
                while (ui.IsTransitioning && Time.time < deadline) yield return null;
                if (ui.IsTransitioning)
                {
                    VideoCaptureTool.StopRecording();
                    Status = "ERROR: transition timed out: " + fixture.skill;
                    yield break;
                }
                if (fixture.skill != "boost_hand")
                    NetworkMessageHandler.Instance.ProcessServerMessage(fixture.afterStatus);
                yield return new WaitForSeconds(1f);
                yield return new WaitForSeconds(3f);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, fixture.skill + "_result.png"));
                yield return null;
                VideoCaptureTool.StopRecording();
                ui.YakuListUI.CloseYakuList();
                yield return new WaitForSeconds(1f);
            }
            Status = "complete";
        }

        public static string StartRules(string directory)
        {
            Prepare(directory);
            ui.StartCoroutine(RulesRoutine());
            return "starting rules";
        }

        private static void Record(string name)
        {
            Status = "recording " + name;
            var result = VideoCaptureTool.StartRecording(Path.Combine(output, name + "_raw.mp4"), 800, 600, 60, 0f);
            if (!result.StartsWith("STARTED")) throw new InvalidOperationException(result);
        }

        private static IEnumerator Finish(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + "_result.png"));
            yield return null;
            VideoCaptureTool.StopRecording();
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator RulesRoutine()
        {
            var data = JsonUtility.FromJson<TutorialRoundData>(JsonUtility.ToJson(scenario.rounds[0]));
            data.allowManualHandSelection = true;
            Set(tutorial, "_round", data);
            Set(tutorial, "_playerHp", 20000);
            Set(tutorial, "_enemyHp", 20000);
            BoardStateManager.Instance.ClearAllBoardData();
            ui.YakuListUI.UpdateBoostData(null, null);
            ui.AbilityUI.SetBellVisible(false);
            Call(tutorial, "SetupBoard", data, null);
            Call(tutorial, "SetBoardVisible", true);
            yield return new WaitForSeconds(1f);
            Record("rules_hand");
            yield return new WaitForSeconds(1f);
            foreach (int tile in TutorialTiles.EncodeAll(data.manganHandBaseIds, data.doraBaseId))
            {
                ui.MoveTileToHand(tile);
                yield return new WaitForSeconds(0.45f);
            }
            yield return new WaitForSeconds(2f);
            yield return Finish("rules_hand");

            Call(tutorial, "SetPhase", RoundStatus.Betting);
            ui.BettingUI.ShowFixedBettingPhase(20000, 20000, data.betAmount, amount => { });
            yield return new WaitForSeconds(1f);
            Record("rules_bet");
            yield return new WaitForSeconds(3f);
            ui.BettingUI.ConfirmButtonRect.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            ui.BettingUI.HideBettingPhase();
            Call(tutorial, "PlaceBet", data.betAmount);
            yield return new WaitForSeconds(2f);
            yield return Finish("rules_bet");

            Call(tutorial, "SetPhase", RoundStatus.Discard);
            Call(tutorial, "SetBoardVisible", true);
            yield return new WaitForSeconds(1f);
            Record("rules_discard");
            for (int i = 0; i < 4; i++)
            {
                BoardStateManager.Instance.SetLocalTurn(true);
                yield return (IEnumerator)Call(tutorial, "AutoDiscardForPlayer");
                yield return new WaitForSeconds(0.8f);
                BoardStateManager.Instance.SetLocalTurn(false);
                ui.EnemyRiverUI.AddTile(TutorialTiles.Encode(data.enemyDiscardBaseIds[i], false));
                yield return new WaitForSeconds(0.8f);
            }
            yield return new WaitForSeconds(1f);
            yield return Finish("rules_discard");

            Record("rules_ron");
            int ron = TutorialTiles.Encode(data.playerWinningTileBaseId, false);
            ui.EnemyRiverUI.AddTile(ron);
            ui.StartCoroutine(ConfirmRonButtons());
            yield return (IEnumerator)Call(tutorial, "RunPlayerRon", data, ron);
            yield return new WaitForSeconds(2f);
            yield return Finish("rules_ron");

            var draw = JsonUtility.FromJson<TutorialRoundData>(JsonUtility.ToJson(scenario.rounds[1]));
            Set(tutorial, "_round", draw);
            Set(tutorial, "_playerHp", 20000);
            Set(tutorial, "_enemyHp", 20000);
            Call(tutorial, "SetupBoard", draw, draw.manganHandBaseIds);
            Call(tutorial, "PlaceBet", draw.betAmount);
            Call(tutorial, "SetPhase", RoundStatus.Discard);
            Call(tutorial, "SetBoardVisible", true);
            yield return new WaitForSeconds(1f);
            Record("rules_draw");
            for (int i = 0; i < 17; i++)
            {
                BoardStateManager.Instance.SetLocalTurn(true);
                yield return (IEnumerator)Call(tutorial, "AutoDiscardForPlayer");
                yield return new WaitForSeconds(0.18f);
                BoardStateManager.Instance.SetLocalTurn(false);
                ui.EnemyRiverUI.AddTile(TutorialTiles.Encode(draw.enemyDiscardBaseIds[i], false));
                yield return new WaitForSeconds(0.18f);
            }
            yield return (IEnumerator)Call(tutorial, "RunDraw", draw);
            yield return new WaitForSeconds(2f);
            yield return Finish("rules_draw");
            Status = "rules complete";
        }

        private static IEnumerator ConfirmRonButtons()
        {
            float end = Time.time + 60f;
            while (Time.time < end)
            {
                yield return new WaitForSeconds(1f);
                foreach (var button in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
                {
                    if (!button.isActiveAndEnabled || !button.interactable) continue;
                    var label = button.GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null && (label.text == "ロン" || label.text == "OK" || label.text == "閉じる"))
                        button.onClick.Invoke();
                }
            }
        }
    }
}
