using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
    [SerializeField] private string tokenEnvFileName = ".env";

    // true の場合、送受信ログを Console に表示
    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

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

        string token = LoadTokenFromEnv();
        if (!string.IsNullOrEmpty(token))
        {
            webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            webSocket.Options.SetRequestHeader("X-Token", token);
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

    private string LoadTokenFromEnv()
    {
        try
        {
            string[] candidates =
            {
                Path.Combine(Directory.GetCurrentDirectory(), tokenEnvFileName),
                Path.Combine(Application.dataPath, "..", tokenEnvFileName),
            };

            foreach (string path in candidates)
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                foreach (string line in File.ReadAllLines(fullPath, Encoding.UTF8))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith("TOKEN="))
                    {
                        string value = trimmed.Substring("TOKEN=".Length).Trim().Trim('"').Trim('\'');
                        if (!string.IsNullOrEmpty(value))
                        {
                            return value;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocket] Failed to load TOKEN from .env: {ex.Message}");
        }

        string envToken = Environment.GetEnvironmentVariable("TOKEN");
        return envToken ?? string.Empty;
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
                // 正常クローズを送信
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
            }
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
    /// 最小サンプルのため、内容解析はせずログ出力のみ行う。
    /// </summary>
    private async void HandleServerMessage(string raw)
    {
        Log($"Recv: {raw}");

        try
        {
            using JsonDocument doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("type", out JsonElement typeElem))
            {
                return;
            }

            string msgType = typeElem.GetString();
            if (string.IsNullOrEmpty(msgType))
            {
                return;
            }

            if (msgType == "connected")
            {
                if (doc.RootElement.TryGetProperty("data", out JsonElement dataElem)
                    && dataElem.TryGetProperty("client_id", out JsonElement clientElem))
                {
                    myClientId = clientElem.GetString() ?? "";
                }
                Log($"[WebSocket] Connection confirmed! Client ID: {myClientId}");
                await SendAsync("{\"type\":\"join\"}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebSocket] Failed to parse message: {ex.Message}");
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

