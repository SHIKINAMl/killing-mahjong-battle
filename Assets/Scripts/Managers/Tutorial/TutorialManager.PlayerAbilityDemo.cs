using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;
using KillingMahjong.Network;
using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private System.Action CaptureIntroDemoBoard()
        {
            var board = BoardStateManager.Instance;
            if (board == null) return () => { };
            var wall = new List<int>(board.OriginalWallTiles);
            var hand = new List<int>(board.CurrentHandTiles);
            var waits = new List<int>(board.CurrentWaitTiles);
            var enemyWall = new List<int>(board.OriginalEnemyWallTiles);
            var enemyHand = new List<int>(board.CurrentEnemyHandTiles);
            var enemyWaits = new List<int>(board.CurrentEnemyWaitTiles);
            var exposed = new HashSet<int>(board.ExposedEnemyHandWallIndexes);
            var bonus = board.LocalBoostHandBonus == null ? null : new Dictionary<string, int>(board.LocalBoostHandBonus);
            return () =>
            {
                if (board == null) return;
                // 消費した持ち点は残し、実演の牌・公開・強化だけを元に戻す。
                board.SetLocalState(wall, hand, waits);
                // SetEnemyStateは空のwallを「既存の山を維持」と扱うので、未配牌へ戻す場合も先に除く。
                board.OriginalEnemyWallTiles.Clear();
                board.CurrentEnemyWallTiles.Clear();
                board.SetEnemyState(enemyWall, enemyHand, enemyWaits);
                board.ExposedEnemyHandWallIndexes.Clear();
                board.ExposedEnemyHandWallIndexes.UnionWith(exposed);
                board.LocalBoostHandBonus = bonus;
                if (gameUIManager != null)
                {
                    gameUIManager.YakuListUI?.UpdateBoostData(bonus, board.EnemyBoostHandBonus);
                    gameUIManager.RefreshBoardAfterTransition();
                }
            };
        }

        private bool CanActivateIntroSkill(string type)
        {
            if (_aborted || _round == null || gameUIManager == null || !gameUIManager.IsTutorialMode
                || gameUIManager.SkillController == null || BoardStateManager.Instance == null) return false;
            if (type != SkillNames.Mulligan && type != SkillNames.Perspective && type != SkillNames.BoostHand) return false;
            int floor = _prevRoundWasDraw ? _playerStake + _lastBetAmount : 0;
            return _playerHp - GameRules.GetSkillCost(type, 0) >= floor;
        }

        // オフラインの紹介だけに使う台本応答。本編の判定・通信には持ち込まない。
        private IEnumerator PlayIntroSkillEffect(string type)
        {
            var board = BoardStateManager.Instance;
            if (!CanActivateIntroSkill(type)) yield break;
            int cost = GameRules.GetSkillCost(type, 0);
            int remaining = _playerHp - cost;
            string local = NetworkMessageHandler.Instance != null ? NetworkMessageHandler.Instance.LocalPlayerId : "tutorial-local";
            var casts = new List<SkillCastedData>();
            var cast = new SkillCastedData { player_id = local, skillType = type, cost = cost, health = remaining };
            casts.Add(cast);
            if (type == SkillNames.Perspective)
            {
                if (board.OriginalEnemyWallTiles.Count == 0)
                {
                    // 敵の牌がまだ配られていない紹介時は、台本の山を実演用に借りる。
                    var wall = TutorialTiles.EncodeAll(_round.wallBaseIds, _round.doraBaseId);
                    board.SetEnemyState(wall, new List<int>());
                }
                cast.exposedHandIndexes = new List<int>();
                for (int i = 0; i < board.OriginalEnemyWallTiles.Count && cast.exposedHandIndexes.Count < 3; i++)
                    if (!board.ExposedEnemyHandWallIndexes.Contains(i)) cast.exposedHandIndexes.Add(i);
            }
            else if (type == SkillNames.BoostHand)
            {
                cast.yaku_name = CurrentHandYaku.Count > 0 ? YakuNameUtil.ToVoiceKey(CurrentHandYaku[0]) : "混一色";
                if (board.LocalBoostHandBonus == null) board.LocalBoostHandBonus = new Dictionary<string, int>();
                board.LocalBoostHandBonus.TryGetValue(cast.yaku_name, out int bonus);
                board.LocalBoostHandBonus[cast.yaku_name] = bonus + 1;
                gameUIManager.YakuListUI?.UpdateBoostData(board.LocalBoostHandBonus, board.EnemyBoostHandBonus);
            }
            else
            {
                // 紹介では山の1枚だけを交換し、交換演出も1回にする。
                for (int i = 0; i < board.OriginalWallTiles.Count; i++)
                {
                    int old = board.OriginalWallTiles[i];
                    if (board.CurrentHandTiles.Contains(old)) continue;
                    cast.mulliganResult = new MulliganResultData { targetHandIndex = i, oldTile = old,
                        newTile = TutorialTiles.Encode((TutorialTiles.BaseOf(old) + 1) % 34) };
                    break;
                }
            }
            var phase = gameUIManager.PhaseTransitionUI;
            bool phaseWasActive = phase != null && phase.gameObject.activeSelf;
            if (phase != null && !phaseWasActive) { phase.gameObject.SetActive(true); yield return null; }
            try
            {
                for (int i = 0; i < casts.Count; i++)
                    yield return gameUIManager.SkillController.PlayTutorialSkillRoutine(casts[i], i > 0);
                _playerHp = remaining;
                ApplyHpToUI();
            }
            finally { if (phase != null && !phaseWasActive) phase.gameObject.SetActive(false); }
        }
    }
}
