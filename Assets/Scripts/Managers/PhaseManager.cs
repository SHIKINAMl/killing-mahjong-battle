using System;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// ゲームのフェイズ進行を管理するクラス
    /// MonoBehaviourを継承したSingletonパターン
    /// </summary>
    public class PhaseManager : MonoBehaviour
    {
        public static PhaseManager Instance { get; private set; }

        [Header("Current Phase State")]
        [SerializeField] private GameStatus currentGameStatus = GameStatus.Waiting;
        [SerializeField] private RoundStatus currentRoundStatus = RoundStatus.None;

        public GameStatus CurrentGameStatus => currentGameStatus;
        public RoundStatus CurrentRoundStatus => currentRoundStatus;

        // フェイズが変更されたときに発火するイベント
        public event Action<RoundStatus, RoundStatus> OnRoundPhaseChanged; // previous, new

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject); // シーン遷移で維持したい場合はコメント解除
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // シーン再読込時に破棄済みオブジェクトを指したままにしない
            if (Instance == this) Instance = null;
        }

        public void ChangeGameStatus(GameStatus newStatus)
        {
            if (currentGameStatus != newStatus)
            {
                currentGameStatus = newStatus;
                Debug.Log($"[PhaseManager] Game Status changed to: {newStatus}");
            }
        }

        public void ChangeRoundStatus(RoundStatus newStatus)
        {
            if (currentRoundStatus != newStatus)
            {
                RoundStatus previousStatus = currentRoundStatus;
                currentRoundStatus = newStatus;
                Debug.Log($"[PhaseManager] Round Phase changed: {previousStatus} -> {newStatus}");
                OnRoundPhaseChanged?.Invoke(previousStatus, newStatus);
            }
        }

        public void ChangeRoundStatus(string statusStr)
        {
            RoundStatus parsedStatus = PhaseStateExtensions.ParseRoundStatus(statusStr);
            ChangeRoundStatus(parsedStatus);
        }
    }
}
