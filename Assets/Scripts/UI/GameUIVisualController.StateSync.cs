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
        private void SyncLocalExposedState(Managers.BoardStateManager board)
        {
            List<int> localExposedActualIds = new List<int>();
            foreach (int exposedIdx in board.ExposedLocalHandWallIndexes)
            {
                if (exposedIdx >= 0 && exposedIdx < board.OriginalWallTiles.Count)
                {
                    localExposedActualIds.Add(board.OriginalWallTiles[exposedIdx]);
                }
            }

            if (uiManager.HandUI != null)
            {
                foreach (var rt in uiManager.HandUI.GetHandSlots())
                {
                    if (rt == null) continue;
                    var interaction = rt.GetComponent<TileInteraction>();
                    var visual = rt.GetComponent<TileVisual>();
                    if (interaction != null && visual != null)
                    {
                        bool isExposed = localExposedActualIds.Contains(interaction.TileId);
                        if (isExposed) localExposedActualIds.Remove(interaction.TileId);
                        visual.SetExposed(isExposed);
                    }
                }
            }

            if (uiManager.WallUI != null)
            {
                foreach (var rt in uiManager.WallUI.GetWallSlots())
                {
                    if (rt == null) continue;
                    var interaction = rt.GetComponent<TileInteraction>();
                    var visual = rt.GetComponent<TileVisual>();
                    if (interaction != null && visual != null)
                    {
                        bool isExposed = localExposedActualIds.Contains(interaction.TileId);
                        if (isExposed) localExposedActualIds.Remove(interaction.TileId);
                        visual.SetExposed(isExposed);
                    }
                }
            }
        }

        private void SyncEnemyExposedState(Managers.BoardStateManager board)
        {
            List<int> enemyExposedActualIds = new List<int>();
            foreach (int exposedIdx in board.ExposedEnemyHandWallIndexes)
            {
                if (exposedIdx >= 0 && exposedIdx < board.OriginalEnemyWallTiles.Count)
                {
                    enemyExposedActualIds.Add(board.OriginalEnemyWallTiles[exposedIdx]);
                }
            }

            if (uiManager.EnemyHandUI != null)
            {
                var handSlots = uiManager.EnemyHandUI.GetHandSlots();
                var realIds = uiManager.EnemyHandUI.GetRealTileIds();

                for (int i = 0; i < handSlots.Count; i++)
                {
                    var rt = handSlots[i];
                    if (rt == null || i >= realIds.Count) continue;

                    int realId = realIds[i];
                    var visual = rt.GetComponent<TileVisual>();
                    if (visual != null)
                    {
                        bool isExposed = enemyExposedActualIds.Contains(realId);
                        if (isExposed) 
                        {
                            enemyExposedActualIds.Remove(realId);
                            uiManager.EnemyHandUI.RevealTileByIndex(i);
                        }
                        // 敵の牌には透視の目アイコンを表示しない
                        visual.SetExposed(false);
                    }
                }
            }

            if (uiManager.EnemyWallUI != null)
            {
                foreach (var rt in uiManager.EnemyWallUI.GetEnemyWallSlots())
                {
                    if (rt == null) continue;
                    var interaction = rt.GetComponent<TileInteraction>();
                    var visual = rt.GetComponent<TileVisual>();
                    if (interaction != null && visual != null)
                    {
                        bool isExposed = enemyExposedActualIds.Contains(interaction.TileId);
                        if (isExposed) enemyExposedActualIds.Remove(interaction.TileId);
                        // 敵の牌には透視の目アイコンを表示しない
                        visual.SetExposed(false);
                    }
                }
            }
        }

        private void SyncLocalFuritenState(Managers.BoardStateManager board)
        {
            // --- 手牌のアラートはすべてオフにする ---
            if (uiManager.HandUI != null)
            {
                foreach (var rt in uiManager.HandUI.GetHandSlots())
                {
                    if (rt == null) continue;
                    var visual = rt.GetComponent<TileVisual>();
                    if (visual != null) visual.SetFuritenHighlight(false);
                }
            }

            if (uiManager.WallUI == null) return;
            
            // --- 打牌フェーズ(Discard)の時だけアラートを表示する ---
            bool isDiscardPhase = (uiManager.CurrentPhaseStatus == RoundStatus.Discard);

            // 自分の待ち牌のベースID（ドラフラグ0x20を除いた純粋な牌ID）のリストを作成
            List<int> waitBaseIds = new List<int>();
            if (isDiscardPhase && board.CurrentWaitTiles != null)
            {
                foreach (int waitId in board.CurrentWaitTiles)
                {
                    waitBaseIds.Add(Common.TileId.BaseId(waitId));
                }
            }

            // --- 壁の牌に対してアラートを設定する ---
            foreach (var rt in uiManager.WallUI.GetWallSlots())
            {
                if (rt == null) continue;
                var interaction = rt.GetComponent<TileInteraction>();
                var visual = rt.GetComponent<TileVisual>();
                if (interaction != null && visual != null)
                {
                    // 壁牌のベースIDが待ち牌のベースIDに含まれていればフリテン警告対象
                    // （isDiscardPhase == false なら waitBaseIds は空なので自動的に false になる）
                    bool isFuritenAlert = waitBaseIds.Contains(Common.TileId.BaseId(interaction.TileId));
                    visual.SetFuritenHighlight(isFuritenAlert);
                }
            }
        }

        private void UpdateSelectedTileVisuals()
        {
            var selectedIds = BoardStateManager.Instance.SelectedTileIds;
            if (uiManager.WallUI != null)
            {
                foreach (var t in uiManager.WallUI.GetWallSlots())
                {
                    var interaction = t.GetComponent<TileInteraction>();
                    if (interaction != null)
                    {
                        t.localPosition = interaction.OriginalWallPosition;
                    }
                }
            }
        }

        private void HandleTileMovedToHand(int tileId)
        {
            _moveAnimator.HandleTileMovedToHand(tileId);
        }

        private void HandleTileMovedToWall(int tileId)
        {
            _moveAnimator.HandleTileMovedToWall(tileId);
        }

        public System.Collections.IEnumerator PlayPerspectiveAnimation(List<int> newlyExposed)
        {
            return _exposedEffectPlayer.PlayPerspectiveAnimation(newlyExposed);
        }
    }
}
