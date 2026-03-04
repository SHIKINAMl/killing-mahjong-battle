using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private HandUI handUI;
        [SerializeField] private WallUI wallUI;
        [SerializeField] private RiverUI riverUI;
        [SerializeField] private WaitUI waitUI; // 追加: 待ち牌表示UI
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private PlayerInfoUI playerInfoUI; // プレイヤー自身のHP
        [SerializeField] private EnemyInfoUI enemyInfoUI;  // 敵のUI管理（HP、パネル）
        [SerializeField] private AbilityUI abilityUI;
        [SerializeField] private YakuListUI yakuListUI;
        [SerializeField] private BettingUI bettingUI;
        [SerializeField] private PhaseTransitionUI phaseTransitionUI;
        [SerializeField] private WebSocketGameClientSample webSocketClient;
        [SerializeField] private MatchmakingUI matchmakingUI;

        [Header("Debug Client")]
        [SerializeField] private bool useDebugClient;
        [SerializeField] private KillingMahjong.Network.DebugWebSocketClient debugWebSocketClient;

        [Header("Character Reactions")]
        [SerializeField] private float reactionDelay = 0.8f; // 打牌宣言からリアクション開始までの遅延時間
        [SerializeField] private float reactionDisplayDuration = 2.0f; // リアクションを表示したまま待つ時間
        
        // --- 内部状態トラッキング用 ---
        private int lastLocalDiscardsCount = 0;
        private int lastEnemyDiscardsCount = 0;

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
            dialogueUI.ShowText("Game Start!");
        }
        
        // Data State
        private System.Collections.Generic.List<int> currentHandTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> currentWallTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> currentWaitTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> selectedTileIds = new System.Collections.Generic.List<int>();
        
        private string currentPhaseStatus = "";
        public string CurrentPhaseStatus => currentPhaseStatus;

        public void InitializeGame(System.Collections.Generic.List<int> initialWall)
        {
            currentWallTiles = new System.Collections.Generic.List<int>(initialWall);
            currentHandTiles.Clear();
            selectedTileIds.Clear();
            currentWaitTiles.Clear();
            RebuildAllTilesFromState();
        }

        public void MoveTileToHand(int tileId)
        {
            // 手牌選択フェイズでのみ移動を許可
            if (currentPhaseStatus != "hand_selection") return;

            if (currentWallTiles.Contains(tileId))
            {
                // 手牌は13枚まで
                if (currentHandTiles.Count < 13)
                {
                    currentWallTiles.Remove(tileId);
                    currentHandTiles.Add(tileId);
                    
                    if (wallUI != null && handUI != null)
                    {
                        RectTransform movedTile = wallUI.GrabTile(tileId);
                        if (movedTile != null)
                        {
                            handUI.AddTileToHand(movedTile, tileId);
                        }
                    }
                }
            }
            ClearSelection();
        }

        public void MoveTileToWall(int tileId)
        {
            // 手牌選択フェイズでのみ移動を許可
            if (currentPhaseStatus != "hand_selection") return;

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
            
            Debug.Log($"Selected Tiles Count: {selectedTileIds.Count}");
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
            if (currentPhaseStatus != "discard") return;
            if (selectedTileIds.Count == 0) return;
            
            // ログ確認中は打牌を無効化する
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;

            int tileToDiscard = selectedTileIds[0];
            Debug.Log($"Discarding tile: {tileToDiscard}");

            // Send action to server
            SendActionToServer("discard", new ActionPayload { tile = tileToDiscard });
            
            // Wait for GameState update to actually remove tile and add to river
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
            if (currentPhaseStatus != "hand_selection") return;
            
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
                wallUI.LayoutWallTiles(wallGenerated, currentWallTiles, currentWaitTiles, currentPhaseStatus == "discard");
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
            SetMatchUIVisibility(false);
            if (riverUI != null) riverUI.gameObject.SetActive(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
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
        }

        public void ApplyGameStateFromJSON(string jsonString, string localPlayerId)
        {
            try
            {
                GameStateData state = JsonUtility.FromJson<GameStateData>(jsonString);
                if (state != null)
                {
                    ApplyGameState(state, localPlayerId);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse GameStateData JSON: {e.Message}\nJSON:\n{jsonString}");
            }
        }

        // シリアライズ用の構造体を定義（JsonUtilityは匿名クラスをシリアライズできないため）
        [System.Serializable]
        public class ActionMessage
        {
            public string type = "action";
            public ActionData data;
        }

        [System.Serializable]
        public class ActionData
        {
            public string action;
            public ActionPayload data; // python側では action_data = data.get("data") として取得されている
        }

        [System.Serializable]
        public class ActionPayload
        {
            public int amount;
            public List<int> hand;
            public int tile; // For discard, etc.
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
                data = new ActionData
                {
                    action = actionType,
                    data = dataPayload
                }
            };

            string json = JsonUtility.ToJson(msg);
            await webSocketClient.SendAsync(json);
        }

        public void ApplyGameState(GameStateData state, string localPlayerId)
        {
            // ゲーム開始（状態を受信）したので待機UIを消す
            if (matchmakingUI != null) matchmakingUI.Hide();

            // 1. Find Local Player & Enemy Player
            PlayerStateData localPlayer = null;
            PlayerStateData enemyPlayer = null;

            if (state.players != null)
            {
                foreach (var p in state.players)
                {
                    if (p.id == localPlayerId)
                    {
                        localPlayer = p;
                    }
                    else
                    {
                        // 2人対戦前提なので、自分以外なら敵
                        enemyPlayer = p;
                    }
                }
            }

            if (localPlayer == null)
            {
                Debug.LogWarning($"Local player {localPlayerId} not found in state data.");
                return;
            }

            // 2. Update HP
            if (playerInfoUI != null)
            {
                playerInfoUI.SetHP(localPlayer.health);
            }
            if (enemyPlayer != null && enemyInfoUI != null)
            {
                enemyInfoUI.SetHP(enemyPlayer.health);
            }

            // フルデータ受信時は再構築（手牌フェイズに入った初回のみ生成する）
            if (currentPhaseStatus != "hand_selection" && state.status == "hand_selection")
            {
                // 3. Update internal tracking lists
                if (localPlayer.hand != null) currentHandTiles = new List<int>(localPlayer.hand);
                if (localPlayer.wall != null) currentWallTiles = new List<int>(localPlayer.wall);
                if (localPlayer.wait != null) currentWaitTiles = new List<int>(localPlayer.wait);
                
                RebuildAllTilesFromState();
            }
            else if (currentPhaseStatus == "discard" && state.status == "discard" || 
                     currentPhaseStatus == "hand_selection" && state.status == "discard")
            {
                // 手牌フェイズ完了後、または打牌フェイズ中のデータ更新時は、差分のみをUIに反映する（ツモ・打牌のエフェクト）
                SyncDiscardPhaseVisuals(localPlayer);
            }

            if (localPlayer.discards != null)
            {
                if (riverUI != null) riverUI.SetRiver(new List<int>(localPlayer.discards));
                
                // --- 自分の打牌検知 ---
                if (localPlayer.discards.Length > lastLocalDiscardsCount)
                {
                    int discardedTileId = localPlayer.discards[localPlayer.discards.Length - 1];
                    OnTileDiscarded(discardedTileId, true);
                }
                lastLocalDiscardsCount = localPlayer.discards.Length;
            }

            // --- 敵の打牌検知 ---
            if (enemyPlayer != null && enemyPlayer.discards != null)
            {
                if (enemyPlayer.discards.Length > lastEnemyDiscardsCount)
                {
                    int discardedTileId = enemyPlayer.discards[enemyPlayer.discards.Length - 1];
                    OnTileDiscarded(discardedTileId, false);
                }
                lastEnemyDiscardsCount = enemyPlayer.discards.Length;
            }

            if (waitUI != null && localPlayer.wait != null)
            {
                waitUI.DisplayWaits(new List<int>(localPlayer.wait));
            }
            
            currentPhaseStatus = state.status; // ★ 現在のフェーズを保存
            
            // 4. Update Game Status Text
            string statusMsg = $"Round {state.round} - {state.honba} Honba\nTarget: {state.status}";
            if (state.current_player == localPlayerId)
                statusMsg += "\nYour Turn!";
            
            // DialogueUIはセリフ専用にするためゲームの進行状況ロゴは出力しない
            // if (dialogueUI != null) dialogueUI.ShowText(statusMsg);
            
            ClearSelection();

            // 手牌のレイアウト更新 (コンテナ切り替えのため)
            if (handUI != null)
            {
                handUI.UpdateLayout(currentPhaseStatus);
            }

            // 5. Handle Phase Logic
            HandlePhaseVisibility(state.status, localPlayer);
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

        private void HandlePhaseVisibility(string status, PlayerStateData localPlayer)
        {
            // デフォルトでriverUIは非表示にし、打牌(discard)フェーズのみ表示する
            if (riverUI != null)
            {
                riverUI.gameObject.SetActive(status == "discard");
            }

            // HPの表示状態を更新（手牌選択中は非表示、対局中(discard等)は表示したい場合はここでオンオフ可能）
            // ユーザー要望: 掛け金決定と手牌決定フェイズの間（つまり、transitonUIの処理の間）敵のHPのUIを消しておきたい
            // ※ここではデフォルト非表示などにしておき、必要なフェーズで表示する形も可能ですが、
            // 今回は Transition の前後で制御するため MatchUI の表示非表示のロジックに組み込ませます

            switch (status)
            {
                case "betting":
                    // 賭けフェイズでは手牌などは消すが、敵のHPや立ち絵は表示する
                    SetMatchUIVisibility(false); 
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    
                    StartBettingPhase(localPlayer);
                    break;
                case "turn_decision":
                case "dealing":
                case "hand_selection":
                    // トランジションが終わるまではMatchUI(= playerInfoUI含む)は非表示のままになるため、自動的に消えます。
                    // 敵の立ち絵・HPもここで非表示リストに含めます。
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
                    // 以降のフェーズ処理
                    break;
                case "discard":
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    break;
                case "liquidation":
                    break;
                default:
                    Debug.LogWarning($"Unknown phase status: {status}");
                    break;
            }
        }

        private void StartBettingPhase(PlayerStateData localPlayer)
        {
            if (bettingUI != null)
            {
                Debug.Log($"Starting Betting Phase for {localPlayer.id}");
                bettingUI.ShowBettingPhase(20000, localPlayer.health, OnBetConfirmed);
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
            // riverUI visibility is managed by HandlePhaseVisibility now
            
            // playerInfoUI(Enemy HP) は Transition 中〜手牌選択前などで非表示にしたい場合、ここでコントロールされます。
            // visible == false なら確実に消えます。
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(visible); 

            // Bettingフェーズで固有に呼び出されるため、SetMatchUIVisibility自体ではEnemyPanelはいったん何もしないか、あるいは一括で消す。
            // 今回は Transition のアニメーション側からフェーズ判定抜きで表示コントロールされる可能性があるので連動させます。
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(visible); 

            if (yakuListUI != null) yakuListUI.gameObject.SetActive(visible);
            
            // DialogueUI (enemy comments) and BettingUI remain active
        }

        private void OnBetConfirmed(int betAmount)
        {
            Debug.Log($"Bet confirmed: {betAmount}");
            bettingUI.HideBettingPhase();

            // エンジンに通知 (Pythonエンジン側は action="betting" を待っている)
            SendActionToServer("betting", new ActionPayload { amount = betAmount });

            // アニメーションなどのトリガーは、ここで直接呼ばずにサーバー検証後 ("bet" メッセージ受信後) に行います。
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
                         
                         // トランジションが終了したら「対局画面（MatchUI）」のUI要素を表示するが、
                         // 今回のご要望として「HP表示などは、特定のフェーズまで非表示にしたい」かによって対応が変わります。
                         // ここでは HandUI / WallUI などを復帰させます。
                         SetMatchUIVisibility(true); 
                         
                         // riverUIはdiscardフェーズ限定なので強制的に非表示にしておく
                         if (riverUI != null) riverUI.gameObject.SetActive(false);
                         
                         // 対局（手牌フェイズ）が始まったら会話UIを出す
                         if (dialogueUI != null) dialogueUI.gameObject.SetActive(true);

                         // 仮のフェーズ直書きを削除し、サーバーからの `{"type": "game_state"}` 待機とする
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
    }
}
