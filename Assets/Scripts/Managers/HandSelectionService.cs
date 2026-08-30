using System.Collections.Generic;
using KillingMahjong.Common;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 手牌構築のドメインロジック（BoardStateManager から分離）。
    ///
    /// 盤面状態の読み取りと、MoveTileToHand / MoveTileToWall による牌の移動のみを行う。
    /// 状態そのものの保持と変更イベントの発火は BoardStateManager 側の責務のまま。
    /// </summary>
    public class HandSelectionService
    {
        private readonly BoardStateManager board;

        public HandSelectionService(BoardStateManager board)
        {
            this.board = board;
        }

        /// <summary>
        /// サーバーのお手本（テンパイ形）どおりに手牌を構築する。
        /// お手本が届いていない場合はランダム選択にフォールバックする。
        /// </summary>
        public void SelectManganHand()
        {
            Debug.Log($"[SelectManganHand] CurrentTenpaiExamples count: {board.CurrentTenpaiExamples?.Count ?? -1}, OriginalWallTiles count: {board.OriginalWallTiles.Count}");

            if (board.CurrentTenpaiExamples == null || board.CurrentTenpaiExamples.Count == 0)
            {
                Debug.LogWarning("[SelectManganHand] サーバーからお手本データが届いていません。テスト続行のため、代わりにランダムな手牌を選択します。");
                SelectRandomHand();
                return;
            }

            int exampleIndex = board.FixedManganExampleIndex;
            if (exampleIndex < 0 || exampleIndex >= board.CurrentTenpaiExamples.Count) exampleIndex = 0;

            int[] rawTargetHand = board.CurrentTenpaiExamples[exampleIndex];

            // Pythonサーバーから送られてきたインデックスをそのまま使用する
            List<int> targetTileIds = new List<int>();

            foreach (int rawIdx in rawTargetHand)
            {
                if (rawIdx >= 0 && rawIdx < board.OriginalWallTiles.Count)
                {
                    targetTileIds.Add(board.OriginalWallTiles[rawIdx]);
                }
                else
                {
                    Debug.LogWarning($"[SelectManganHand] 無効なインデックスを受け取りました: {rawIdx}");
                }
            }

            Debug.Log($"[SelectManganHand] targetTileIds (count: {targetTileIds.Count}): [{string.Join(", ", targetTileIds)}]");

            // 現在の手牌から、目標に含まれない牌だけを壁に戻す
            List<int> currentHandCopy = new List<int>(board.CurrentHandTiles);
            foreach (int id in currentHandCopy)
            {
                if (targetTileIds.Contains(id))
                {
                    // 目標にも含まれているので手牌に残す
                    targetTileIds.Remove(id); // 同一IDが複数ある場合のために1つ消費する
                }
                else
                {
                    // 目標に含まれていないので壁に戻す
                    board.MoveTileToWall(id);
                }
            }

            // まだ手牌に足りていない牌を壁から取る
            foreach (int id in targetTileIds)
            {
                bool moved = board.MoveTileToHand(id);
                if (!moved) Debug.LogWarning($"[SelectManganHand] Failed to move tile {id} to hand!");
            }

            Debug.Log($"[SelectManganHand] Result: hand has {board.CurrentHandTiles.Count} tiles");
        }

        /// <summary>手牌を全て山へ戻したうえで、山からランダムに13枚（不足時は残数）を手牌へ移す。</summary>
        public void SelectRandomHand()
        {
            ResetHandToWall();

            int tilesToPick = Mathf.Min(13, board.CurrentWallTiles.Count);
            List<int> tempWall = new List<int>(board.CurrentWallTiles);
            List<int> targetIds = new List<int>();

            for (int i = 0; i < tilesToPick; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, tempWall.Count);
                int selectedId = tempWall[randomIndex];
                targetIds.Add(selectedId);
                tempWall.RemoveAt(randomIndex);
            }

            foreach (int id in targetIds)
            {
                board.MoveTileToHand(id);
            }
        }

        private void ResetHandToWall()
        {
            List<int> currentHandCopy = new List<int>(board.CurrentHandTiles);
            foreach (int id in currentHandCopy)
            {
                board.MoveTileToWall(id);
            }
        }
    }
}
