using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    // フェイズ状態・BGMのこもり・演出中の保留キューまわり。GameUIManager から分離（partial）。
    // クラス・namespace・[SerializeField] は変えていないのでシーン参照には影響しない。
    public partial class GameUIManager
    {
        public void SetCurrentPhaseStatus(RoundStatus status)
        {
            currentPhaseStatus = status;
            UpdateTurnIndicatorVisibility();

            // 通常対局時、打牌フェイズ以外はBGMをくぐもらせる（ローパス）。
            // 開くとき・こもるときの秒数は AudioManager 側の既定に任せる
            // （開く 2.0 秒 / こもる 1.5 秒。log 補間なので端から端まで動いて聞こえる）
            //
            // **開くのは演出が明けてから（2026-08-26）。**
            //
            // `UpdatePhaseStatus` はここを呼んだ直後に `EnterPhase` を呼ぶが、
            // あちらは演出中なら `DeferUntilIdle` で演出明けまで保留される。
            // ここで即座にフェードを始めると、**2秒のフェードが暗転に覆われている
            // あいだに走り切ってしまい**、プレイヤーが盤面を見たときには既に開き切っている。
            // log 補間にしても「フェイズが変わった瞬間にクリアになった」と聞こえていたのはこれが理由。
            //
            // **こもらせる方は待たない。** 打牌フェイズを抜ける先はロン・流局などの
            // 決着演出で、プレイヤーはその演出の開始をもってフェイズの変化を認識する。
            // ここで待つと、演出が終わるまでBGMが開いたままになる。
            if (!IsTutorialMode && KillingMahjong.Managers.AudioManager.Instance != null)
            {
                bool willOpen = (status == RoundStatus.Discard);
                if (willOpen && holdDiscardBgmOpeningForBattleStart)
                {
                    // 賭け確定の暗転中だけは、盤面が見え始める瞬間まで開かない。
                    // onMidpoint は進行を先に Discard へ進めるため、ここで止めないと
                    // 暗転の裏で 2 秒のローパス解除が終わってしまう。
                }
                else if (willOpen && IsBusyWithTransition)
                {
                    DeferUntilIdle(BgmFilterDeferKey, ApplyBgmFilterForCurrentPhase);
                }
                else
                {
                    ApplyBgmFilterForCurrentPhase();
                }
            }
        }

        /// <summary>保留キューでの識別名。後勝ちで畳みたいので固定の1本にする。</summary>
        private const string BgmFilterDeferKey = "bgmFilter";

        // 賭け確定の既存シーケンスだけが、暗転解除の瞬間まで Discard の開放を持つ。
        // BGMの開放待ちは入力ロックと別の責任なので、ロックの取得数には含めない。
        private bool holdDiscardBgmOpeningForBattleStart;

        /// <summary>
        /// 賭け確定後の BGM 開放を、盤面が見え始める瞬間まで待たせる。
        ///
        /// onMidpoint の進行順や DeferUntilIdle の保留順を変えずに、音だけを演出の起点へ揃える。
        /// </summary>
        public void HoldDiscardBgmOpeningForBattleStart()
        {
            if (IsTutorialMode) return;
            holdDiscardBgmOpeningForBattleStart = true;
        }

        /// <summary>
        /// 賭け確定後の BGM 開放を実行する。
        ///
        /// 保留時点のフェイズを焼き込まず、既存どおり実行時点の状態を読むことで、
        /// 演出中に別フェイズへ進んだ場合も古い Discard 用 BGM を開かない。
        /// </summary>
        public void ReleaseDiscardBgmOpeningForBattleStart()
        {
            if (!holdDiscardBgmOpeningForBattleStart) return;

            holdDiscardBgmOpeningForBattleStart = false;
            if (IsTutorialMode) return;

            ApplyBgmFilterForCurrentPhase();
        }

        /// <summary>
        /// 今のフェイズに合わせてBGMのこもりを当てる。
        ///
        /// **引数を取らず、実行した時点の `currentPhaseStatus` を読む。**
        /// 保留したあとに更にフェイズが進むことがあるので、保留した時点の値を
        /// 焼き込むと古い行き先へ開いてしまう。読み直せば、置き去りになった保留が
        /// 後から流れても「今のフェイズ」に落ち着く。
        /// </summary>
        private void ApplyBgmFilterForCurrentPhase()
        {
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (IsTutorialMode || audio == null) return;

            audio.SetBgmFilter(currentPhaseStatus != RoundStatus.Discard);

            // 曲そのものもここで合わせる（2026-09-11）。
            // こもりと同じく「今のフェイズを読み直す」経路に乗せておけば、
            // 保留から遅れて流れてきても行き先が食い違わない。
            audio.SetPhaseBgm(currentPhaseStatus);

            // ドラム層（旧方式）。`UsePhaseBgm` が true のあいだは中で何もしない。
            audio.SetPhaseDrum(currentPhaseStatus);
        }



        private int coveredBoardUpdateDepth;
        internal bool IsUpdatingCoveredBoard => coveredBoardUpdateDepth > 0;
        internal bool CanRebuildBoard => !IsTransitioning || IsUpdatingCoveredBoard;

        /// <summary>黒幕内の同期更新だけを許可する。操作ロックは演出完了まで保持する。</summary>
        internal void RunCoveredBoardUpdate(Action update)
        {
            coveredBoardUpdateDepth++;
            try { update(); }
            finally { coveredBoardUpdateDepth--; }
        }

        // --- 演出中に届いたサーバーイベントの保留 ---
        //
        // サーバーメッセージは再送されないため、演出中だからと早期 return で捨てると
        // そのイベントは永久に失われる（流局の取りこぼしで進行が止まる等）。
        // 捨てる代わりにここへ積み、演出が明けてから実行する。

        private readonly List<KeyValuePair<string, Action>> deferredActions = new List<KeyValuePair<string, Action>>();
        private int deferredFirstFrame;
        private float deferredWaitedSeconds;
        private bool deferredTimeoutReported;
        private bool processingDeferredActions;

        /// <summary>
        /// 何らかの演出が進行中で、UI を触ると壊れる状態かどうか。
        /// </summary>
        public bool IsBusyWithTransition =>
            isTransitioning || (phaseTransitionUI != null && phaseTransitionUI.IsDarkenTransitioning);

        /// <summary>配牌・状態同期の共通入口。再実行時にも局頭の進行管理へ確認する。</summary>
        public bool DeferRoundStartBoardUpdate(RoundStartCoordinator.BoardUpdateKind kind, Action retry)
        {
            bool shouldDefer = PhaseController != null
                ? PhaseController.ShouldDeferRoundStartBoardUpdate(kind, IsBusyWithTransition)
                : IsBusyWithTransition;
            if (!shouldDefer) return false;
            string key = kind == RoundStartCoordinator.BoardUpdateKind.DealingCompleted
                ? "dealingCompleted" : "roundStartStatus";
            DeferUntilIdle(key, retry);
            return true;
        }

        /// <summary>
        /// 演出が明けるまで処理を保留する。
        /// 同じ key の保留は後勝ちで上書きするので、連続して届いても積み上がらない。
        /// 上書きは元の位置で行う（末尾に付け直すと到着順が壊れるため）。
        /// </summary>
        public void DeferUntilIdle(string key, Action action)
        {
            if (action == null) return;

            if (deferredActions.Count == 0)
            {
                deferredFirstFrame = Time.frameCount;
                ResetDeferredWait();
            }

            var entry = new KeyValuePair<string, Action>(key, action);
            int existing = deferredActions.FindIndex(p => p.Key == key);
            if (existing >= 0) deferredActions[existing] = entry;
            else deferredActions.Add(entry);
            Debug.Log($"[GameUIManager] 演出中のため '{key}' を保留しました。演出完了後に実行します。");

        }

        private void ResetDeferredWait()
        {
            deferredWaitedSeconds = 0f;
            deferredTimeoutReported = false;
        }

        internal void ResetDeferredActions()
        {
            deferredActions.Clear();
            deferredFirstFrame = Time.frameCount;
            ResetDeferredWait();
        }

        private void Update()
        {
            TryStartGameResult();
            // 待機用コルーチンを持たない。再有効化後のUpdateが保留を引き継ぐ。
            ProcessDeferredActions(Time.unscaledDeltaTime);
        }

        private void ProcessDeferredActions(float deltaTime)
        {
            if (!isActiveAndEnabled || processingDeferredActions || deferredActions.Count == 0
                || deferredFirstFrame == Time.frameCount) return;
            if (IsBusyWithTransition)
            {
                deferredWaitedSeconds += Mathf.Max(0f, deltaTime);
                if (!deferredTimeoutReported && deferredWaitedSeconds >= DeferredActionTimeoutSeconds)
                {
                    deferredTimeoutReported = true;
                    Debug.LogWarning($"[GameUIManager] 保留が {DeferredActionTimeoutSeconds} 秒続いています。演出・通信待ちの完了または中止によるロック解除を待ちます（{deferredActions.Count}件）。");
                }
                return;
            }
            ResetDeferredWait();
            // 再入・同じフレームの自己再登録でループしない。実行の直前に一件ずつ取り出す。
            int remaining = deferredActions.Count;
            processingDeferredActions = true;
            try
            {
                while (remaining-- > 0 && deferredActions.Count > 0 && isActiveAndEnabled
                    && !IsBusyWithTransition && deferredFirstFrame != Time.frameCount)
                {
                    var entry = deferredActions[0];
                    deferredActions.RemoveAt(0);
                    try
                    {
                        entry.Value?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameUIManager] 保留処理 '{entry.Key}' の実行に失敗: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            finally
            {
                processingDeferredActions = false;
            }
        }

        private const float DeferredActionTimeoutSeconds = 8f;

        private void UpdateTurnIndicatorVisibility()
        {
            // 打牌フェイズで、かつ演出中（先行・後攻演出など）ではない時だけ表示する
            bool shouldShow = (currentPhaseStatus == RoundStatus.Discard) && !IsTransitioning;

            // 「YOUR TURN / ENEMY TURN」の文字は出さない（ユーザーの指示 2026-09-08）。
            // その場所にはボルテージのゲージを置いた（VoltageUI）。
            // 戻すときは false を shouldShow に戻す。
            if (turnIndicatorUI != null)
            {
                turnIndicatorUI.SetVisible(false);
            }

            // **手番を体力表示の光り物で示すのは、両側ともやめた。**
            //
            // - 相手側: 2026-08-14 の指示で停止。EnemyInfoUI.SetTurnGlow の中身が空になっている
            //   （立ち絵を染める TurnCharacterGlow も、点滴の影絵 TurnGlow も生成されない）
            // - 自分側: 2026-09-06 の指示で停止。ここから呼ばなければ TurnGlow.Attach が
            //   走らないので、影絵そのものが作られない
            //
            // 以前あった画面ふちの枠（TurnVignette）も、盤面が狭く見えるのでやめてある。
            //
            // 戻すときは、下の2行のコメントを外す。クラスは両方とも残してある。
            // bool isLocalTurn = KillingMahjong.Managers.BoardStateManager.Instance != null
            //                 && KillingMahjong.Managers.BoardStateManager.Instance.IsLocalTurn;
            // if (playerInfoUI != null) playerInfoUI.SetTurnGlow(shouldShow && isLocalTurn);
            // if (enemyInfoUI != null) enemyInfoUI.SetTurnGlow(shouldShow && !isLocalTurn);
        }
    }
}
