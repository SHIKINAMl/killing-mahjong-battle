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
        
        /// <summary>
        /// 血の初期値。**サーバーが本物を持っている**（game_engine.py:61 の `health=20000`）。
        ///
        /// ここの値は、対局が始まってサーバーの health が届くまでの**つなぎ**でしかない。
        /// 前の対局の残り血（0 など）がゲージに残ったまま見えるのを防ぐためだけに使う。
        /// 対局開始時に status を投げて取り直すので、サーバーが値を変えればそちらが勝つ。
        /// </summary>
        public const int PlaceholderInitialHp = 20000;

        public int LocalPlayerHp { get; private set; } = PlaceholderInitialHp;
        public int EnemyPlayerHp { get; private set; } = PlaceholderInitialHp;
        public int LocalPlayerSpecialVictoryCount { get; set; } = 0;

        /// <summary>
        /// 血をサーバーの値で扱うか。**2026-08-04 の検証用スイッチ。**
        ///
        /// true  … `status` の `health` をそのまま採用し、ベットでは引かない。
        ///         サーバーの実装（ベットでは health を減らさず、賭け金は別枠で持つ）に合わせる形。
        /// false … 従来どおりクライアントが賭け金を血から引く。
        ///         チュートリアルの説明・ベット演出のHP減少アニメと辻褄が合う形。
        ///
        /// **2026-08-04 に true で確定。サーバーの health をそのまま演出へ流す。**
        ///
        /// クライアントで賭け金を引き算して辻褄を合わせると、サーバー側の誤りが
        /// 画面に出なくなり、ズレたまま気づけない。**おかしさをそのまま見せるため**、
        /// 血はサーバーが送ってきた値だけで動かす。クライアントは一切計算しない。
        ///
        /// 現状こう見える（サーバーが A-8 に対応するまで）:
        ///   ベット確定 … 血は減らない。賭け金だけが場の表示に出る
        ///   スキル     … 減る（skill_casted の health）
        ///   決着       … 減る（liquidation の winner_health / loser_health）
        ///
        /// A-8 で「血が動く場面すべてで引き、health を返す」よう依頼済み。
        /// 対応が入れば、この定数ごと消して常時サーバー値にしてよい。
        /// </summary>
        public const bool UseServerHealth = true;

        // --- 賭け金のルール（サーバーが正） ---
        //
        // 同じ表が GameRules にもあるが、あちらはクライアント側の複製。
        // bet_accepted で本物が届くので、届いた後はこちらを使う。
        // 0 のうちは「まだ受け取っていない」の意味で、GameRules の既定値に頼る。

        public int ServerBetMax { get; private set; } = 0;
        public int ServerBetUnit { get; private set; } = 0;
        public bool HasServerBetRules => ServerBetMax > 0 && ServerBetUnit > 0;

        /// <summary>bet_accepted で届いた賭け金の上限・単位を控える。</summary>
        public void SetServerBetRules(int maxBet, int betUnit)
        {
            if (maxBet <= 0 || betUnit <= 0) return;

            // クライアント側の複製とズレていたら気づけるようにしておく。
            // ズレたまま黙って動くと、予想報酬や上限表示が静かに嘘になる
            var rules = GameRules.GetRuleSet(LocalPlayerSpecialVictoryCount);
            if (rules.BetMax != maxBet || rules.BetUnit != betUnit)
            {
                Debug.LogWarning(
                    $"[BoardState] 賭け金ルールがサーバーと違います。" +
                    $"サーバー max={maxBet} unit={betUnit} / GameRules max={rules.BetMax} unit={rules.BetUnit}" +
                    $"（特殊勝利 {LocalPlayerSpecialVictoryCount} 回）。GameRules 側を直してください");
            }

            ServerBetMax = maxBet;
            ServerBetUnit = betUnit;
        }

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

        private void OnDestroy()
        {
            // シーン再読込時に破棄済みオブジェクトを指したままにしない。
            // 重複インスタンスが破棄された場合に本物を消さないよう this と比較する。
            if (Instance == this) Instance = null;
        }

        public void InitializeGame(List<int> initialWall)
        {
            CurrentWallTiles = new List<int>(initialWall);
            ClearAllBoardData();
            LocalPlayerHp = PlaceholderInitialHp;
            EnemyPlayerHp = PlaceholderInitialHp;
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

                // hand はサーバー由来の「牌の値」のリスト（壁インデックスではない）。
                // 値一致で壁スロットを1枚ずつ消費して手牌に割り当てる（重複牌対応）。
                // 完全一致を優先し、無ければ baseId(0x1F) でドラフラグ差を許容して照合する。
                List<int> remainingHand = hand != null ? new List<int>(hand) : null;

                for (int i = 0; i < wall.Count; i++)
                {
                    int matchIdx = -1;
                    if (remainingHand != null)
                    {
                        matchIdx = remainingHand.IndexOf(wall[i]);
                        if (matchIdx < 0)
                        {
                            int baseId = Common.TileId.BaseId(wall[i]);
                            matchIdx = remainingHand.FindIndex(t => Common.TileId.BaseId(t) == baseId);
                        }
                    }

                    if (matchIdx >= 0)
                    {
                        remainingHand.RemoveAt(matchIdx);
                        actualHandIds.Add(wall[i]);
                        LocalHandIndexes.Add(i);
                    }
                    else
                    {
                        displayWall.Add(wall[i]);
                    }
                }

                if (remainingHand != null && remainingHand.Count > 0)
                {
                    Debug.LogWarning($"[BoardStateManager] SetLocalState: {remainingHand.Count} 枚の手牌が壁に見つかりませんでした: [{string.Join(",", remainingHand)}]");
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
                    // hand は「牌の値」のリスト（壁インデックスではない）。SetLocalState と同じ規約で照合する。
                    List<int> remainingHand = new List<int>(hand);
                    for (int i = 0; i < wall.Count; i++)
                    {
                        int matchIdx = remainingHand.IndexOf(wall[i]);
                        if (matchIdx < 0)
                        {
                            int baseId = Common.TileId.BaseId(wall[i]);
                            matchIdx = remainingHand.FindIndex(t => Common.TileId.BaseId(t) == baseId);
                        }
                        if (matchIdx >= 0)
                        {
                            remainingHand.RemoveAt(matchIdx);
                            actualHandIds.Add(wall[i]);
                            EnemyHandIndexes.Add(i);
                        }
                    }
                    if (remainingHand.Count > 0)
                    {
                        Debug.LogWarning($"[BoardStateManager] SetEnemyState: {remainingHand.Count} 枚の手牌が壁に見つかりませんでした: [{string.Join(",", remainingHand)}]");
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

        /// <summary>
        /// 清算で血が動く直前の値。**ロン演出の「減る前」に使う。**
        ///
        /// 演出は「前の血 → 後の血」を見せるが、後の血はサーバーが確定値で送ってくる
        /// （`liquidation.winner_health` / `loser_health`）ので、前の血を
        /// 「後の血 − 獲得」のように逆算する必要はない。**覚えておけば済む。**
        /// 逆算していた頃は、獲得と損失が非対称になる仕様（強襲）が入ると式が崩れた。
        /// </summary>
        public int HpBeforeLiquidationLocal { get; private set; } = PlaceholderInitialHp;
        public int HpBeforeLiquidationEnemy { get; private set; } = PlaceholderInitialHp;

        /// <summary>清算を適用する直前に呼ぶ。今の血を「減る前」として控える。</summary>
        public void RememberHpBeforeLiquidation()
        {
            HpBeforeLiquidationLocal = LocalPlayerHp;
            HpBeforeLiquidationEnemy = EnemyPlayerHp;
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

        /// <summary>
        /// 山の指定位置の牌を差し替える（牌交換で使う）。
        ///
        /// サーバーは wall 内のインデックスで交換位置を指定してくる。
        /// OriginalWallTiles は配牌時に一度作るだけだったので、交換しても更新されず
        /// サーバーの山とズレていた。位置で牌を特定する処理（TileInteraction.WallIndex）が
        /// 2回目以降の交換で誤動作する原因になるため、ここで必ず同期させる。
        ///
        /// 同じ牌IDが山に複数あるので、**牌IDで探して書き換えてはいけない。**
        /// 必ずサーバーが指定した位置をそのまま使うこと。
        /// </summary>
        /// <returns>差し替えに成功したら true</returns>
        public bool ReplaceWallTileAt(int wallIndex, int newTileId)
        {
            if (wallIndex < 0 || wallIndex >= OriginalWallTiles.Count)
            {
                Debug.LogWarning($"[BoardState] ReplaceWallTileAt: index {wallIndex} が範囲外 (山 {OriginalWallTiles.Count} 枚)");
                return false;
            }

            int old = OriginalWallTiles[wallIndex];
            OriginalWallTiles[wallIndex] = newTileId;

            // 表示用の現在の山からも、同じ1枚だけを差し替える
            int cur = CurrentWallTiles.IndexOf(old);
            if (cur >= 0) CurrentWallTiles[cur] = newTileId;

            return true;
        }

        // --- 牌の操作ロジック ---

        public bool MoveTileToHand(int tileId)
        {
            Debug.Log($"[BoardStateManager] MoveTileToHand called with tileId: {tileId}");
            int actualId = tileId;
            if (!CurrentWallTiles.Contains(tileId))
            {
                int baseId = Common.TileId.BaseId(tileId);
                int foundIndex = CurrentWallTiles.FindIndex(t => Common.TileId.BaseId(t) == baseId);
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
            ids.Sort(Common.TileId.CompareForDisplay);
            return ids;
        }
    }
}
