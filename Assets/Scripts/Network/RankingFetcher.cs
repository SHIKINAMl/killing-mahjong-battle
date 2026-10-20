using System;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using UnityEngine;

namespace KillingMahjong.Network
{
    [Serializable] public class RankingEntry { public int rank; public string name; public long value; }

    /// <summary>自分の順位。JsonUtility は null を rank = 0 にするので、rank が 0 なら「順位なし」。</summary>
    [Serializable] public class RankingMe { public int rank; public long value; }

    [Serializable] public class RankingData { public string kind; public RankingEntry[] entries; public RankingMe me; }
    [Serializable] public class RankingMessage { public string type; public RankingData data; }

    /// <summary>
    /// タイトルのランキング（2026-10-21）を、サーバーから取ってくる。
    ///
    /// **対局の接続（<see cref="WebSocketGameClientSample"/>）は使わない。** あちらは繋ぐと
    /// すぐ <c>join</c> を送って対戦相手を探しに行くので、ランキングを見るだけの用には使えない。
    /// ここでは別に1本だけ繋ぎ、<c>ranking</c> を種類ごとに送って、全部返ってきたら切る。
    /// 名乗らない（<c>register</c> しない）ので、<c>me</c> はいつも空。
    ///
    /// 繋ぐ先とトークンは、対局の接続の Prefab（Resources/Network/GameClient）の設定を読む。
    /// サーバーは使わない間は眠っていて、起きるまで数十秒かかることがあるので、待ち時間は長めにとる。
    /// 決まりはサーバーの <c>mahjong_engine/records/README.md</c> の「ランキングを出す」。
    /// </summary>
    public sealed class RankingFetcher : MonoBehaviour
    {
        public const string ClientPrefabPath = "Network/GameClient";
        private const float TimeoutSeconds = 60f;

        /// <summary>最後に取れた結果（種類 → 中身）。表示を待たせないため、次に開いたときはまずこれを出す。</summary>
        public static readonly Dictionary<string, RankingData> Latest = new Dictionary<string, RankingData>();

        /// <summary>最後に取れた時刻（realtimeSinceStartup）。まだなら負。</summary>
        public static float LatestAt { get; private set; } = -1f;

        public bool IsBusy => socket != null;

        private WebSocket socket;
        private string[] pendingKinds;
        private int limit;
        private float startedAt;
        private Action<bool> onDone;
        private readonly HashSet<string> received = new HashSet<string>();

        /// <summary>
        /// 取りに行く。終わったら <paramref name="done"/>（取れたら true）。結果は <see cref="Latest"/> に入る。
        /// 取りに行っている最中なら何もしない。
        /// </summary>
        public async void Fetch(string[] kinds, int count, Action<bool> done)
        {
            if (socket != null) return;
            string url = ResolveUrl();
            if (string.IsNullOrEmpty(url)) { done?.Invoke(false); return; }

            pendingKinds = kinds;
            limit = Mathf.Clamp(count, 1, 50);
            onDone = done;
            received.Clear();
            startedAt = Time.realtimeSinceStartup;

            var ws = new WebSocket(url);
            socket = ws;
            ws.OnMessage += bytes => { if (socket == ws) OnMessage(Encoding.UTF8.GetString(bytes)); };
            ws.OnError += err => { if (socket == ws) { Debug.LogWarning($"[RankingFetcher] 接続エラー: {err}"); Finish(false); } };
            ws.OnClose += code => { if (socket == ws) Finish(received.Count == pendingKinds.Length); };
            try
            {
                // 閉じるまで戻らない
                await ws.Connect();
            }
            catch (Exception ex)
            {
                if (socket == ws) { Debug.LogWarning($"[RankingFetcher] 繋げませんでした: {ex.Message}"); Finish(false); }
            }
        }

        private static string ResolveUrl()
        {
            var prefab = Resources.Load<GameObject>(ClientPrefabPath);
            var client = prefab != null ? prefab.GetComponent<WebSocketGameClientSample>() : null;
            if (client == null)
            {
                Debug.LogWarning("[RankingFetcher] 対局の接続の Prefab が見つからないので、ランキングを取れません");
                return null;
            }
            return client.BuildConnectUrl();
        }

        [Serializable] private class TypeOnly { public string type; }
        [Serializable] private class RankingRequestData { public string kind; public int limit; }
        [Serializable] private class RankingRequest { public string type = "ranking"; public RankingRequestData data; }

        private async void OnMessage(string raw)
        {
            var head = JsonUtility.FromJson<TypeOnly>(raw);
            if (head == null) return;
            if (head.type == "connected")
            {
                foreach (string kind in pendingKinds)
                {
                    string json = JsonUtility.ToJson(new RankingRequest { data = new RankingRequestData { kind = kind, limit = limit } });
                    if (socket == null) return;
                    await socket.SendText(json);
                }
                return;
            }
            if (head.type == "ranking")
            {
                var msg = JsonUtility.FromJson<RankingMessage>(raw);
                if (msg == null || msg.data == null || string.IsNullOrEmpty(msg.data.kind)) return;
                if (msg.data.entries == null) msg.data.entries = new RankingEntry[0];
                Latest[msg.data.kind] = msg.data;
                received.Add(msg.data.kind);
                if (received.Count >= pendingKinds.Length)
                {
                    LatestAt = Time.realtimeSinceStartup;
                    Finish(true);
                }
                return;
            }
            if (head.type == "error") Debug.LogWarning($"[RankingFetcher] サーバーのエラー: {raw}");
        }

        private async void Finish(bool ok)
        {
            var ws = socket;
            if (ws == null) return;
            socket = null;
            var done = onDone;
            onDone = null;
            done?.Invoke(ok);
            try { if (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting) await ws.Close(); }
            catch (Exception) { /* 閉じられなくても困らない */ }
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            socket?.DispatchMessageQueue();
#endif
            if (socket != null && Time.realtimeSinceStartup - startedAt > TimeoutSeconds)
            {
                Debug.LogWarning("[RankingFetcher] 時間内に返事が揃わなかったので、取るのをやめました");
                Finish(false);
            }
        }

        private void OnDestroy() { Finish(false); }
    }
}
