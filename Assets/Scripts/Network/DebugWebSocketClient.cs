using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI;
using KillingMahjong.EngineData;

namespace KillingMahjong.Network
{
    /// <summary>
    /// Pythonバックエンド（game_session.py）の動作を完全に模倣するデバッグ用WebSocketクライアント。
    /// Pythonは絶対に変更しないため、このクライアントでメッセージフローを再現する。
    /// 
    /// === Pythonの正式なメッセージフロー ===
    /// game_started
    ///   → phase_change(dealing)
    ///   → dealing_completed
    ///   → phase_change(hand_selection)
    ///   → [is_tenpai → is_tenpai/not_tenpai]
    ///   → [select → hand_selection_accepted (満貫以上) or hand_selection_confirmation_required]
    ///   → [select_confirm → hand_selection_accepted (強制)]
    ///   → (全員確定後) hand_selection_completed
    ///   → phase_change(betting)
    ///   → [bet → bet_completed]
    ///   → phase_change(discard)
    ///   → discard_phase_started
    ///   → [discard → discard_completed (broadcast)] → round_end or 次のdiscard_phase_started
    ///   → round_end → next_round_waiting
    ///   → [next_round → next_round_accepted → (ラウンド再開)]
    /// </summary>
    public class DebugWebSocketClient : MonoBehaviour
    {
        [SerializeField] private GameUIManager gameUIManager;

        [Header("Delay Settings")]
        [SerializeField] private float networkDelay = 0.5f;

        [Header("Mock Data Options")]
        public string localPlayerId = "player_local";
        public string enemyPlayerId = "enemy_bot";

        // HP管理
        private int localPlayerHp = 20000;
        private int enemyPlayerHp = 20000;
        private int lastLocalBet = 0;
        private int lastEnemyBet = 0;

        // 手牌・山牌の状態
        private List<int> mockLocalHand = new List<int>();
        private List<int> mockLocalWall = new List<int>();
        private List<int> mockLocalDiscards = new List<int>();

        private List<int> mockEnemyHand = new List<int>();
        private List<int> mockEnemyWall = new List<int>();
        private List<int> mockEnemyDiscards = new List<int>();

        private int mockTurnCount = 0;
        private const int MAX_TURNS = 17;

        // === 固定マンガン手牌定義 ===
        // TileData.cs エンコード: bit4-0 が牌種 (0=一萬, 1=二萬, ..., 8=九萬, 9=一筒, ..., 27=東, 28=南, 29=西, 30=北, 31=白, 32=發, 33=中)
        // ドラビット: bit5(0x20)=ドラ, bit6(0x40)=赤ドラ

        // Player1: 清一色七対子（一萬〜七萬の七対子）→ ハネ満確定
        //   手牌インデックス 0〜12、待ち: 七萬(6)
        private static readonly List<int> MANGAN_LOCAL_WALL = new List<int>
        {
            0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6,         // index 0-12: 清一色七対子
            27, 27, 28, 28, 29, 29, 30, 30, 31, 31, 32, 32, 33, 33, 7, 8, 9, 10, 11, 12, 17 // index 13-33: フィラー
        };

        // Player2: 混一色三暗刻（東×3, 南×3, 西×3, 一萬二萬三萬三萬）→ 満貫以上確定
        //   手牌インデックス 0〜12、待ち: 一萬(0)か四萬(3)
        private static readonly List<int> MANGAN_ENEMY_WALL = new List<int>
        {
            27, 27, 27, 28, 28, 28, 29, 29, 29, 0, 1, 2, 2, // index 0-12: 混一色三暗刻
            3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23     // index 13-33: フィラー
        };

        // Player1の待ち牌（七萬=6）
        private static readonly int LOCAL_WAIT_TILE = 6;
        // Player1の役
        private static readonly string[] LOCAL_YAKU = new string[] { "清一色", "七対子" };

        private void Start()
        {
            if (gameUIManager == null)
                gameUIManager = FindFirstObjectByType<GameUIManager>();
        }

        // ===================================================
        // 接続開始〜配牌フロー
        // ===================================================

        public void StartMockConnection()
        {
            Debug.Log("[Debug Client] 接続開始（固定マンガン手牌モード）");
            ResetRoundState();
            StartCoroutine(MockConnectionSequence());
        }

