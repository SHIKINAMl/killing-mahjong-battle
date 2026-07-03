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

        /// <summary>手牌構築ロジック（分離クラス）</summary>
        private HandSelectionService _handSelection;

        // --- データモデル ---
        public List<int> CurrentHandTiles { get; private set; } = new List<int>();
        public List<int> LocalHandIndexes { get; private set; } = new List<int>();
        public List<int> CurrentWallTiles { get; private set; } = new List<int>();
        public List<int> OriginalWallTiles { get; private set; } = new List<int>();
        public List<int> CurrentWaitTiles { get; private set; } = new List<int>();
        public List<int> NonManganWaitTiles { get; private set; } = new List<int>();
        
        public List<int> CurrentEnemyHandTiles { get; private set; } = new List<int>();
        public List<int> EnemyHandIndexes { get; private set; } = new List<int>();
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
                _handSelection = new HandSelectionService(this);
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
            LocalHandIndexes.Clear();
            CurrentEnemyHandTiles.Clear();
            EnemyHandIndexes.Clear();
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
                OriginalWallTiles = new List<int>(wall);
                List<int> displayWall = new List<int>();
                List<int> actualHandIds = new List<int>();
                LocalHandIndexes = new List<int>();

                for (int i = 0; i < wall.Count; i++)
                {
                    if (hand != null && hand.Contains(i))
                    {
                        actualHandIds.Add(wall[i]);
                        LocalHandIndexes.Add(i);
                    }
                    else
                    {
                        displayWall.Add(wall[i]);
                    }
                }
                
                // CurrentWallTiles は山牌として残る牌（表示側で手牌と結合して理牌し、引っこ抜く）
                CurrentWallTiles = SortTileIds(displayWall);
                
                if (hand != null)
                {
                    CurrentHandTiles = SortTileIds(actualHandIds);
                    HiddenTiles.Clear();
                }
            }
            if (wait != null) CurrentWaitTiles = new List<int>(wait);
        }

        public void SetEnemyState(List<int> wall, List<int> hand, List<int> waits = null)
        {
            if (wall != null && wall.Count > 0)
            {
                OriginalEnemyWallTiles = new List<int>(wall);
                
                List<int> actualHandIds = new List<int>();
                EnemyHandIndexes = new List<int>();
                
                if (hand != null)
                {
                    foreach (int hIndex in hand)
                    {
                        if (hIndex >= 0 && hIndex < wall.Count)
                        {
                            int actualId = wall[hIndex];
                            actualHandIds.Add(actualId);
                            EnemyHandIndexes.Add(hIndex);
                        }
                    }
                }
                CurrentEnemyWallTiles = OriginalEnemyWallTiles;
                
                if (hand != null) CurrentEnemyHandTiles = new List<int>(actualHandIds);
            }
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

        /// <summary>SetTenpaiExamples 時に抽選されたお手本インデックス（HandSelectionService から参照）</summary>
        internal int FixedManganExampleIndex { get { return _fixedManganExampleIndex; } }

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

        public void SelectManganHand()
        {
            _handSelection.SelectManganHand();
        }

        public void SelectRandomHand()
        {
            _handSelection.SelectRandomHand();
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

        public List<int> SortTileIds(List<int> ids)
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
