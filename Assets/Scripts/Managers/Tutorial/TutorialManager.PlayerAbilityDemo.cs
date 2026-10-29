using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;
using KillingMahjong.Network;
using KillingMahjong.UI;
using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private readonly System.Random _introSkillRandom = new System.Random();
        private int _introPerspectiveUses;
        private bool _introChoiceOpen, _introYakuOpen;
        private System.Action CaptureIntroDemoBoard()
        {
            var board = BoardStateManager.Instance;
            if (board == null) return () => { };
            var hand = new List<int>(board.CurrentHandTiles);
            var waits = new List<int>(board.CurrentWaitTiles);
            var enemyWall = new List<int>(board.OriginalEnemyWallTiles);
            var enemyHand = new List<int>(board.CurrentEnemyHandTiles);
            var enemyWaits = new List<int>(board.CurrentEnemyWaitTiles);
            var exposed = new HashSet<int>(board.ExposedEnemyHandWallIndexes);
            return () =>
            {
                if (board == null) return;
                // 確定した牌交換・役強化と、消費した持ち点は残す。公開と手牌の実演だけを戻す。
                // **役強化も残す**（2026-10-29 バグ報告「役強化がチュートリアル画面に反映されていない」）。
                // 以前は実演前の強化に戻していて、点を払ったのに役一覧から +1 が消えていた
                board.SetLocalState(new List<int>(board.OriginalWallTiles), hand, waits);
                // SetEnemyStateは空のwallを「既存の山を維持」と扱うので、未配牌へ戻す場合も先に除く。
                board.OriginalEnemyWallTiles.Clear();
                board.CurrentEnemyWallTiles.Clear();
                board.SetEnemyState(enemyWall, enemyHand, enemyWaits);
                board.ExposedEnemyHandWallIndexes.Clear();
                board.ExposedEnemyHandWallIndexes.UnionWith(exposed);
                if (gameUIManager != null)
                {
                    gameUIManager.YakuListUI?.UpdateBoostData(board.LocalBoostHandBonus, board.EnemyBoostHandBonus);
                    gameUIManager.RefreshBoardAfterTransition();
                }
            };
        }

        private bool CanActivateIntroSkill(string type)
        {
            if (_aborted || _round == null || gameUIManager == null || !gameUIManager.IsTutorialMode
                || gameUIManager.SkillController == null || BoardStateManager.Instance == null) return false;
            if (type != SkillNames.Mulligan && type != SkillNames.Perspective && type != SkillNames.BoostHand) return false;
            if (type == SkillNames.Perspective && _introPerspectiveUses >= 5) return false;
            if (type == SkillNames.Mulligan && GetIntroMulliganReplacements() == null) return false;
            int floor = _prevRoundWasDraw ? _playerStake + _lastBetAmount : 0;
            return _playerHp - GameRules.GetSkillCost(type, 0) >= floor;
        }

        // オフラインの紹介だけに使う台本応答。本編の判定・通信には持ち込まない。
        private IEnumerator PlayIntroSkillEffect(string type, string suit, System.Action<bool> onCompleted)
        {
            onCompleted(false);
            var board = BoardStateManager.Instance;
            if (!CanActivateIntroSkill(type)) yield break;
            List<int> outgoing = null;
            List<int> mulliganOptions = null;
            string yaku = null;
            if (type == SkillNames.Mulligan)
            {
                var controller = gameUIManager.SkillController;
                System.Action<List<int>> picked = indexes => outgoing = new List<int>(indexes);
                controller.TutorialMulliganPicked = picked;
                controller.TutorialMulliganCanPick = CanPickIntroMulliganWallIndex;
                try
                {
                    controller.StartMulliganSelection();
                    yield return new WaitUntil(() => outgoing != null || !controller.IsMulliganSelection || _aborted);
                }
                finally
                {
                    if (controller != null && controller.TutorialMulliganPicked == picked)
                    {
                        controller.TutorialMulliganPicked = null;
                        controller.TutorialMulliganCanPick = null;
                    }
                    controller?.CancelSkillSelection();
                }
                if (_aborted || outgoing == null || outgoing.Count != MulliganChoiceUI.KeepCount) yield break;
                // 支払う前に候補まで確保する。対象の重複や不正な位置では発動しない。
                var unique = new HashSet<int>(outgoing);
                if (unique.Count != outgoing.Count) yield break;
                mulliganOptions = new List<int>();
                foreach (int index in outgoing)
                {
                    if (index < 0 || index >= board.OriginalWallTiles.Count) yield break;
                    mulliganOptions.Add(board.OriginalWallTiles[index]);
                }
                var replacements = GetIntroMulliganReplacements();
                if (replacements == null) yield break;
                mulliganOptions.AddRange(replacements);
            }
            else if (type == SkillNames.BoostHand)
            {
                bool decided = false;
                var selection = YakuSelectionUI.FindOrCreate();
                _introYakuOpen = true;
                try
                {
                    selection.Show(value => { yaku = value; decided = true; }, () => decided = true,
                        gameUIManager.TileResourceManager, board.LocalBoostHandBonus);
                    yield return new WaitUntil(() => decided || _aborted);
                }
                finally { _introYakuOpen = false; if (selection != null) selection.Hide(); }
                if (_aborted || string.IsNullOrEmpty(yaku)) yield break;
                if (board.LocalBoostHandBonus == null) board.LocalBoostHandBonus = new Dictionary<string, int>();
                if (board.LocalBoostHandBonus.TryGetValue(yaku, out int already) && already > 0) yield break;
            }

            // 対象選択が終わるまでは一切支払わない。キャンセルではHPも使用回数も変わらない。
            if (!CanActivateIntroSkill(type)) yield break;
            int cost = GameRules.GetSkillCost(type, 0);
            int remaining = _playerHp - cost;
            string local = NetworkMessageHandler.Instance != null ? NetworkMessageHandler.Instance.LocalPlayerId : "tutorial-local";
            var cast = new SkillCastedData { player_id = local, skillType = type, cost = cost, health = remaining, yaku_name = yaku };
            if (type == SkillNames.Perspective)
            {
                if (suit != null && suit != "manzu" && suit != "pinzu" && suit != "souzu" && suit != "jihai") yield break;
                var hidden = new List<int>();
                for (int i = 0; i < board.OriginalEnemyWallTiles.Count; i++)
                {
                    if (!board.ExposedEnemyHandWallIndexes.Contains(i)
                        && TutorialSkillStock.MatchesSuit(board.OriginalEnemyWallTiles[i], suit)) hidden.Add(i);
                }
                cast.exposedHandIndexes = TutorialSkillStock.Draw(hidden, Mathf.Min(3, hidden.Count), _introSkillRandom);
                _introPerspectiveUses++;
            }
            else if (type == SkillNames.BoostHand)
            {
                board.LocalBoostHandBonus[yaku] = 1;
                gameUIManager.YakuListUI?.UpdateBoostData(board.LocalBoostHandBonus, board.EnemyBoostHandBonus);
            }
            // 受理した時点で台本側も確定する。表示は共通の発動演出内で減らす。
            // 演出中に中断されても、支払済みの値と次の分岐条件を食い違わせない。
            _playerHp = remaining;
            var phase = gameUIManager.PhaseTransitionUI;
            bool phaseWasActive = phase != null && phase.gameObject.activeSelf;
            if (phase != null && !phaseWasActive) { phase.gameObject.SetActive(true); yield return null; }
            try
            {
                yield return gameUIManager.SkillController.PlayTutorialSkillRoutine(cast);
                ApplyHpToUI();
                if (type == SkillNames.Mulligan)
                    yield return ChooseIntroMulliganTiles(outgoing, mulliganOptions);
                if (!_aborted) onCompleted(true);
            }
            finally { if (phase != null && !phaseWasActive) phase.gameObject.SetActive(false); }
        }

        private IEnumerator ChooseIntroMulliganTiles(List<int> outgoing, List<int> options)
        {
            var choice = MulliganChoiceUI.FindOrCreate();
            List<int> selected = null;
            _introChoiceOpen = true;
            try
            {
                choice.Show(options, gameUIManager.TileResourceManager, values => selected = values);
                yield return new WaitUntil(() => selected != null || _aborted);
                if (_aborted) yield break;
                var order = GameUISkillController.ArrangeTutorialMulliganChoices(selected);
                var changes = new List<MulliganChangeData>();
                for (int i = 0; i < outgoing.Count; i++) changes.Add(new MulliganChangeData {
                    wallIndex = outgoing[i], oldTile = options[i], newTile = options[order[i]],
                });
                yield return gameUIManager.SkillController.ApplyTutorialMulliganChoice(
                    new MulliganSelectedData { changes = changes });
            }
            finally { _introChoiceOpen = false; if (choice != null) choice.Hide(); }
        }

        private void ClearIntroSkillSelection()
        {
            var controller = gameUIManager != null ? gameUIManager.SkillController : null;
            if (controller != null)
            {
                controller.TutorialMulliganPicked = null;
                controller.TutorialMulliganCanPick = null;
                controller.CancelSkillSelection();
            }
            if (_introChoiceOpen) FindFirstObjectByType<MulliganChoiceUI>(FindObjectsInactive.Include)?.Hide();
            if (_introYakuOpen) FindFirstObjectByType<YakuSelectionUI>(FindObjectsInactive.Include)?.Hide();
            _introChoiceOpen = _introYakuOpen = false;
        }

        private string IntroSkillUnusableReason(string type)
        {
            if (type == SkillNames.Mulligan && GetIntroMulliganReplacements() == null)
                return "交換できる予備牌がありません。";
            if (type == SkillNames.Perspective && _introPerspectiveUses >= 5)
                return "透視は1局に5回までです。";
            return CanActivateIntroSkill(type) ? null : "持ち点が足りません。";
        }

        private void OnDisable()
        {
            _aborted = true;
            CloseIntro();
            ClearIntroSkillSelection();
            if (gameUIManager != null && gameUIManager.AbilityUI != null)
            {
                gameUIManager.AbilityUI.TutorialActivationHandler = null;
                gameUIManager.AbilityUI.TutorialUnusableReason = null;
            }
        }
    }
}