        private void ResetRoundState()
        {
            mockLocalWall = new List<int>(MANGAN_LOCAL_WALL);
            mockLocalHand.Clear();
            mockLocalDiscards.Clear();

            mockEnemyWall = new List<int>(MANGAN_ENEMY_WALL);
            // 敵の手牌はインデックス0-12で固定
            mockEnemyHand = new List<int>(MANGAN_ENEMY_WALL.GetRange(0, 13));
            mockEnemyDiscards.Clear();

            mockTurnCount = 0;
        }

        private IEnumerator MockConnectionSequence()
        {
            yield return new WaitForSeconds(networkDelay);
            gameUIManager.ShowMatchmakingWaiting();

            yield return new WaitForSeconds(networkDelay * 2f);

            // game_started
            SendRawJson("{\"type\":\"game_started\"}");
            Debug.Log("[Debug Client] game_started 送信");

            yield return new WaitForSeconds(networkDelay);

            // phase_change(dealing)
            SendRawJson("{\"type\":\"phase_change\",\"new_status\":\"dealing\"}");
            yield return new WaitForSeconds(0.2f);

            // dealing_completed（固定マンガン手牌を配る）
            SendDealingCompleted();
            yield return new WaitForSeconds(0.2f);

            // phase_change(hand_selection)
            SendRawJson("{\"type\":\"phase_change\",\"new_status\":\"hand_selection\"}");
        }

        // ===================================================
        // プレイヤーからのアクション受信
        // ===================================================

        public void ReceiveActionFromPlayer(string actionType, ActionPayload payload)
        {
            StartCoroutine(HandleActionWithDelay(actionType, payload));
        }

        private IEnumerator HandleActionWithDelay(string actionType, ActionPayload payload)
        {
            yield return new WaitForSeconds(networkDelay);

            switch (actionType)
            {
                case "is_tenpai":
                    HandleIsTenpai(payload);
                    break;

                case "select":
                    HandleSelect(payload);
                    break;

                case "select_confirm":
                    HandleSelectConfirm(payload);
                    break;

                case "bet":
                    yield return StartCoroutine(HandleBet(payload));
                    break;

                case "discard":
                    yield return StartCoroutine(HandleDiscard(payload));
                    break;

                case "next_round":
                    Debug.Log("[Debug Client] next_round 受信 → 次ラウンド開始");
                    // next_round_accepted を送る
                    SendRawJson("{\"type\":\"next_round_accepted\",\"data\":{\"started\":true}}");
                    yield return new WaitForSeconds(0.5f);
                    StartCoroutine(StartNextRound());
                    break;
            }
        }

        // ===================================================
        // is_tenpai ハンドラ
        // Pythonの _is_tenpai: 手牌を判定して is_tenpai / not_tenpai を返す
        // ===================================================
        private void HandleIsTenpai(ActionPayload payload)
        {
            // 固定マンガン手牌なので常に is_tenpai（清一色七対子、待ち: 七萬）を返す
            string yakuJson = "[";
            for (int i = 0; i < LOCAL_YAKU.Length; i++)
            {
                if (i > 0) yakuJson += ",";
                yakuJson += "\"" + LOCAL_YAKU[i] + "\"";
            }
            yakuJson += "]";

            string json = $"{{\"type\":\"is_tenpai\",\"data\":{{\"waits\":[" +
                          $"{{\"tile\":{LOCAL_WAIT_TILE},\"mangan_or_more\":true,\"yaku\":{yakuJson},\"base_yaku\":{yakuJson}}}" +
                          $"]}}}}";
            SendRawJson(json);
            Debug.Log($"[Debug Client] is_tenpai 返答: 待ち=七萬(6), 清一色七対子, マンガン以上=true");
        }

        // ===================================================
        // select ハンドラ
        // Pythonの _select: マンガン以上なら hand_selection_accepted を返し、
        //                    全員確定後に hand_selection_completed をブロードキャスト
        // ===================================================
        private void HandleSelect(ActionPayload payload)
        {
            // hand_indexes からローカル手牌を確定
            if (payload.hand_indexes != null)
            {
                mockLocalHand.Clear();
                foreach (int idx in payload.hand_indexes)
                {
                    if (idx >= 0 && idx < mockLocalWall.Count)
                        mockLocalHand.Add(mockLocalWall[idx]);
                }
            }

            // 固定マンガン手牌なので hand_selection_accepted を返す（満貫以上確定）
            SendHandSelectionAccepted();

            // 全員確定（デバッグでは即時）→ hand_selection_completed をブロードキャスト
            StartCoroutine(SendHandSelectionCompletedAfterDelay(0.3f));
        }

