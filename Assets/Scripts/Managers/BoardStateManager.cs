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
        public List<int> NonManganWaitTiles { get; private set; } = new List<int>();
        
        public List<int> CurrentEnemyHandTiles { get; private set; } = new List<int>();
        public List<int> CurrentEnemyWallTiles { get; private set; } = new List<int>();
        public List<int> OriginalEnemyWallTiles { get; private set; } = new List<int>();
        public List<int> CurrentEnemyWaitTiles { get; private set; } = new List<int>();
        public HashSet<int> ExposedEnemyHandWallIndexes { get; private set; } = new HashSet<int>();
        public HashSet<int> ExposedLocalHandWallIndexes { get; private set; } = new HashSet<int>();
        
        public List<int> SelectedTileIds { get; private set; } = new List<int>();
        public List<int[]> CurrentTenpaiExamples { get; private set; } = new List<int[]>();
        
        // 追加: 強化された役の保持
        public Dictionary<string, int> LocalBoostHandBonus { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> EnemyBoostHandBonus { get; set; } = new Dictionary<string, int>();

        public HashSet<int> DiscardedWallIndexes { get; private set; } = new HashSet<int>();
        public List<int> TargetHandIndexes { get; set; } = null;
        public HashSet<int> HiddenTiles { get; private set; } = new HashSet<int>();
        
        public List<WaitData> LocalWaitDataList { get; private set; } = new List<WaitData>();
        
        public bool LastIsLocalWin { get; set; } = true; 
        public LiquidationData LastLiquidationData { get; set; } = null;
        public bool IsLocalTurn { get; private set; } = false;
        public bool IsLocalPlayerFirstRound { get; private set; } = false;
        public int LastDiscardedTileId { get; set; } = -1;
        public int CurrentDoraId { get; set; } = -1;
        
        public int LocalPlayerHp { get; private set; } = 20000;
        public int EnemyPlayerHp { get; private set; } = 20000;
        public int LocalPlayerSpecialVictoryCount { get; set; } = 0;

        public void SetLocalTurn(bool isLocalTurn)
        {
            if (IsLocalTurn != isLocalTurn)
            {
                IsLocalTurn = isLocalTurn;
                OnTurnChanged?.Invoke(IsLocalTurn);
            }
        }
        
        public void SetLocalPlayerFirstRound(bool isFirst)
        {
            IsLocalPlayerFirstRound = isFirst;
        }

        // --- イベント ---
        public event Action<bool> OnTurnChanged;
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
            ClearAllBoardData();
            LocalPlayerHp = 20000;
            EnemyPlayerHp = 20000;
            OnBoardStateRebuilt?.Invoke();
        }

        public void ClearAllBoardData()
        {
            CurrentHandTiles.Clear();
            CurrentEnemyHandTiles.Clear();
            CurrentEnemyWallTiles.Clear();
            SelectedTileIds.Clear();
            CurrentWaitTiles.Clear();
            NonManganWaitTiles.Clear();
            DiscardedWallIndexes.Clear();
            OriginalWallTiles.Clear();
            OriginalEnemyWallTiles.Clear();
            CurrentDoraId = -1;
            TargetHandIndexes = null;
            HiddenTiles.Clear();
            ExposedEnemyHandWallIndexes.Clear();
            ExposedLocalHandWallIndexes.Clear();
            LocalWaitDataList.Clear();
        }

        public void ClearBoosts()
        {
            LocalBoostHandBonus?.Clear();
            EnemyBoostHandBonus?.Clear();
        }

        /// <summary>
        /// 全データを外部から受け取って一括セットする（サーバーからのwall_dealt/hand_selectedなど）
        /// </summary>
        public void SetLocalState(List<int> wall, List<int> hand, List<int> wait = null)
        {
            if (wall != null && wall.Count > 0) 
            {
                if (OriginalWallTiles.Count == 0 || OriginalWallTiles.Count != wall.Count) 
                {
                    OriginalWallTiles = new List<int>(wall);
                    DiscardedWallIndexes.Clear();
                }
                else
                {
                    for (int i = 0; i < wall.Count; i++)
                    {
                        OriginalWallTiles[i] = wall[i];
                    }
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
            if (hand != null) 
            {
                CurrentHandTiles = SortTileIds(new List<int>(hand));
                HiddenTiles.Clear();
            }
            if (wait != null) CurrentWaitTiles = new List<int>(wait);
        }

        public void SetEnemyState(List<int> wall, List<int> hand, List<int> waits = null)
        {
            if (wall != null && wall.Count > 0)
            {
                OriginalEnemyWallTiles = new List<int>(wall);
                List<int> displayEnemyWall = new List<int>();
                for (int i = 0; i < wall.Count; i++)
                {
                    if (hand == null || !hand.Contains(i))
                    {
                        displayEnemyWall.Add(wall[i]);
                    }
                }
                CurrentEnemyWallTiles = displayEnemyWall;
            }
            if (hand != null) CurrentEnemyHandTiles = new List<int>(hand);
            if (waits != null) CurrentEnemyWaitTiles = new List<int>(waits);
        }

        public void ClearWaitTiles()
        {
            CurrentWaitTiles.Clear();
            NonManganWaitTiles.Clear();
        }

        public void SetNonManganWaits(List<int> nonManganTiles)
        {
            NonManganWaitTiles = new List<int>(nonManganTiles ?? new List<int>());
        }

        private int _fixedManganExampleIndex = 0;

        public void SetTenpaiExamples(List<int[]> tenpaiExamples)
        {
            if (tenpaiExamples != null && tenpaiExamples.Count > 0)
            {
                CurrentTenpaiExamples = tenpaiExamples;
                _fixedManganExampleIndex = UnityEngine.Random.Range(0, CurrentTenpaiExamples.Count);
            }
            else
            {
                CurrentTenpaiExamples.Clear();
                _fixedManganExampleIndex = 0;
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

        /// <summary>
        /// 打牌済みwall_indexを除外して、指定の牌IDに対応するwall_indexを検索する
        /// </summary>
        public int FindAvailableWallIndex(int tileId)
        {
            for (int i = 0; i < OriginalWallTiles.Count; i++)
            {
                if (OriginalWallTiles[i] == tileId && !DiscardedWallIndexes.Contains(i))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// wall_indexを打牌済みとして記録する
        /// </summary>
        public void MarkWallIndexAsDiscarded(int wallIndex)
        {
            DiscardedWallIndexes.Add(wallIndex);
        }

        // --- 牌の操作ロジック ---

        public bool MoveTileToHand(int tileId)
        {
            Debug.Log($"[BoardStateManager] MoveTileToHand called with tileId: {tileId}");
            int actualId = tileId;
            if (!CurrentWallTiles.Contains(tileId))
            {
                int baseId = tileId & 0x1F;
                int foundIndex = CurrentWallTiles.FindIndex(t => (t & 0x1F) == baseId);
                if (foundIndex != -1)
                {
                    actualId = CurrentWallTiles[foundIndex];
                    Debug.Log($"[BoardStateManager] tileId {tileId} not in Wall, mapped to actualId {actualId} using baseId {baseId}");
                }
                else
                {
                    Debug.LogWarning($"[MoveTileToHand] Tile ID {tileId} (Base {baseId}) is not in Wall!");
                    return false;
                }
            }

            Debug.Log($"[BoardStateManager] CurrentHandTiles count: {CurrentHandTiles.Count}");
            if (CurrentHandTiles.Count < 13)
            {
                CurrentWallTiles.Remove(actualId);
                CurrentHandTiles.Add(actualId);
                Debug.Log($"[BoardStateManager] Moved actualId {actualId} to hand. Invoking OnTileMovedToHand.");
                OnTileMovedToHand?.Invoke(actualId);
                return true;
            }
            Debug.LogWarning($"[BoardStateManager] Hand is full (13 tiles). Cannot move {actualId} to hand.");
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

        private int[] OptimizeHandIndicesForDora(int[] originalIndexes)
        {
            List<int> upgraded = new List<int>(originalIndexes);
            List<int> wall = OriginalWallTiles;
            
            for (int i = 0; i < upgraded.Count; i++)
            {
                int currentIndex = upgraded[i];
                if (currentIndex < 0 || currentIndex >= wall.Count) continue;
                
                int currentTile = wall[currentIndex];
                int baseId = currentTile & 0x1F;
                int currentDoraFlag = currentTile >> 5;
                
                int bestIndex = currentIndex;
                int bestDoraFlag = currentDoraFlag;
                
                for (int w = 0; w < wall.Count; w++)
                {
                    if (upgraded.Contains(w)) continue;
                    
                    int wTile = wall[w];
                    if ((wTile & 0x1F) == baseId)
                    {
                        int wDoraFlag = wTile >> 5;
                        if (wDoraFlag > bestDoraFlag)
                        {
                            bestDoraFlag = wDoraFlag;
                            bestIndex = w;
                        }
                    }
                }
                
                upgraded[i] = bestIndex;
            }
            
            return upgraded.ToArray();
        }

        public void SelectManganHand()
        {
            Debug.Log($"[SelectManganHand] CurrentTenpaiExamples count: {CurrentTenpaiExamples?.Count ?? -1}, OriginalWallTiles count: {OriginalWallTiles.Count}");
            
            if (CurrentTenpaiExamples == null || CurrentTenpaiExamples.Count == 0)
            {
                Debug.LogWarning("[SelectManganHand] サーバーからお手本データが届いていません。テスト続行のため、代わりにランダムな手牌を選択します。");
                SelectRandomHand();
                return;
            }

            int exampleIndex = _fixedManganExampleIndex;
            if (exampleIndex < 0 || exampleIndex >= CurrentTenpaiExamples.Count) exampleIndex = 0;

            int[] rawTargetHand = CurrentTenpaiExamples[exampleIndex];
            
            // Pythonサーバーから送られてきたインデックスをそのまま使用する
            List<int> targetTileIds = new List<int>();
            
            foreach (int rawIdx in rawTargetHand)
            {
                if (rawIdx >= 0 && rawIdx < OriginalWallTiles.Count)
                {
                    targetTileIds.Add(OriginalWallTiles[rawIdx]);
                }
                else
                {
                    Debug.LogWarning($"[SelectManganHand] 無効なインデックスを受け取りました: {rawIdx}");
                }
            }

            Debug.Log($"[SelectManganHand] targetTileIds (count: {targetTileIds.Count}): [{string.Join(", ", targetTileIds)}]");

            // 現在の手牌から、目標に含まれない牌だけを壁に戻す
            List<int> currentHandCopy = new List<int>(CurrentHandTiles);
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
                    MoveTileToWall(id);
                }
            }

            // まだ手牌に足りていない牌を壁から取る
            foreach (int id in targetTileIds)
            {
                bool moved = MoveTileToHand(id);
                if (!moved) Debug.LogWarning($"[SelectManganHand] Failed to move tile {id} to hand!");
            }
            
            Debug.Log($"[SelectManganHand] Result: hand has {CurrentHandTiles.Count} tiles");
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
            ids.Sort((a, b) =>
            {
                int baseA = a & 0x1F;
                int baseB = b & 0x1F;
                if (baseA != baseB) return baseA.CompareTo(baseB);
                return a.CompareTo(b);
            });
            return ids;
        }
    }
}
