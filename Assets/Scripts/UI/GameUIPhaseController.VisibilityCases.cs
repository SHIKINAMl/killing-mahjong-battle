using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 現在フェイズの表示だけを更新する。タイマー・演出開始は Entry.cs が担当する。
    public partial class GameUIPhaseController
    {
        /// <summary>賭け金フェイズの盤面・情報パネルの表示。</summary>
        private void ApplyBettingVisibility()
        {
            SetMatchUIVisibility(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (!uiManager.IsTutorialMode) ApplyPhaseReadyMarks(RoundStatus.Betting);
        }

        /// <summary>手牌構築フェイズ。待ち牌UI・ドラの表示。</summary>
        private void ApplyHandSelectionVisibility()
        {
            SetMatchUIVisibility(true);
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(false);

            if (!uiManager.IsTutorialMode)
            {
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(true);
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
            }
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
        }

        /// <summary>打牌フェイズ。盤面・待ち牌・手番の表示。</summary>
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
            }

            if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                uiManager.WaitUI.gameObject.SetActive(true);
                uiManager.WaitUI.DisplayWaits(BoardStateManager.Instance.CurrentWaitTiles);
            }
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            UpdateDoraDisplay();
        }

        /// <summary>和了（Agari / Ron / Result）の表示。</summary>
        private void ApplyAgariVisibility()
        {
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }

        /// <summary>流局。</summary>
        private void ApplyDrawVisibility()
        {
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(true);
        }
    }
}
