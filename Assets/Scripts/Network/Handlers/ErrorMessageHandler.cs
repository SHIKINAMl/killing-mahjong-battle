using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "error": サーバーエラーの通知。
    /// </summary>
    public class ErrorMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "error" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            ErrorMessage errorMsg = JsonUtility.FromJson<ErrorMessage>(jsonString);
            Debug.LogError($"[Server Error] {errorMsg?.message}");
            network.RaiseError(errorMsg?.message);
        }
    }
}