        // ===================================================
        // select_confirm ハンドラ
        // Pythonの _select_confirm: 強制確定。同じく hand_selection_accepted → hand_selection_completed
        // ===================================================
        private void HandleSelectConfirm(ActionPayload payload)
        {
            if (payload.hand_indexes != null)
            {
                mockLocalHand.Clear();
                foreach (int idx in payload.hand_indexes)
                {
                    if (idx >= 0 && idx < mockLocalWall.Count)
                        mockLocalHand.Add(mockLocalWall[idx]);
                }
            }

            // forced=true で hand_selection_accepted を返す
            SendHandSelectionAccepted(forced: true);
            StartCoroutine(SendHandSelectionCompletedAfterDelay(0.3f));
        }

        // hand_selection_accepted（Pythonは選択したプレイヤーに個別送信）
        private void SendHandSelectionAccepted(bool forced = false)
        {
            string handJson = "[" + string.Join(",", mockLocalHand) + "]";
            string wallJson = "[" + string.Join(",", mockLocalWall) + "]";
            string waitsJson = $"[{LOCAL_WAIT_TILE}]";
            string forcedStr = forced ? ",\"forced\":true" : "";

            string json = $"{{\"type\":\"hand_selection_accepted\",\"data\":{{" +
                          $"\"hand\":{handJson},\"waits\":{waitsJson},\"wall\":{wallJson}{forcedStr}" +
                          $"}}}}";
            SendRawJson(json);
            Debug.Log("[Debug Client] hand_selection_accepted 送信");
        }

        // hand_selection_completed（Pythonは全員確定後にブロードキャスト）
        private IEnumerator SendHandSelectionCompletedAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            string localHandJson = "[" + string.Join(",", mockLocalHand) + "]";
            string localWallJson = "[" + string.Join(",", mockLocalWall) + "]";
            string localWaitsJson = $"[{LOCAL_WAIT_TILE}]";

            string enemyHandJson = "[" + string.Join(",", mockEnemyHand) + "]";
            string enemyWallJson = "[" + string.Join(",", mockEnemyWall) + "]";

            string json = $"{{\"type\":\"hand_selection_completed\",\"data\":{{\"hands\":[" +
                          $"{{\"client_id\":\"{localPlayerId}\",\"hand\":{localHandJson},\"waits\":{localWaitsJson},\"wall\":{localWallJson}}}," +
                          $"{{\"client_id\":\"{enemyPlayerId}\",\"hand\":{enemyHandJson},\"waits\":[],\"wall\":{enemyWallJson}}}" +
                          $"]}}}}";
            SendRawJson(json);
            Debug.Log("[Debug Client] hand_selection_completed 送信");

            // 少し待ってからベッティングフェーズへ
            yield return new WaitForSeconds(0.3f);
            SendRawJson("{\"type\":\"phase_change\",\"new_status\":\"betting\"}");
            Debug.Log("[Debug Client] phase_change(betting) 送信");
        }

        // ===================================================
        // bet ハンドラ
        // Pythonの _bet / on_bet: bet_completed → phase_change(discard) → discard_phase_started
        // ===================================================
        private IEnumerator HandleBet(ActionPayload payload)
        {
            lastLocalBet = payload.bet_amount;
            lastEnemyBet = payload.bet_amount;
            localPlayerHp -= lastLocalBet;
            enemyPlayerHp -= lastEnemyBet;

            // bet_completed ブロードキャスト
            string json = $"{{\"type\":\"bet_completed\",\"data\":{{\"bets\":[" +
                          $"{{\"client_id\":\"{localPlayerId}\",\"bet\":{lastLocalBet}}}," +
                          $"{{\"client_id\":\"{enemyPlayerId}\",\"bet\":{lastEnemyBet}}}" +
                          $"]}}}}";
            SendRawJson(json);
            Debug.Log($"[Debug Client] bet_completed 送信: local={lastLocalBet}, enemy={lastEnemyBet}");

            yield return new WaitForSeconds(3.5f);

            // phase_change(discard) → discard_phase_started
            SendRawJson("{\"type\":\"phase_change\",\"new_status\":\"discard\"}");
            yield return new WaitForSeconds(0.1f);
            SendRawJson($"{{\"type\":\"discard_phase_started\",\"data\":{{\"first_player\":\"{localPlayerId}\"}}}}");
            Debug.Log("[Debug Client] discard_phase_started 送信（先攻: ローカル）");
        }

