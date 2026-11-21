using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace KillingMahjong.Network
{
    /// <summary>
    /// サーバーが発行したプレイヤーの ID と秘密の文字列（2026-10-20、名前・ランキングのデータベース対応）。
    ///
    /// 決まりはサーバーの <c>mahjong_engine/records/README.md</c>。要点:
    /// - **起動している間だけメモリに持つ。端末には保存しない。** 起動し直すと別のプレイヤーになる
    /// - **秘密の文字列は画面にもログにも出さない**（<see cref="Redact"/> を通してから出す）
    /// - 名前を登録した後の対人戦だけがランキングに入る
    ///
    /// この対局ゲームは、対局シーンに入るたびに繋ぎ、抜けると切る。なので
    /// 起動して最初の接続は <c>register</c>、2回目からは <c>identify</c> で同じプレイヤーに戻る。
    /// 名前は端末（<see cref="KillingMahjong.Core.PlayerProfile"/>）にあるものを、名乗るたびに送る。
    /// </summary>
    public static class PlayerIdentity
    {
        public static string Id { get; private set; } = "";
        public static string Secret { get; private set; } = "";
        public static bool HasIdentity => !string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(Secret);

        /// <summary>サーバーが整えた後の名前（<c>name_set</c> / <c>identified</c>）。まだ無ければ空文字。</summary>
        public static string ServerName { get; private set; } = "";

        /// <summary>サーバーのデータベースが使えるか。false でも遊べるが、記録は残らない。</summary>
        public static bool Persistent { get; private set; }

        /// <summary>いまの接続で名乗り終えたか（<c>registered</c> / <c>identified</c> を受け取った）。</summary>
        public static bool IdentifiedOnThisConnection { get; private set; }

        public static void Set(string playerId, string secret) { Id = playerId ?? ""; Secret = secret ?? ""; }

        /// <summary><c>identify_failed</c> のとき。持っている ID を捨てて register からやり直す。</summary>
        public static void Clear() { Id = ""; Secret = ""; ServerName = ""; }

        // ---- 送るもの（JSON は JsonUtility で作る。名前に " や \ が入ることがある）----

        [Serializable] private class TypeOnly { public string type; }
        [Serializable] private class IdentifyData { public string player_id; public string secret; }
        [Serializable] private class IdentifyMessage { public string type = "identify"; public IdentifyData data; }
        [Serializable] private class SetNameData { public string name; }
        [Serializable] private class SetNameMessage { public string type = "set_name"; public SetNameData data; }

        public static string BuildRegisterJson() => JsonUtility.ToJson(new TypeOnly { type = "register" });

        public static string BuildIdentifyJson() => JsonUtility.ToJson(new IdentifyMessage
        {
            data = new IdentifyData { player_id = Id, secret = Secret },
        });

        public static string BuildSetNameJson(string name) => JsonUtility.ToJson(new SetNameMessage
        {
            data = new SetNameData { name = name },
        });

        // ---- 受け取るもの ----

        [Serializable] private class IdentityData { public string player_id; public string secret; public string name; public bool persistent; public string reason; }
        [Serializable] private class IdentityMessage { public string type; public IdentityData data; }

        /// <summary>名乗りと名前の返事の type。<see cref="WebSocketGameClientSample"/> がここで受けて、UI へは流さない。</summary>
        public static bool IsIdentityMessage(string type) =>
            type == "registered" || type == "register_failed" || type == "identified" || type == "identify_failed"
            || type == "name_set" || type == "set_name_failed";

        /// <summary>接続が張られた（<c>connected</c>）。まだ名乗っていない状態に戻す。</summary>
        public static void OnConnected() { IdentifiedOnThisConnection = false; }

        /// <summary>
        /// 返事を受けて状態を進め、次に送るべき JSON を返す（無ければ null）。
        /// 送るのは呼び出し側（接続を持っている <see cref="WebSocketGameClientSample"/>）。
        /// </summary>
        /// <param name="retryLater">名前を書けなかった（<c>unavailable</c>）とき true。少し待って名前を送り直す。</param>
        public static string Handle(string type, string raw, out bool retryLater)
        {
            retryLater = false;
            var msg = JsonUtility.FromJson<IdentityMessage>(raw);
            var d = msg != null ? msg.data : null;
            switch (type)
            {
                case "registered":
                    if (d == null || string.IsNullOrEmpty(d.player_id) || string.IsNullOrEmpty(d.secret))
                    {
                        Debug.LogWarning("[PlayerIdentity] registered に ID か秘密の文字列が入っていません");
                        return null;
                    }
                    Set(d.player_id, d.secret);
                    Persistent = d.persistent;
                    IdentifiedOnThisConnection = true;
                    Debug.Log($"[PlayerIdentity] registered id={Id} persistent={Persistent}");
                    return NameJsonIfAny();

                case "register_failed":
                    // already_registered は、この接続で発行済みということ。持っている値をそのまま使う
                    Debug.LogWarning($"[PlayerIdentity] register_failed: {d?.reason}");
                    return null;

                case "identified":
                    Persistent = d != null && d.persistent;
                    ServerName = d != null && !string.IsNullOrEmpty(d.name) ? d.name : "";
                    IdentifiedOnThisConnection = true;
                    Debug.Log($"[PlayerIdentity] identified id={Id} name={ServerName} persistent={Persistent}");
                    // 端末の名前とサーバーの名前が違えば送り直す（未登録のまま繋ぎ直した、など）
                    return ServerName != KillingMahjong.Core.PlayerProfile.Name ? NameJsonIfAny() : null;

                case "identify_failed":
                    // どの理由でも、持っている ID を捨てて register からやり直す（名前は registered の後に送る）
                    Debug.LogWarning($"[PlayerIdentity] identify_failed: {d?.reason}。register からやり直します");
                    Clear();
                    return BuildRegisterJson();

                case "name_set":
                    ServerName = d != null && !string.IsNullOrEmpty(d.name) ? d.name : "";
                    Debug.Log($"[PlayerIdentity] name_set name={ServerName}");
                    return null;

                case "set_name_failed":
                    Debug.LogWarning($"[PlayerIdentity] set_name_failed: {d?.reason}");
                    if (d != null && d.reason == "not_identified")
                        return HasIdentity ? BuildIdentifyJson() : BuildRegisterJson();
                    retryLater = d != null && d.reason == "unavailable";
                    return null;
            }
            return null;
        }

        /// <summary>端末に名前があれば、それを送る JSON。</summary>
        public static string NameJsonIfAny()
        {
            string name = KillingMahjong.Core.PlayerProfile.Name;
            return string.IsNullOrEmpty(name) ? null : BuildSetNameJson(name);
        }

        private static readonly Regex SecretPattern = new Regex("(\"secret\"\\s*:\\s*\")[^\"]*(\")", RegexOptions.Compiled);

        /// <summary>ログに出す前に、秘密の文字列を伏せる。</summary>
        public static string Redact(string json) =>
            string.IsNullOrEmpty(json) || json.IndexOf("\"secret\"", StringComparison.Ordinal) < 0
                ? json
                : SecretPattern.Replace(json, "$1***$2");
    }
}
