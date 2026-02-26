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
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private PlayerInfoUI playerInfoUI; // プレイヤー自身のHP
        [SerializeField] private EnemyInfoUI enemyInfoUI;  // 敵のUI管理（HP、パネル）
        [SerializeField] private AbilityUI abilityUI;
        [SerializeField] private YakuListUI yakuListUI;
        [SerializeField] private BettingUI bettingUI;
        [SerializeField] private PhaseTransitionUI phaseTransitionUI;
        [SerializeField] private WebSocketGameClientSample webSocketClient;
        [SerializeField] private MatchmakingUI matchmakingUI;

        private void Start()
        {
            // Initialization if needed
            SetupUI();
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
        private System.Collections.Generic.List<int> selectedTileIds = new System.Collections.Generic.List<int>();
        
        private string currentPhaseStatus = "";
        public string CurrentPhaseStatus => currentPhaseStatus;

        public void InitializeGame(System.Collections.Generic.List<int> initialWall)
        {
            currentWallTiles = new System.Collections.Generic.List<int>(initialWall);
            currentHandTiles.Clear();
            selectedTileIds.Clear();
            RebuildAllTilesFromState();
        }

        public void MoveTileToHand(int tileId)
        {
            // 手牌選択フェイズでのみ移動を許可
            if (currentPhaseStatus != "hand_selection") return;

            if (selectedTileIds.Contains(tileId))
            {
                MoveSelectedFiles(true);
            }
            else
            {
                if (currentWallTiles.Contains(tileId))
                {
                    // 手牌は13枚まで
                    if (currentHandTiles.Count < 13)
                    {
                        currentWallTiles.Remove(tileId);
                        currentHandTiles.Add(tileId);
                        
                        // 以前のようなRefreshUI()による全再構築は行わず、
                        // WallUIからTransformを引き抜いてHandUIにそのまま渡す
                        if (wallUI != null && handUI != null)
                        {
                            Transform movedTile = wallUI.GrabTile(tileId);
                            if (movedTile != null)
                            {
                                handUI.AddTileToHand(movedTile, tileId);
                            }
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

             if (selectedTileIds.Contains(tileId))
            {
                MoveSelectedFiles(false);
            }
            else
            {
                if (currentHandTiles.Contains(tileId))
                {
                    currentHandTiles.Remove(tileId);
                    currentWallTiles.Add(tileId);
                    
                    if (handUI != null && wallUI != null)
                    {
                        // 1. HandUIからTransformを探して取得
                        Transform movedTile = null;
                        foreach (Transform t in handUI.GetHandSlots())
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
                        Transform movedTile = wallUI.GrabTile(id);
                        if (movedTile != null) handUI.AddTileToHand(movedTile, id);
                    }
                    else if (!toHand && handUI != null && wallUI != null)
                    {
                        // Hand -> Wall
                        Transform movedTile = null;
                        foreach (Transform t in handUI.GetHandSlots()) 
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
            if (!multiSelect) selectedTileIds.Clear();
            if (!selectedTileIds.Contains(tileId)) selectedTileIds.Add(tileId);
            
            Debug.Log($"Selected Tiles Count: {selectedTileIds.Count}");
            DeselectAbility();
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
            // TODO: Visual Update
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
            
            Debug.Log("Hand Selection Complete! Transitioning to discard phase.");
            
            // エンジンに選択完了（打牌フェーズへの移行等）を通知する
            SendActionToServer("hand_selected", new {
                selected_tiles = currentHandTiles
            });
            
            // 状態に応じたUIの切り替えなどは今後Engineからの {"type":"game_state"} 受信時に ApplyGameState で一括処理されるため、
            // ここでの直書き（currentPhaseStatus = "discard"）は原則不要ですが、レスポンス前の先行UI変更として残すことも可能です。
        }

        [Header("Tile Prefab")]
        [SerializeField] private GameObject tilePrefab; // JSON受信時など全体再構築用に保持

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
                    handUI.AddTileToHand(obj.transform, id);
                }
            }

            // --- 2. WallUIの再構築 ---
            // ※WallUI側のLayoutロジック（隙間開けなど）が複雑な場合は、
            // 以前のSetWallのような処理をWallUI内に「RebuildWall(List<int>)」として残すのが正解ですが、
            // 今回の依頼「再生成はせずに単純移動(HandUIからSetHandを消去)」に伴いコンパイルエラーを解消するため
            // WallUI.cs自体に `Rebuild` メソッドを追加する前提とするか、
            // コンパイルエラー部分のみを対象とします。
            // HandDebug が落ちていたので、ひとまず `wallUI.SetWall` が残っているならそれを呼びます。
            if (wallUI != null)
            {
                // もしWallUIからSetWallを消していないならそのまま呼ぶ。
                // 消しているなら同様にInstantiateしてReturnTileToWallで渡す。
                // (WallUI.csの変更差分では SetWall 自体は消していなかったので呼び出せるはず)
                wallUI.SetWall(currentWallTiles);
            }
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

        // Helper string sending method targeting Network
        public async void SendActionToServer(string actionType, object dataPayload)
        {
            if (webSocketClient == null) return;

            string json = JsonUtility.ToJson(new
            {
                type = "action",
                data = new
                {
                    action = actionType,
                    payload = dataPayload
                }
            });
            await webSocketClient.SendAsync(json);
        }

        public void ApplyGameState(GameStateData state, string localPlayerId)
        {
            // ゲーム開始（状態を受信）したので待機UIを消す
            if (matchmakingUI != null) matchmakingUI.Hide();

            // 1. Find Local Player
            PlayerStateData localPlayer = null;
            if (state.players != null)
            {
                foreach (var p in state.players)
                {
                    if (p.id == localPlayerId)
                    {
                        localPlayer = p;
                        break;
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

            // 3. Update internal tracking lists and UI
            if (localPlayer.hand != null)
            {
                currentHandTiles = new List<int>(localPlayer.hand);
            }

            if (localPlayer.wall != null)
            {
                currentWallTiles = new List<int>(localPlayer.wall);
            }

            // フルデータ受信時は再構築
            RebuildAllTilesFromState();

            if (localPlayer.discards != null)
            {
                if (riverUI != null) riverUI.SetRiver(new List<int>(localPlayer.discards));
            }
            
            // 4. Update Game Status Text
            string statusMsg = $"Round {state.round} - {state.honba} Honba\nTarget: {state.status}";
            if (state.current_player == localPlayerId)
                statusMsg += "\nYour Turn!";
            
            if (dialogueUI != null) dialogueUI.ShowText(statusMsg);
            
            currentPhaseStatus = state.status; // ★ 現在のフェーズを保存
            
            ClearSelection();

            // 5. Handle Phase Logic
            HandlePhaseVisibility(state.status, localPlayer);
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

            // エンジンに通知
            SendActionToServer("bet", new { amount = betAmount });

            // アニメーションなどのトリガー（本来はサーバー側のステータスがターン決定に変わった段階で呼ぶべきですが、仮配置）
            TriggerBettingAnimationPhase($"Round 1"); 
        }

        public void TriggerBettingAnimationPhase(string roundString)
        {
             if (phaseTransitionUI != null)
             {
                 Debug.Log("Triggering Phase Transition Animation.");
                 
                 // 画面に横線が入る瞬間で敵の会話UIが消えるようにする
                 if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);

                 // Start transition
                 phaseTransitionUI.PlayTransition(roundString, playerInfoUI, 
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
    }
}
