using UnityEngine;
using System.Collections.Generic;
using System;

namespace KillingMahjong.Network
{
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        // ロック外で実行するための一時バッファ（毎フレームの確保を避けるため使い回す）
        private static readonly List<Action> _pending = new List<Action>();

        public void Update()
        {
            // ロックを握ったまま Invoke すると、実行中ずっと WebSocket 受信スレッドの
            // Enqueue がブロックされる。取り出しだけをロック内で行うこと。
            lock (_executionQueue)
            {
                if (_executionQueue.Count == 0) return;

                _pending.Clear();
                while (_executionQueue.Count > 0)
                {
                    _pending.Add(_executionQueue.Dequeue());
                }
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                // 1件の例外で残りのアクションを落とさない
                try
                {
                    _pending[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            _pending.Clear();
        }

        public static void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }

        private static UnityMainThreadDispatcher _instance = null;
        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<UnityMainThreadDispatcher>();
                if (_instance == null)
                {
                    var go = new GameObject("UnityMainThreadDispatcher");
                    _instance = go.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
        }
    }
}
