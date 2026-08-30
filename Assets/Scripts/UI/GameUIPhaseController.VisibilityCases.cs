using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // HandlePhaseVisibility のフェイズごとの中身。
    //
    // **この7本は HandlePhaseVisibility の switch から丸ごと切り出したもので、
    // 呼ばれる順・条件は switch のときと変わらない。** 元は1メソッドで282行あり、
    // どのフェイズを読んでいるのか見失うので分けた（2026-08-30）。
    public partial class GameUIPhaseController
    {
        /// <summary>賭け金フェイズ。スマホを拡大して賭け金UIを出す。</summary>
        private void ApplyBettingVisibility()
        {
            SetMatchUIVisibility(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            // チュートリアルでは TutorialManager が「セリフ → 拡大 → 賭け金UI」の順で進める。
            // ここで拡大やベット開始をしてしまうと、セリフを送る前にスマホが拡大してしまう。
            if (!uiManager.IsTutorialMode)
            {
                if (uiManager.PlayerInfoUI != null)
                {
                    uiManager.PlayerInfoUI.StartCoroutine(
                        uiManager.PlayerInfoUI.ZoomInRoutine(0.4f, PlayerInfoUI.BettingZoomScale));
                }
                StartBettingPhase(Managers.BoardStateManager.Instance.LocalPlayerHp);

                // スマホが拡大している間は札がその裏に入る。
                // 賭け金を確定してスマホが縮んでから出す（OnBetConfirmed）
                SetReadyBadgesSuppressed(true);
                ApplyPhaseReadyMarks(RoundStatus.Betting);
            }
        }

        /// <summary>局の頭。前の局の後始末をして、次局の入りの演出を始める。</summary>
        private void ApplyDealingVisibility()
        {
            _hasShownHandSelectionPrompt = false; // 次の局のためにフラグをリセット
            _hasExecutedRonAnimation = false; // ロン演出の二重再生防止フラグをリセット
            // 待ち候補の推理は局ごとにやり直す
            if (!uiManager.IsTutorialMode) uiManager.WaitDeduction.ResetForNewRound();
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
            ResetPhaseReadyMarks(); // 手牌選択・ベットの印は局ごとに引き直す
            // 賭け金を確定しないままフェイズが進むと伏せたままになるので、局の頭で戻す
            SetReadyBadgesSuppressed(false);

            if (_pendingDrawTransition)
            {
                _pendingDrawTransition = false;
                ExecuteDrawTransitionForDealing();
            }
            else
            {
                StartNextRoundTransitionForDealing();
            }
        }

        /// <summary>手牌構築フェイズ。待ち牌UI・ドラ・「手牌を選んでください」。</summary>
        private void ApplyHandSelectionVisibility()
        {
            SetMatchUIVisibility(true);
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(false);

            if (!uiManager.IsTutorialMode)
            {
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(true);
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.StartTurnTimer(15f);
                SetReadyBadgesSuppressed(false); // 手牌選択ではスマホは拡大しない
                ApplyPhaseReadyMarks(RoundStatus.HandSelection);
            }

            // **チュートリアルでは決定を押すまで待ち牌UIを出さない。**
            // 『おまかせ』は待ち牌を盤面に入れたうえで SetPhase(HandSelection) を通るため、
            // 素直に書くと押した瞬間に左下へ出て、手牌確認のUIと重なる。
            // 決定後は TutorialManager.ConfirmHandSelectionComplete が出す。
            bool waitUiAllowed = !uiManager.IsTutorialMode
                || (uiManager.TutorialManager != null && uiManager.TutorialManager.IsHandSelectionConfirmed);

            if (uiManager.WaitUI != null && waitUiAllowed
                && Managers.BoardStateManager.Instance.CurrentWaitTiles != null
                && Managers.BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                uiManager.WaitUI.gameObject.SetActive(true);
                uiManager.WaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentWaitTiles);
            }
            else if (uiManager.WaitUI != null)
            {
                uiManager.WaitUI.gameObject.SetActive(false);
            }
            UpdateDoraDisplay();

            if (ReactionController.Instance != null && !uiManager.IsTutorialMode)
            {
                ReactionController.Instance.StartHandSelectionTimer();
            }

            // 黒幕が晴れて手牌フェイズに入った時に表示（1局につき1回のみ）
            if (uiManager.PhaseTransitionUI != null && !_hasShownHandSelectionPrompt && !uiManager.IsTutorialMode)
            {
                uiManager.PhaseTransitionUI.PlayPromptText("手牌を選んでください", 1.5f);
                _hasShownHandSelectionPrompt = true;
            }
        }

        /// <summary>親決め。スマホを引っ込める。</summary>
        private void ApplyTurnDecisionVisibility()
        {
            // ベットの「準備完了」はここで役目を終える。
            // PlayerInfoUI は非表示にするだけで箱は開いたままなので、明示的に閉じる
            HideReadyBoxes();
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.gameObject.SetActive(false);
                uiManager.PlayerInfoUI.StopTurnTimer();
            }
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
        }

        /// <summary>打牌フェイズ。盤面を出し、手番側のタイマーを回す。</summary>
        private void ApplyDiscardVisibility()
        {
            // TurnDecision が保留で飛ばされた場合に備えて、ここでも閉じておく
            HideReadyBoxes();
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(true);
            if (uiManager.HandUI != null) uiManager.HandUI.gameObject.SetActive(true);
            if (uiManager.WallUI != null) uiManager.WallUI.gameObject.SetActive(true);
            if (uiManager.EnemyWallUI != null) uiManager.EnemyWallUI.gameObject.SetActive(false);

            if (uiManager.RiverUI != null) uiManager.RiverUI.UpdateTurnText();
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.UpdateTurnText();

            if (!uiManager.IsTutorialMode)
            {
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                if (uiManager.PlayerInfoUI != null)
                {
                    if (Managers.BoardStateManager.Instance.IsLocalTurn)
                    {
                        uiManager.PlayerInfoUI.StartTurnTimer(10f); // 10秒
                    }
                    else
                    {
                        uiManager.PlayerInfoUI.StopTurnTimer();
                    }
                }
            }

            if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                uiManager.WaitUI.gameObject.SetActive(true);
                uiManager.WaitUI.DisplayWaits(BoardStateManager.Instance.CurrentWaitTiles);
            }
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            UpdateDoraDisplay();
        }

        /// <summary>和了（Agari / Ron / Result）。本編ならここからロン演出へ入る。</summary>
        private void ApplyAgariVisibility()
        {
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();

            // チュートリアルではロンボタンを押させてから TutorialManager が演出を出す。
            // ここで実行すると、ボタンを押す前にロンが走ってしまう。
            // （LastIsLocalWin はサーバー通信でしか更新されず、チュートリアルでは
            //   初期値の true のままなので、敵のロンでも自分の勝ちとして走ってしまう）
            if (!uiManager.IsTutorialMode && uiManager.RonAnimationUI != null)
            {
                bool isLocalWin = BoardStateManager.Instance.LastIsLocalWin;

                if (isLocalWin)
                {
                    uiManager.ExecuteRonAction();
                }
            }
        }

        /// <summary>流局。</summary>
        private void ApplyDrawVisibility()
        {
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);

            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText("流局…次の対局へ");
            }
        }
    }
}
