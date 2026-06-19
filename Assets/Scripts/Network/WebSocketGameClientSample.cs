using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
/// 使い方:
/// 1) GameObject にアタッチ
/// 2) Inspector の serverUrl を設定
/// 3) autoConnectOnStart を ON にするか、ContextMenu の Connect を実行
/// 4) sampleMessage を設定して ContextMenu の Send Sample を実行
/// </summary>
public class WebSocketGameClientSample : MonoBehaviour
{
    // 接続先 URL（例: ws://127.0.0.1:8765）
    [Header("Connection")]
    [SerializeField] private string serverUrl = "ws://127.0.0.1:8765";
    // true の場合、Start 時に自動接続
    [SerializeField] private bool autoConnectOnStart = true;

    // ContextMenu の「Send Sample」で送るテキスト
    [Header("Send")]
    [SerializeField] private string sampleMessage = "{\"type\":\"ping\"}";

    [Header("Auth")]
    [SerializeField, Tooltip("トークンを直接入力します")]
    private string authToken = "";

    // true の場合、送受信ログを Console に表示
    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    [Header("Game Reference")]
    [SerializeField] private KillingMahjong.UI.GameUIManager gameUIManager;
    private string myClientId = "";

    // .NET 標準の WebSocket クライアント
    private ClientWebSocket webSocket;
    // 非同期処理停止用トークン
    private CancellationTokenSource cancellationTokenSource;
    // 受信スレッド -> Unity メインスレッドへの受け渡しキュー
    private readonly ConcurrentQueue<string> receiveQueue = new ConcurrentQueue<string>();

    /// <summary>
    /// 接続中かどうか。
    /// </summary>
    public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

    /// <summary>
    /// 起動時。必要なら自動接続する。
    /// </summary>
    private async void Start()
    {
        if (gameUIManager == null)
        {
            gameUIManager = FindFirstObjectByType<KillingMahjong.UI.GameUIManager>();
        }

        if (!autoConnectOnStart)
        {
            return;
        }

        await ConnectAsync();
    }

    /// <summary>
    /// 破棄時に安全に切断する。
    /// </summary>
    private async void OnDestroy()
    {
        await DisconnectAsync();
    }

    /// <summary>
    /// メインスレッド側で受信キューを処理する。
    /// </summary>
    private void Update()
    {
        while (receiveQueue.TryDequeue(out var message))
        {
            HandleServerMessage(message);
        }
    }

    /// <summary>
    /// サーバーへ接続して、受信ループを開始する。
    /// </summary>
    public async Task ConnectAsync()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogError("WebSocketGameClientSample: WebGL では ClientWebSocket は使用できません。");
        return;
#else
        if (IsConnected)
        {
            return;
        }

        cancellationTokenSource = new CancellationTokenSource();
        webSocket = new ClientWebSocket();

        if (!string.IsNullOrEmpty(authToken))
        {
            webSocket.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");
            webSocket.Options.SetRequestHeader("X-Token", authToken);
        }

        try
        {
            string normalizedUrl = NormalizeServerUrl(serverUrl);
            // WebSocket 接続
            await webSocket.ConnectAsync(new Uri(normalizedUrl), cancellationTokenSource.Token);
            Log($"Connected: {normalizedUrl}");

            // 受信待ちループをバックグラウンド開始
            _ = ReceiveLoopAsync(cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket connect failed: {ex.Message}");
        }
#endif
    }

    private string NormalizeServerUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;

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
    /// 接続を閉じてリソースを解放する。
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (webSocket == null)
        {
            return;
        }

        try
        {
            cancellationTokenSource?.Cancel();
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
            {
                // タイムアウト付きでクローズを送信（サーバーがブロック中でもハングしない）
                using (var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", closeCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.LogWarning("WebSocket close timed out (server may be busy). Forcing disconnect.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"WebSocket close warning: {ex.Message}");
        }
        finally
        {
            webSocket.Dispose();
            webSocket = null;
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }

    /// <summary>
    /// アプリ終了/エディタ再生停止時に確実に切断する。
    /// </summary>
    private void OnApplicationQuit()
    {
        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            try
            {
                // 同期的にAbortして即座に接続を切る（OnDestroyのasyncが間に合わない場合の保険）
                webSocket.Abort();
                Debug.Log("[WebSocket] Connection aborted on application quit.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WebSocket abort warning: {ex.Message}");
            }
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
    /// テキストメッセージを 1 フレームとして送信する。
    /// </summary>
    private async Task SendTextAsync(string message)
    {
        if (!IsConnected)
        {
            Debug.LogWarning("WebSocket is not connected.");
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(bytes);

        try
        {
            await webSocket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationTokenSource.Token);
            Log($"Send: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket send failed: {ex.Message}");
        }
    }

    /// <summary>
    /// サーバーからの受信を継続監視するループ。
    /// 受信結果はスレッドセーフキューへ積み、Update で処理する。
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        var buffer = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested && webSocket != null && webSocket.State == WebSocketState.Open)
            {
                var builder = new StringBuilder();
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    // サーバーからクローズ要求が来た場合
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("Server requested close.");
                        await DisconnectAsync();
                        return;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                receiveQueue.Enqueue(builder.ToString());
            }
        }
        catch (OperationCanceledException)
        {
            // 切断時の想定内キャンセル
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket receive failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 受信メッセージの処理。
    /// 簡単なJSONパースを行い、GameUIManagerに流し込みます。
    /// </summary>
    private async void HandleServerMessage(string raw)
    {
        Log($"Recv: {raw}");

        try
        {
            // JsonUtility parses the exact matching types. So we write a quick struct to grab the type.
            ServerMessage msg = JsonUtility.FromJson<ServerMessage>(raw);

            if (msg == null || string.IsNullOrEmpty(msg.type)) return;

            if (msg.type == "connected")
            {
                myClientId = msg.data.client_id;
                Log($"[WebSocket] Connection confirmed! Client ID: {myClientId}");
                // "join" を送信してマッチングキューに入る
                await SendAsync("{\"type\":\"join\"}");
            }

            // Route all messages to GameUIManager for phase updates and UI logic
            if (gameUIManager != null)
            {
                gameUIManager.ApplyGameStateFromJSON(raw, myClientId);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebSocket] Failed to parse message: {ex.Message}");
        }
    }

    [Serializable]
    private class ServerMessage
    {
        public string type;
        public string message;
        public ServerMessageData data;
    }

    [Serializable]
    private class ServerMessageData
    {
        public string client_id;
        // The game state JSON mapping fields 
        public string status;
        public int round;
        public int honba;
        public string current_player;
        public System.Collections.Generic.List<KillingMahjong.EngineData.PlayerStateData> players;
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

