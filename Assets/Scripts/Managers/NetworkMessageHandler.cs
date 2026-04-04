using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Network
{
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
        public event Action<int, int> OnGameEnded; // localScore, enemyScore
        
        // ハンド選択のフェーズに関するイベント（UI制御用）
        public event Action OnHandSelectionAccepted; 

        private string localPlayerId = ""; // GameManager等からセットされる想定

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
                data = new ActionMessageData
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
                        OnPhaseStatusChanged?.Invoke(RoundStatus.Dealing);
                        break;

                    case "bet":
                        // サーバーからのレスポンス形式確定までダミー値を渡す
                        OnBettingComplete?.Invoke(2000, 2000, 50000, 50000);
                        break;

                    case "wall_dealt":
                        HandleWallDealt(jsonString);
                        break;

                    case "hand_selected":
                        HandleHandSelected(jsonString);
                        break;

                    case "turn_decided":
                        TurnDecidedMessage turnMsg = JsonUtility.FromJson<TurnDecidedMessage>(jsonString);
                        bool isLocalTurn = (turnMsg != null && turnMsg.current_player == 0);
                        Managers.BoardStateManager.Instance.SetLocalTurn(isLocalTurn);
                        OnPhaseStatusChanged?.Invoke(RoundStatus.Discard);
                        break;
                        
                    case "is_tenpai":
                        IsTenpaiMessage tenpaiMsg = JsonUtility.FromJson<IsTenpaiMessage>(jsonString);
                        if (tenpaiMsg != null && tenpaiMsg.data != null && tenpaiMsg.data.waits != null)
                        {
                            var waits = new List<int>();
                            foreach (var w in tenpaiMsg.data.waits) waits.Add(w.tile);
                            // BoardStateManagerのデータを上書きしてRebuildトリガー
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
                        
                    case "discard":
                        DiscardMessage discardMsg = JsonUtility.FromJson<DiscardMessage>(jsonString);
                        if (discardMsg != null)
                        {
                            bool isLocal = (discardMsg.client_id == localPlayerId);
                            if (isLocal)
                            {
                                Managers.BoardStateManager.Instance.SetLocalTurn(false);
                            }
                            OnTileDiscarded?.Invoke(discardMsg.tile, isLocal);
                        }
                        break;
                        
                    case "agari":
                        AgariMessage agariMsg = JsonUtility.FromJson<AgariMessage>(jsonString);
                        if (agariMsg != null)
                        {
                            bool isLocalWin = (agariMsg.winner_client_id == localPlayerId);
                            Managers.BoardStateManager.Instance.LastIsLocalWin = isLocalWin;
                            OnAgari?.Invoke(isLocalWin);
                        }
                        OnPhaseStatusChanged?.Invoke(RoundStatus.Agari);
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

        private void HandleWallDealt(string jsonString)
        {
            OnPhaseStatusChanged?.Invoke(RoundStatus.HandSelection);
            
            WallDealtMessage msg = JsonUtility.FromJson<WallDealtMessage>(jsonString);
            if (msg.hands == null) return;
            
            var tenpaiExamples = ParseTenpaiExamplesFromJson(jsonString, localPlayerId);
            var convertedExamples = tenpaiExamples.ConvertAll(e => e.ToArray());
            Managers.BoardStateManager.Instance.SetTenpaiExamples(convertedExamples);

            foreach (var h in msg.hands)
            {
                if (h.client_id == localPlayerId)
                {
                    Managers.BoardStateManager.Instance.SetLocalState(new List<int>(h.hand), new List<int>());
                }
                else if (h.hand != null)
                {
                    Managers.BoardStateManager.Instance.SetEnemyState(new List<int>(h.hand), new List<int>());
                }
            }
            Managers.BoardStateManager.Instance.FireRebuildEvent();
        }

        private void HandleHandSelected(string jsonString)
        {
            HandSelectedMessage msg = JsonUtility.FromJson<HandSelectedMessage>(jsonString);
            if (msg.hands == null)
            {
                OnHandSelectionAccepted?.Invoke();
                return;
            }
            
            OnPhaseStatusChanged?.Invoke(RoundStatus.Betting);

            foreach (var h in msg.hands)
            {
                if (h.client_id == localPlayerId)
                {
                    Managers.BoardStateManager.Instance.SetLocalState(new List<int>(h.wall), new List<int>(h.hand), new List<int>(h.wait));
                }
                else if (h.hand != null)
                {
                    Managers.BoardStateManager.Instance.SetEnemyState(h.wall != null ? new List<int>(h.wall) : null, new List<int>(h.hand));
                }
            }
            
            Managers.BoardStateManager.Instance.ClearSelection();
            Managers.BoardStateManager.Instance.FireRebuildEvent();
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
                        if (tenpaiKey < 0) break;

                        int arrStart = jsonString.IndexOf('[', tenpaiKey + 17);
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
    }
}
