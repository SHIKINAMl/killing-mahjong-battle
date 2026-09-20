using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// フェイズ遷移に紐づく演出を、ゲーム進行とは分けて受け持つ窓口。
    ///
    /// 今回は既存の演出経路を移さず、イベント購読と演出シーケンスの置き場だけを用意する。
    /// ここで入力を止めたり画面・音を変えたりすると現在の保留順が変わるため、
    /// 次の依頼で各シーケンスを実装するまで何もしない。
    /// </summary>
    public class PhaseTransitionDirector : MonoBehaviour
    {
        private static PhaseTransitionDirector instance;

        private PhaseManager subscribedPhaseManager;
        private Coroutine subscribeRoutine;

        /// <summary>
        /// シーンを編集せず、対局とチュートリアルのどちらにも同じ窓口を置く。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateIfNeeded()
        {
            if (Object.FindFirstObjectByType<PhaseTransitionDirector>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var directorObject = new GameObject(nameof(PhaseTransitionDirector));
            DontDestroyOnLoad(directorObject);
            directorObject.AddComponent<PhaseTransitionDirector>();
        }

        private void Awake()
        {
            // 誤ってシーン側にも置かれても、同じイベントを二重に購読させない。
            if (instance != null && instance != this)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            SubscribeToPhaseManager();
        }

        private void OnEnable()
        {
            // ドメイン再読み込みで static が初期化されても、既存の1個を使い続ける。
            if (instance == null) instance = this;
            if (instance != this) return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SubscribeToPhaseManager();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopSubscribeRoutine();
            UnsubscribeFromPhaseManager();
        }

        private void OnDestroy()
        {
            // 破棄経路では OnDisable が先に来るが、購読解除漏れをここでも防ぐ。
            UnsubscribeFromPhaseManager();
            if (instance == this) instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 前シーンの PhaseManager を残したままにせず、新しい場面のものだけを購読する。
            SubscribeToPhaseManager();
        }

        private void StartSubscribeRoutine()
        {
            StopSubscribeRoutine();
            subscribeRoutine = StartCoroutine(SubscribeAfterSceneLoadedRoutine());
        }

        private void StopSubscribeRoutine()
        {
            if (subscribeRoutine == null) return;

            StopCoroutine(subscribeRoutine);
            subscribeRoutine = null;
        }

        private IEnumerator SubscribeAfterSceneLoadedRoutine()
        {
            // PhaseManager の Awake 完了後に読む。これでシーン切り替え時も二重購読しない。
            yield return null;

            SubscribeToPhaseManager();
            subscribeRoutine = null;
        }

        private void SubscribeToPhaseManager()
        {
            PhaseManager phaseManager = EnsurePhaseManager();
            if (phaseManager == subscribedPhaseManager) return;

            UnsubscribeFromPhaseManager();
            if (phaseManager == null) return;

            subscribedPhaseManager = phaseManager;
            subscribedPhaseManager.OnRoundPhaseChanged += HandleRoundPhaseChanged;
        }

        private PhaseManager EnsurePhaseManager()
        {
            if (PhaseManager.Instance != null) return PhaseManager.Instance;

            var phaseManager = GetComponent<PhaseManager>();
            if (phaseManager == null)
            {
                phaseManager = gameObject.AddComponent<PhaseManager>();
            }

            return PhaseManager.Instance != null ? PhaseManager.Instance : phaseManager;
        }

        private void UnsubscribeFromPhaseManager()
        {
            if (subscribedPhaseManager != null)
            {
                subscribedPhaseManager.OnRoundPhaseChanged -= HandleRoundPhaseChanged;
            }

            subscribedPhaseManager = null;
        }

        private void HandleRoundPhaseChanged(RoundStatus previous, RoundStatus next)
        {
            Debug.Log($"[PhaseTransitionDirector] {previous} -> {next}");
            StartCoroutine(PlayTransitionRoutine(previous, next));
        }

        /// <summary>
        /// 遷移ごとの演出を置くコルーチン。
        /// 今回は枠だけで、既存の暗転・フラッシュ・BGM・画面揺れには触れない。
        /// </summary>
        private IEnumerator PlayTransitionRoutine(RoundStatus previous, RoundStatus next)
        {
            // PhaseManager は同じ状態への変更でイベントを発火しないが、呼び出し側の保険にする。
            if (previous == next) yield break;

            // 次回、演出の前後で SetInputTransitionLock(true/false) を呼ぶ。
            // 今回ここで呼ぶと DeferUntilIdle の保留順が変わるため、意図的に呼ばない。
            switch (next)
            {
                case RoundStatus.None:
                    // 次回: 対局前へ戻るときの演出が必要になったらここへ置く。
                    break;

                case RoundStatus.Dealing:
                    // 次回: 配牌に入る演出をここへ置く。
                    break;

                case RoundStatus.HandSelection:
                    // 次回: 手牌選択へ切り替わる演出をここへ置く。
                    break;

                case RoundStatus.Betting:
                    // 次回: 賭け入力へ入る演出をここへ置く。
                    break;

                case RoundStatus.TurnDecision:
                    // 次回: 相手の番の圧を出す演出をここへ置く。
                    break;

                case RoundStatus.Discard:
                    // 次回: 賭け確定後の開戦演出をここへ置く。
                    break;

                case RoundStatus.Liquidation:
                    // 次回: 清算へ入る演出をここへ置く。
                    break;

                case RoundStatus.Agari:
                case RoundStatus.Ron:
                    // 次回: ロンのヒットストップをここへ置く。
                    break;

                case RoundStatus.Draw:
                    // 次回: 流局へ入る演出をここへ置く。
                    break;

                case RoundStatus.Result:
                    // 次回: 結果表示へ入る演出をここへ置く。
                    break;
            }

            yield break;
        }

        /// <summary>
        /// 次回の遷移シーケンスが入力を止めるための窓口。
        /// 現在は呼び出さないため、既存の保留キューと見た目は変わらない。
        /// </summary>
        private void SetInputTransitionLock(bool value)
        {
            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager != null) uiManager.SetIsTransitioning(value);
        }
    }
}
