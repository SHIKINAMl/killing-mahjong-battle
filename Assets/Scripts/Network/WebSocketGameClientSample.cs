using System;
using System.Text;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

/// <summary>
/// Unity 用の最小 WebSocket クライアントサンプル。
///
/// 目的:
/// - 接続
/// - 送信
/// - 受信
/// - 切断
/// の基本フローだけを確認するためのコンポーネントです。
///
/// 通信ライブラリには NativeWebSocket（com.endel.nativewebsocket）を使用します。
/// これによりエディタ / Windows / WebGL のすべてで同一 API で動作します。
/// （標準の System.Net.WebSockets.ClientWebSocket は WebGL では動作しません）
///
/// 使い方:
/// 1) GameObject にアタッチ
/// 2) Inspector の serverUrl を設定
/// 3) autoConnectOnStart を ON にするか、ContextMenu の Connect を実行
/// 4) sampleMessage を設定して ContextMenu の Send Sample を実行
/// </summary>
public class WebSocketGameClientSample : MonoBehaviour
{
    // 接続先 URL（例: wss://jongpire.onrender.com/ws）
    [Header("Connection")]
    [SerializeField] private string serverUrl = "wss://jongpire.onrender.com/ws";
    private string myClientId = "";

    // true の場合、Start 時に自動接続
    [SerializeField] private bool autoConnectOnStart = true;

    // ContextMenu の「Send Sample」で送るテキスト
    [Header("Send")]
    [SerializeField] private string sampleMessage = "{\"type\":\"ping\"}";

    [Header("Auth")]
    [SerializeField, Tooltip("認証トークンを直接入力します。接続 URL に ?token=... として付与されます（全プラットフォーム対応）")]
    private string authToken = "";

    // true の場合、送受信ログを Console に表示
    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    // NativeWebSocket のクライアント（全プラットフォーム共通）
    private WebSocket webSocket;

    /// <summary>
    /// 生きている接続。**シーンをまたいで1つだけ**。
    ///
    /// 合言葉で部屋に入るときは、対局シーンへ移る前に接続して結果を見る必要がある。
    /// サーバーは合言葉が当たった瞬間にその接続で対局を成立させ、部屋を消してしまうので、
    /// 「タイトルで確かめて、繋ぎ直して本番」ができない
    /// （`websocket_server.py` の `_join_private_room`）。
    /// そのため接続そのものを持ち越す。
    /// </summary>
    public static WebSocketGameClientSample Instance { get; private set; }

    /// <summary>join を二度送らないための印。</summary>
    private bool hasSentJoin;

    /// <summary>接続を張っている最中。ConnectAsync の再入を弾くのに使う。</summary>
    private bool isConnecting;

    /// <summary>
    /// UI がまだ居ないうちに届いたメッセージ。
    /// タイトルで `private_join` が通ると、対局シーンを読み込む前に `game_started` が届く。
    /// 捨てると対局が始まらないので、貯めておいて UI が現れたら流す。
    /// </summary>
    private readonly System.Collections.Generic.List<string> pendingMessages =
        new System.Collections.Generic.List<string>();

    /// <summary>サーバーからの error。タイトルが合言葉の失敗を知るために使う。</summary>
    public event Action<string> OnServerError;

    /// <summary>対局が成立した合図（game_started）。</summary>
    public event Action OnMatchStarted;

    /// <summary>
    /// 接続中かどうか。
    /// </summary>
    public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

    private void Awake()
    {
        // タイトルで作った接続が既に居るなら、対局シーン側の実体は要らない。
        // 破棄しないと二重に繋いで join を二度送ってしまう
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// **対局のあるシーン以外へ移ったら接続を畳む。**
    ///
    /// 接続がシーンをまたいで生き残るようになったので、対局を抜けてタイトルへ戻っても
    /// 繋がったままになる。サーバー側では対局中の扱いが残るため、次に対戦を始めようとすると
    /// `Already in a match` で弾かれる。
    ///
    /// 判定は「新しいシーンに GameUIManager が居るか」。sceneLoaded は
    /// そのシーンの Awake が全部済んでから呼ばれるので、ここで見れば確実。
    /// タイトルで合言葉を確認している最中はシーンを読み込まないので、巻き添えにはならない。
    /// </summary>
    private async void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                     UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (Instance != this) return;
        if (FindFirstObjectByType<KillingMahjong.UI.GameUIManager>() != null) return;

        Log($"[WebSocket] 対局シーンを離れたので接続を閉じます (scene={scene.name})");
        await ResetConnectionAsync();
    }

    /// <summary>
    /// 起動時。必要なら自動接続する。
    /// </summary>
    private async void Start()
    {
        if (Instance != this)
        {
            return;
        }

        if (!autoConnectOnStart || IsConnected)
        {
            return;
        }

        await ConnectAsync();
    }