        // ===================================================
        // discard ハンドラ
        // Pythonの _discard / on_discarded: discard_completed をブロードキャスト
        // ===================================================
        private IEnumerator HandleDiscard(ActionPayload payload)
        {
            // wall_index からタイルIDを取得
            int discardTileId = -1;
            // 牌IDは 0 始まり（0 = 一萬）で、無効値は -1。
            // 0 を「無し」と扱うと一萬の打牌が捨て牌に記録されない。
            if (payload.wall_index >= 0 && payload.wall_index < mockLocalWall.Count)
                discardTileId = mockLocalWall[payload.wall_index];
            else if (payload.tile >= 0)
                discardTileId = payload.tile;

            if (discardTileId >= 0)
                mockLocalDiscards.Add(discardTileId);

            // discard_completed をブロードキャスト（Pythonの on_discarded と同じ）
            SendDiscardCompleted(localPlayerId, discardTileId);
            Debug.Log($"[Debug Client] discard_completed 送信: player={localPlayerId}, tile={discardTileId}");

            yield return new WaitForSeconds(networkDelay * 2f);

            // 敵の打牌
            if (mockEnemyHand.Count > 0)
            {
                // デバッグ用: 必ずローカルプレイヤーの待ち牌を切ってロンさせる
                int enemyDiscard = LOCAL_WAIT_TILE;
                if (mockEnemyHand.Contains(LOCAL_WAIT_TILE))
                {
                    mockEnemyHand.Remove(LOCAL_WAIT_TILE);
                }
                else
                {
                    mockEnemyHand.RemoveAt(0);
                }
                mockEnemyDiscards.Add(enemyDiscard);

                string tileName = new TileData(enemyDiscard).GetTileName();
                gameUIManager.ShowDialogue($"「{tileName}を切るわ！」");

                yield return new WaitForSeconds(1.0f);

                // 敵の discard_completed をブロードキャスト
                SendDiscardCompleted(enemyPlayerId, enemyDiscard);
                Debug.Log($"[Debug Client] discard_completed 送信: player={enemyPlayerId}, tile={enemyDiscard}");

                // ロン判定（自分の待ち牌と一致するか）
                if (enemyDiscard == LOCAL_WAIT_TILE && mockLocalHand.Count >= 13)
                {
                    yield return new WaitForSeconds(1.0f);
                    // ロン成立 → round_end
                    SendRon(localPlayerId, enemyPlayerId, enemyDiscard);
                    yield break;
                }
            }

            // 巻数カウント
            mockTurnCount++;
            Debug.Log($"[Debug Client] 巻数: {mockTurnCount}/{MAX_TURNS}");

            if (mockTurnCount >= MAX_TURNS)
            {
                Debug.Log("[Debug Client] 17巻到達 → 流局");
                SendRawJson("{\"type\":\"round_end\",\"data\":{\"is_draw\":true}}");
                yield break;
            }

            // 次のターン開始（discard_phase_started）
            // Pythonは discard_completed の後に次の discard_phase_started を on_discard_started で送る
            yield return new WaitForSeconds(0.5f);
            SendRawJson($"{{\"type\":\"discard_phase_started\",\"data\":{{\"first_player\":\"{localPlayerId}\"}}}}");
        }

        // discard_completed JSON を組み立てて送信（Pythonの on_discarded に相当）
        private void SendDiscardCompleted(string playerId, int tileId)
        {
            string json = $"{{\"type\":\"discard_completed\",\"data\":{{\"player_id\":\"{playerId}\",\"tile\":{tileId}}}}}";
            SendRawJson(json);
        }

        // ロン成立時の round_end 送信（Pythonの on_round_end に相当）
        private void SendRon(string winnerId, string loserId, int ronTile)
        {
            int winnerGain = lastLocalBet + lastEnemyBet; // 簡易計算
            int winnerNewHp = localPlayerHp + winnerGain;
            int loserNewHp = enemyPlayerHp;

            string json = $"{{\"type\":\"round_end\",\"data\":{{\"is_draw\":false," +
                          $"\"liquidation\":{{" +
                          $"\"winner_id\":\"{winnerId}\"," +
                          $"\"loser_id\":\"{loserId}\"," +
                          $"\"han\":8," +
                          $"\"multiplier\":2.0," +
                          $"\"winner_bet\":{lastLocalBet}," +
                          $"\"loser_bet\":{lastEnemyBet}," +
                          $"\"winner_gain\":{winnerGain}," +
                          $"\"loser_loss\":{lastEnemyBet}," +
                          $"\"winner_health\":{winnerNewHp}," +
                          $"\"loser_health\":{loserNewHp}," +
                          $"\"yaku\":[\"清一色\",\"七対子\"]" +
                          $"}}}}}}";
            SendRawJson(json);

            // Pythonは round_end の後に next_round_waiting を送る
            StartCoroutine(SendNextRoundWaitingAfterDelay(0.5f));
            Debug.Log($"[Debug Client] round_end(ロン) 送信: winner={winnerId}");
        }

