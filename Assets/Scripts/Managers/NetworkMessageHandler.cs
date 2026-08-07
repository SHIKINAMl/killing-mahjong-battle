using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;
using KillingMahjong.Network.Handlers;

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

        /// <summary>
        /// **コストを払ったあとの、発動した側の血。サーバーが正。**
        /// 2026-08-04 にサーバーが返すようになった（それまでは無かったので
        /// クライアントが「今の血 − cost」で自前計算していた）。
        /// 0 のときは「入っていない」とみなし、従来どおりの引き算に落とす。
        /// </summary>
        public int health;

        public string yaku_name;
        public List<int> exposedHandIndexes;
        // manually populated
        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>> exposedHandIndexesByPlayer;
        public MulliganResultData mulliganResult;
    }

    [System.Serializable]
    public class MulliganResultData
    {
        public int targetHandIndex;
        public int oldTile;
        public int newTile;
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
        
        [SerializeField] private bool useDebugClient;
        public bool UseDebugClient => useDebugClient;

        [SerializeField] private KillingMahjong.Network.DebugWebSocketClient debugWebSocketClient;

        // イベントルーティング
        public event Action OnMatchmakingWaiting;
        public event Action<string> OnMatchCancelled;
        public event Action OnGameStarted;
        public event Action<int, int, int, int> OnBettingComplete;
        public event Action<RoundStatus> OnPhaseStatusChanged;
        public event Action<int, bool> OnTileDiscarded; // tileId, isLocalPlayer
        public event Action<bool> OnAgari; // isLocalWin
        public event Action<DrawPlayerData[]> OnDraw; // 流局
        public event Action<int, int> OnGameEnded; // localScore, enemyScore
        public event Action<NextRoundWaitingData> OnNextRoundWaitingReceived; // 相手からの次局待機（ロンボタン押下の合図として利用）
        public event Action<PhaseCompletedNoticeData> OnPhaseCompletedNotice; // 手牌選択・ベットを確定したプレイヤーの通知（「準備完了」表示用）
        public event Action OnLocalBetAccepted; // 自分のベットが受理された（本人にだけ届く。「準備完了」表示用）

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

        // ---- ハンドラ分割用 内部API (IServerMessageHandler 実装クラスから使用) ----
        // C# の event は宣言クラスの外から Invoke できないため、
        // 分割したハンドラクラスはこれらの Raise 系メソッドを通してイベントを発火する。

        /// <summary>ロン二重発火防止フラグ (round_end / discard_accepted 間で共有)</summary>
        internal bool AgariProcessed { get { return agariProcessed; } set { agariProcessed = value; } }

        internal void RaiseMatchmakingWaiting() => OnMatchmakingWaiting?.Invoke();
        internal void RaiseMatchCancelled(string reason) => OnMatchCancelled?.Invoke(reason);
        internal void RaiseGameStarted() => OnGameStarted?.Invoke();
        internal void RaiseBettingComplete(int playerBet, int enemyBet, int playerHp, int enemyHp) => OnBettingComplete?.Invoke(playerBet, enemyBet, playerHp, enemyHp);
        internal void RaisePhaseStatusChanged(RoundStatus status) => OnPhaseStatusChanged?.Invoke(status);
        internal void RaiseTileDiscarded(int tileId, bool isLocalPlayer) => OnTileDiscarded?.Invoke(tileId, isLocalPlayer);
        internal void RaiseAgari(bool isLocalWin) => OnAgari?.Invoke(isLocalWin);
        internal void RaiseDraw(DrawPlayerData[] drawData) => OnDraw?.Invoke(drawData);
        internal void RaiseGameEnded(int localScore, int enemyScore) => OnGameEnded?.Invoke(localScore, enemyScore);
        internal void RaiseNextRoundWaitingReceived(NextRoundWaitingData data) => OnNextRoundWaitingReceived?.Invoke(data);
        internal void RaisePhaseCompletedNotice(PhaseCompletedNoticeData data) => OnPhaseCompletedNotice?.Invoke(data);
        internal void RaiseLocalBetAccepted() => OnLocalBetAccepted?.Invoke();
        internal void RaiseStatusReceived(StatusData data) => OnStatusReceived?.Invoke(data);
        internal void RaiseAgariPendingReceived(AgariPendingData data) => OnAgariPendingReceived?.Invoke(data);
        internal void RaiseError(string message) => OnError?.Invoke(message);
        internal void RaiseHandSelectionConfirmation(HandSelectionConfirmationData data) => OnHandSelectionConfirmation?.Invoke(data);
        internal void RaiseIsTenpaiReceived(IsTenpaiData data) => OnIsTenpaiReceived?.Invoke(data);
        internal void RaiseNotTenpaiReceived(string reason) => OnNotTenpaiReceived?.Invoke(reason);
        internal void RaiseHandSelectionAccepted() => OnHandSelectionAccepted?.Invoke();
        internal void RaiseDealingStarted() => OnDealingStarted?.Invoke();
        internal void RaiseDealingCompleted() => OnDealingCompleted?.Invoke();
        internal void RaiseSkillCasted(SkillCastedData data) => OnSkillCasted?.Invoke(data);
        internal void RaiseSpecialVictoryWon(string playerId) => OnSpecialVictoryWon?.Invoke(playerId);
        internal void RaiseOpeningBoostAssigned() => OnOpeningBoostAssigned?.Invoke();


        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
                RegisterMessageHandlers();
            } else {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // シーン再読込時に破棄済みオブジェクトを指したままにしない
            if (Instance == this) Instance = null;
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

                if (messageHandlers.TryGetValue(baseMsg.type, out IServerMessageHandler handler))
                {
                    handler.Handle(baseMsg.type, jsonString, this);
                }
                // 登録されていない type は従来の switch と同様に無視する
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse JSON: {e.Message}\nStackTrace:\n{e.StackTrace}\n{jsonString}");
            }
        }

        // ---- サーバーメッセージのディスパッチ ----

        private readonly Dictionary<string, IServerMessageHandler> messageHandlers =
            new Dictionary<string, IServerMessageHandler>();

        /// <summary>
        /// メッセージ種別ごとのハンドラを登録する。
        /// 新しいメッセージ type を追加する場合は、IServerMessageHandler 実装を
        /// Network/Handlers に作成してここに追加すること。
        /// </summary>
        private void RegisterMessageHandlers()
        {
            var handlers = new IServerMessageHandler[]
            {
                new StatusMessageHandler(),
                new MatchLifecycleMessageHandler(),
                new PhaseMessageHandler(),
                new DealingMessageHandler(),
                new HandSelectionMessageHandler(),
                new BettingMessageHandler(),
                new DiscardMessageHandler(),
                new RoundLifecycleMessageHandler(),
                new PhaseCompletedNoticeMessageHandler(),
                new SkillMessageHandler(),
                new ErrorMessageHandler(),
            };

            messageHandlers.Clear();
            foreach (var handler in handlers)
            {
                foreach (var type in handler.MessageTypes)
                {
                    messageHandlers[type] = handler;
                }
            }
        }
    }
}
