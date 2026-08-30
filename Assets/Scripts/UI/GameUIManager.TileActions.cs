using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 牌の選択・移動・打牌など、盤面操作のエントリーポイント。GameUIManager から分離（partial）。
    // クラス・namespace・[SerializeField] は変えていないのでシーン参照には影響しない。
    public partial class GameUIManager
    {
        // --- Entry points from external classes / old API ---

        public void ApplyGameStateFromJSON(string jsonString, string localPlayerId)
        {
            NetworkMessageHandler.Instance.SetLocalPlayerId(localPlayerId);
            NetworkMessageHandler.Instance.ProcessServerMessage(jsonString);
        }

        public void SendActionToServer(string actionType, ActionPayload dataPayload)
        {
            NetworkMessageHandler.Instance.SendActionToServer(actionType, dataPayload);
        }

        // --- UI interaction wrappers bridging to BoardStateManager ---

        public void MoveTileToHand(int tileId)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted)
            {
                Debug.Log("[GameUIManager] MoveTileToHand aborted. HandUI is already submitted.");
                return;
            }

            if (IsTutorialMode && TutorialManager != null)
            {
                if (!TutorialManager.OnTryMoveTile(tileId, toHand: true)) return;
            }

            Debug.Log($"[GameUIManager] Executing BoardStateManager.MoveTileToHand({tileId})");
            BoardStateManager.Instance.TargetHandIndexes = null;

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() =>
                {
                    BoardStateManager.Instance.MoveTileToHand(tileId);
                    ClearSelection();
                }));
            }
            else
            {
                BoardStateManager.Instance.MoveTileToHand(tileId);
                ClearSelection();
            }
        }

        public void MoveTileToWall(int tileId)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;

            if (IsTutorialMode && TutorialManager != null)
            {
                if (!TutorialManager.OnTryMoveTile(tileId, toHand: false)) return;
            }

            BoardStateManager.Instance.TargetHandIndexes = null;

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() =>
                {
                    BoardStateManager.Instance.MoveTileToWall(tileId);
                    ClearSelection();
                }));
            }
            else
            {
                BoardStateManager.Instance.MoveTileToWall(tileId);
                ClearSelection();
            }
        }

        public void SelectManganHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;

            if (IsTutorialMode && TutorialManager != null)
            {
                TutorialManager.ApplyMockAutoMangan();
                return;
            }

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() =>
                {
                    BoardStateManager.Instance.SelectManganHand();
                }));
            }
            else
            {
                BoardStateManager.Instance.SelectManganHand();
            }
        }

        public void SelectRandomHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() =>
                {
                    BoardStateManager.Instance.SelectRandomHand();
                }));
            }
            else
            {
                BoardStateManager.Instance.SelectRandomHand();
            }
        }

        public void SelectTile(int tileId, bool isInHand, bool multiSelect)
        {
            if (currentPhaseStatus == RoundStatus.Discard && !BoardStateManager.Instance.IsLocalTurn) return;

            BoardStateManager.Instance.SelectTile(tileId, multiSelect);
            DeselectAbility();
        }

        public void SelectTiles(List<int> ids)
        {
            if (currentPhaseStatus == RoundStatus.Discard && !BoardStateManager.Instance.IsLocalTurn) return;
            BoardStateManager.Instance.SelectTiles(ids);
            DeselectAbility();
        }

        public void ClearSelection()
        {
            BoardStateManager.Instance.ClearSelection();
        }

        public bool IsTileSelected(int tileId)
        {
            return BoardStateManager.Instance.IsTileSelected(tileId);
        }

        public void DiscardSelectedTile()
        {
            if (currentPhaseStatus != RoundStatus.Discard) return;
            if (!BoardStateManager.Instance.IsLocalTurn) return;
            if (BoardStateManager.Instance.SelectedTileIds.Count == 0) return;
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;

            int tileToDiscard = BoardStateManager.Instance.SelectedTileIds[0];

            if (IsTutorialMode && TutorialManager != null)
            {
                bool allowDiscard = TutorialManager.OnTryDiscardTile(tileToDiscard);
                if (!allowDiscard)
                {
                    ClearSelection();
                    return; // 指定牌以外は打てない
                }

                if (playerInfoUI != null) playerInfoUI.StopTurnTimer();
                BoardStateManager.Instance.SetLocalTurn(false);
                ClearSelection();

                // 疑似的に河へ移動
                if (wallUI != null)
                {
                    RectTransform tileRt = wallUI.GrabTileById(tileToDiscard);
                    if (tileRt != null)
                    {
                        if (riverUI != null) riverUI.AddExistingTile(tileRt, tileToDiscard);
                    }
                    else
                    {
                        if (riverUI != null) riverUI.AddTile(tileToDiscard);
                    }
                    wallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }

                if (KillingMahjong.Managers.AudioManager.Instance != null)
                    KillingMahjong.Managers.AudioManager.Instance.PlayDiscardSE(KillingMahjong.Managers.AudioManager.Instance.discardSE);

                return;
            }

            if (playerInfoUI != null) playerInfoUI.StopTurnTimer();

            BoardStateManager.Instance.SetLocalTurn(false);

            int wallIndex = BoardStateManager.Instance.FindAvailableWallIndex(tileToDiscard);
            if (wallIndex < 0) wallIndex = tileToDiscard;

            BoardStateManager.Instance.MarkWallIndexAsDiscarded(wallIndex);
            SendActionToServer("discard", new ActionPayload { wall_index = wallIndex, tile = tileToDiscard });
            ClearSelection();
        }

        public void CompleteHandSelection()
        {
            if (IsTutorialMode && TutorialManager != null)
            {
                if (!TutorialManager.OnTryCompleteHandSelection()) return;
            }
            HandSelectionController?.CompleteHandSelection();
        }

        public void DeselectAbility()
        {
            if (abilityUI != null) abilityUI.DeselectAll();
        }

        public void OnTileHoverEnter(TileInteraction interaction)
        {
            if (currentPhaseStatus == RoundStatus.Discard && BoardStateManager.Instance.IsLocalTurn && !interaction.IsInHand && interaction.TileId != -1)
            {
                if (wallUI != null) wallUI.SetActiveDiscardTile(interaction);

                // 牌の上を行ったり来たりしている（Tile_HoverHesitation）。
                // 自分の手番の打牌フェイズだけ数える。それ以外は普通にカーソルが通るだけ
                var watcher = KillingMahjong.Managers.PlayerActivityWatcher.Instance;
                if (watcher != null) watcher.NotifyTileHover();
            }
        }

        public void OnTileHoverExit(TileInteraction interaction)
        {
            // Do nothing
        }

        /// <summary>
        /// 「この牌が通った」の推理表示。無ければ実行時に作る。
        /// 判定はサーバー任せで、こちらは見えている打牌から候補を数えるだけ。
        /// </summary>
        private WaitDeductionUI _waitDeduction;
        public WaitDeductionUI WaitDeduction
        {
            get
            {
                if (_waitDeduction == null) _waitDeduction = GetComponentInChildren<WaitDeductionUI>(true);
                if (_waitDeduction == null)
                {
                    var go = new GameObject("WaitDeduction");
                    go.transform.SetParent(transform, false);
                    _waitDeduction = go.AddComponent<WaitDeductionUI>();
                }
                return _waitDeduction;
            }
        }

        /// <summary>
        /// 獲得ポイントのゲージ（左＝相手／右＝自分）。無ければ実行時に作る。
        /// 「30000で勝ち」の判定自体はサーバーの担当で、ここは積み上げを見せるだけ。
        /// </summary>
        private ScoreGaugeUI _scoreGauge;
        public ScoreGaugeUI ScoreGauge
        {
            get
            {
                if (_scoreGauge == null) _scoreGauge = GetComponentInChildren<ScoreGaugeUI>(true);
                if (_scoreGauge == null)
                {
                    var go = new GameObject("ScoreGauge");
                    go.transform.SetParent(transform, false);
                    _scoreGauge = go.AddComponent<ScoreGaugeUI>();
                }
                return _scoreGauge;
            }
        }

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            BoardStateManager.Instance.LastDiscardedTileId = discardedTileId;

            // 通った牌・相手が切った牌のどちらも「相手の待ちではない」情報になる。
            // ロン成立時は局が終わって次局でリセットされるので、ここで弾く必要はない。
            if (!IsTutorialMode) WaitDeduction.RegisterDiscard(discardedTileId, isLocalPlayer);

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayDiscardSE(KillingMahjong.Managers.AudioManager.Instance.discardSE);

            bool isGameEndPhase = currentPhaseStatus == RoundStatus.Agari ||
                                  currentPhaseStatus == RoundStatus.Ron ||
                                  currentPhaseStatus == RoundStatus.Result ||
                                  currentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            if (playerInfoUI != null) playerInfoUI.SetDiscardingState(false);
            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);

            if (isLocalPlayer)
            {
                // 打牌した瞬間に自分のターンは終了したとみなしてUIを更新する
                BoardStateManager.Instance.SetLocalTurn(false);
                BoardStateManager.Instance.RemoveTileFromWall(discardedTileId);

                if (wallUI != null && riverUI != null)
                {
                    RectTransform tileRt = wallUI.GrabTile(discardedTileId);
                    if (tileRt != null)
                    {
                        riverUI.AddExistingTile(tileRt, discardedTileId);
                    }
                    else
                    {
                        riverUI.AddTile(discardedTileId); // fallback
                    }

                    wallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }
            }
            else
            {
                BoardStateManager.Instance.RemoveTileFromEnemyWall();

                if (enemyWallUI != null && enemyRiverUI != null)
                {
                    RectTransform tileRt = enemyWallUI.GrabEnemyTile();
                    if (tileRt != null)
                    {
                        enemyRiverUI.AddExistingTile(tileRt, discardedTileId);
                    }
                    else
                    {
                        enemyRiverUI.AddTile(discardedTileId); // fallback
                    }
                }
            }

            // 手番が来てから切るまでの速さを見る（Tile_InstantDiscard）。
            // CheckDiscardConditions より先に伝えて、判定に間に合わせる
            if (isLocalPlayer)
            {
                var watcher = KillingMahjong.Managers.PlayerActivityWatcher.Instance;
                if (watcher != null) watcher.NotifyLocalDiscard();
            }

            ReactionController.Instance.CheckDiscardConditions(discardedTileId, isLocalPlayer);
        }

        public void ClearAllTiles()
        {
            if (handUI != null)
            {
                foreach (RectTransform t in handUI.GetHandSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                handUI.GetHandSlots().Clear();
            }
            if (wallUI != null)
            {
                foreach (Transform t in wallUI.GetWallSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                wallUI.GetWallSlots().Clear();
            }
            if (enemyHandUI != null)
            {
                // ClearHand() はリストをクリアするだけなので、先にGameObjectをプールに返却する。
                // （返却しないと前局の敵手牌が孤児化して画面に残り続ける）
                foreach (RectTransform t in enemyHandUI.GetHandSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                enemyHandUI.ClearHand();
            }
            if (enemyWallUI != null)
            {
                foreach (Transform t in enemyWallUI.GetEnemyWallSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                enemyWallUI.GetEnemyWallSlots().Clear();
            }
            if (riverUI != null) riverUI.Clear();
            if (enemyRiverUI != null) enemyRiverUI.Clear();

            if (waitUI != null) waitUI.gameObject.SetActive(false);

            // 牌をすべてプールへ返したので、差分リビルドの前提（UIに前回の牌が残っている）が崩れる。
            // 無効化しておかないと、返却前と牌の構成が偶然一致したときに
            // 「変化なし＝再生成不要」と誤判定され、盤面が空のままになる。
            if (VisualController != null) VisualController.InvalidateRebuildCache();

            Managers.BoardStateManager.Instance.ClearAllBoardData();
        }
    }
}
