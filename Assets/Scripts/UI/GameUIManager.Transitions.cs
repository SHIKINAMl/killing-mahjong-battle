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
            // `UpdatePhaseStatus` はここを呼んだ直後に `HandlePhaseVisibility` を呼ぶが、
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
                if (willOpen && IsBusyWithTransition)
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
        }

        public void SetIsTransitioning(bool value)
        {
            isTransitioning = value;
            UpdateTurnIndicatorVisibility();

            // 能力パネルと説明ツールチップは通常 20/25 で、フェーズ演出の帯(19)より手前に出る。
            // 演出のあいだだけ帯より下へ退避させる（2026-08-19 のプランナー要望 R-2）。
            if (abilityUI != null) abilityUI.SetSuppressedForTransition(value);
        }

        // --- 演出中に届いたサーバーイベントの保留 ---
        //
        // サーバーメッセージは再送されないため、演出中だからと早期 return で捨てると
        // そのイベントは永久に失われる（流局の取りこぼしで進行が止まる等）。
        // 捨てる代わりにここへ積み、演出が明けてから実行する。

        private readonly List<KeyValuePair<string, Action>> deferredActions = new List<KeyValuePair<string, Action>>();
        private bool ignoreBusyForForcedFlush = false;

        /// <summary>
        /// 保留を流す見張り。**bool ではなく Coroutine のハンドルで持つ。**
        ///
        /// 以前は `isFlushWatcherRunning` という bool で二重起動を防いでいたが、
        /// コルーチンが外から止められると true のまま取り残され、
        /// `if (!isFlushWatcherRunning)` が二度と通らなくなる。
        /// そうなると保留は永久に実行されず、8秒の強制実行という安全網ごと死ぬ
        /// （実際にロン猶予が保留されたまま対局が停止した）。
        /// ハンドルなら StopCoroutine されても null 判定と併せて張り直せる。
        /// </summary>
        private Coroutine flushWatcher;

        /// <summary>
        /// 何らかの演出が進行中で、UI を触ると壊れる状態かどうか。
        /// </summary>
        public bool IsBusyWithTransition =>
            !ignoreBusyForForcedFlush
            && (isTransitioning || (phaseTransitionUI != null && phaseTransitionUI.IsDarkenTransitioning));

        /// <summary>
        /// 演出が明けるまで処理を保留する。
        /// 同じ key の保留は後勝ちで上書きするので、連続して届いても積み上がらない。
        /// 上書きは元の位置で行う（末尾に付け直すと到着順が壊れるため）。
        /// </summary>
        public void DeferUntilIdle(string key, Action action)
        {
            if (action == null) return;

            var entry = new KeyValuePair<string, Action>(key, action);
            int existing = deferredActions.FindIndex(p => p.Key == key);
            if (existing >= 0) deferredActions[existing] = entry;
            else deferredActions.Add(entry);
            Debug.Log($"[GameUIManager] 演出中のため '{key}' を保留しました。演出完了後に実行します。");

            EnsureFlushWatcher();
        }

        /// <summary>
        /// 見張りが動いていなければ張り直す。保留がある限り、何度呼んでも安全。
        /// </summary>
        private void EnsureFlushWatcher()
        {
            if (flushWatcher != null) return;
            if (!isActiveAndEnabled) return;
            flushWatcher = StartCoroutine(FlushDeferredActionsRoutine());
        }

        private void Update()
        {
            // コルーチンが外から止められても、保留が残っていれば必ず拾い直す。
            // これが最後の砦で、ここが無いと「進行が止まったまま何も起きない」に戻る。
            if (deferredActions.Count > 0) EnsureFlushWatcher();
        }

        private IEnumerator FlushDeferredActionsRoutine()
        {

            // 演出の途中で一瞬だけ isTransitioning が false に戻る箇所があるため
            // （TriggerBettingAnimationPhase の onMidpoint）、必ず1フレーム待ってから判定する。
            float waited = 0f;
            do
            {
                yield return null;
                waited += Time.deltaTime;
            }
            while (IsBusyWithTransition && waited < DeferredActionTimeoutSeconds);

            // 演出フラグが立ちっぱなしになると保留が永久に実行されず、
            // 取りこぼしと同じ「進行停止」になる。見た目の乱れより進行を優先する。
            bool forced = IsBusyWithTransition;
            if (forced)
            {
                Debug.LogWarning($"[GameUIManager] 演出が {DeferredActionTimeoutSeconds} 秒明けませんでした。保留していた処理を強制実行します。");
            }

            var toRun = new List<KeyValuePair<string, Action>>(deferredActions);
            deferredActions.Clear();
            flushWatcher = null;

            // 強制実行のときはガードを一時的に無効化する。
            // そうしないと各処理が冒頭で再び「演出中」と判定して保留し直し、
            // 永久に実行されないまま警告だけ出し続ける。
            ignoreBusyForForcedFlush = forced;
            try
            {
                foreach (var entry in toRun)
                {
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
                ignoreBusyForForcedFlush = false;
            }
        }

        private const float DeferredActionTimeoutSeconds = 8f;

        private void UpdateTurnIndicatorVisibility()
        {
            // 打牌フェイズで、かつ演出中（先行・後攻演出など）ではない時だけ表示する
            bool shouldShow = (currentPhaseStatus == RoundStatus.Discard) && !IsTransitioning;

            if (turnIndicatorUI != null)
            {
                turnIndicatorUI.SetVisible(shouldShow);
            }

            // **手番を体力表示の光り物で示すのは、両側ともやめた。**
            // 手番の合図は「YOUR TURN / ENEMY TURN」の文字だけに任せる。
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
