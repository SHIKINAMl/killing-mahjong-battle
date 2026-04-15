using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Network
{
    [System.Serializable]
    public class ActionPayloadWrapper
    {
        public string action;
        public ActionPayload data;
    }

    [System.Serializable]
    public class ActionMessage
    {
        public string type = "action";
        public ActionPayloadWrapper data;
    }

    [System.Serializable]
    public class ActionPayload
    {
        public int bet_amount;
        public List<int> hand_indexes;
        public List<int> wall_indexes;
        public int wall_index;
        public string skill_type;
        
        // --- 互換性維持 ---
        public int amount;
        public List<int> hand;
        public int tile; 
    }

    /// <summary>
    /// サーバーとの通信メッセージ（JSON）のパース、ルーティング、およびアクション発行を行うクラス
    /// </summary>
    public class NetworkMessageHandler : MonoBehaviour
    {
        public static NetworkMessageHandler Instance { get; private set; }

        [Header("Client References")]
        [SerializeField] private WebSocketGameClientSample webSocketClient;
        
        [Header("Debug")]
        [SerializeField] private bool useDebugClient;
        [SerializeField] private KillingMahjong.Network.DebugWebSocketClient debugWebSocketClient;

        // イベントルーティング
        public event Action OnMatchmakingWaiting;
        public event Action OnGameStarted;
        public event Action<int, int, int, int> OnBettingComplete;
        public event Action<RoundStatus> OnPhaseStatusChanged;
        public event Action<int, bool> OnTileDiscarded; // tileId, isLocalPlayer
        public event Action<bool> OnAgari; // isLocalWin
        public event Action OnDraw; // 流局
        public event Action<int, int> OnGameEnded; // localScore, enemyScore
        
        // ハンド選択のフェーズに関するイベント（UI制御用）
        public event Action OnHandSelectionAccepted; 
        
        // ローディング表示用：配牌生成の待機状態イベント
        public event Action OnDealingStarted;
        public event Action OnDealingCompleted;

        private string localPlayerId = ""; // GameManager等からセットされる想定
        private bool agariProcessed = false; // ロン二重発火防止フラグ

        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (useDebugClient && debugWebSocketClient != null)
            {
                debugWebSocketClient.StartMockConnection();
            }
        }

        public void SetLocalPlayerId(string clientId)
        {
            this.localPlayerId = clientId;
        }

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
                data = new ActionPayloadWrapper
                {
                    action = actionType,
                    data = dataPayload
                }
            };

            string json = JsonUtility.ToJson(msg);
            await webSocketClient.SendAsync(json);
        }

        public void ProcessServerMessage(string jsonString)
        {
            try
            {
                ServerMessageBase baseMsg = JsonUtility.FromJson<ServerMessageBase>(jsonString);
                if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type)) return;

                switch (baseMsg.type)
                {
                    case "matching_waiting":
                        OnMatchmakingWaiting?.Invoke();
                        break;

                    case "game_started":
                        OnGameStarted?.Invoke();
                        break;
                        
                    case "phase_change":
                        PhaseChangeMessage pMsg = JsonUtility.FromJson<PhaseChangeMessage>(jsonString);
                        if (pMsg != null) {
                            if (pMsg.new_status == "dealing") 
                            {
                                agariProcessed = false; // 新ラウンドでリセット
                                OnPhaseStatusChanged?.Invoke(RoundStatus.Dealing);
                                OnDealingStarted?.Invoke();
                            }
                            else if (pMsg.new_status == "hand_selection") OnPhaseStatusChanged?.Invoke(RoundStatus.HandSelection);
                            else if (pMsg.new_status == "betting") OnPhaseStatusChanged?.Invoke(RoundStatus.Betting);
                            else if (pMsg.new_status == "discard") OnPhaseStatusChanged?.Invoke(RoundStatus.Discard);
                        }
                        break;

                    case "dealing_completed":
                        OnDealingCompleted?.Invoke();
                        HandleDealingCompleted(jsonString);
                        break;

                    case "hand_selection_completed":
                        HandleHandSelectionCompleted(jsonString);
                        break;

                    case "bet_completed":
                        BetCompletedMessage bMsg = JsonUtility.FromJson<BetCompletedMessage>(jsonString);
                        if (bMsg != null && bMsg.data != null && bMsg.data.bets != null) {
                            int pBet = 0; int eBet = 0;
                            foreach(var b in bMsg.data.bets) {
                                if (b.client_id == localPlayerId) pBet = b.bet;
                                else eBet = b.bet;
                            }
                            int currentLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                            int currentEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                            
                            Managers.BoardStateManager.Instance.UpdateHp(currentLocalHp - pBet, currentEnemyHp - eBet);
                            OnBettingComplete?.Invoke(pBet, eBet, currentLocalHp, currentEnemyHp);
                        }
                        break;

                    case "discard_phase_started":
                        DiscardPhaseStartedMessage dpsMsg = JsonUtility.FromJson<DiscardPhaseStartedMessage>(jsonString);
                        bool isLocalTurn = (dpsMsg != null && dpsMsg.data != null && dpsMsg.data.first_player == localPlayerId);
                        Managers.BoardStateManager.Instance.SetLocalTurn(isLocalTurn);
                        OnPhaseStatusChanged?.Invoke(RoundStatus.Discard);
                        break;

                    case "is_tenpai":
                        IsTenpaiMessage tenpaiMsg = JsonUtility.FromJson<IsTenpaiMessage>(jsonString);
                        if (tenpaiMsg != null && tenpaiMsg.data != null && tenpaiMsg.data.waits != null)
                        {
                            var waits = new List<int>();
                            foreach (var w in tenpaiMsg.data.waits) waits.Add(w.tile);
                            Managers.BoardStateManager.Instance.SetLocalState(null, null, waits);
                            Managers.BoardStateManager.Instance.FireRebuildEvent();
                        }
                        break;

                    case "not_tenpai":
                        Managers.BoardStateManager.Instance.SetLocalState(null, null, new List<int>());
                        Managers.BoardStateManager.Instance.FireRebuildEvent();
                        break;

                    case "error":
                        ErrorMessage errorMsg = JsonUtility.FromJson<ErrorMessage>(jsonString);
                        Debug.LogError($"[Server Error] {errorMsg?.message}");
                        break;

                    case "discard_completed":
                        DiscardCompletedMessage discardMsg = JsonUtility.FromJson<DiscardCompletedMessage>(jsonString);
                        if (discardMsg != null && discardMsg.data != null)
                        {
                            bool isLocal = (discardMsg.data.player_id == localPlayerId);
                            if (isLocal)
                            {
                                Managers.BoardStateManager.Instance.SetLocalTurn(false);
                            }
                            else
                            {
                                Managers.BoardStateManager.Instance.SetLocalTurn(true);
                            }
                            OnTileDiscarded?.Invoke(discardMsg.data.tile, isLocal);
                        }
                        break;

                    case "hand_selection_accepted":
                        HandSelectionAcceptedMessage hsaMsg = JsonUtility.FromJson<HandSelectionAcceptedMessage>(jsonString);
                        OnHandSelectionAccepted?.Invoke();
                        break;

                    case "discard_accepted":
                        DiscardAcceptedMessage daMsg = JsonUtility.FromJson<DiscardAcceptedMessage>(jsonString);
                        if (daMsg != null && daMsg.data != null)
                        {
                            if (daMsg.data.is_win && !agariProcessed) {
                                agariProcessed = true;
                                Debug.Log($"[Network] discard_accepted: ロン成立 (is_win=true)");
                                // discard_accepted にも liquidation が含まれている場合はここで処理
                                LiquidationData daLiq = daMsg.data.liquidation;
                                if (daLiq == null || string.IsNullOrEmpty(daLiq.winner_id))
                                {
                                    daLiq = ParseLiquidationFromJson(jsonString);
                                }
                                if (daLiq != null && !string.IsNullOrEmpty(daLiq.winner_id))
                                {
                                    Debug.Log($"[Network] discard_accepted ロン: winner={daLiq.winner_id}");
                                    bool isLocalWinDa = (daLiq.winner_id == localPlayerId);
                                    Managers.BoardStateManager.Instance.LastIsLocalWin = isLocalWinDa;

                                    int newLocalHpDa = isLocalWinDa ? daLiq.winner_health : daLiq.loser_health;
                                    int newEnemyHpDa = isLocalWinDa ? daLiq.loser_health : daLiq.winner_health;
                                    Managers.BoardStateManager.Instance.UpdateHp(newLocalHpDa, newEnemyHpDa);

                                    OnPhaseStatusChanged?.Invoke(RoundStatus.Agari);
                                    OnAgari?.Invoke(isLocalWinDa);
                                }
                            }
                        }
                        break;

                    case "round_end":
                        Debug.Log($"[Network] round_end 受信: {jsonString}");
                        RoundEndMessage reMsg = JsonUtility.FromJson<RoundEndMessage>(jsonString);
                        if (reMsg != null && reMsg.data != null) {
                            Debug.Log($"[Network] round_end パース結果: is_draw={reMsg.data.is_draw}, liquidation={reMsg.data.liquidation != null}");
                            if (reMsg.data.is_draw) {
                                // 流局
                                Debug.Log("[Network] 流局が発生しました");
                                OnPhaseStatusChanged?.Invoke(RoundStatus.Draw);
                                OnDraw?.Invoke();
                            }
                            else if (!agariProcessed) {
                                agariProcessed = true;
                                // ロン判定 - JsonUtility がネストした liquidation をパースできない場合に手動パースする
                                LiquidationData liq = reMsg.data.liquidation;
                                if (liq == null || string.IsNullOrEmpty(liq.winner_id))
                                {
                                    liq = ParseLiquidationFromJson(jsonString);
                                }

                                if (liq != null && !string.IsNullOrEmpty(liq.winner_id))
                                {
                                    Debug.Log($"[Network] ロン成立: winner={liq.winner_id}, loser={liq.loser_id}, winner_health={liq.winner_health}, loser_health={liq.loser_health}");
                                    bool isLocalWin = (liq.winner_id == localPlayerId);
                                    Managers.BoardStateManager.Instance.LastIsLocalWin = isLocalWin;
                                    
                                    int newLocalHp = isLocalWin ? liq.winner_health : liq.loser_health;
                                    int newEnemyHp = isLocalWin ? liq.loser_health : liq.winner_health;
                                    Managers.BoardStateManager.Instance.UpdateHp(newLocalHp, newEnemyHp);

                                    OnPhaseStatusChanged?.Invoke(RoundStatus.Agari);
                                    OnAgari?.Invoke(isLocalWin);
                                }
                                else
                                {
                                    Debug.LogWarning("[Network] round_end: is_draw=false だが liquidation データが取得できませんでした");
                                }
                            }
                        }
                        break;

                    case "next_round_waiting":
                        // サーバーが次局待ちに入ったので、自動的に承認を送る
                        Debug.Log("[Network] 次局進行待ち - 自動承認を送信");
                        SendActionToServer("next_round", new ActionPayload());
                        break;

                    case "next_round_accepted":
                        Debug.Log("[Network] 次局進行承認済み");
                        break;

                    case "game_end":
                        ParseGameEnd(jsonString);
                        break;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse JSON: {e.Message}\n{jsonString}");
            }
        }

        private void HandleDealingCompleted(string jsonString)
        {
            DealingCompletedMessage msg = JsonUtility.FromJson<DealingCompletedMessage>(jsonString);
            if (msg.hands == null) return;
            
            var tenpaiExamples = ParseTenpaiExamplesFromJson(jsonString, localPlayerId);
            
            // --- デバッグログ追加 ---
            Debug.Log($"[Network] サーバーからのJSON: {jsonString}");
            Debug.Log($"[Network] 抽出されたお手本の数: {tenpaiExamples.Count}");
            for (int i = 0; i < tenpaiExamples.Count; i++)
            {
                Debug.Log($"[Network] お手本 {i}: [{string.Join(", ", tenpaiExamples[i])}]");
            }
            // ------------------------

            var convertedExamples = tenpaiExamples.ConvertAll(e => e.ToArray());
            Managers.BoardStateManager.Instance.SetTenpaiExamples(convertedExamples);

            // 手動独自パースで wall 配列を抽出 (JsonUtilityが int[] を上手くさばけない場合のフェールセーフ)
            var wallDict = ParseIntArrays(jsonString, "wall");

            foreach (var h in msg.hands)
            {
                List<int> wallTiles = new List<int>();
                if (wallDict.ContainsKey(h.client_id)) wallTiles = wallDict[h.client_id];
                // JsonUtility が取得できていればそちらを優先
                if (h.wall != null && h.wall.Length > 0) wallTiles = new List<int>(h.wall);

                if (h.client_id == localPlayerId)
                {
                    Managers.BoardStateManager.Instance.SetLocalState(wallTiles, new List<int>());
                }
                else
                {
                    Managers.BoardStateManager.Instance.SetEnemyState(wallTiles, new List<int>());
                }
            }
            Managers.BoardStateManager.Instance.FireRebuildEvent();
        }

        private void HandleHandSelectionCompleted(string jsonString)
        {
            HandSelectionCompletedMessage msg = JsonUtility.FromJson<HandSelectionCompletedMessage>(jsonString);
            if (msg.data == null || msg.data.hands == null) return;

            var handDict = ParseIntArrays(jsonString, "hand");
            var wallDict = ParseIntArrays(jsonString, "wall");
            var waitDict = ParseIntArrays(jsonString, "wait"); // wait or waits, API doc depends
            if (waitDict.Count == 0) waitDict = ParseIntArrays(jsonString, "waits");

            foreach (var h in msg.data.hands)
            {
                List<int> handTiles = new List<int>();
                List<int> wallTiles = new List<int>();
                List<int> waitTiles = new List<int>();

                if (handDict.ContainsKey(h.client_id)) handTiles = handDict[h.client_id];
                if (wallDict.ContainsKey(h.client_id)) wallTiles = wallDict[h.client_id];
                if (waitDict.ContainsKey(h.client_id)) waitTiles = waitDict[h.client_id];

                if (h.hand != null) handTiles = new List<int>(h.hand);
                if (h.wall != null) wallTiles = new List<int>(h.wall);
                if (h.waits != null) waitTiles = new List<int>(h.waits);

                if (h.client_id == localPlayerId)
                {
                    Managers.BoardStateManager.Instance.SetLocalState(wallTiles, handTiles, waitTiles);
                }
                else
                {
                    Managers.BoardStateManager.Instance.SetEnemyState(wallTiles, handTiles);
                }
            }
            
            Managers.BoardStateManager.Instance.ClearSelection();
            Managers.BoardStateManager.Instance.FireRebuildEvent();
        }

        // JsonUtilityがプリミティブ配列をパースできない問題等のフォールバック
        private Dictionary<string, List<int>> ParseIntArrays(string jsonString, string arrayKeyName)
        {
            var result = new Dictionary<string, List<int>>();
            int searchFrom = 0;
            while (true)
            {
                int cidStart = jsonString.IndexOf("\"client_id\"", searchFrom);
                if (cidStart < 0) break;

                int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                int valEnd = jsonString.IndexOf('"', valStart);
                if (valStart < 0 || valEnd < 0) break;
                string cid = jsonString.Substring(valStart, valEnd - valStart);

                int keyStart = jsonString.IndexOf($"\"{arrayKeyName}\"", valEnd);
                if (keyStart > 0 && keyStart < cidStart + 200) // 次のデータに飛びすぎないよう簡易チェック
                {
                    int arrStart = jsonString.IndexOf('[', keyStart);
                    int arrEnd = jsonString.IndexOf(']', arrStart);
                    if (arrStart > 0 && arrEnd > arrStart)
                    {
                        string inner = jsonString.Substring(arrStart + 1, arrEnd - arrStart - 1);
                        var list = new List<int>();
                        foreach (var token in inner.Split(','))
                        {
                            if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                        }
                        result[cid] = list;
                    }
                }
                searchFrom = valEnd + 1;
            }
            return result;
        }

        private List<List<int>> ParseTenpaiExamplesFromJson(string jsonString, string targetClientId)
        {
            var result = new List<List<int>>();
            try
            {
                int handsStart = jsonString.IndexOf("\"hands\"");
                if (handsStart < 0) return result;

                int searchFrom = handsStart;
                while (true)
                {
                    int cidStart = jsonString.IndexOf("\"client_id\"", searchFrom);
                    if (cidStart < 0) break;

                    int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                    int valEnd = jsonString.IndexOf('"', valStart);
                    if (valStart < 0 || valEnd < 0) break;
                    string cid = jsonString.Substring(valStart, valEnd - valStart);

                    if (cid == targetClientId)
                    {
                        int tenpaiKey = jsonString.IndexOf("\"tenpai_examples\"", valEnd);
                        int keyLength = 17;
                        if (tenpaiKey < 0)
                        {
                            tenpaiKey = jsonString.IndexOf("\"tenpai_example\"", valEnd);
                            keyLength = 16;
                        }
                        if (tenpaiKey < 0) break;

                        int arrStart = jsonString.IndexOf('[', tenpaiKey + keyLength);
                        if (arrStart < 0) break;

                        int peek = arrStart + 1;
                        while (peek < jsonString.Length && jsonString[peek] == ' ') peek++;

                        if (peek < jsonString.Length && jsonString[peek] == '[')
                        {
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
                                            if (int.TryParse(token.Trim(), out int val)) hand.Add(val);
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
                            int arrEnd = jsonString.IndexOf(']', arrStart);
                            if (arrEnd > arrStart)
                            {
                                string inner = jsonString.Substring(arrStart + 1, arrEnd - arrStart - 1);
                                var hand = new List<int>();
                                foreach (var token in inner.Split(','))
                                {
                                    if (int.TryParse(token.Trim(), out int val)) hand.Add(val);
                                }
                                if (hand.Count > 0) result.Add(hand);
                            }
                        }
                        break;
                    }
                    searchFrom = valEnd + 1;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ParseTenpaiExamples] Error: {e.Message}");
            }
            return result;
        }

        private void ParseGameEnd(string jsonString)
        {
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
                    OnGameEnded?.Invoke(localScore, enemyScore);
                }
            }
        }

        /// <summary>
        /// round_end JSON から liquidation データを手動パースする（JsonUtility のフォールバック）
        /// </summary>
        private LiquidationData ParseLiquidationFromJson(string jsonString)
        {
            try
            {
                int liqStart = jsonString.IndexOf("\"liquidation\"");
                if (liqStart < 0) return null;

                int objStart = jsonString.IndexOf('{', liqStart);
                if (objStart < 0) return null;

                // ネストされた {} を考慮して末尾を見つける
                int depth = 0;
                int objEnd = -1;
                for (int i = objStart; i < jsonString.Length; i++)
                {
                    if (jsonString[i] == '{') depth++;
                    else if (jsonString[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            objEnd = i;
                            break;
                        }
                    }
                }
                if (objEnd < 0) return null;

                string liqJson = jsonString.Substring(objStart, objEnd - objStart + 1);
                Debug.Log($"[Network] liquidation 手動パース: {liqJson}");

                LiquidationData liq = JsonUtility.FromJson<LiquidationData>(liqJson);
                return liq;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Network] liquidation 手動パース失敗: {e.Message}");
                return null;
            }
        }
    }
}