        private IEnumerator SendNextRoundWaitingAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            string json = $"{{\"type\":\"next_round_waiting\",\"data\":{{\"ready_players\":[],\"ready_count\":0,\"required_count\":2}}}}";
            SendRawJson(json);
        }

        // ===================================================
        // ラウンドリセット（next_round 後）
        // ===================================================
        private IEnumerator StartNextRound()
        {
            ResetRoundState();

            yield return new WaitForSeconds(0.3f);

            SendRawJson("{\"type\":\"phase_change\",\"new_status\":\"dealing\"}");
            yield return new WaitForSeconds(0.2f);

            SendDealingCompleted();
            yield return new WaitForSeconds(0.2f);

            SendRawJson("{\"type\":\"phase_change\",\"new_status\":\"hand_selection\"}");
            Debug.Log("[Debug Client] 次ラウンド開始 → hand_selection フェーズへ");
        }

        // ===================================================
        // dealing_completed 送信（Pythonの on_dealt に相当）
        // ===================================================
        private void SendDealingCompleted()
        {
            string localWallJson = "[" + string.Join(",", mockLocalWall) + "]";
            string enemyWallJson = "[" + string.Join(",", mockEnemyWall) + "]";

            // tenpai_examples: インデックス0〜12が手牌候補
            string tenpaiExStr = "[0,1,2,3,4,5,6,7,8,9,10,11,12]";

            string json = $"{{\"type\":\"dealing_completed\",\"dora_id\":5,\"hands\":[" +
                          $"{{\"client_id\":\"{localPlayerId}\",\"wall\":{localWallJson},\"tenpai_examples\":[{tenpaiExStr}]}}," +
                          $"{{\"client_id\":\"{enemyPlayerId}\",\"wall\":{enemyWallJson},\"tenpai_examples\":[{tenpaiExStr}]}}" +
                          $"]}}";
            SendRawJson(json);
            Debug.Log("[Debug Client] dealing_completed 送信（固定マンガン手牌）");
        }

        // ===================================================
        // ユーティリティ
        // ===================================================

        private void SendRawJson(string json)
        {
            gameUIManager.ApplyGameStateFromJSON(json, localPlayerId);
        }

        // Inspector のコンテキストメニューからテスト用ロンを発火
        [ContextMenu("Test Ron (Player Win)")]
        private void TriggerPlayerRon()
        {
            SendRon(localPlayerId, enemyPlayerId, LOCAL_WAIT_TILE);
        }

        [ContextMenu("Test Ron (Enemy Win)")]
        private void TriggerEnemyRon()
        {
            // 敵がロンした場合のテスト
            int lossBet = lastLocalBet > 0 ? lastLocalBet : 1000;
            string json = $"{{\"type\":\"round_end\",\"data\":{{\"is_draw\":false," +
                          $"\"liquidation\":{{" +
                          $"\"winner_id\":\"{enemyPlayerId}\"," +
                          $"\"loser_id\":\"{localPlayerId}\"," +
                          $"\"han\":5," +
                          $"\"multiplier\":1.0," +
                          $"\"winner_bet\":{lastEnemyBet}," +
                          $"\"loser_bet\":{lastLocalBet}," +
                          $"\"winner_gain\":{lossBet * 2}," +
                          $"\"loser_loss\":{lossBet}," +
                          $"\"winner_health\":{enemyPlayerHp + lossBet * 2}," +
                          $"\"loser_health\":{localPlayerHp - lossBet}," +
                          $"\"yaku\":[\"混一色\",\"三暗刻\"]" +
                          $"}}}}}}";
            SendRawJson(json);
            StartCoroutine(SendNextRoundWaitingAfterDelay(0.5f));
        }
    }
}
