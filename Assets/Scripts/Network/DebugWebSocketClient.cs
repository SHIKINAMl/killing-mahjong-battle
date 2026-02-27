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
                    // Transition to discard phase for local player
                    SendMockGameState("discard");
                    break;

                case "discard":
                    // Simulate enemy turn and then back to local player
                    SendMockGameState("discard", isEnemyTurn: true);
                    yield return new WaitForSeconds(networkDelay * 2f);
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
                health = localPlayerHp, // Use tracked HP
                wall = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 },
                hand = new int[] { },
                discards = new int[] { }
            };

            // 手牌フェイズになっても、Mockサーバーからは自動で手牌を構築せずユーザーのクリック操作に任せる
            if (status == "discard")
            {
                p.discards = new int[] { 34 };
            }

            return p;
        }

        private PlayerStateData GenerateMockEnemyPlayer(string status)
        {
            return new PlayerStateData
            {
                id = enemyPlayerId,
                health = enemyPlayerHp, // Use tracked HP
                // changed max from 30 to 34 to match local player count
                wall = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34 },
                hand = new int[] { }, // Initialize as empty for all phases as user requested
                discards = new int[] { } // Reset mock discards initially
            };
        }
    }
}
