using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

namespace KillingMahjong.Network
{
    [Serializable] public class OpenRoom { public string code; public string host; }
    [Serializable] public class OpenRoomList { public OpenRoom[] rooms; }
    [Serializable] public class OpenRoomMessage { public string type; public OpenRoomList data; }

    /// <summary>
    /// 募集中のフレンドマッチの部屋の一覧を、サーバーから取ってくる（2026-11-24、ルームマッチング画面の右の一覧）。
    ///
    /// **対局の接続（<see cref="WebSocketGameClientSample"/>）は使わない。** あちらは繋ぐとすぐ <c>join</c> を送るので、
    /// 見るだけの用には使えない（<see cref="RankingFetcher"/> と同じ理由）。別に1本だけ繋ぎ、<c>rooms</c> を送って、
    /// 返事が来たら切る。名乗らない。
    ///
    /// 返事: <c>{"type":"rooms","data":{"rooms":[{"code":"XD323","host":"名前"}]}}</c>（新しい順・自分の部屋は入らない）。
    /// サーバー側は <c>mahjong_engine/communication/websocket_server.py</c> の <c>_handle_list_rooms</c>。
    /// 古いサーバーは <c>rooms</c> を知らず <c>error</c>（Unknown type）を返すので、そのときは取れなかった扱いにする。
    /// </summary>
    public sealed class RoomListFetcher : MonoBehaviour
    {
        /// <summary>サーバーは使わない間は眠っていて、起きるまで数十秒かかることがある。</summary>
        private const float TimeoutSeconds = 60f;

        public bool IsBusy => socket != null;

        private WebSocket socket;
        private float startedAt;
        private Action<bool, OpenRoom[]> onDone;

        /// <summary>取りに行く。終わったら <paramref name="done"/>（取れたか, 部屋）。取りに行っている最中なら何もしない。</summary>
        public async void Fetch(Action<bool, OpenRoom[]> done)
        {
            if (socket != null) return;
            string url = ResolveUrl();
            if (string.IsNullOrEmpty(url)) { done?.Invoke(false, null); return; }

            onDone = done;
            startedAt = Time.realtimeSinceStartup;

            var ws = new WebSocket(url);
            socket = ws;
            ws.OnMessage += bytes => { if (socket == ws) OnMessage(Encoding.UTF8.GetString(bytes)); };
            ws.OnError += err => { if (socket == ws) { Debug.LogWarning($"[RoomListFetcher] 接続エラー: {err}"); Finish(false, null); } };
            ws.OnClose += code => { if (socket == ws) Finish(false, null); };
            try
            {
                // 閉じるまで戻らない
                await ws.Connect();
            }
            catch (Exception ex)
            {
                if (socket == ws) { Debug.LogWarning($"[RoomListFetcher] 繋げませんでした: {ex.Message}"); Finish(false, null); }
            }
        }

        private static string ResolveUrl()
        {
            var prefab = Resources.Load<GameObject>(RankingFetcher.ClientPrefabPath);
            var client = prefab != null ? prefab.GetComponent<WebSocketGameClientSample>() : null;
            if (client == null)
            {
                Debug.LogWarning("[RoomListFetcher] 対局の接続の Prefab が見つからないので、部屋の一覧を取れません");
                return null;
            }
            return client.BuildConnectUrl();
        }

        [Serializable] private class TypeOnly { public string type; }

        private async void OnMessage(string raw)
        {
            var head = JsonUtility.FromJson<TypeOnly>(raw);
            if (head == null) return;
            if (head.type == "connected")
            {
                if (socket != null) await socket.SendText("{\"type\":\"rooms\"}");
                return;
            }
            if (head.type == "rooms")
            {
                var msg = JsonUtility.FromJson<OpenRoomMessage>(raw);
                var rooms = msg != null && msg.data != null && msg.data.rooms != null ? msg.data.rooms : new OpenRoom[0];
                Finish(true, rooms);
                return;
            }
            if (head.type == "error")
            {
                Debug.LogWarning($"[RoomListFetcher] サーバーのエラー: {raw}");
                Finish(false, null);
            }
        }

        private async void Finish(bool ok, OpenRoom[] rooms)
        {
            var ws = socket;
            if (ws == null) return;
            socket = null;
            var done = onDone;
            onDone = null;
            done?.Invoke(ok, rooms);
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
                Debug.LogWarning("[RoomListFetcher] 時間内に返事が来なかったので、取るのをやめました");
                Finish(false, null);
            }
        }

        private void OnDestroy() { Finish(false, null); }
    }
}
