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
    public class NextRoundWaitingData
    {
        public List<string> ready_players;
        public int ready_count;
        public int required_count;
    }

    [System.Serializable]
    public class NextRoundWaitingMessage
    {
        public string type;
        public NextRoundWaitingData data;
    }

    [System.Serializable]
    public class SkillCastedData
    {
        public string player_id;
        public string skillType;
        public int cost;
        public string yaku_name;
        public List<int> exposedHandIndexes;
        // manually populated
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> exposedHandIndexesByPlayer;
    }

    [System.Serializable]
    public class SpecialVictoryWonData
    {
        public string player_id;
    }

    [System.Serializable]
    public class SpecialVictoryWonMessage
    {
        public string type;
        public SpecialVictoryWonData data;
    }

    [System.Serializable]
    public class SkillCastedMessage
    {
        public string type;
        public SkillCastedData data;
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
        public int target_hand_index;
        public string yaku_name;
        
        // --- 互換性維持 ---
        public int amount;
        public List<int> hand;
        public int tile; 
        
        public bool accept;
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
        public event Action<string> OnMatchCancelled;
        public event Action OnGameStarted;
        public event Action<int, int, int, int> OnBettingComplete;
        public event Action<RoundStatus> OnPhaseStatusChanged;
        public event Action<int, bool> OnTileDiscarded; // tileId, isLocalPlayer
        public event Action<bool> OnAgari; // isLocalWin
        public event Action OnDraw; // 流局
        public event Action<int, int> OnGameEnded; // localScore, enemyScore
        public event Action OnNextRoundWaitingReceived; // 相手からの次局待機（ロンボタン押下の合図として利用）
        
        public event Action<StatusData> OnStatusReceived;
        public event Action<AgariPendingData> OnAgariPendingReceived;
        
        public event Action<string> OnError;
        public event Action<HandSelectionConfirmationData> OnHandSelectionConfirmation;
        public event Action<IsTenpaiData> OnIsTenpaiReceived;
        public event Action<string> OnNotTenpaiReceived;
        // ハンド選択のフェーズに関するイベント（UI制御用）
        public event Action OnHandSelectionAccepted; 
        
        // ローディング表示用：配牌生成の待機状態イベント
        public event Action OnDealingStarted;
        public event Action OnDealingCompleted;

        public event Action<SkillCastedData> OnSkillCasted;
        public event Action<string> OnSpecialVictoryWon;
        public event Action OnOpeningBoostAssigned;

        private string localPlayerId = ""; // GameManager等からセットされる想定
        public string LocalPlayerId => localPlayerId;
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
                    case "status":
                        StatusMessage statusMsg = JsonUtility.FromJson<StatusMessage>(jsonString);
                        if (statusMsg != null && statusMsg.data != null)
                        {
                            bool shouldRebuild = false;

                            // 文字列抽出による boost_hand_bonus の簡単な取得 (JsonUtility制約回避用)
                            if (statusMsg.data.player_state != null)
                            {
                                Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount = statusMsg.data.player_state.special_victory_count;
                                var bonusDict = ParseBoostHandBonusFromStateKey(jsonString, "player_state");
                                if (bonusDict != null) {
                                    Managers.BoardStateManager.Instance.LocalBoostHandBonus = bonusDict;
                                }

                                if (statusMsg.data.player_state.wall != null && statusMsg.data.player_state.hand != null)
                                {
                                    var localWall = new System.Collections.Generic.List<int>(statusMsg.data.player_state.wall);
                                    var localHand = new System.Collections.Generic.List<int>();
                                    
                                    if (statusMsg.data.game_state != null && statusMsg.data.game_state.status == "hand_selection") 
                                    {
                                        var currentHand = Managers.BoardStateManager.Instance.CurrentHandTiles;
                                        if (currentHand != null && currentHand.Count > 0)
                                        {
                                            localHand.AddRange(currentHand);
                                        }
                                        else
                                        {
                                            var targetIndexes = Managers.BoardStateManager.Instance.TargetHandIndexes;
                                            if (targetIndexes != null) {
                                                foreach(var idx in targetIndexes) {
                                                    if (idx >= 0 && idx < localWall.Count) {
                                                        localHand.Add(localWall[idx]);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else 
                                    {
                                        var localHandIndices = statusMsg.data.player_state.hand;
                                        if (localHandIndices != null) {
                                            foreach(var idx in localHandIndices) {
                                                if (idx >= 0 && idx < localWall.Count) {
                                                    localHand.Add(localWall[idx]);
                                                }
                                            }
                                        }
                                    }

                                    Managers.BoardStateManager.Instance.SetLocalState(
                                        localWall,
                                        localHand,
                                        statusMsg.data.player_state.waits != null ? new System.Collections.Generic.List<int>(statusMsg.data.player_state.waits) : new System.Collections.Generic.List<int>()
                                    );
                                    shouldRebuild = true;
                                }
                            }
                            if (statusMsg.data.opponent_player_state != null)
                            {
                                var bonusDict = ParseBoostHandBonusFromStateKey(jsonString, "opponent_player_state");
                                if (bonusDict != null) {
                                    Managers.BoardStateManager.Instance.EnemyBoostHandBonus = bonusDict;
                                }

                                if (statusMsg.data.opponent_player_state.wall != null && statusMsg.data.opponent_player_state.hand != null)
                                {
                                    var dummyEnemyHand = new System.Collections.Generic.List<int>();
                                    foreach(var _ in statusMsg.data.opponent_player_state.hand) {
                                        dummyEnemyHand.Add(-1);
                                    }

                                    Managers.BoardStateManager.Instance.SetEnemyState(
                                        new System.Collections.Generic.List<int>(statusMsg.data.opponent_player_state.wall),
                                        dummyEnemyHand
                                    );
                                    shouldRebuild = true;
                                }
                            }

                            if (shouldRebuild)
                            {
                                Managers.BoardStateManager.Instance.FireRebuildEvent();
                            }

                            // 恒常強化された役のパース等、後でUI表示用に利用
                            OnStatusReceived?.Invoke(statusMsg.data);
                        }
                        break;

                    case "agari_pending":
                        AgariPendingMessage agariPendingMsg = JsonUtility.FromJson<AgariPendingMessage>(jsonString);
                        if (agariPendingMsg != null && agariPendingMsg.data != null)
                        {
                            OnAgariPendingReceived?.Invoke(agariPendingMsg.data);
                        }
                        break;

                    case "matching_waiting":
                        OnMatchmakingWaiting?.Invoke();
                        break;

                    case "match_cancelled":
                        MatchCancelledMessage cancelMsg = JsonUtility.FromJson<MatchCancelledMessage>(jsonString);
                        string reasonMsg = "通信が切断されました。マッチング待機中です...";
                        if (cancelMsg != null && cancelMsg.data != null && cancelMsg.data.reason == "player_disconnected")
                        {
                            reasonMsg = "対戦相手が切断しました。マッチング待機中です...";
                        }
                        OnMatchCancelled?.Invoke(reasonMsg);
                        break;

                    case "game_started":
                        OnGameStarted?.Invoke();
                        break;

                    case "skill_casted":
                        SkillCastedMessage scMsg = JsonUtility.FromJson<SkillCastedMessage>(jsonString);
                        if (scMsg != null && scMsg.data != null)
                        {
                            scMsg.data.exposedHandIndexesByPlayer = ParseExposedHandIndexesByPlayer(jsonString);
                            OnSkillCasted?.Invoke(scMsg.data);
                            
                            // スキル使用直後に、役の強化状況や最新のHPを確実に取り寄せる
                            SendActionToServer("status", null);
                        }
                        break;

                    case "special_victory_won":
                        SpecialVictoryWonMessage svwMsg = JsonUtility.FromJson<SpecialVictoryWonMessage>(jsonString);
                        if (svwMsg != null && svwMsg.data != null)
                        {
                            OnSpecialVictoryWon?.Invoke(svwMsg.data.player_id);
                        }
                        break;

                    case "opening_boost_assigned":
                        OpeningBoostAssignedMessage obMsg = JsonUtility.FromJson<OpeningBoostAssignedMessage>(jsonString);
                        if (obMsg != null && obMsg.data != null && obMsg.data.boosts != null)
                        {
                            foreach (var boost in obMsg.data.boosts)
                            {
                                if (boost.client_id == localPlayerId)
                                {
                                    if (Managers.BoardStateManager.Instance.LocalBoostHandBonus == null)
                                        Managers.BoardStateManager.Instance.LocalBoostHandBonus = new System.Collections.Generic.Dictionary<string, int>();
                                    Managers.BoardStateManager.Instance.LocalBoostHandBonus[boost.yaku_name] = boost.bonus_han;
                                }
                                else
                                {
                                    if (Managers.BoardStateManager.Instance.EnemyBoostHandBonus == null)
                                        Managers.BoardStateManager.Instance.EnemyBoostHandBonus = new System.Collections.Generic.Dictionary<string, int>();
                                    Managers.BoardStateManager.Instance.EnemyBoostHandBonus[boost.yaku_name] = boost.bonus_han;
                                }
                            }
                            OnOpeningBoostAssigned?.Invoke();
                        }
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
                        Managers.BoardStateManager.Instance.SetLocalPlayerFirstRound(isLocalTurn);
                        Managers.BoardStateManager.Instance.SetLocalTurn(isLocalTurn);
                        OnPhaseStatusChanged?.Invoke(RoundStatus.Discard);
                        break;

                    case "is_tenpai":
                        IsTenpaiMessage tenpaiMsg = JsonUtility.FromJson<IsTenpaiMessage>(jsonString);
                        if (tenpaiMsg != null && tenpaiMsg.data != null && tenpaiMsg.data.waits != null)
                        {
                            var waits = new List<int>();
                            var nonManganList = new List<int>();
                            foreach (var w in tenpaiMsg.data.waits) 
                            {
                                waits.Add(w.tile);
                                if (!w.mangan_or_more) 
                                {
                                    nonManganList.Add(w.tile);
                                }
                            }
                            Managers.BoardStateManager.Instance.SetNonManganWaits(nonManganList);
                            Managers.BoardStateManager.Instance.SetLocalState(null, null, waits);
                            Managers.BoardStateManager.Instance.FireRebuildEvent();

                            OnIsTenpaiReceived?.Invoke(tenpaiMsg.data);
                        }
                        break;

                    case "not_tenpai":
                        Managers.BoardStateManager.Instance.SetLocalState(null, null, new List<int>());
                        Managers.BoardStateManager.Instance.FireRebuildEvent();

                        NotTenpaiMessage notTenpaiMsg = JsonUtility.FromJson<NotTenpaiMessage>(jsonString);
                        OnNotTenpaiReceived?.Invoke(notTenpaiMsg?.message ?? "Hand is not in tenpai");
                        break;

                    case "error":
                        ErrorMessage errorMsg = JsonUtility.FromJson<ErrorMessage>(jsonString);
                        Debug.LogError($"[Server Error] {errorMsg?.message}");
                        OnError?.Invoke(errorMsg?.message);
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
                        if (hsaMsg != null && hsaMsg.data != null && hsaMsg.data.waits != null && hsaMsg.data.waits.Length > 0)
                        {
                            var acceptedWaits = new List<int>(hsaMsg.data.waits);
                            Managers.BoardStateManager.Instance.SetLocalState(null, null, acceptedWaits);
                            Managers.BoardStateManager.Instance.FireRebuildEvent();
                        }
                        OnHandSelectionAccepted?.Invoke();
                        break;

                    case "hand_selection_confirmation_required":
                        HandSelectionConfirmationMessage confirmMsg = JsonUtility.FromJson<HandSelectionConfirmationMessage>(jsonString);
                        if (confirmMsg != null && confirmMsg.data != null)
                        {
                            OnHandSelectionConfirmation?.Invoke(confirmMsg.data);
                        }
                        break;

                    case "discard_accepted":
                        DiscardAcceptedMessage daMsg = JsonUtility.FromJson<DiscardAcceptedMessage>(jsonString);
                        if (daMsg != null && daMsg.data != null)
                        {
                            if (daMsg.data.is_win && !agariProcessed) {
                                // ロン判定時のみ、フェーズがAgariに変わる前に打牌を反映させる
                                if (daMsg.data.tile > 0)
                                {
                                    OnTileDiscarded?.Invoke(daMsg.data.tile, true);
                                }
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
                                    Managers.BoardStateManager.Instance.LastLiquidationData = daLiq;

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
                                    Managers.BoardStateManager.Instance.LastLiquidationData = liq;
                                    
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
                        var nrwMsg = JsonUtility.FromJson<NextRoundWaitingMessage>(jsonString);
                        if (nrwMsg != null && nrwMsg.data != null)
                        {
                            if (nrwMsg.data.ready_count > 0)
                            {
                                Debug.Log("[Network] 次局進行待ち (ready_count > 0) - 相手が演出を進行させた合図として処理します");
                                OnNextRoundWaitingReceived?.Invoke();
                            }
                            else
                            {
                                Debug.Log("[Network] 次局進行待ち (ready_count == 0) - ラウンド終了直後の通知のため無視します");
                            }
                        }
                        else
                        {
                            // パース失敗時等のフォールバック
                            OnNextRoundWaitingReceived?.Invoke();
                        }
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

        private Dictionary<string, int> ParseBoostHandBonusFromStateKey(string jsonString, string stateKey)
        {
            try
            {
                string pattern = $"\"{stateKey}\"\\s*:\\s*{{.*?\"boost_hand_bonus\"\\s*:\\s*{{([^}}]*?)}}";
                var match = System.Text.RegularExpressions.Regex.Match(jsonString, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!match.Success) return null;

                string dictStr = match.Groups[1].Value.Trim();
                var result = new Dictionary<string, int>();
                if (string.IsNullOrEmpty(dictStr)) return result;

                var pairs = dictStr.Split(',');
                foreach (var pair in pairs)
                {
                    var kv = pair.Split(':');
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim().Trim('"');
                        if (int.TryParse(kv[1].Trim(), out int val))
                        {
                            result[key] = val;
                        }
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ParseBoostHandBonus failed: " + ex.Message);
                return null;
            }
        }

        private void HandleDealingCompleted(string jsonString)
        {
            DealingCompletedMessage msg = JsonUtility.FromJson<DealingCompletedMessage>(jsonString);
            if (msg.hands == null) return;
            
            // --- デバッグログ追加 ---
            Debug.Log($"[Network] サーバーからのJSON: {jsonString}");

            var tenpaiDict = ParseTenpaiExamples(jsonString);
            var tenpaiExamples = new System.Collections.Generic.List<int[]>();
            
            if (tenpaiDict.ContainsKey(localPlayerId))
            {
                tenpaiExamples = tenpaiDict[localPlayerId];
            }

            Debug.Log($"[Network] 抽出されたお手本の数: {tenpaiExamples.Count}");
            for (int i = 0; i < tenpaiExamples.Count; i++)
            {
                Debug.Log($"[Network] お手本 {i}: [{string.Join(", ", tenpaiExamples[i])}]");
            }
            // ------------------------

            Managers.BoardStateManager.Instance.SetTenpaiExamples(tenpaiExamples);

            // ドラ表示牌を保存
            Managers.BoardStateManager.Instance.CurrentDoraId = msg.dora_id;

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
                    var dummyEnemyHand = new System.Collections.Generic.List<int>();
                    foreach(var _ in handTiles) {
                        dummyEnemyHand.Add(-1);
                    }
                    Managers.BoardStateManager.Instance.SetEnemyState(wallTiles, dummyEnemyHand);
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

                int nextCidStart = jsonString.IndexOf("\"client_id\"", cidStart + 11);
                if (nextCidStart < 0) nextCidStart = jsonString.Length;

                int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                int valEnd = jsonString.IndexOf('"', valStart);
                if (valStart < 0 || valEnd < 0) break;
                string cid = jsonString.Substring(valStart, valEnd - valStart);

                int keyStart = jsonString.IndexOf($"\"{arrayKeyName}\"", valEnd);
                if (keyStart > 0 && keyStart < nextCidStart) // 次のデータに飛びすぎないようチェック
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

        private Dictionary<string, List<int>> ParseExposedHandIndexesByPlayer(string jsonString)
        {
            var result = new Dictionary<string, List<int>>();
            int startIdx = jsonString.IndexOf("\"exposedHandIndexesByPlayer\"");
            if (startIdx < 0) return result;
            
            int objStart = jsonString.IndexOf('{', startIdx);
            if (objStart < 0) return result;
            
            int objEnd = FindMatchingBracket(jsonString, objStart, '{', '}');
            if (objEnd < 0) return result;
            
            string innerObj = jsonString.Substring(objStart + 1, objEnd - objStart - 1);
            
            int searchIdx = 0;
            while (true)
            {
                int quote1 = innerObj.IndexOf('"', searchIdx);
                if (quote1 < 0) break;
                int quote2 = innerObj.IndexOf('"', quote1 + 1);
                if (quote2 < 0) break;
                
                string clientId = innerObj.Substring(quote1 + 1, quote2 - quote1 - 1);
                
                int arrStart = innerObj.IndexOf('[', quote2);
                if (arrStart < 0) break;
                int arrEnd = innerObj.IndexOf(']', arrStart);
                if (arrEnd < 0) break;
                
                string arrStr = innerObj.Substring(arrStart + 1, arrEnd - arrStart - 1);
                var list = new List<int>();
                foreach (var token in arrStr.Split(','))
                {
                    if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                }
                
                result[clientId] = list;
                searchIdx = arrEnd + 1;
            }
            return result;
        }

        private Dictionary<string, List<int[]>> ParseTenpaiExamples(string jsonString)
        {
            var result = new Dictionary<string, List<int[]>>();
            int searchFrom = 0;
            while (true)
            {
                int cidStart = jsonString.IndexOf("\"client_id\"", searchFrom);
                if (cidStart < 0) break;

                int nextCidStart = jsonString.IndexOf("\"client_id\"", cidStart + 11);
                if (nextCidStart < 0) nextCidStart = jsonString.Length;

                int valStart = jsonString.IndexOf('"', cidStart + 11) + 1;
                int valEnd = jsonString.IndexOf('"', valStart);
                if (valStart < 0 || valEnd < 0) break;
                string cid = jsonString.Substring(valStart, valEnd - valStart);

                int keyStart = jsonString.IndexOf("\"tenpai_examples\"", valEnd);
                if (keyStart > 0 && keyStart < nextCidStart) 
                {
                    int arrStart = jsonString.IndexOf('[', keyStart);
                    if (arrStart > 0)
                    {
                        int outerArrEnd = FindMatchingBracket(jsonString, arrStart);
                        if (outerArrEnd > arrStart)
                        {
                            string inner = jsonString.Substring(arrStart + 1, outerArrEnd - arrStart - 1);
                            var examplesList = new List<int[]>();
                            
                            int innerArrStartCheck = inner.IndexOf('[');
                            if (innerArrStartCheck < 0)
                            {
                                // Pythonサーバーからの実データ (フラットな配列) に対応: [0, 1, 2, ...]
                                var list = new List<int>();
                                foreach (var token in inner.Split(','))
                                {
                                    if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                                }
                                if (list.Count > 0)
                                {
                                    examplesList.Add(list.ToArray());
                                }
                            }
                            else
                            {
                                // ネストされた配列 (モッククライアント等) に対応: [[0, 1, ...], [2, 3, ...]]
                                int innerSearchFrom = 0;
                                while (true)
                                {
                                    int innerArrStart = inner.IndexOf('[', innerSearchFrom);
                                    if (innerArrStart < 0) break;
                                    int innerArrEnd = inner.IndexOf(']', innerArrStart);
                                    if (innerArrEnd < 0) break;
                                    
                                    string arrayStr = inner.Substring(innerArrStart + 1, innerArrEnd - innerArrStart - 1);
                                    var list = new List<int>();
                                    foreach (var token in arrayStr.Split(','))
                                    {
                                        if (int.TryParse(token.Trim(), out int val)) list.Add(val);
                                    }
                                    examplesList.Add(list.ToArray());
                                    innerSearchFrom = innerArrEnd + 1;
                                }
                            }
                            result[cid] = examplesList;
                        }
                    }
                }
                searchFrom = valEnd + 1;
            }
            return result;
        }

        private int FindMatchingBracket(string s, int startIndex, char openBracket = '[', char closeBracket = ']')
        {
            int depth = 0;
            for (int i = startIndex; i < s.Length; i++)
            {
                if (s[i] == openBracket) depth++;
                else if (s[i] == closeBracket)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
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
