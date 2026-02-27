using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI;
using KillingMahjong.EngineData;

namespace KillingMahjong.Network
{
    /// <summary>
    /// Debug WebSocket Client to mock server behavior without connecting to the actual network.
    /// This provides constant opponent states and fixed responses to enable UI development and testing.
    /// </summary>
    public class DebugWebSocketClient : MonoBehaviour
    {
        [SerializeField] private GameUIManager gameUIManager;

        [Header("Delay Settings")]
        [SerializeField] private float networkDelay = 0.5f;

        [Header("Mock Data Options")]
        public string localPlayerId = "player_local";
        public string enemyPlayerId = "enemy_bot";

        private int currentRound = 1;
        
        // Mock State Variables
        private int localPlayerHp = 50000;
        private int enemyPlayerHp = 50000;
        private int lastLocalBet = 0;
        private int lastEnemyBet = 0;

        private List<int> mockLocalHand = new List<int>();
        private List<int> mockLocalWall = new List<int>();
        private List<int> mockLocalDiscards = new List<int>();
        
        private List<int> mockEnemyHand = new List<int>();
        private List<int> mockEnemyWall = new List<int>();
        private List<int> mockEnemyDiscards = new List<int>();

        private void Start()
        {
            if (gameUIManager == null)
            {
                gameUIManager = FindFirstObjectByType<GameUIManager>();
            }
        }

        // --- Mock Connecting Phase ---
        public void StartMockConnection()
        {
            Debug.Log("[Debug Client] Starting Mock Connection Sequence...");
            
            // Initialize mock state data
            mockLocalWall = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 };
            mockLocalHand.Clear();
            mockLocalDiscards.Clear();

            mockEnemyWall = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 };
            mockEnemyHand.Clear();
            mockEnemyDiscards.Clear();

            StartCoroutine(MockConnectionSequence());
        }

        private IEnumerator MockConnectionSequence()
        {
            yield return new WaitForSeconds(networkDelay);
            
            // "connected" -> "matchmaking"
            gameUIManager.ShowMatchmakingWaiting();
            Debug.Log("[Debug Client] In matchmaking queue, waiting for opponents...");

            yield return new WaitForSeconds(networkDelay * 2f);

            // "game_started"
            gameUIManager.OnGameStarted();
            Debug.Log("[Debug Client] Match found! Game started.");

            yield return new WaitForSeconds(networkDelay);

            // "betting" state
            SendMockGameState("betting");
        }

        // --- Handle Incoming Actions from Player ---
        public void ReceiveActionFromPlayer(string actionType, GameUIManager.ActionPayload payload)
        {
            Debug.Log($"[Debug Client] Received action from player: {actionType}");
            StartCoroutine(HandleActionWithDelay(actionType, payload));
        }

        private IEnumerator HandleActionWithDelay(string actionType, GameUIManager.ActionPayload payload)
        {
            yield return new WaitForSeconds(networkDelay);

            switch (actionType)
            {
                case "betting":
                    // Record bet and update HP
                    lastLocalBet = payload.amount;
                    lastEnemyBet = payload.amount; // Simulation: enemy matches bet
                    
                    int oldLocalHp = localPlayerHp;
                    int oldEnemyHp = enemyPlayerHp;
                    
                    localPlayerHp -= lastLocalBet;
                    enemyPlayerHp -= lastEnemyBet;

                    // Simulate both players completing bet, pass the actual values
                    gameUIManager.OnBettingCompleteFromServer(lastLocalBet, lastEnemyBet, oldLocalHp, oldEnemyHp);
                    
                    // アニメーション（PhaseTransitionUI）が画面を覆う時間分だけ待機し、ファントムタイル現象を防ぎます
                    yield return new WaitForSeconds(3.5f);
                    
                    // Transition to dealing -> hand_selection
                    SendMockGameState("hand_selection");
                    break;

                case "selected":
                    if (payload.hand != null)
                    {
                        mockLocalHand = new List<int>(payload.hand);
                        foreach (int tile in payload.hand)
                        {
                            mockLocalWall.Remove(tile);
                        }
                    }

                    // ====== 本ゲーム特有ルール ======
                    // ツモ無し。13枚の手牌を決めたら、以後は残りの「壁（Wall）」21枚から選んで打牌する。
                    // したがってここでツモは行わない。

                    // Transition to discard phase for local player
                    SendMockGameState("discard");
                    break;

                case "discard":
                    if (payload.tile > 0)
                    {
                        // プレイヤーは「壁(Wall)」から牌を選んで打牌する
                        if (mockLocalWall.Contains(payload.tile))
                        {
                            mockLocalWall.Remove(payload.tile);
                            mockLocalDiscards.Add(payload.tile);
                        }
                        else
                        {
                            Debug.LogWarning($"[Debug Client] Player tried to discard {payload.tile} but it is not in Wall!");
                        }
                    }

                    // Simulate enemy turn
                    SendMockGameState("discard", isEnemyTurn: true);
                    yield return new WaitForSeconds(networkDelay * 2f);
                    
                    // Enemy discards a random tile from their wall
                    if (mockEnemyWall.Count > 0)
                    {
                        int enemyDiscard = mockEnemyWall[0];
                        mockEnemyWall.RemoveAt(0);
                        mockEnemyDiscards.Add(enemyDiscard);
                    }

                    // 自分ターンに戻る（ツモは無いのでこのまま）
                    SendMockGameState("discard");
                    break;

                default:
                    Debug.LogWarning($"[Debug Client] Unhandled action type: {actionType}");
                    break;
            }
        }

        // --- Mock State Generation ---
        private void SendMockGameState(string status, bool isEnemyTurn = false)
        {
            GameStateData mockState = new GameStateData
            {
                status = status,
                round = currentRound,
                honba = 0,
                dora_id = 15,
                current_player = isEnemyTurn ? enemyPlayerId : localPlayerId,
                players = new PlayerStateData[]
                {
                    GenerateMockLocalPlayer(status),
                    GenerateMockEnemyPlayer(status)
                }
            };

            string json = JsonUtility.ToJson(mockState);
            Debug.Log($"[Debug Client] Applying Mock GameState: {status}");
            gameUIManager.ApplyGameStateFromJSON(json, localPlayerId);
        }

        private PlayerStateData GenerateMockLocalPlayer(string status)
        {
            var p = new PlayerStateData
            {
                id = localPlayerId,
                health = localPlayerHp,
                wall = mockLocalWall.ToArray(),
                hand = mockLocalHand.ToArray(),
                discards = mockLocalDiscards.ToArray()
            };

            return p;
        }

        private PlayerStateData GenerateMockEnemyPlayer(string status)
        {
            return new PlayerStateData
            {
                id = enemyPlayerId,
                health = enemyPlayerHp,
                wall = mockEnemyWall.ToArray(),
                hand = mockEnemyHand.ToArray(),
                discards = mockEnemyDiscards.ToArray()
            };
        }
    }
}