    /// <summary>
    /// メインスレッド側で受信メッセージのディスパッチを行う。
    ///
    /// 注意: DispatchMessageQueue() は NativeWebSocket の「非WebGL版」にしか
    /// 存在しないメソッドです（WebGL ではブラウザが受信コールバックを自動で呼ぶため不要）。
    /// そのため、この 1 行だけは #if で分岐しないと WebGL ビルドがコンパイルできません。
    /// これは通信を「WebGLで除外する」のではなく、WebGLで正しく動かすための公式パターンです。
    /// </summary>
    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        webSocket?.DispatchMessageQueue();
#endif
        FlushPendingIfUIReady();
    }

    /// <summary>
    /// 破棄時に安全に切断する。
    /// </summary>
    private async void OnDestroy()
    {
        if (Instance != this)
        {
            // Awake で弾かれた重複。生きている接続を巻き添えに切らないこと
            return;
        }

        Instance = null;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        await DisconnectAsync();
    }

    /// <summary>
    /// 接続を捨てて、次の join をやり直せる状態に戻す。
    /// 合言葉を間違えてタイトルに留まったときに使う。
    /// </summary>
    public async Task ResetConnectionAsync()
    {
        hasSentJoin = false;
        isConnecting = false;
        pendingMessages.Clear();
        await DisconnectAsync();
    }

    /// <summary>
    /// アプリ終了時にも安全に切断する。
    /// </summary>
    private async void OnApplicationQuit()
    {
        await DisconnectAsync();
    }

    /// <summary>
    /// サーバーへ接続して、受信コールバックを開始する。
    /// </summary>
    public async Task ConnectAsync()
    {
        if (IsConnected)
        {
            return;
        }

        // **接続中の再入を弾く。**
        // タイトルから合言葉で入るときは、こちらから ConnectAsync を呼んだ直後に
        // Prefab の Start（autoConnectOnStart）が走る。IsConnected はまだ false なので、
        // この番人が無いと張りかけの接続を DisconnectAsync で切って張り直してしまい、
        // 最初の connected と join が宙に浮く
        if (isConnecting)
        {
            return;
        }

        isConnecting = true;

        // 既存インスタンスが残っていれば破棄
        if (webSocket != null)
        {
            await DisconnectAsync();
        }

        string normalizedUrl = NormalizeServerUrl(serverUrl);

        // 認証トークンはクエリパラメータ ?token= で渡す。
        // ブラウザ(WebGL)は WebSocket にヘッダーを付けられないが、URL のクエリなら
        // 全プラットフォーム（エディタ / Windows / WebGL）で送れる。
        // 本番サーバーは ?token= でのトークン受付に対応済み。
        string connectUrl = normalizedUrl;
        if (!string.IsNullOrEmpty(authToken))
        {
            string separator = normalizedUrl.Contains("?") ? "&" : "?";
            connectUrl = $"{normalizedUrl}{separator}token={Uri.EscapeDataString(authToken)}";
        }

        webSocket = new WebSocket(connectUrl);

        webSocket.OnOpen += () =>
        {
            Log($"Connected: {normalizedUrl}");
        };

        webSocket.OnError += (errMsg) =>
        {
            Debug.LogError($"WebSocket error: {errMsg}");
        };

        webSocket.OnClose += (closeCode) =>
        {
            Log($"Closed: {closeCode}");
        };

        webSocket.OnMessage += (bytes) =>
        {
            // NativeWebSocket はメインスレッドでコールバックするため直接処理して問題ない
            var message = Encoding.UTF8.GetString(bytes);
            HandleServerMessage(message);
        };

        try
        {
            // Connect() は接続がクローズされるまで完了しない（ネイティブ）ので await しっぱなしにする
            await webSocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket connect failed: {ex.Message}");
        }
        finally
        {
            // Connect() は閉じるまで戻らないので、ここに来た時点で接続は終わっている
            isConnecting = false;
        }
    }

    /// <summary>
    /// 接続を閉じてリソースを解放する。
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (webSocket == null)
        {
            return;
        }

        var socket = webSocket;
        webSocket = null;

        try
        {
            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting)
            {
                await socket.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"WebSocket close warning: {ex.Message}");
        }
    }

    /// <summary>
    /// 任意文字列を送信する。
    /// </summary>
    /// <param name="message">送信するテキスト（通常は JSON 文字列）</param>
    public async Task SendAsync(string message)
    {
        await SendTextAsync(message);
    }

    /// <summary>
    /// テキストメッセージを送信する。
    /// </summary>
    private async Task SendTextAsync(string message)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("WebSocket is not connected.");
            return;
        }

        try
        {
            await webSocket.SendText(message);
            Log($"Send: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket send failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 受信メッセージの処理。
    /// </summary>
    private async void HandleServerMessage(string raw)
    {
        Log($"Recv: {raw}");

        try
        {
            var baseMsg = UnityEngine.JsonUtility.FromJson<KillingMahjong.EngineData.ServerMessageBase>(raw);
            if (baseMsg == null || string.IsNullOrEmpty(baseMsg.type))
            {
                return;
            }

            string msgType = baseMsg.type;

            if (msgType == "connected")
            {
                var connectedMsg = UnityEngine.JsonUtility.FromJson<KillingMahjong.EngineData.ConnectedMessage>(raw);
                if (connectedMsg != null && connectedMsg.data != null)
                {
                    myClientId = connectedMsg.data.client_id ?? "";
                }
                Log($"[WebSocket] Connection confirmed! Client ID: {myClientId}");

                // 野良か、部屋を作るか、合言葉で入るか。タイトル側が MatchJoinRequest に入れておく。
                // 繋ぎ直し（match_cancelled のあとなど）で二度送らないよう印を立てる
                if (!hasSentJoin)
                {
                    hasSentJoin = true;
                    string joinJson = KillingMahjong.Network.MatchJoinRequest.BuildJoinJson();
                    Log($"[WebSocket] join mode={KillingMahjong.Network.MatchJoinRequest.Mode}");
                    await SendAsync(joinJson);
                }
                return;
            }

            if (msgType == "error")
            {
                // 合言葉が無い部屋、既に対局中、など。タイトルが受けて画面に留まる
                var errMsg = UnityEngine.JsonUtility.FromJson<KillingMahjong.EngineData.ErrorMessage>(raw);
                string text = errMsg != null && !string.IsNullOrEmpty(errMsg.message) ? errMsg.message : "サーバーエラー";
                Debug.LogWarning($"[WebSocket] server error: {text}");

                // 弾かれたので join はまだ成立していない。次の試行で送り直せるようにする
                hasSentJoin = false;
                OnServerError?.Invoke(text);
            }

            if (msgType == "game_started")
            {
                OnMatchStarted?.Invoke();
            }

            DeliverToUI(raw);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebSocket] Failed to parse message: {ex.Message}");
        }
    }

    /// <summary>
    /// 受信メッセージを UI へ渡す。
    ///
    /// **UI がまだ居ないときは捨てずに貯める。** タイトルで合言葉が通ると、
    /// 対局シーンを読み込むより先に `game_started` や `phase_change` が届く。
    /// 捨てると盤面が初期化されないまま対局だけが進む。
    /// </summary>
    private void DeliverToUI(string raw)
    {
        var gameUIManager = FindFirstObjectByType<KillingMahjong.UI.GameUIManager>();
        if (gameUIManager == null)
        {
            pendingMessages.Add(raw);
            return;
        }

        if (pendingMessages.Count > 0)
        {
            // 貯めた順に流してから今の1件を渡す。順番が入れ替わるとフェーズが飛ぶ
            var queued = pendingMessages.ToArray();
            pendingMessages.Clear();
            Log($"[WebSocket] UI が現れたので保留 {queued.Length} 件を流します");
            foreach (var q in queued)
            {
                gameUIManager.ApplyGameStateFromJSON(q, myClientId);
            }
        }

        gameUIManager.ApplyGameStateFromJSON(raw, myClientId);
    }

    /// <summary>
    /// 貯めたメッセージを、UI が現れたタイミングで流す。
    /// シーン読み込みの直後は GameUIManager がまだ Awake していないことがあるため、
    /// 受信が無い間も毎フレーム見にいく。
    /// </summary>
    private void FlushPendingIfUIReady()
    {
        if (pendingMessages.Count == 0) return;

        var gameUIManager = FindFirstObjectByType<KillingMahjong.UI.GameUIManager>();
        if (gameUIManager == null) return;

        var queued = pendingMessages.ToArray();
        pendingMessages.Clear();
        Log($"[WebSocket] UI が現れたので保留 {queued.Length} 件を流します");
        foreach (var q in queued)
        {
            gameUIManager.ApplyGameStateFromJSON(q, myClientId);
        }
    }

    private string NormalizeServerUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return "wss://jongpire.onrender.com/ws";
        }

        string url = rawUrl.Trim();

        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "wss://" + url.Substring("https://".Length);
        }
        else if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            url = "ws://" + url.Substring("http://".Length);
        }

        if (url.EndsWith("/"))
        {
            url = url.TrimEnd('/');
        }
        if (!url.EndsWith("/ws", StringComparison.OrdinalIgnoreCase))
        {
            url += "/ws";
        }

        return url;
    }

    /// <summary>
    /// ログ出力ヘルパー。
    /// </summary>
    private void Log(string message)
    {
        if (verboseLog)
        {
            Debug.Log($"[WebSocketGameClientSample] {message}");
        }
    }

    /// <summary>
    /// Inspector のコンテキストメニューから接続する。
    /// </summary>
    [ContextMenu("Connect")]
    private async void ConnectFromContextMenu()
    {
        await ConnectAsync();
    }

    /// <summary>
    /// Inspector のコンテキストメニューから sampleMessage を送信する。
    /// </summary>
    [ContextMenu("Send Sample")]
    private async void SendSampleFromContextMenu()
    {
        await SendAsync(sampleMessage);
    }

    /// <summary>
    /// Inspector のコンテキストメニューから切断する。
    /// </summary>
    [ContextMenu("Disconnect")]
    private async void DisconnectFromContextMenu()
    {
        await DisconnectAsync();
    }
}
