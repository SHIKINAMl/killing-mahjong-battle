using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket; // 導入したWebGL対応WebSocketライブラリ

/// <summary>
/// Unity 用の最小 WebSocket クライアントサンプル（NativeWebSocket版）
///
/// 目的:
/// - 接続
/// - 送信
/// - 受信
/// - 切断
/// の基本フローだけを確認するためのコンポーネントです。
/// </summary>
public class WebSocketGameClientSample : MonoBehaviour
{
    // 接続先 URL（例: ws://127.0.0.1:8765）
    [Header("Connection")]
    [SerializeField] private string serverUrl = "ws://localhost:8765";
    [SerializeField] private bool autoConnectOnStart = false; // 手動でGameUIManagerなどから接続するように変更
    [SerializeField] private int autoReconnectDelayMs = 3000;

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

    // NativeWebSocket の WebSocket クライアント
    private WebSocket webSocket;

    /// <summary>
    /// 接続中かどうか。
    /// </summary>
    public bool IsConnected => webSocket != null && webSocket.State == WebSocketState.Open;

    private bool isConnecting = false;

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
    /// メインスレッド側で受信キューを処理する。
    /// </summary>
    private void Update()
    {
        if (webSocket != null)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            // WebGL以外の環境では、メインスレッドでキューを処理するためにこれを呼ぶ必要があります
            webSocket.DispatchMessageQueue();
#endif
        }
    }

    /// <summary>
    /// サーバーへ接続して、受信ループを開始する。
    /// </summary>
    public async Task ConnectAsync()
    {
        if (IsConnected || isConnecting)
        {
            return;
        }

        isConnecting = true;

        // 古い接続があれば確実にキャンセル・解放する
        if (webSocket != null)
        {
            await webSocket.Close();
            webSocket = null;
        }

        string normalizedUrl = NormalizeServerUrl(serverUrl);
        
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL環境（ブラウザ）では、JavaScriptの標準WebSocket APIの仕様上、
        // 接続時のカスタムヘッダー（Authorization等）の付与が禁止されています。
        // クエリパラメータを付与するとサーバー側で404になるため、ヘッダー・パラメータなしで接続します。
        webSocket = new WebSocket(normalizedUrl);
#else
        // ヘッダー（認証トークン）の設定 (PC/エディタ環境用)
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(authToken))
        {
            headers.Add("Authorization", $"Bearer {authToken}");
            headers.Add("X-Token", authToken);
        }

        // NativeWebSocket のインスタンス化
        webSocket = new WebSocket(normalizedUrl, headers);
#endif

        // --- イベントの登録 ---
        webSocket.OnOpen += () =>
        {
            Log($"Connected: {normalizedUrl}");
        };

        webSocket.OnError += (e) =>
        {
            Debug.LogError($"WebSocket Error: {e}");
        };

        webSocket.OnClose += (e) =>
        {
            Log("WebSocket Closed!");
        };

        webSocket.OnMessage += (bytes) =>
        {
            // 受信したバイナリデータを文字列に変換して処理
            var message = Encoding.UTF8.GetString(bytes);
            HandleServerMessage(message);
        };

        try
        {
            KillingMahjong.UI.LoadingManager.Instance.Show();
            
            // WebSocket 接続開始
            await webSocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError($"WebSocket connect failed: {ex.Message}");
        }
        finally
        {
            isConnecting = false;
            KillingMahjong.UI.LoadingManager.Instance.Hide();
        }
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
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"WebSocket close warning: {ex.Message}");
        }
        finally
        {
            webSocket = null;
        }
    }

    /// <summary>
    /// アプリ終了/エディタ再生停止時に確実に切断する。
    /// </summary>
    private async void OnApplicationQuit()
    {
        await DisconnectAsync();
    }
    
    private async void OnDestroy()
    {
        await DisconnectAsync();
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
    /// 簡単なJSONパースを行い、GameUIManagerに流し込みます。
    /// </summary>
    private async void HandleServerMessage(string raw)
    {
        Log($"Recv: {raw}");

        try
        {
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
