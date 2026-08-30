using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class GameUIVisualController
    {
        public void RebuildAllTilesFromState(List<int> suppressRevealWallIndexes = null, bool forceRebuildInEndPhase = false)
        {
            // --- アニメーション競合防止 ---
            foreach (var cleanup in _activeAnimationCleanups)
            {
                cleanup?.Invoke();
            }
            _activeAnimationCleanups.Clear();

            if (uiManager.IsTransitioning)
            {
                Debug.Log("[GameUIVisualController] IsTransitioning is true. Skipping RebuildAllTilesFromState to prevent visible tile movement.");
                return;
            }

            // Bettingフェーズ中は手牌のリビルドを行わない。
            // リビルドするとDiscardフェーズ用のコンテナからHandSelection用コンテナに牌が移され、
            // 牌が消えてプールに戻ってしまうバグの原因となる。
            if (uiManager.CurrentPhaseStatus == RoundStatus.Betting)
            {
                Debug.Log("[GameUIVisualController] Betting phase. Skipping RebuildAllTilesFromState.");
                return;
            }

            var board = BoardStateManager.Instance;
            List<int> currentWallIds = new List<int>(board.CurrentWallTiles);
            List<int> currentHandIds = new List<int>(board.CurrentHandTiles);

            bool needFullRebuild = true;

            if (_lastWallIds.Count == currentWallIds.Count && _lastHandIds.Count == currentHandIds.Count)
            {
                List<int> oldWallIds = new List<int>(_lastWallIds);
                List<int> newWallIds = new List<int>(currentWallIds);
                foreach (int id in _lastWallIds)
                {
                    if (newWallIds.Contains(id)) { newWallIds.Remove(id); oldWallIds.Remove(id); }
                }

                List<int> oldHandIds = new List<int>(_lastHandIds);
                List<int> newHandIds = new List<int>(currentHandIds);
                foreach (int id in _lastHandIds)
                {
                    if (newHandIds.Contains(id)) { newHandIds.Remove(id); oldHandIds.Remove(id); }
                }

                if (oldWallIds.Count == newWallIds.Count && oldHandIds.Count == newHandIds.Count)
                {
                    bool success = true;
                    for (int i = 0; i < oldWallIds.Count; i++) success &= UpdateTileIdInUI(oldWallIds[i], newWallIds[i]);
                    for (int i = 0; i < oldHandIds.Count; i++) success &= UpdateTileIdInUI(oldHandIds[i], newHandIds[i]);

                    if (success)
                    {
                        needFullRebuild = false;
                    }
                }
            }

            if (forceRebuildInEndPhase)
            {
                needFullRebuild = true;
            }

            _lastWallIds = currentWallIds;
            _lastHandIds = currentHandIds;

            _lastSuppressedIndices.Clear();
            if (uiManager.TilePrefab == null) return;
            
            bool isGameEndPhase = uiManager.CurrentPhaseStatus == RoundStatus.Agari || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Ron || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Result || 
                                  uiManager.CurrentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase && !forceRebuildInEndPhase) return;

            if (needFullRebuild)
            {
                // 1. HandUI / WallUI を一括クリア
                if (uiManager.HandUI != null)
                {
                    for (int i = uiManager.HandUI.GetHandSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.HandUI.GetHandSlots()[i];
                        if (t != null) {
                            poolManager.ReturnTileToPool(t.gameObject);
                        }
                    }
                    uiManager.HandUI.GetHandSlots().Clear();
                }
                if (uiManager.WallUI != null)
                {
                    // 矢印インジケータを牌スロットから安全に切り離してからプールに返す
                    uiManager.WallUI.SetActiveDiscardTile(null);
                    for (int i = uiManager.WallUI.GetWallSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.WallUI.GetWallSlots()[i];
                        if (t != null) {
                            poolManager.ReturnTileToPool(t.gameObject);
                        }
                    }
                    uiManager.WallUI.GetWallSlots().Clear();
                }

                if (uiManager.WallUI != null)
                {
                    List<int> combinedIds = new List<int>(board.CurrentWallTiles);
                    combinedIds.AddRange(board.CurrentHandTiles);

                    List<RectTransform> combinedGenerated = new List<RectTransform>();
                    foreach (var id in combinedIds)
                    {
                        GameObject obj = poolManager.GetTileById(id, transform, uiManager);
                        RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                        if (rt != null)
                        {
                            InitializeTileComponent(rt, id, false);
                            combinedGenerated.Add(rt);
                        }
                    }

                    uiManager.WallUI.LayoutWallTiles(combinedGenerated, combinedIds, board.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);

                    if (uiManager.HandUI != null)
                    {
                        List<int> localExposedActualIds = new List<int>();
                        foreach (int exposedIdx in board.ExposedLocalHandWallIndexes)
                        {
                            if (exposedIdx >= 0 && exposedIdx < board.OriginalWallTiles.Count)
                            {
                                localExposedActualIds.Add(board.OriginalWallTiles[exposedIdx]);
                            }
                        }

                        foreach (var id in board.CurrentHandTiles)
                        {
                            RectTransform rt = uiManager.WallUI.GrabTileById(id);
                            if (rt != null)
                            {
                                InitializeTileComponent(rt, id, true);
                                uiManager.HandUI.AddTileToHand(rt, id);
                            }
                            else
                            {
                                Debug.LogWarning($"[Visual] Failed to grab tile by id: {id} for Local Hand!");
                            }
                        }
                        
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(uiManager.HandUI.GetComponent<RectTransform>());
                        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(uiManager.WallUI.GetComponent<RectTransform>());
                    }
                }

                // 3. Enemy HandUI (フルリビルド)
                if (uiManager.EnemyHandUI != null)
                {
                    for (int i = uiManager.EnemyHandUI.GetHandSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.EnemyHandUI.GetHandSlots()[i];
                        if (t != null) poolManager.ReturnTileToPool(t.gameObject);
                    }
                    uiManager.EnemyHandUI.GetHandSlots().Clear();
                    uiManager.EnemyHandUI.ClearHand();

                    List<int> exposedEncodedIds = new List<int>();
                    foreach (int exposedIdx in board.ExposedEnemyHandWallIndexes)
                    {
                        if (exposedIdx >= 0 && exposedIdx < board.OriginalEnemyWallTiles.Count)
                        {
                            exposedEncodedIds.Add(board.OriginalEnemyWallTiles[exposedIdx]);
                        }
                    }

                    if (board.CurrentEnemyHandTiles != null && board.CurrentEnemyHandTiles.Count > 0 && uiManager.CurrentPhaseStatus >= RoundStatus.Discard)
                    {
                        uiManager.EnemyHandUI.gameObject.SetActive(true);

                        for (int i = 0; i < board.CurrentEnemyHandTiles.Count; i++)
                        {
                            int currentTileVal = board.CurrentEnemyHandTiles[i];
                            GameObject obj = poolManager.GetTileById(currentTileVal, transform, uiManager);
                            RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                            if (rt != null) {
                                int visualId = -1;
                                int actualTileId = currentTileVal;

                                InitializeTileComponent(rt, visualId, false);
                                uiManager.EnemyHandUI.AddEnemyTile(rt, visualId, actualTileId);

                                if (exposedEncodedIds.Contains(actualTileId))
                                {
                                    exposedEncodedIds.Remove(actualTileId);
                                    uiManager.EnemyHandUI.RevealTileByIndex(i);
                                }
                            }
                        }

                        uiManager.EnemyHandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                    }
                }

                // 4. Enemy WallUI (フルリビルド)
                if (uiManager.EnemyWallUI != null)
                {
                    for (int i = uiManager.EnemyWallUI.GetEnemyWallSlots().Count - 1; i >= 0; i--)
                    {
                        Transform t = uiManager.EnemyWallUI.GetEnemyWallSlots()[i];
                        if (t != null) poolManager.ReturnTileToPool(t.gameObject);
                    }
                    uiManager.EnemyWallUI.GetEnemyWallSlots().Clear();

                    bool isEndPhase = uiManager.CurrentPhaseStatus >= RoundStatus.Liquidation;
                    if ((uiManager.CurrentPhaseStatus == RoundStatus.Discard && suppressRevealWallIndexes == null) || isEndPhase)
                    {
                        uiManager.EnemyWallUI.gameObject.SetActive(false);
                    }
                    else
                    {
                        uiManager.EnemyWallUI.gameObject.SetActive(true);
                        List<RectTransform> enemyWallGenerated = new List<RectTransform>();
                        List<int> displayIdsForWall = new List<int>();

                        for (int i = 0; i < board.OriginalEnemyWallTiles.Count; i++)
                        {
                            int actualTileId = board.OriginalEnemyWallTiles[i];

                            if (board.EnemyHandIndexes != null && board.EnemyHandIndexes.Contains(i))
                            {
                                continue;
                            }

                            displayIdsForWall.Add(actualTileId);

                            GameObject obj = poolManager.GetTileById(actualTileId, transform, uiManager);
                            RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                            if (rt != null) {
                                int visualId = -1;
                                bool isExposedInWall = board.ExposedEnemyHandWallIndexes.Contains(i);
                                if (isExposedInWall) visualId = actualTileId;
                                
                                InitializeTileComponent(rt, visualId, false);
                                
                                var interactions = rt.GetComponentsInChildren<TileInteraction>(true);
                                foreach(var interaction in interactions) interaction.enabled = false;
                                var visual = rt.GetComponent<TileVisual>();
                                if (visual != null) visual.SetHoverHighlight(false);
                                var images = rt.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                                foreach(var img in images) img.raycastTarget = false;
                                
                                var btn = rt.gameObject.GetComponent<UnityEngine.UI.Button>();
                                if (btn != null) btn.interactable = false;

                                enemyWallGenerated.Add(rt);
                            }
                        }
                        uiManager.EnemyWallUI.LayoutEnemyWallTiles(enemyWallGenerated, displayIdsForWall, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                    }
                }
            } // End of needFullRebuild
            else
            {
                // 差分同期 (Enemy ツモ処理など)
                if (uiManager.EnemyHandUI != null && uiManager.EnemyWallUI != null)
                {
                    int currentHandCount = uiManager.EnemyHandUI.GetHandSlots().Count;
                    int targetHandCount = board.CurrentEnemyHandTiles != null ? board.CurrentEnemyHandTiles.Count : 0;
                    
                    if (currentHandCount < targetHandCount)
                    {
                        // ツモ（EnemyWall -> EnemyHand）
                        int drawCount = targetHandCount - currentHandCount;
                        for (int i = 0; i < drawCount; i++)
                        {
                            RectTransform drawnTile = uiManager.EnemyWallUI.GrabEnemyTile();
                            if (drawnTile != null)
                            {
                                int newTileId = board.CurrentEnemyHandTiles[currentHandCount + i];
                                InitializeTileComponent(drawnTile, -1, false);
                                uiManager.EnemyHandUI.AddEnemyTile(drawnTile, -1, newTileId);
                            }
                        }
                    }
                }
            }

            // 毎回の更新で、透視状態だけはフルリビルドに関わらず必ず同期する
            SyncLocalExposedState(board);
            SyncEnemyExposedState(board);
            SyncLocalFuritenState(board);

            if (uiManager.WaitUI != null && (uiManager.CurrentPhaseStatus == RoundStatus.Discard || uiManager.CurrentPhaseStatus == RoundStatus.HandSelection))
            {
                // **チュートリアルの手牌選択中は、決定を押すまで待ち牌UIを出さない。**
                // 『おまかせ』は待ち牌を盤面に入れてからここ（牌の再構築）を通るため、
                // 素直に出すと押した瞬間に左下へ現れ、手牌確認のUIと重なる。
                // 決定後は TutorialManager.ConfirmHandSelectionComplete が出す。
                //
                // 出す条件は GameUIPhaseController の手牌選択ケースにもある。
                // **経路が2つあるので、片方だけ塞いでも直らない**（実際にここを見落として空振りした）。
                bool blockedByTutorial = uiManager.IsTutorialMode
                    && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection
                    && (uiManager.TutorialManager == null || !uiManager.TutorialManager.IsHandSelectionConfirmed);

                if (!blockedByTutorial && board.CurrentWaitTiles != null && board.CurrentWaitTiles.Count > 0)
                {
                    uiManager.WaitUI.gameObject.SetActive(true);
                    uiManager.WaitUI.DisplayWaits(board.CurrentWaitTiles);
                }
            }
        }
    }
}
