using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI;
using KillingMahjong.EngineData;

namespace KillingMahjong.Network
{
    public class DebugWebSocketClient : MonoBehaviour
    {
        [SerializeField] private GameUIManager gameUIManager;

        [Header("Delay Settings")]
        [SerializeField] private float networkDelay = 0.5f;

        [Header("Mock Data Options")]
        public string localPlayerId = "player_local";
        public string enemyPlayerId = "enemy_bot";
        
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

        public void StartMockConnection()
        {
            Debug.Log("[Debug Client] Starting Mock Connection Sequence...");
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
            gameUIManager.ShowMatchmakingWaiting();
            Debug.Log("[Debug Client] In matchmaking queue, waiting for opponents...");

            yield return new WaitForSeconds(networkDelay * 2f);
            
            SendMockMessage(new GameStartedMessage { type = "game_started" });
            Debug.Log("[Debug Client] Match found! Game started.");

            yield return new WaitForSeconds(networkDelay);
            
            SendMockMessage(new PhaseChangeMessage { type = "phase_change", new_status = "dealing" });
            yield return new WaitForSeconds(0.1f);
            
            SendMockWallDealt();
            
            yield return new WaitForSeconds(0.1f);
            SendMockMessage(new PhaseChangeMessage { type = "phase_change", new_status = "hand_selection" });
        }

        public void ReceiveActionFromPlayer(string actionType, ActionPayload payload)
        {
            StartCoroutine(HandleActionWithDelay(actionType, payload));
        }

        private IEnumerator HandleActionWithDelay(string actionType, ActionPayload payload)
        {
            yield return new WaitForSeconds(networkDelay);

            switch (actionType)
            {
                case "bet": // "betting" から "bet" へ
                    lastLocalBet = payload.bet_amount;
                    lastEnemyBet = payload.bet_amount; 
                    
                    int oldLocalHp = localPlayerHp;
                    int oldEnemyHp = enemyPlayerHp;
                    localPlayerHp -= lastLocalBet;
                    enemyPlayerHp -= lastEnemyBet;

                    // ベット完了イベント送信（UIのアニメーションがこれで発火する）
                    SendMockMessage(new BetCompletedMessage { 
                        type = "bet_completed", 
                        data = new BetCompletedData { 
                            bets = new PlayerBetData[] {
                                new PlayerBetData { client_id = localPlayerId, bet = lastLocalBet },
                                new PlayerBetData { client_id = enemyPlayerId, bet = lastEnemyBet }
                            }
                        }
                    });
                    
                    yield return new WaitForSeconds(3.5f);
                    
                    SendMockMessage(new PhaseChangeMessage { type = "phase_change", new_status = "discard" });
                    SendMockMessage(new DiscardPhaseStartedMessage { type = "discard_phase_started", data = new DiscardPhaseStartedData { first_player = localPlayerId } });
                    yield return new WaitForSeconds(1.0f);
                    break;

                case "select": // "selected" から "select" へ
                    if (payload.hand_indexes != null)
                    {
                        mockLocalHand = new List<int>(payload.hand_indexes);
                        foreach (int tile in payload.hand_indexes)
                        {
                            mockLocalWall.Remove(tile);
                        }
                    }

                    if (mockEnemyHand.Count == 0 && mockEnemyWall.Count >= 13)
                    {
                        for (int i = 0; i < 13; i++)
                        {
                            int r = UnityEngine.Random.Range(0, mockEnemyWall.Count);
                            mockEnemyHand.Add(mockEnemyWall[r]);
                            mockEnemyWall.RemoveAt(r);
                        }
                    }

                    SendMockHandSelected();
                    
                    yield return new WaitForSeconds(0.5f);
                    SendMockMessage(new PhaseChangeMessage { type = "phase_change", new_status = "betting" });
                    break;

                case "discard":
                    int discardTile = payload.wall_index > 0 ? payload.wall_index : payload.tile; // 互換
                    if (discardTile > 0)
                    {
                        if (mockLocalWall.Contains(discardTile))
                        {
                            mockLocalWall.Remove(discardTile);
                            mockLocalDiscards.Add(discardTile);
                        }
                    }

                    gameUIManager.HandleDiscardEvent(discardTile, true);

                    yield return new WaitForSeconds(networkDelay * 2f);
                    
                    if (mockEnemyWall.Count > 0)
                    {
                        int enemyDiscard = mockEnemyWall[0];
                        mockEnemyWall.RemoveAt(0);
                        mockEnemyDiscards.Add(enemyDiscard);

                        string tileName = new TileData(enemyDiscard).GetTileName();
                        gameUIManager.ShowDialogue($"「{tileName}を切るわ！」");
                        
                        yield return new WaitForSeconds(1.0f);

                        gameUIManager.HandleDiscardEvent(enemyDiscard, false);

                        List<int> localWaits = CalculateSimpleWaits(mockLocalHand);
                        if (localWaits.Contains(enemyDiscard))
                        {
                            mockLocalHand.Add(enemyDiscard); 
                            yield return new WaitForSeconds(1.0f);
                            
                            SendMockMessage(new RoundEndMessage { 
                                type = "round_end", 
                                data = new RoundEndData { is_draw = false, liquidation = new LiquidationData { winner_id = localPlayerId } } 
                            });
                            yield break; 
                        }
                    }

                    SendMockMessage(new DiscardPhaseStartedMessage { type = "discard_phase_started", data = new DiscardPhaseStartedData { first_player = localPlayerId } });
                    break;
            }
        }

        private void SendMockMessage<T>(T messageObj)
        {
            string json = JsonUtility.ToJson(messageObj);
            gameUIManager.ApplyGameStateFromJSON(json, localPlayerId);
        }

        private void SendMockWallDealt()
        {
            string json = "{\"type\":\"dealing_completed\",\"dora_id\":15,\"hands\":[";
            json += "{\"client_id\":\"" + localPlayerId + "\",\"wall\":[" + string.Join(",", mockLocalWall) + "],\"tenpai_examples\":[[1,2,3],[4,5,6]]},";
            json += "{\"client_id\":\"" + enemyPlayerId + "\",\"wall\":[" + string.Join(",", mockEnemyWall) + "]}";
            json += "]}";
            gameUIManager.ApplyGameStateFromJSON(json, localPlayerId);
        }
        
        private void SendMockHandSelected()
        {
            var msg = new HandSelectionCompletedMessage
            {
                type = "hand_selection_completed",
                data = new HandSelectionCompletedData
                {
                    hands = new HandData[]
                    {
                        new HandData
                        {
                            client_id = localPlayerId,
                            hand = mockLocalHand.ToArray(),
                            wall = mockLocalWall.ToArray(),
                            waits = CalculateSimpleWaits(mockLocalHand).ToArray()
                        },
                        new HandData
                        {
                            client_id = enemyPlayerId,
                            hand = mockEnemyHand.ToArray(),
                            wall = mockEnemyWall.ToArray(),
                            waits = new int[] {}
                        }
                    }
                }
            };
            SendMockMessage(msg);
        }

        private List<int> CalculateSimpleWaits(List<int> hand)
        {
            List<int> waits = new List<int>();
            if (hand.Count >= 13) 
            {
                for (int i = 0; i < 34; i++) waits.Add(i);
            }
            return waits;
        }

        [ContextMenu("Test Ron (Player Win)")]
        private void TriggerPlayerRon()
        {
        }

        [ContextMenu("Test Ron (Enemy Win)")]
        private void TriggerEnemyRon()
        {
        }
    }
}
