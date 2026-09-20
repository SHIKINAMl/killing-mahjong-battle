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
    /// 既存の演出経路は移さず、賭け確定の暗転解除だけを PhaseTransitionUI から呼ばれる
    /// 小さな追加口として持つ。入力を止める責務は既存シーケンスに残す。
    /// </summary>
    public class PhaseTransitionDirector : MonoBehaviour
    {
        private static PhaseTransitionDirector instance;

        private PhaseManager subscribedPhaseManager;
        private Coroutine subscribeRoutine;
        private bool battleStartReleasePrepared;

        // 既存の遷移に足す時間は 0.6 秒以内という制約のうち、静止に使う時間。
        // 以降の揺れ・BGM解除・手牌せり上がりは同じフレームから並行して始める。
        private const float BattleStartHoldSeconds = 0.30f;



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
            SubscribeToBoard();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            StopSubscribeRoutine();
            UnsubscribeFromPhaseManager();
            UnsubscribeFromBoard();
        }

        private void OnDestroy()
        {
            // 破棄経路では OnDisable が先に来るが、購読解除漏れをここでも防ぐ。
            UnsubscribeFromPhaseManager();
            UnsubscribeFromBoard();
            if (instance == this) instance = null;
        }

        // --- 相手の番のあいだ、盤面の見せ方を静かに切り替える（2026-09-20 のユーザー指示）---
        //
        // 手番は BoardStateManager が持っていて、変わるたびに OnTurnChanged が飛ぶ。
        // **打牌フェイズのときだけ**効かせる。手牌選択や賭けの最中に落とすと、
        // 操作できるのに操作できないように見える。

        private BoardStateManager subscribedBoard;

        private void SubscribeToBoard()
        {
            var board = BoardStateManager.Instance;
            if (board == subscribedBoard) return;

            UnsubscribeFromBoard();
            if (board == null) return;

            subscribedBoard = board;
            subscribedBoard.OnTurnChanged += HandleTurnChanged;
        }

        private void UnsubscribeFromBoard()
        {
            if (subscribedBoard != null) subscribedBoard.OnTurnChanged -= HandleTurnChanged;
            subscribedBoard = null;
        }

        private void HandleTurnChanged(bool isLocalTurn)
        {
            ApplyOpponentTurnQuiet();
        }

        /// <summary>
        /// いまの手番とフェイズから、相手の番用の見せ方を当て直す。
        /// フェイズが変わったときにも呼ぶ（打牌を抜けたら必ず元へ戻す）。
        /// </summary>
        private void ApplyOpponentTurnQuiet()
        {
            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager == null) return;

            var board = BoardStateManager.Instance;
            bool quiet = board != null
                && !uiManager.IsTutorialMode
                && !board.IsLocalTurn
                && uiManager.CurrentPhaseStatus == RoundStatus.Discard;

            // **落とすのは山牌の段だけ。** 打牌で触るのはここで、自分の手牌は
            // 「手牌を見る」で覗く方式のため画面に出ていない（2026-09-20 に実機で確認）
            if (uiManager.WallUI != null)
            {
                uiManager.WallUI.SetQuietForOpponentTurn(quiet);
            }

            // 相手パネルは位置を変えず、同じ手番判定だけでわずかに大きくする。
            // 別イベントを購読すると山牌の段と食い違うため、この既存の当て直しに集約する。
            if (uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.SetOpponentTurnEmphasis(quiet);
            }
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
            SubscribeToBoard();
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
            SubscribeToBoard();
            ApplyOpponentTurnQuiet();
            StartCoroutine(PlayTransitionRoutine(previous, next));
        }

        /// <summary>
        /// 賭け確定シーケンスが始まった時点で、Discard への BGM 開放だけを保留する。
        ///
        /// PhaseTransitionUI は既存シーケンスの所有者のままにし、演出の時刻管理だけをここへ集める。
        /// </summary>
        public static void PrepareBattleStartRelease()
        {
            var director = GetInstance();
            if (director != null) director.PrepareBattleStartReleaseInternal();
        }

        /// <summary>
        /// 既存の暗転解除の直前に置く「タメ」。**ここではまだ揺らさない。**
        /// 暗転が明ける前に揺らすと、画面が真っ暗なので揺れが見えない（2026-09-20 に直した）。
        /// </summary>
        public static IEnumerator PlayBattleStartRelease()
        {
            var director = GetInstance();
            if (director == null) yield break;

            yield return director.PlayBattleStartReleaseInternal();
        }

        /// <summary>
        /// 盤面が見え始めたところで出す「着弾」。揺れ・BGMの開き・手牌のせり上がりをここでまとめて出す。
        /// **待たない。** 市松模様が晴れていく動きと重ねるため、呼んだ側は流したまま進む。
        /// </summary>
        public static void PlayBattleStartImpact()
        {
            var director = GetInstance();
            if (director != null) director.PlayBattleStartImpactInternal();
        }

        private static PhaseTransitionDirector GetInstance()
        {
            if (instance != null) return instance;
            return Object.FindFirstObjectByType<PhaseTransitionDirector>(FindObjectsInactive.Include);
        }

        private void PrepareBattleStartReleaseInternal()
        {
            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager == null || uiManager.IsTutorialMode) return;

            battleStartReleasePrepared = true;
            uiManager.HoldDiscardBgmOpeningForBattleStart();
        }

        private IEnumerator PlayBattleStartReleaseInternal()
        {
            if (!battleStartReleasePrepared) yield break;

            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager == null || uiManager.IsTutorialMode)
            {
                battleStartReleasePrepared = false;
                yield break;
            }

            // 暗転をもう 0.30 秒だけ保ち、次の一瞬を「開戦」の区切りにする。
            // 旗は下ろさない。晴れ際の着弾（PlayBattleStartImpact）で使う
            yield return new WaitForSeconds(BattleStartHoldSeconds);
        }

        private void PlayBattleStartImpactInternal()
        {
            if (!battleStartReleasePrepared) return;
            battleStartReleasePrepared = false;

            var uiManager = FindFirstObjectByType<GameUIManager>();
            if (uiManager == null || uiManager.IsTutorialMode) return;

            // 進行が想定より先へ進んだときは、現在フェイズの音へ戻すだけにする。
            // 古い Discard 用の揺れや手牌移動を後から出さない。
            if (uiManager.CurrentPhaseStatus != RoundStatus.Discard)
            {
                uiManager.ReleaseDiscardBgmOpeningForBattleStart();
                return;
            }

            // **揺らさない（2026-09-20 のユーザー指示）。**
            // 「盤面をハチャメチャにするより、盤面に集中させたい」。
            // 開戦の合図は、音が開くことと手牌がせり上がることで出す。
            uiManager.ReleaseDiscardBgmOpeningForBattleStart();
            if (uiManager.HandUI != null) uiManager.HandUI.PlayBattleStartRise();
        }

        /// <summary>
        /// 遷移ごとの演出を置くコルーチン。
        /// いまは枠だけで、賭け確定の演出は PhaseTransitionUI 側の既存シーケンスから
        /// PlayBattleStartRelease / PlayBattleStartImpact を呼んでいる。
        /// </summary>
        private IEnumerator PlayTransitionRoutine(RoundStatus previous, RoundStatus next)
        {
            // PhaseManager は同じ状態への変更でイベントを発火しないが、呼び出し側の保険にする。
            if (previous == next) yield break;

            switch (next)
            {
                case RoundStatus.Dealing:
                    // 次回: 配牌に入る演出をここへ置く。
                    break;

                case RoundStatus.TurnDecision:
                    // 次回（B案）: 相手の番の圧を出す演出をここへ置く。
                    break;

                case RoundStatus.Discard:
                    // 賭け確定の開戦演出は、既存暗転の晴れ際に合わせるため
                    // PhaseTransitionUI から直接呼んでいる。ここでは何もしない。
                    break;

                case RoundStatus.Agari:
                case RoundStatus.Ron:
                    // 次回（C案）: ロンのヒットストップをここへ置く。
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
