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
            SendMockMessage(new ServerMessageBase { type = "game_started" });
            Debug.Log("[Debug Client] Match found! Game started.");

            yield return new WaitForSeconds(networkDelay);

            // Automatically transition to HandSelection (Dealing) phase
            Debug.Log("[Debug Client] Sending initial wall (Haipai)...");
            SendMockWallDealt();
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
                    
                    // ベッティング終了後、ターンの決定（打牌フェイズの開始）へ移行する
                    Debug.Log("[Debug Client] Betting complete. Transitioning to TurnDecision.");
                    SendMockMessage(new TurnDecidedMessage { type = "turn_decided", current_player = 0 });
                    
                    yield return new WaitForSeconds(1.0f); // 少し待機
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

                    // ====== 敵の手牌ランダム取得 ======
                    if (mockEnemyHand.Count == 0 && mockEnemyWall.Count >= 13)
                    {
                        for (int i = 0; i < 13; i++)
                        {
                            int r = UnityEngine.Random.Range(0, mockEnemyWall.Count);
                            mockEnemyHand.Add(mockEnemyWall[r]);
                            mockEnemyWall.RemoveAt(r);
                        }
                    }

                    // ====== 本ゲーム特有ルール ======
                    // ツモ無し。13枚の手牌を決めたら、以後は残りの「壁（Wall）」21枚から選んで打牌する。
                    // したがってここでツモは行わない。

                    // Transition to discard phase for local player
                    SendMockHandSelected();
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

                    // To Simulate discard event we'd send a TurnDecided, but we just trigger the discard visually here
                    gameUIManager.HandleDiscardEvent(payload.tile, true);

                    // Simulate enemy turn
                    yield return new WaitForSeconds(networkDelay * 2f);
                    
                    // Enemy discards a random tile from their wall
                    if (mockEnemyWall.Count > 0)
                    {
                        int enemyDiscard = mockEnemyWall[0];
                        mockEnemyWall.RemoveAt(0);
                        mockEnemyDiscards.Add(enemyDiscard);

                        // 女の子（敵）の打牌宣言
                        TileData discardedData = new TileData(enemyDiscard);
                        string tileName = discardedData.GetTileName();
                        gameUIManager.ShowDialogue($"「{tileName}を切るわ！」");
                        
                        // 宣言を見せるため少し待機時間を増やす
                        yield return new WaitForSeconds(1.0f);

                        // ここでゲームUI側に敵が打牌したことを伝える（これがないと画面上で動かない）
                        gameUIManager.HandleDiscardEvent(enemyDiscard, false);

                        // ★ オートロン判定 (Auto-Ron Check)
                        List<int> localWaits = CalculateSimpleWaits(mockLocalHand);
                        if (localWaits.Contains(enemyDiscard))
                        {
                            Debug.Log($"[Debug Client] AUTO-RON TRIGGERED! Enemy discarded {enemyDiscard} which is a winning tile!");
                            
                            // 相手の打牌が手牌に追加される演出（GameUIManager.cs等で対応）用に保持するか、
                            // そのまま agari ステータスとして送信する
                            mockLocalHand.Add(enemyDiscard); // 上がった牌を手牌の最後に加える形にする
                            
                            // 少しだけ間を置いてロン演出へ（宣言が出た直後）
                            yield return new WaitForSeconds(1.0f);
                            
                            // "agari" イベントを送信
                            SendMockMessage(new ServerMessageBase { type = "agari" });
                            yield break; // これ以降の処理（ターン遷移）は行わず終了
                        }
                    }

                    // オートロンしなかった場合は、自分ターンに戻る（ツモは無いのでこのまま）
                    // SendMockGameState(RoundStatus.Discard); -> 状態の送信ではなくTurnDecisionへ
                    break;

                default:
                    Debug.LogWarning($"[Debug Client] Unhandled action type: {actionType}");
                    break;
            }
        }

        // --- Mock JSON Generation ---
        private void SendMockMessage<T>(T messageObj)
        {
            string json = JsonUtility.ToJson(messageObj);
            Debug.Log($"[Debug Client] Applying Mock Message: {json}");
            gameUIManager.ApplyGameStateFromJSON(json, localPlayerId);
        }

        private void SendMockWallDealt()
        {
            var msg = new WallDealtMessage
            {
                type = "wall_dealt",
                dora_id = 15,
                hands = new WallDealtHand[]
                {
                    new WallDealtHand
                    {
                        client_id = localPlayerId,
                        hand = new List<int>(mockLocalWall),
                        tenpai_examples = new List<int[]>() // TODO: Generate dummy data if requested
                    },
                    new WallDealtHand
                    {
                        client_id = enemyPlayerId,
                        hand = new List<int>(mockEnemyWall),
                        tenpai_examples = new List<int[]>()
                    }
                }
            };
            SendMockMessage(msg);
        }
        
        private void SendMockHandSelected()
        {
            var msg = new HandSelectedMessage
            {
                type = "hand_selected",
                hands = new HandSelectedData[]
                {
                    new HandSelectedData
                    {
                        client_id = localPlayerId,
                        hand = mockLocalHand.ToArray(),
                        wall = mockLocalWall.ToArray(),
                        wait = CalculateSimpleWaits(mockLocalHand).ToArray()
                    },
                    new HandSelectedData
                    {
                        client_id = enemyPlayerId,
                        hand = mockEnemyHand.ToArray(),
                        wall = mockEnemyWall.ToArray(),
                        wait = new int[] {}
                    }
                }
            };
            SendMockMessage(msg);
        }

        // --- Mock Utility: Simple Wait Calculation ---
        // 開発用の仮データ：サーバーから「一萬（0）」と「四萬（3）」が待ち牌として送られてくる想定
        private List<int> CalculateSimpleWaits(List<int> hand)
        {
            List<int> waits = new List<int>();
            if (hand.Count >= 13) // 手牌が13枚以上揃っている時だけ待ちを表示する
            {
                // テスト用：全ての牌（0〜33）を待ち牌とする
                for (int i = 0; i < 34; i++)
                {
                    waits.Add(i);
                }
            }
            return waits;
        }

        // --- Tester Context Menus for Ron ---
        [ContextMenu("Test Ron (Player Win)")]
        private void TriggerPlayerRon()
        {
            Debug.Log("[Debug Client] Triggering Player Ron Animation Test");
            // SendMockGameState(RoundStatus.Agari, isEnemyTurn: false);
        }

        [ContextMenu("Test Ron (Enemy Win)")]
        private void TriggerEnemyRon()
        {
            Debug.Log("[Debug Client] Triggering Enemy Ron Animation Test");
            // SendMockGameState(RoundStatus.Agari, isEnemyTurn: true); 
        }
    }
}
