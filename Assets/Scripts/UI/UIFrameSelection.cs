using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>プレイヤーごとの装備IDの入口。今回は選択画面・課金・サーバー同期を実装しない。</summary>
    public static class UIFrameSelection
    {
        public const string LocalPlayer = "local";
        private static readonly Dictionary<string, string> equipped = new Dictionary<string, string>(StringComparer.Ordinal);
        public static int Revision { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { equipped.Clear(); Revision++; }

        public static string GetForPlayer(string playerKey)
        {
            return !string.IsNullOrEmpty(playerKey) && equipped.TryGetValue(playerKey, out var id)
                ? UIFrameCatalog.Resolve(id).Id : UIFrameCatalog.DefaultId;
        }

        public static bool SetForPlayer(string playerKey, string frameId)
        {
            if (string.IsNullOrWhiteSpace(playerKey)) return false;
            bool known = UIFrameCatalog.Contains(frameId);
            string resolved = UIFrameCatalog.Resolve(frameId).Id;
            if (!equipped.TryGetValue(playerKey, out var previous) || previous != resolved)
            { equipped[playerKey] = resolved; Revision++; }
            return known;
        }
    }
}
