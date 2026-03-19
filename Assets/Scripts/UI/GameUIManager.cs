using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private HandUI handUI;
        [SerializeField] private WallUI wallUI;
        [SerializeField] private RiverUI riverUI;
        [SerializeField] private EnemyHandUI enemyHandUI; // 追加: 敵の手牌UI
        [SerializeField] private EnemyWallUI enemyWallUI; // 追加: 敵の壁UI
        [SerializeField] private RiverUI enemyRiverUI;   // 追加: 敵の河UI
        [SerializeField] private WaitUI waitUI; // 追加: 待ち牌表示UI
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private PlayerInfoUI playerInfoUI; // プレイヤー自身のHP
        [SerializeField] private EnemyInfoUI enemyInfoUI;  // 敵のUI管理（HP、パネル）
        [SerializeField] private AbilityUI abilityUI;
        [SerializeField] private YakuListUI yakuListUI;
        [SerializeField] private BettingUI bettingUI;
        [SerializeField] private PhaseTransitionUI phaseTransitionUI;
        [SerializeField] private RonAnimationUI ronAnimationUI; // 追加: ロン演出管理UI
        [SerializeField] private WebSocketGameClientSample webSocketClient;
        [SerializeField] private MatchmakingUI matchmakingUI;

        [Header("Effects")]
        [SerializeField] private GameObject victoryEffectPrefab;
        [SerializeField] private GameObject damageEffectPrefab;

        [Header("Debug Client")]
        [SerializeField] private bool useDebugClient;
        [SerializeField] private KillingMahjong.Network.DebugWebSocketClient debugWebSocketClient;
        [SerializeField] private bool showEnemyHandDebug = true;

        [Header("Character Reactions")]
        [SerializeField] private float reactionDisplayDuration = 2.0f; // リアクションを表示したまま待つ時間
        
        // --- 内部状態トラッキング用 ---

        private void Start()
        {
            // Initialization if needed
            SetupUI();

            if (useDebugClient && debugWebSocketClient != null)
            {
                debugWebSocketClient.StartMockConnection();
            }
        }

        private void SetupUI()
        {
            // Initial setup logic
            if (handUI != null) handUI.Setup(this);
            if (wallUI != null) wallUI.Setup(this);
            if (enemyWallUI != null) enemyWallUI.Setup(this);
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            
            // Ensure UI is hidden at very beginning before matching starts
            SetMatchUIVisibility(false);
            if (riverUI != null) riverUI.gameObject.SetActive(false);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
            if (enemyHandUI != null) enemyHandUI.gameObject.SetActive(false);
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
            if (abilityUI != null) abilityUI.gameObject.SetActive(false);
            if (bettingUI != null) bettingUI.HideBettingPhase();
            
            // dialogueUI.ShowText("Game Start!"); // 待機中には見せないためコメントアウト
        }
        
        // Data State
        private bool isTransitioning = false;
        private System.Collections.Generic.List<int> currentHandTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> currentWallTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> currentWaitTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> selectedTileIds = new System.Collections.Generic.List<int>();
        
        private RoundStatus currentPhaseStatus = RoundStatus.None;
        public RoundStatus CurrentPhaseStatus => currentPhaseStatus;

        private System.Collections.Generic.List<int> currentEnemyHandTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> currentEnemyWallTiles = new System.Collections.Generic.List<int>();
        
        private bool lastIsLocalWin = true; // Temporary state for Agari processing

        public void InitializeGame(System.Collections.Generic.List<int> initialWall)
        {
            currentWallTiles = new System.Collections.Generic.List<int>(initialWall);
            currentHandTiles.Clear();
            currentEnemyHandTiles.Clear();
            currentEnemyWallTiles.Clear();
            selectedTileIds.Clear();
            currentWaitTiles.Clear();
            RebuildAllTilesFromState();
        }

        public void MoveTileToHand(int tileId)
        {
            // 手牌選択フェイズでのみ移動を許可
            if (currentPhaseStatus != RoundStatus.HandSelection) return;

            // tenpai_examplesはベースID（下位5ビット＝牌種別）で返ってくるが、
            // currentWallTilesはドラフラグ付きの実際IDで格納されている。
            // 完全一致を先に試み、なければ牌種別（& 0x1F）で検索する。
            int actualId = tileId;
            if (!currentWallTiles.Contains(tileId))
            {
                int baseId = tileId & 0x1F;
                actualId = currentWallTiles.Find(t => (t & 0x1F) == baseId);
                // Find returns 0 if not found; check it's genuinely in the list
                if (!currentWallTiles.Contains(actualId)) return;
            }

            if (currentHandTiles.Count < 13)
            {
                currentWallTiles.Remove(actualId);
                currentHandTiles.Add(actualId);
                
                if (wallUI != null && handUI != null)
                {
                    RectTransform movedTile = wallUI.GrabTile(actualId);
                    if (movedTile != null)
                    {
                        handUI.AddTileToHand(movedTile, actualId);
                    }
                }
            }
        }

        public void MoveTileToWall(int tileId)
        {
            // 手牌選択フェイズでのみ移動を許可
            if (currentPhaseStatus != RoundStatus.HandSelection) return;

            if (currentHandTiles.Contains(tileId))
            {
                currentHandTiles.Remove(tileId);
                currentWallTiles.Add(tileId);
                
                if (handUI != null && wallUI != null)
                {
                    // 1. HandUIからTransformを探して取得
                    RectTransform movedTile = null;
                    foreach (RectTransform t in handUI.GetHandSlots())
                    {
                        var interaction = t.GetComponent<TileInteraction>();
                        if (interaction != null && interaction.TileId == tileId)
                        {
                            movedTile = t;
                            break;
                        }
                    }

                    // 2. HandUIから取り除く
                    if (movedTile != null)
                    {
                        handUI.RemoveTileFromHand(movedTile, tileId);
                        // 3. WallUIに戻す
                        wallUI.ReturnTileToWall(movedTile, tileId);
                    }
                }
            }
            ClearSelection();
        }

        private List<int[]> currentTenpaiExamples = new List<int[]>();

        public void SelectManganHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (currentTenpaiExamples == null || currentTenpaiExamples.Count == 0)
            {
                Debug.LogWarning("No tenpai examples available. Falling back to random hand.");
                SelectRandomHand();
                return;
            }

            // まず手牌を一度すべて壁に戻す（リセット）
            List<int> currentHandCopy = new List<int>(currentHandTiles);
            foreach (int id in currentHandCopy)
            {
                MoveTileToWall(id);
            }

            // tenpai_examples の中からランダムに1つの形（13枚）を選ぶ
            int exampleIndex = UnityEngine.Random.Range(0, currentTenpaiExamples.Count);
            int[] targetHand = currentTenpaiExamples[exampleIndex];
            
            // 選ばれた13枚のIDを手牌へ移動する
            foreach (int id in targetHand)
            {
                MoveTileToHand(id);
            }
        }

        public void SelectRandomHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;

            // まず手牌を一度すべて壁に戻す（リセット）
            // ループ中に要素を削除・移動するため、コピーリストを使用
            List<int> currentHandCopy = new List<int>(currentHandTiles);
            foreach (int id in currentHandCopy)
            {
                MoveTileToWall(id);
            }

            // ランダムに13個の牌を選ぶ
            int tilesToPick = Mathf.Min(13, currentWallTiles.Count);
            
            // 一時的な壁リストを作成し、そこからランダムにピックしていく
            List<int> tempWall = new List<int>(currentWallTiles);
            List<int> targetIds = new List<int>();

            for (int i = 0; i < tilesToPick; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, tempWall.Count);
                int selectedId = tempWall[randomIndex];
                targetIds.Add(selectedId);
                tempWall.RemoveAt(randomIndex);
            }

            // 選択された牌をUIと内部データで手牌へ移動する
            foreach (int id in targetIds)
            {
                MoveTileToHand(id);
            }
        }
        
        private void MoveSelectedFiles(bool toHand)
        {
            List<int> targetList = toHand ? currentHandTiles : currentWallTiles;
            List<int> sourceList = toHand ? currentWallTiles : currentHandTiles;
            
            foreach (var id in selectedTileIds)
            {
                if (sourceList.Contains(id))
                {
                    // 手牌へ送る場合で、かつ既に13枚持っているならこれ以上は追加しない
                    if (toHand && targetList.Count >= 13)
                    {
                        continue;
                    }

                    sourceList.Remove(id);
                    targetList.Add(id);

                    // Transformの物理移動
                    if (toHand && wallUI != null && handUI != null)
                    {
                        // Wall -> Hand
                        RectTransform movedTile = wallUI.GrabTile(id);
                        if (movedTile != null) handUI.AddTileToHand(movedTile, id);
                    }
                    else if (!toHand && handUI != null && wallUI != null)
                    {
                        // Hand -> Wall
                        RectTransform movedTile = null;
                        foreach (RectTransform t in handUI.GetHandSlots()) 
                        {
                            var interaction = t.GetComponent<TileInteraction>();
                            if (interaction != null && interaction.TileId == id)
                            {
                                movedTile = t;
                                break;
                            }
                        }
                        if (movedTile != null)
                        {
                            handUI.RemoveTileFromHand(movedTile, id);
                            wallUI.ReturnTileToWall(movedTile, id);
                        }
                    }
                }
            }
            // RefreshUI();
        }

        public void SelectTile(int tileId, bool isInHand, bool multiSelect)
        {
            if (!multiSelect) ClearSelection();
            if (!selectedTileIds.Contains(tileId)) 
            {
                selectedTileIds.Add(tileId);
                UpdateSelectedTileVisuals();
            }
            
            string tileName = new TileData(tileId).GetTileName();
            Debug.Log($"[SelectTile] ID: {tileId} ({tileName}) | Selected Count: {selectedTileIds.Count}");
            DeselectAbility();
        }

        private void UpdateSelectedTileVisuals()
        {
            // Simple visual feedback: move selected tiles up slightly
            // In Discard phase, we select from Wall. In other phases (maybe ability targeting?), from Hand.
            /*
            if (handUI != null)
            {
                var slots = handUI.GetHandSlots();
                foreach (var t in slots)
                {
                    var interaction = t.GetComponent<TileInteraction>();
                    if (interaction != null && selectedTileIds.Contains(interaction.TileId))
                        t.anchoredPosition = new Vector2(t.anchoredPosition.x, 20f);
                    else if (interaction != null)
                        t.anchoredPosition = new Vector2(t.anchoredPosition.x, 0f); // BUG: This breaks HandUI layout
                }
            }
            */
            if (wallUI != null)
            {
                var slots = wallUI.GetWallSlots();
                foreach (var t in slots)
                {
                    var interaction = t.GetComponent<TileInteraction>();
                    if (interaction != null && selectedTileIds.Contains(interaction.TileId))
                    {
                        // 壁の牌は元の位置からY軸に少し浮かせる
                        t.localPosition = interaction.OriginalWallPosition + new Vector3(0, 20f, 0); 
                    }
                    else if (interaction != null)
                    {
                        t.localPosition = interaction.OriginalWallPosition;
                    }
                }
            }
        }
        
        public void SelectTiles(System.Collections.Generic.List<int> ids)
        {
            selectedTileIds = new System.Collections.Generic.List<int>(ids);
            Debug.Log($"Box Selected Tiles Count: {selectedTileIds.Count}");
            DeselectAbility();
        }
        
        private void ClearSelection()
        {
            selectedTileIds.Clear();
            UpdateSelectedTileVisuals();
        }

        public bool IsTileSelected(int tileId)
        {
            return selectedTileIds.Contains(tileId);
        }

        private void SyncDiscardPhaseVisuals(PlayerStateData localPlayer)
        {
            if (localPlayer.hand == null || localPlayer.wall == null) return;

            // ツモ無しルール：Handは不変。Wallから減っていく
            // 壁から無くなった牌（打牌された牌）をWallUIから削除
            if (wallUI != null)
            {
                List<RectTransform> slots = wallUI.GetWallSlots();
                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    if (slots[i] == null) continue;
                    var interaction = slots[i].GetComponent<TileInteraction>();
                    if (interaction != null && !System.Array.Exists(localPlayer.wall, t => t == interaction.TileId))
                    {
                        // WallSlots からも除去されるよう GrabTile などを経由するか直接消す
                        RectTransform t = wallUI.GrabTile(interaction.TileId);
                        if (t != null)
                        {
                            Destroy(t.gameObject);
                        }
                    }
                }
                
                // 再レイアウト（フリテンや打牌後の整頓のため）
                List<RectTransform> remainingTiles = new List<RectTransform>();
                foreach (var st in wallUI.GetWallSlots())
                {
                    if (st != null) remainingTiles.Add(st);
                }
                wallUI.LayoutWallTiles(remainingTiles, new List<int>(localPlayer.wall), 
                    localPlayer.wait != null ? new List<int>(localPlayer.wait) : new List<int>(), true);
            }

            // 3. Update internal tracking
            currentHandTiles = new List<int>(localPlayer.hand);
            currentWallTiles = new List<int>(localPlayer.wall);
            currentWaitTiles = new List<int>(localPlayer.wait != null ? localPlayer.wait : new int[0]);

            // 4. 待ち牌表示の更新
            if (waitUI != null)
            {
                waitUI.DisplayWaits(currentWaitTiles);
            }
        }

        public void DiscardSelectedTile()
        {
            if (currentPhaseStatus != RoundStatus.Discard) return;
            if (selectedTileIds.Count == 0) return;
            
            // ログ確認中は打牌を無効化する
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;

            int tileToDiscard = selectedTileIds[0];
            Debug.Log($"Discarding tile: {tileToDiscard}");

            // Send action to server
            SendActionToServer("discard", new ActionPayload { tile = tileToDiscard });
            
            // Wait for GameState update or explicit discard response to actually remove tile and add to river
            ClearSelection();
        }

        public void DeselectAbility()
        {
            if (abilityUI != null) abilityUI.DeselectAll();
        }

        public bool IsPointerInHandArea(Vector2 screenPos)
        {
            if (handUI != null) return handUI.IsPointInHandArea(screenPos);
            return false;
        }

        private void RefreshUI()
        {
            // 旧RefreshUIは廃止し、全体再構築はRebuildAllTilesFromStateで行う想定です。
            // ここは空にしておくか、削除します。
            Debug.Log($"UI Data state - Hand: {currentHandTiles.Count}, Wall: {currentWallTiles.Count}");
        }

        public void CompleteHandSelection()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            
            // ログ確認中は無効化
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;
            
            Debug.Log("Hand Selection Complete! Transitioning to discard phase.");
            
            // エンジンに選択完了（打牌フェーズへの移行等）を通知する
            // Pythonエンジン側は action="selected" を待っている
            SendActionToServer("selected", new ActionPayload { hand = currentHandTiles });
            
            // 状態に応じたUIの切り替えなどは今後Engineからの {"type":"game_state"} 受信時に ApplyGameState で一括処理されるため、
            // ここでの直書き（currentPhaseStatus = "discard"）は原則不要ですが、レスポンス前の先行UI変更として残すことも可能です。
        }

        [Header("Tile Prefab")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager; // Added to set visuals centrally // JSON受信時など全体再構築用に保持

        private void RebuildAllTilesFromState()
        {
            if (tilePrefab == null)
            {
                Debug.LogError("GameUIManager: tilePrefab is missing! Cannot rebuild Hand/Wall from scratch.");
                return;
            }

            // --- 1. HandUIの再構築 ---
            if (handUI != null)
            {
                // 一旦手牌のTransformをすべて破棄
                for (int i = handUI.GetHandSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = handUI.GetHandSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                handUI.GetHandSlots().Clear();

                foreach (var id in currentHandTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform); // 一時的にUIManager下
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    if (rt == null) rt = obj.transform as RectTransform; // Fallback
                    
                    if (rt != null) {
                        // Initialize interaction and visual here centrally for Hand
                        InitializeTileComponent(rt, id, true);
                        handUI.AddTileToHand(rt, id);
                    }
                }
            }

            // --- 2. WallUIの再構築 ---
            if (wallUI != null)
            {
                // 一旦壁のTransformをすべて破棄
                for (int i = wallUI.GetWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = wallUI.GetWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                wallUI.GetWallSlots().Clear();

                // Generate physical tiles for Wall
                List<RectTransform> wallGenerated = new List<RectTransform>();
                foreach (var id in currentWallTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    if (rt == null) rt = obj.transform as RectTransform;

                    if (rt != null) {
                        InitializeTileComponent(rt, id, false);
                        wallGenerated.Add(rt);
                    }
                }

                // Pass generated tiles to WallUI for layout only
                wallUI.LayoutWallTiles(wallGenerated, currentWallTiles, currentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
            }
            // --- 3. Enemy HandUIの再構築 ---
            if (enemyHandUI != null)
            {
                enemyHandUI.ClearHand();

                // サーバーから13枚の手牌データが届いている（またはモックで生成された）ものだけを生成
                List<int> tilesToSpawn = new List<int>(currentEnemyHandTiles);

                foreach (var id in tilesToSpawn)
                {
                    GameObject obj = Instantiate(tilePrefab, transform); 
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    if (rt == null) rt = obj.transform as RectTransform; 
                    
                    if (rt != null) {
                        // デバッグフラグがONの場合のみ実際のIDで表示。通常は裏向き(0)
                        int visualId = showEnemyHandDebug ? id : 0;
                        InitializeTileComponent(rt, visualId, false);
                        
                        // 敵専用のメソッドへ追加（realIdに本来のIDを渡す）
                        enemyHandUI.AddEnemyTile(rt, visualId, id);
                    }
                }
            }

            // --- 4. Enemy WallUIの再構築 ---
            if (enemyWallUI != null)
            {
                // 一旦壁のTransformをすべて破棄
                for (int i = enemyWallUI.GetEnemyWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = enemyWallUI.GetEnemyWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                enemyWallUI.GetEnemyWallSlots().Clear();

                List<RectTransform> enemyWallGenerated = new List<RectTransform>();
                foreach (var id in currentEnemyWallTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>();
                    if (rt == null) rt = obj.transform as RectTransform;

                    if (rt != null) {
                        // 相手の壁牌は裏向き(0)で表示する
                        int visualId = showEnemyHandDebug ? id : 0;
                        InitializeTileComponent(rt, visualId, false);
                        enemyWallGenerated.Add(rt);
                    }
                }

                // Pass generated tiles to EnemyWallUI for layout
                enemyWallUI.LayoutEnemyWallTiles(enemyWallGenerated, currentEnemyWallTiles, currentPhaseStatus == RoundStatus.Discard);
            }
        }
        
        // Helper method to set up components freshly instantiated by GameUIManager
        private void InitializeTileComponent(RectTransform rt, int id, bool inHand)
        {
            if (tileResourceManager != null)
            {
                var visual = rt.GetComponent<TileVisual>();
                if (visual != null) visual.SetTile(id, tileResourceManager.GetTileSprite(id));
            }

            var interaction = rt.GetComponent<TileInteraction>();
            if (interaction == null) interaction = rt.gameObject.AddComponent<TileInteraction>();
            
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>(); // Fallback
            
            interaction.Initialize(id, inHand, this, canvas);
        }
        
        // Public methods to access UI components if needed
        public HandUI HandUI => handUI;
        public DialogueUI DialogueUI => dialogueUI;
        public PlayerInfoUI PlayerInfoUI => playerInfoUI;
        public EnemyInfoUI EnemyInfoUI => enemyInfoUI;
        public AbilityUI AbilityUI => abilityUI;
        public YakuListUI YakuListUI => yakuListUI;
        public BettingUI BettingUI => bettingUI;
        public PhaseTransitionUI PhaseTransitionUI => phaseTransitionUI;

        public void ShowMatchmakingWaiting()
        {
            if (matchmakingUI != null)
            {
                matchmakingUI.ShowWaiting();
            }
            
            // その他のUI構造を待機中は非表示にする
            SetMatchUIVisibility(false); // Hand, Wall, YakuListなどを隠す
            
            if (riverUI != null) riverUI.gameObject.SetActive(false);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
            if (enemyHandUI != null) enemyHandUI.gameObject.SetActive(false);
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            
            // HPパネル等も確実に非表示
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
            
            if (abilityUI != null) abilityUI.gameObject.SetActive(false);
            if (ronAnimationUI != null) ronAnimationUI.gameObject.SetActive(false);
            
            // BettingUIも見えてしまわないように隠す
            if (bettingUI != null) bettingUI.HideBettingPhase();
        }

        public void OnGameStarted()
        {
            // マッチ成立！待機UIを消す
            if (matchmakingUI != null) matchmakingUI.Hide();
            // DialogueUIで開始を知らせる
            if (dialogueUI != null) 
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText("Match Found! Game Starting...");
            }
            // ゲーム開始でアビリティボタン等も表示を許可するならここでSetActive(true)するか、
            // 後の HandSelection(Dealing) フェーズで一括制御します
        }

        public void ApplyGameStateFromJSON(string jsonString, string localPlayerId)
        {
            try
            {
                ServerMessageBase baseMsg = JsonUtility.FromJson<ServerMessageBase>(jsonString);
                if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
                {
                    Debug.LogWarning($"Unknown or empty JSON received: {jsonString}");
                    return;
                }

                switch (baseMsg.type)
                {
                    case "matching_waiting":
                        Debug.Log("[GameUIManager] Waiting for match...");
                        ShowMatchmakingWaiting();
                        break;

                    case "game_started":
                        Debug.Log("[GameUIManager] Game Started!");
                        if (matchmakingUI != null) matchmakingUI.Hide();
                        if (dialogueUI != null) 
                        {
                            dialogueUI.gameObject.SetActive(true);
                            dialogueUI.ShowText("Match Found! Game Starting...");
                        }
                        UpdatePhaseStatus(RoundStatus.Dealing);
                        break;

                    case "bet":
                        Debug.Log("[GameUIManager] Betting complete for both players. Starting animation...");
                        // サーバーからのレスポンス形式が確定するまで、一旦ダミー値を入れてエラーを解消します
                        OnBettingCompleteFromServer(2000, 2000, 50000, 50000);
                        break;

                    case "wall_dealt":
                        Debug.Log($"[GameUIManager] parsing wall_dealt: {jsonString}");
                        WallDealtMessage dealtMsg = JsonUtility.FromJson<WallDealtMessage>(jsonString);
                        // tenpai_examplesはJsonUtilityでデシリアライズできないため手動パースする
                        List<List<int>> tenpaiExamplesForLocal = ParseTenpaiExamplesFromJson(jsonString, localPlayerId);
                        HandleWallDealt(dealtMsg, localPlayerId, tenpaiExamplesForLocal);
                        break;

                    case "hand_selected":
                        HandSelectedMessage handMsg = JsonUtility.FromJson<HandSelectedMessage>(jsonString);
                        HandleHandSelected(handMsg, localPlayerId);
                        break;

                    case "turn_decided":
                        TurnDecidedMessage turnMsg = JsonUtility.FromJson<TurnDecidedMessage>(jsonString);
                        // Update which player is active based on turnMsg.current_player
                        if (turnMsg != null)
                        {
                            bool isLocalTurn = (turnMsg.current_player == 0); // Assuming 0 is local, 1 is enemy
                            Debug.Log($"[GameUIManager] Turn Decided. Local Turn: {isLocalTurn}");
                            // TODO: Show visual indicator for turn
                        }
                        UpdatePhaseStatus(RoundStatus.Discard);
                        break;
                        
                    case "is_tenpai":
                        IsTenpaiMessage tenpaiMsg = JsonUtility.FromJson<IsTenpaiMessage>(jsonString);
                        if (tenpaiMsg != null && tenpaiMsg.data != null && tenpaiMsg.data.waits != null)
                        {
                            currentWaitTiles.Clear();
                            foreach (var wait in tenpaiMsg.data.waits)
                            {
                                currentWaitTiles.Add(wait.tile);
                            }
                            if (waitUI != null && currentPhaseStatus == RoundStatus.Discard) 
                                waitUI.DisplayWaits(currentWaitTiles);
                        }
                        break;
                        
                    case "not_tenpai":
                        currentWaitTiles.Clear();
                        if (waitUI != null) waitUI.Hide();
                        break;

                    case "error":
                        ErrorMessage errorMsg = JsonUtility.FromJson<ErrorMessage>(jsonString);
                        Debug.LogError($"[Server Error] {errorMsg?.message}");
                        break;
                        
                    case "discard":
                        DiscardMessage discardMsg = JsonUtility.FromJson<DiscardMessage>(jsonString);
                        if (discardMsg != null)
                        {
                            bool isLocal = (discardMsg.client_id == localPlayerId);
                            // イベントをトリガーしてUIやアニメーションを連動
                            HandleDiscardEvent(discardMsg.tile, isLocal);
                        }
                        break;
                        
                    case "agari":
                        // ロンやツモの上がりイベント
                        AgariMessage agariMsg = JsonUtility.FromJson<AgariMessage>(jsonString);
                        if (agariMsg != null)
                        {
                            bool isLocalWin = (agariMsg.winner_client_id == localPlayerId);
                            // Store the win state temporarily for HandlePhaseVisibility to read.
                            // To avoid adding fields, we will pass it indirectly or handle it in UpdatePhaseStatus.
                            // Due to script length limits, we will handle it directly below.
                            lastIsLocalWin = isLocalWin;
                        }
                        UpdatePhaseStatus(RoundStatus.Agari);
                        break;

                    case "game_end":
                        // Python: {"type": "game_end", "final_scores": {"client_id1": score1, "client_id2": score2}}
                        Debug.Log($"[GameUIManager] Game End Received: {jsonString}");
                        ParseGameEndAndShowResults(jsonString, localPlayerId);
                        break;

                    default:
                        // Debug.Log($"[GameUIManager] Unhandled message type: {baseMsg.type}");
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse JSON: {e.Message}\nJSON:\n{jsonString}");
            }
        }
        
        private void UpdatePhaseStatus(RoundStatus newStatus)
        {
            currentPhaseStatus = newStatus;
            if (PhaseManager.Instance != null)
            {
                PhaseManager.Instance.ChangeRoundStatus(newStatus);
            }
            
            // 手牌のレイアウト更新 (コンテナ切り替えのため)
            if (handUI != null)
            {
                handUI.UpdateLayout(currentPhaseStatus);
            }

            // 壁のレイアウト更新 (打牌フェイズへの移行などのため)
            if (wallUI != null)
            {
                List<RectTransform> remainingTiles = new List<RectTransform>();
                foreach (var st in wallUI.GetWallSlots())
                {
                    if (st != null) remainingTiles.Add(st);
                }
                wallUI.LayoutWallTiles(remainingTiles, currentWallTiles, currentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
            }
            
            // UI制御 (GameUIManagerのこれまでの処理を流用)
            HandlePhaseVisibility(newStatus, null, null);
        }
        
        private void HandleWallDealt(WallDealtMessage msg, string localPlayerId, List<List<int>> tenpaiExamples = null)
        {
            UpdatePhaseStatus(RoundStatus.HandSelection);
            
            if (msg.hands == null)
            {
                Debug.LogWarning("[HandleWallDealt] msg.hands is null!");
                return;
            }
            
            Debug.Log($"[HandleWallDealt] myClientId='{localPlayerId}', hands count={msg.hands.Length}");
            foreach (var h in msg.hands)
            {
                Debug.Log($"[HandleWallDealt] checking h.client_id='{h.client_id}' == localPlayerId='{localPlayerId}' => {h.client_id == localPlayerId}");
                if (h.client_id == localPlayerId)
                {
                    currentWallTiles = SortTileIds(new List<int>(h.hand));
                    currentHandTiles = new List<int>();
                    Debug.Log($"[HandleWallDealt] => 自分のWall ({currentWallTiles.Count}枚) をセット");
                    
                    // 手動パースされた tenpai_examples を使用
                    if (tenpaiExamples != null && tenpaiExamples.Count > 0)
                    {
                        currentTenpaiExamples = tenpaiExamples.ConvertAll(e => e.ToArray());
                        Debug.Log($"[HandleWallDealt] tenpai_examples: {currentTenpaiExamples.Count}形");
                    }
                    else
                    {
                        currentTenpaiExamples.Clear();
                        Debug.LogWarning("[HandleWallDealt] tenpai_examplesが空です");
                    }
                }
                else
                {
                    if (h.hand != null)
                    {
                        currentEnemyWallTiles = new List<int>(h.hand);
                        currentEnemyHandTiles = new List<int>();
                        Debug.Log($"[HandleWallDealt] => 相手({h.client_id})のWall ({currentEnemyWallTiles.Count}枚) をセット");
                    }
                }
            }
            
            RebuildAllTilesFromState();
        }

        private void HandleHandSelected(HandSelectedMessage msg, string localPlayerId)
        {
            // msg.handsが存在しない場合は個人向け確認メッセージ（自分の選択が受理された）
            // msg.handsが存在する場合は全員完了のブロードキャスト → ベットフェイズへ
            if (msg.hands == null)
            {
                // 自分の手牌選択が受理された。相手の選択待ち。
                Debug.Log("[HandleHandSelected] 自分の手牌選択が受理されました。相手の選択を待っています...");
                if (dialogueUI != null) dialogueUI.ShowText("相手の手牌選択を待っています...");
                return;
            }
            
            UpdatePhaseStatus(RoundStatus.Betting);
            
            Debug.Log($"[HandleHandSelected] myClientId='{localPlayerId}', hands count={msg.hands.Length}");
            foreach (var h in msg.hands)
            {
                Debug.Log($"[HandleHandSelected] checking h.client_id='{h.client_id}' == localPlayerId='{localPlayerId}' => {h.client_id == localPlayerId}");
                if (h.client_id == localPlayerId)
                {
                    currentHandTiles = SortTileIds(new List<int>(h.hand));
                    currentWallTiles = SortTileIds(new List<int>(h.wall));
                    currentWaitTiles = new List<int>(h.wait);
                    Debug.Log($"[HandleHandSelected] => 自分のHand ({currentHandTiles.Count}枚), Wall ({currentWallTiles.Count}枚) をセット");
                }
                else
                {
                    if (h.hand != null)
                    {
                        currentEnemyHandTiles = new List<int>(h.hand);
                        if (h.wall != null)
                        {
                            currentEnemyWallTiles = new List<int>(h.wall);
                        }
                        Debug.Log($"[HandleHandSelected] => 相手({h.client_id})のHand ({currentEnemyHandTiles.Count}枚) をセット");
                    }
                }
            }
            
            RebuildAllTilesFromState();
            ClearSelection();
            // We already call HandlePhaseVisibility within UpdatePhaseStatus
        }

        // シリアライズ用の構造体を定義（JsonUtilityは匿名クラスをシリアライズできないため）
        // サーバー期待形式: {"type":"action","data":{"action":"...","data":{...}}}
        [System.Serializable]
        public class ActionMessage
        {
            public string type = "action";
            public ActionMessageData data;
        }

        [System.Serializable]
        public class ActionMessageData
        {
            public string action;
            public ActionPayload data;
        }

        [System.Serializable]
        public class ActionPayload
        {
            public int amount;
            public List<int> hand;
            public int tile; // For discard, etc.
        }

        /// <summary>
        /// wall_dealt JSONから指定client_idのtenpai_examplesを手動パースして取得する。
        /// JsonUtilityはList&lt;int[]&gt;(ネストした配列)をデシリアライズできないため必要。
        /// </summary>
        private List<List<int>> ParseTenpaiExamplesFromJson(string jsonString, string targetClientId)
        {
            var result = new List<List<int>>();
            try
            {
                // "hands" の中から targetClientId のブロックを探す
                // JSONは {"type":"wall_dealt","hands":[{"client_id":"C0001","hand":[...],"tenpai_examples":[[...],[...]]},...],...}
                int handsStart = jsonString.IndexOf("\"hands\"");
                if (handsStart < 0) return result;

                // client_idを持つブロックを探す
                int searchFrom = handsStart;
                while (true)
                {
                    int cidStart = jsonString.IndexOf("\"client_id\"", searchFrom);
                    if (cidStart < 0) break;

                    // client_idの値を取得
                    int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                    int valEnd = jsonString.IndexOf('"', valStart);
                    if (valStart < 0 || valEnd < 0) break;
                    string cid = jsonString.Substring(valStart, valEnd - valStart);

                    if (cid == targetClientId)
                    {
                        // tenpai_examplesを探す
                        int tenpaiKey = jsonString.IndexOf("\"tenpai_examples\"", valEnd);
                        if (tenpaiKey < 0) break;

                        int arrStart = jsonString.IndexOf('[', tenpaiKey + 17);
                        if (arrStart < 0) break;

                        // 配列の直後の最初の非空白文字を確認する
                        // '[' なら入れ子配列（2D）、それ以外なら1Dフラット配列
                        int peek = arrStart + 1;
                        while (peek < jsonString.Length && jsonString[peek] == ' ') peek++;

                        if (peek < jsonString.Length && jsonString[peek] == '[')
                        {
                            // 2Dネスト配列: [[id, id, ...], [...]] 各 [] を1つの例として取得
                            int depth = 0;
                            int innerStart = -1;
                            for (int i = arrStart; i < jsonString.Length; i++)
                            {
                                if (jsonString[i] == '[')
                                {
                                    depth++;
                                    if (depth == 2) innerStart = i;
                                }
                                else if (jsonString[i] == ']')
                                {
                                    if (depth == 2 && innerStart >= 0)
                                    {
                                        string inner = jsonString.Substring(innerStart + 1, i - innerStart - 1);
                                        var hand = new List<int>();
                                        foreach (var token in inner.Split(','))
                                        {
                                            if (int.TryParse(token.Trim(), out int val))
                                                hand.Add(val);
                                        }
                                        if (hand.Count > 0) result.Add(hand);
                                        innerStart = -1;
                                    }
                                    depth--;
                                    if (depth == 0) break;
                                }
                            }
                        }
                        else
                        {
                            // 1Dフラット配列: [id, id, ...] → 1つの例として扱う（サーバーが単一例を送る場合）
                            int arrEnd = jsonString.IndexOf(']', arrStart);
                            if (arrEnd > arrStart)
                            {
                                string inner = jsonString.Substring(arrStart + 1, arrEnd - arrStart - 1);
                                var hand = new List<int>();
                                foreach (var token in inner.Split(','))
                                {
                                    if (int.TryParse(token.Trim(), out int val))
                                        hand.Add(val);
                                }
                                if (hand.Count > 0) result.Add(hand);
                            }
                        }
                        break; // 対象のclient_idが見つかったので終了
                    }

                    searchFrom = valEnd + 1;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ParseTenpaiExamples] パースエラー: {e.Message}");
            }

            Debug.Log($"[ParseTenpaiExamples] client_id='{targetClientId}' -> {result.Count}形の聴牌例を取得");
            return result;
        }

        /// <summary>
        /// 牌IDリストを「萬子(0-8) → 筒子(9-17) → 索子(18-26) → 字牌(27-33)」の順に、
        /// かつ同じIDの牌が隣り合うようにソートする。
        /// </summary>
        private List<int> SortTileIds(List<int> ids)
        {
            // IDの昇順でソートするだけで自然に 萬子→筒子→索子→字牌 の順になる
            ids.Sort();
            return ids;
        }

        // Helper string sending method targeting Network
        public async void SendActionToServer(string actionType, ActionPayload dataPayload)
        {
            if (useDebugClient && debugWebSocketClient != null)
            {
                debugWebSocketClient.ReceiveActionFromPlayer(actionType, dataPayload);
                return;
            }

            if (webSocketClient == null) return;

            var msg = new ActionMessage
            {
                type = "action",
                data = new ActionMessageData
                {
                    action = actionType,
                    data = dataPayload
                }
            };

            string json = JsonUtility.ToJson(msg);
            await webSocketClient.SendAsync(json);
        }

        // --- イベントベースに移行したため、フルデータ同期（ApplyGameState）は不要になります ---
        // 打牌イベントなどの差分用処理のみを今後ここに追加します

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            // 打牌が送信されたことを通知するイベントハンドラです
            
            // 1. UIの表示を更新: Wallから削除しRiverへ追加
            if (isLocalPlayer)
            {
                if (currentWallTiles.Contains(discardedTileId))
                {
                    currentWallTiles.Remove(discardedTileId);
                }

                if (wallUI != null)
                {
                    // WallSlotsから取り出し、ゲームオブジェクトを破棄（RiverUIは新規生成するため）
                    RectTransform tileRt = wallUI.GrabTile(discardedTileId);
                    if (tileRt != null)
                    {
                        Destroy(tileRt.gameObject);
                    }
                    
                    // 隙間を詰めるなどの再レイアウト
                    List<RectTransform> remainingTiles = new List<RectTransform>();
                    foreach (var st in wallUI.GetWallSlots())
                    {
                        if (st != null) remainingTiles.Add(st);
                    }
                    wallUI.LayoutWallTiles(remainingTiles, currentWallTiles, currentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }

                if (riverUI != null)
                {
                    riverUI.AddTile(discardedTileId);
                }
            }
            else
            {
                if (currentEnemyWallTiles.Count > 0)
                {
                    currentEnemyWallTiles.RemoveAt(0); // 内部データから削除
                }

                if (enemyWallUI != null && enemyRiverUI != null)
                {
                    RectTransform tileRt = enemyWallUI.GrabEnemyTile();
                    if (tileRt != null)
                    {
                        // アニメーションや小手先のハックなしで、完全にWallContainerの子からRiverContainerの子へ付け替える
                        enemyRiverUI.AddExistingTile(tileRt, discardedTileId);
                    }
                }
                else if (enemyRiverUI != null)
                {
                    enemyRiverUI.AddTile(discardedTileId);
                }
            }

            // 2. キャラクターのアニメーションや会話を再生
            OnTileDiscarded(discardedTileId, isLocalPlayer);
        }

        private System.Collections.IEnumerator AnimateTileToRiver(RectTransform tileRt, Vector3 targetLocalPos, float duration, System.Action onComplete)
        {
            if (tileRt == null) yield break;

            Vector3 startPos = tileRt.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (tileRt == null) yield break;
                float t = elapsed / duration;
                // Ease out quad
                t = t * (2f - t);
                tileRt.localPosition = Vector3.Lerp(startPos, targetLocalPos, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (tileRt != null)
            {
                tileRt.localPosition = targetLocalPos;
            }
            onComplete?.Invoke();
        }

        // --- リアクション用キュー ---
        private Queue<System.Action> reactionQueue = new Queue<System.Action>();
        private bool isProcessingReactions = false;

        private void OnTileDiscarded(int tileId, bool isLocalPlayer)
        {
            reactionQueue.Enqueue(() => StartCoroutine(ProcessDiscardEvent(tileId, isLocalPlayer)));
            if (!isProcessingReactions)
            {
                ProcessNextReaction();
            }
        }

        public void ProcessNextReaction()
        {
            if (reactionQueue.Count > 0)
            {
                // ログが開かれている間はキューの消化を止める
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    isProcessingReactions = false; // 止まっている状態
                    return;
                }

                isProcessingReactions = true;
                var action = reactionQueue.Dequeue();
                action.Invoke();
            }
            else
            {
                isProcessingReactions = false;
            }
        }

        private System.Collections.IEnumerator WaitWhileLogIsOpen(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // ログが開いている間はタイマーを進めずに待機
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private System.Collections.IEnumerator ProcessDiscardEvent(int tileId, bool isLocalPlayer)
        {
            // 打牌された牌の名前を取得
            string tileName = new TileData(tileId).GetTileName();
            Sprite reactionSprite = null; // 本来はResources等から取得

            if (isLocalPlayer)
            {
                // プレイヤーの打牌：敵がそれに反応するのみ
                if (dialogueUI != null)
                {
                    dialogueUI.ShowText("「プレイヤーが何かを捨てたな…」");
                }
                
                // キャラクターの立ち絵を変更（プレイヤーの打牌への反応）
                if (enemyInfoUI != null && reactionSprite != null) 
                    enemyInfoUI.SetCharacterSprite(reactionSprite);
            }
            else
            {
                // 敵の打牌：自身が打牌する宣言のみ
                if (dialogueUI != null)
                {
                    dialogueUI.ShowText($"「{tileName}を切るわ！」");
                }
                
                // 必要に応じてキャラクターの立ち絵も変更
                if (enemyInfoUI != null && reactionSprite != null) 
                    enemyInfoUI.SetCharacterSprite(reactionSprite);
            }

            // ログが開かれている間は時間のカウントを一時停止して待つ
            yield return StartCoroutine(WaitWhileLogIsOpen(reactionDisplayDuration));

            // 次のイベントへ
            ProcessNextReaction();
        }

        private void HandlePhaseVisibility(RoundStatus status, int? localHealth = null, int? enemyHealth = null)
        {
            if (isTransitioning) return; // トランジション中は画面表示を強制上書きしない

            // デフォルトでriverUIは非表示にし、打牌(discard)フェーズのみ表示する
            if (riverUI != null)
            {
                riverUI.gameObject.SetActive(status == RoundStatus.Discard);
            }
            if (enemyRiverUI != null)
            {
                enemyRiverUI.gameObject.SetActive(status == RoundStatus.Discard);
            }
            if (enemyHandUI != null)
            {
                enemyHandUI.gameObject.SetActive(status == RoundStatus.Discard);
            }

            // HPの表示状態を更新（手牌選択中は非表示、対局中(discard等)は表示したい場合はここでオンオフ可能）
            // ユーザー要望: 掛け金決定と手牌決定フェイズの間（つまり、transitonUIの処理の間）敵のHPのUIを消しておきたい
            // ※ここではデフォルト非表示などにしておき、必要なフェーズで表示する形も可能ですが、
            // 今回は Transition の前後で制御するため MatchUI の表示非表示のロジックに組み込ませます

            switch (status)
            {
                case RoundStatus.Betting:
                    // 賭けフェイズでは手牌などは消すが、敵のHPや立ち絵は表示する。自分のHPは非表示にする。
                    SetMatchUIVisibility(false); 
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                    if (waitUI != null) waitUI.gameObject.SetActive(false); // 待ち牌UIを非表示
                    
                    StartBettingPhase(localHealth ?? 20000);
                    break;
                case RoundStatus.Dealing:
                case RoundStatus.HandSelection:
                    // 手牌選択フェイズでは手牌・壁、および自分・敵のHPパネルを表示する
                    SetMatchUIVisibility(true);
                    
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (waitUI != null) waitUI.gameObject.SetActive(false); // 待ち牌UIを非表示
                    if (abilityUI != null) abilityUI.gameObject.SetActive(true); // 手牌選択時はアビリティ使用可能
                    break;
                case RoundStatus.TurnDecision:
                    // トランジション中（掛け金→打牌移行時）、敵も自分もHP（パネル全体）を非表示にする
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                    if (waitUI != null) waitUI.gameObject.SetActive(false); // 待ち牌UIを非表示
                    break;
                case RoundStatus.Discard:
                    // 打牌フェイズでは自分の手牌UI自体は表示し、内部のコンテナ切り替えで打牌用レイアウトを見せる
                    if (handUI != null) handUI.gameObject.SetActive(true);
                    // 自分の壁UIは表示したままとし、打牌用配置になっていることを保証する
                    if (wallUI != null) wallUI.gameObject.SetActive(true);
                    
                    // 敵の壁UIも表示したままとする
                    if (enemyWallUI != null) enemyWallUI.gameObject.SetActive(true);
                    
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    
                    // 打牌フェイズに入ったら、待ち牌がある場合のみ表示する
                    if (waitUI != null && currentWaitTiles != null && currentWaitTiles.Count > 0)
                    {
                        waitUI.gameObject.SetActive(true);
                        waitUI.DisplayWaits(currentWaitTiles);
                    }
                    if (abilityUI != null) abilityUI.gameObject.SetActive(true); // スキルボタンは出しておく
                    break;
                case RoundStatus.Agari:
                case RoundStatus.Ron:
                case RoundStatus.Result:
                    // ★ ロン（あがり）演出の開始
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    
                    if (ronAnimationUI != null)
                    {
                        // サーバーのGameState拡張により、実際の役リストや上がったプレイヤー情報が渡される想定ですが、
                        // まずは「自分の手牌」か「敵の手牌」かを current_player（あるいは勝者フラグ）等で判定してアニメーションに渡します
                        bool isLocalWin = lastIsLocalWin; // Agariイベントで保存したフラグを使用
                        List<int> winningHand = isLocalWin ? new List<int>(currentHandTiles) : new List<int>(currentEnemyHandTiles);
                        
                        // 今回はテストとして仮データを渡してアニメーションを実行
                        List<string> dummyYaku = new List<string> { "立直 (1飜)", "一発 (1飜)" };
                        string dummyFormula = "30符 2飜";
                        string dummyRank = "満貫";
                        int dummyRonTile = winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0; // 手牌の最後を仮に当たり牌とする
                        
                        // 川や会話ログはいったん隠す
                        if (riverUI != null) riverUI.gameObject.SetActive(false);
                        if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
                        if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
                        SetMatchUIVisibility(false); // 手牌や壁を隠してロン専用のオーバーレイを出す

                        ronAnimationUI.gameObject.SetActive(true); // Coroutineを実行するためアクティブ化
                        ronAnimationUI.PlayRonSequence(
                            winningHand, 
                            dummyRonTile, 
                            dummyYaku, 
                            dummyFormula, 
                            dummyRank, 
                            isLocalWin, 
                            () => OnRonAnimationComplete(isLocalWin)
                        );
                    }
                    break;
                case RoundStatus.Liquidation:
                    break;
                default:
                    Debug.LogWarning($"Unknown phase status: {status}");
                    break;
            }
        }
        
        private void OnRonAnimationComplete(bool isLocalWin)
        {
            // 演出終了後、エフェクトを再生する
            if (isLocalWin)
            {
                if (victoryEffectPrefab != null && playerInfoUI != null) 
                    Instantiate(victoryEffectPrefab, playerInfoUI.transform.position, Quaternion.identity);
                if (damageEffectPrefab != null && enemyInfoUI != null) 
                    Instantiate(damageEffectPrefab, enemyInfoUI.transform.position, Quaternion.identity);
            }
            else
            {
                if (victoryEffectPrefab != null && enemyInfoUI != null) 
                    Instantiate(victoryEffectPrefab, enemyInfoUI.transform.position, Quaternion.identity);
                if (damageEffectPrefab != null && playerInfoUI != null) 
                    Instantiate(damageEffectPrefab, playerInfoUI.transform.position, Quaternion.identity);
            }
            
            Debug.Log("Ron Animation and Effects Complete.");
        }

        private void StartBettingPhase(int currentHealth)
        {
            if (bettingUI != null)
            {
                Debug.Log($"Starting Betting Phase with HP: {currentHealth}");
                bettingUI.ShowBettingPhase(20000, currentHealth, OnBetConfirmed);
            }
            else
            {
                Debug.LogError("BettingUI reference is missing in GameUIManager! Please assign it in the Inspector.");
            }
        }

        public void SetMatchUIVisibility(bool visible)
        {
            // The components to hide during betting phase & phase transition
            if (handUI != null) handUI.gameObject.SetActive(visible);
            if (wallUI != null) wallUI.gameObject.SetActive(visible);
            if (enemyWallUI != null) enemyWallUI.gameObject.SetActive(visible);
            // riverUI visibility is managed by HandlePhaseVisibility now
            
            // playerInfoUI(Enemy HP) は Transition 中〜手牌選択前などで非表示にしたい場合、ここでコントロールされます。
            // visible == false なら確実に消えます。
            // ※ここではHPの表示を個別で制御するため、SetMatchUIVisibility から playerInfoUI, enemyInfoUI の個別の制御を外します。
            
            if (yakuListUI != null) yakuListUI.gameObject.SetActive(visible);
            
            // 待ち牌表示も打牌フェイズ以外では消すベースとし、特定のフェーズ（基本Discard）のみ表示したい場合
            // ここで一旦手牌フェイズ・掛け金フェイズに合わせて消しておく（表示されるかは後続の処理次第）
            if (waitUI != null && currentWaitTiles != null && currentWaitTiles.Count > 0)
            {
                // Discardフェーズの時は内容に応じて残し、それ以外の一時不可視（Transition等）の場合は隠す
                if (!visible)
                {
                    waitUI.gameObject.SetActive(false);
                }
            }
            
            // DialogueUI (enemy comments) and BettingUI remain active
        }

        private void OnBetConfirmed(int betAmount)
        {
            Debug.Log($"Bet confirmed: {betAmount}");
            bettingUI.HideBettingPhase();

            // エンジンに通知 (Pythonエンジン側は action="betting" を待っている)
            SendActionToServer("betting", new ActionPayload { amount = betAmount });

            // 本来はサーバーからの "bet" メッセージ受信後にアニメーションを開始しますが、
            // サーバー側の_betting実装が未完了のため、今回は直接ローカルで完了処理を呼び出してフェーズを進めます。
            StartCoroutine(MockBettingCompleteRoutine(betAmount));
        }

        private System.Collections.IEnumerator MockBettingCompleteRoutine(int playerBet)
        {
            yield return new WaitForSeconds(1.0f); // 相手のベット完了を少し待機する演出
            
            // 適当なダミー値を渡して進める。必要なら自分・相手のHPを反映
            int localHp = playerInfoUI != null ? 20000 : 20000;
            int enemyHp = enemyInfoUI != null ? 20000 : 20000;
            
            OnBettingCompleteFromServer(playerBet, playerBet, localHp, enemyHp);
        }

        public void OnBettingCompleteFromServer(int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
            // サーバーから "bet" (両者のベットが完了した) 通知が来たらアニメーション開始
            TriggerBettingAnimationPhase($"Round 1", playerBet, enemyBet, playerHp, enemyHp); 
        }

        public void TriggerBettingAnimationPhase(string roundString, int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
             if (phaseTransitionUI != null)
             {
                 Debug.Log("Triggering Phase Transition Animation.");
                 isTransitioning = true;
                 
                 // トランジション（移行時）なので、HPパネルを自分も敵も非表示にする
                 if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
                 if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                 
                 // 画面に横線が入る瞬間で敵の会話UIが消えるようにする
                 if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);

                 // Start transition
                 phaseTransitionUI.PlayTransition(roundString, playerInfoUI, playerBet, enemyBet, playerHp, enemyHp,
                    onMidpoint: () => {
                         // Swap UI to Match UI here behind the dark screen if necessary
                         // This is where you might enable HandUI, WallUI, etc, if they were hidden during betting
                         Debug.Log("Midpoint of Transition (Screen is Dark)");

                         // なお、ここでは会話UIは非表示のまま維持する
                    },
                    onComplete: () => {
                         Debug.Log("Transition Complete, Match Phase begins.");
                         isTransitioning = false;
                         
                         // トランジションが終了したら「対局画面（MatchUI）」のUI要素を表示する
                         SetMatchUIVisibility(true); 
                         
                         // 今回はサーバーからの "bet" 以降のステート移行応答がないため、
                         // アニメーション完了時に強制的に打牌フェイズ (Discard) へ進めます。
                         if (currentPhaseStatus == RoundStatus.Betting)
                         {
                             UpdatePhaseStatus(RoundStatus.Discard);
                         }

                         // 現在のフェーズに合わせて表示状態を反映する
                         HandlePhaseVisibility(currentPhaseStatus);
                         
                         // 対局（打牌フェイズ）が始まったら会話UIを出す
                         if (dialogueUI != null) dialogueUI.gameObject.SetActive(true);
                    }
                 );
             }
             else
             {
                 Debug.LogError("PhaseTransitionUI reference is missing in GameUIManager! Please assign it in the Inspector.");
             }
        }
        public void ShowDialogue(string text)
        {
            if (dialogueUI != null)
            {
                dialogueUI.ShowText(text);
            }
        }
        
        private void ParseGameEndAndShowResults(string jsonString, string localPlayerId)
        {
            // JsonUtility cannot parse Dictionaries. We need to parse "final_scores": {"client1": score, "client2": score}
            int scoresStart = jsonString.IndexOf("\"final_scores\"");
            if (scoresStart >= 0)
            {
                int dictStart = jsonString.IndexOf('{', scoresStart);
                int dictEnd = jsonString.IndexOf('}', dictStart);
                if (dictStart >= 0 && dictEnd > dictStart)
                {
                    string dictStr = jsonString.Substring(dictStart + 1, dictEnd - dictStart - 1);
                    string[] pairs = dictStr.Split(',');
                    
                    int localScore = 0;
                    int enemyScore = 0;

                    foreach (var pair in pairs)
                    {
                        string[] kvp = pair.Split(':');
                        if (kvp.Length == 2)
                        {
                            string key = kvp[0].Replace("\"", "").Trim();
                            string val = kvp[1].Trim();
                            if (int.TryParse(val, out int score))
                            {
                                if (key == localPlayerId) localScore = score;
                                else enemyScore = score;
                            }
                        }
                    }

                    Debug.Log($"[GameEnd] Final Scores - Local: {localScore}, Enemy: {enemyScore}");
                    // TODO: Implement Result screen/UI call here using the parsed scores
                }
            }
        }
    }
}
