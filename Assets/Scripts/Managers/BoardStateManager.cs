using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// ゲーム盤面の状態（手牌、壁、選択状態など）を管理するクラス
    /// データの中央集権化を行い、UIクラスはここからデータを読み取るか、イベントを購読します。
    /// </summary>
    public class BoardStateManager : MonoBehaviour
    {
        public static BoardStateManager Instance { get; private set; }

        // --- データモデル ---
        public List<int> CurrentHandTiles { get; private set; } = new List<int>();
        public List<int> CurrentWallTiles { get; private set; } = new List<int>();
        public List<int> OriginalWallTiles { get; private set; } = new List<int>();
        public List<int> CurrentWaitTiles { get; private set; } = new List<int>();
        
        public List<int> CurrentEnemyHandTiles { get; private set; } = new List<int>();
        public List<int> CurrentEnemyWallTiles { get; private set; } = new List<int>();
        
        public List<int> SelectedTileIds { get; private set; } = new List<int>();
        public List<int[]> CurrentTenpaiExamples { get; private set; } = new List<int[]>();
        
        public bool LastIsLocalWin { get; set; } = true; // Agari処理時の一時ステート
        public bool IsLocalTurn { get; private set; } = false;
        
        public int LocalPlayerHp { get; private set; } = 20000;
        public int EnemyPlayerHp { get; private set; } = 20000;

        public void SetLocalTurn(bool isLocalTurn)
        {
            IsLocalTurn = isLocalTurn;
        }

        // --- イベント ---
        public event Action<int> OnTileMovedToHand;
        public event Action<int> OnTileMovedToWall;
        public event Action OnSelectionChanged;
        public event Action OnBoardStateRebuilt; // 全体を再構築する際のイベント

        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
            }
        }

        public void InitializeGame(List<int> initialWall)
        {
            CurrentWallTiles = new List<int>(initialWall);
            CurrentHandTiles.Clear();
            CurrentEnemyHandTiles.Clear();
            CurrentEnemyWallTiles.Clear();
            SelectedTileIds.Clear();
            CurrentWaitTiles.Clear();
            LocalPlayerHp = 20000;
            EnemyPlayerHp = 20000;
            OnBoardStateRebuilt?.Invoke();
        }

        /// <summary>
        /// 全データを外部から受け取って一括セットする（サーバーからのwall_dealt/hand_selectedなど）
        /// </summary>
        public void SetLocalState(List<int> wall, List<int> hand, List<int> wait = null)
        {
            if (wall != null) 
            {
                if (OriginalWallTiles.Count == 0 || OriginalWallTiles.Count != wall.Count) 
                {
                    OriginalWallTiles = new List<int>(wall);
                }
                
                List<int> displayWall = new List<int>(wall);
                if (hand != null)
                {
                    foreach (int hTile in hand)
                    {
                        displayWall.Remove(hTile);
                    }
                }
                CurrentWallTiles = SortTileIds(displayWall);
            }
            if (hand != null) CurrentHandTiles = SortTileIds(new List<int>(hand));
            if (wait != null) CurrentWaitTiles = new List<int>(wait);
        }

        public void SetEnemyState(List<int> wall, List<int> hand)
        {
            if (wall != null) 
            {
                List<int> displayEnemyWall = new List<int>(wall);
                if (hand != null)
                {
                    foreach (int hTile in hand)
                    {
                        displayEnemyWall.Remove(hTile);
                    }
                }
                CurrentEnemyWallTiles = displayEnemyWall;
            }
            if (hand != null) CurrentEnemyHandTiles = new List<int>(hand);
        }

        public void SetTenpaiExamples(List<int[]> tenpaiExamples)
        {
            if (tenpaiExamples != null)
            {
                CurrentTenpaiExamples = tenpaiExamples;
            }
            else
            {
                CurrentTenpaiExamples.Clear();
            }
        }

        public void UpdateHp(int localHp, int enemyHp)
        {
            LocalPlayerHp = localHp;
            EnemyPlayerHp = enemyHp;
        }

        public void FireRebuildEvent()
        {
            OnBoardStateRebuilt?.Invoke();
        }

        // --- 牌の操作ロジック ---

        public bool MoveTileToHand(int tileId)
        {
            int actualId = tileId;
            if (!CurrentWallTiles.Contains(tileId))
            {
                int baseId = tileId & 0x1F;
                int foundIndex = CurrentWallTiles.FindIndex(t => (t & 0x1F) == baseId);
                if (foundIndex != -1)
                {
                    actualId = CurrentWallTiles[foundIndex];
                }
                else
                {
                    Debug.LogWarning($"[MoveTileToHand] Tile ID {tileId} (Base {baseId}) is not in Wall!");
                    return false;
                }
            }

            if (CurrentHandTiles.Count < 13)
            {
                CurrentWallTiles.Remove(actualId);
                CurrentHandTiles.Add(actualId);
                OnTileMovedToHand?.Invoke(actualId);
                return true;
            }
            return false;
        }

        public bool MoveTileToWall(int tileId)
        {
            if (CurrentHandTiles.Contains(tileId))
            {
                CurrentHandTiles.Remove(tileId);
                CurrentWallTiles.Add(tileId);
                OnTileMovedToWall?.Invoke(tileId);
                return true;
            }
            return false;
        }

        // --- 選択・デモ支援ロジック ---

        public void SelectManganHand()
        {
            Debug.Log($"[SelectManganHand] CurrentTenpaiExamples count: {CurrentTenpaiExamples?.Count ?? -1}, OriginalWallTiles count: {OriginalWallTiles.Count}, CurrentWallTiles count: {CurrentWallTiles.Count}");
            
            if (CurrentTenpaiExamples == null || CurrentTenpaiExamples.Count == 0)
            {
                Debug.LogWarning("[SelectManganHand] サーバーからお手本データが届いていません。テスト続行のため、代わりにランダムな手牌を選択します。");
                SelectRandomHand();
                return;
            }

            ResetHandToWall();

            int exampleIndex = UnityEngine.Random.Range(0, CurrentTenpaiExamples.Count);
            int[] targetHand = CurrentTenpaiExamples[exampleIndex];
            Debug.Log($"[SelectManganHand] Using example {exampleIndex}, indexes: [{string.Join(", ", targetHand)}]");
            
            foreach (int index in targetHand)
            {
                if (index >= 0 && index < OriginalWallTiles.Count)
                {
                    int tileId = OriginalWallTiles[index];
                    bool moved = MoveTileToHand(tileId);
                    if (!moved) Debug.LogWarning($"[SelectManganHand] Failed to move tile {tileId} (index={index}) to hand!");
                }
                else
                {
                    Debug.LogWarning($"[SelectManganHand] Index {index} is out of range! OriginalWallTiles.Count={OriginalWallTiles.Count}");
                }
            }
            Debug.Log($"[SelectManganHand] Result: hand has {CurrentHandTiles.Count} tiles: [{string.Join(", ", CurrentHandTiles)}]");
        }

        public void SelectRandomHand()
        {
            ResetHandToWall();

            int tilesToPick = Mathf.Min(13, CurrentWallTiles.Count);
            List<int> tempWall = new List<int>(CurrentWallTiles);
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
                MoveTileToHand(id);
            }
        }

        private void ResetHandToWall()
        {
            List<int> currentHandCopy = new List<int>(CurrentHandTiles);
            foreach (int id in currentHandCopy)
            {
                MoveTileToWall(id);
            }
        }

        public void SelectTile(int tileId, bool multiSelect)
        {
            if (!multiSelect) ClearSelection(false);
            if (!SelectedTileIds.Contains(tileId)) 
            {
                SelectedTileIds.Add(tileId);
            }
            OnSelectionChanged?.Invoke();
        }

        public void SelectTiles(List<int> ids)
        {
            SelectedTileIds = new List<int>(ids);
            OnSelectionChanged?.Invoke();
        }
        
        public void ClearSelection(bool fireEvent = true)
        {
            SelectedTileIds.Clear();
            if (fireEvent) OnSelectionChanged?.Invoke();
        }

        public bool IsTileSelected(int tileId)
        {
            return SelectedTileIds.Contains(tileId);
        }
        
        public void RemoveTileFromWall(int discardedTileId)
        {
            if (CurrentWallTiles.Contains(discardedTileId))
            {
                CurrentWallTiles.Remove(discardedTileId);
            }
        }

        public void RemoveTileFromEnemyWall()
        {
            if (CurrentEnemyWallTiles.Count > 0)
            {
                CurrentEnemyWallTiles.RemoveAt(0);
            }
        }

        private List<int> SortTileIds(List<int> ids)
        {
            ids.Sort();
            return ids;
        }
    }
}
