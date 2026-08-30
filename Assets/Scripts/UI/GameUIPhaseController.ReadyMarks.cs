using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 手牌選択・ベットの「準備完了」の印と、その札の出し入れ。
    // GameUIPhaseController から分離（partial）。
    public partial class GameUIPhaseController
    {

        /// <summary>
        /// "phase_completed_notice"：手牌選択・ベットを確定したプレイヤーの通知。
        /// 確定した本人ぶんが1通ずつ届くので、届いた側にだけ印を立てて描き直す。
        /// </summary>
        public void HandlePhaseCompletedNotice(PhaseCompletedNoticeData data)
        {
            if (data == null || uiManager == null) return;
            // チュートリアルはサーバーに繋がず待ち合わせも無いので、印は出さない
            if (uiManager.IsTutorialMode) return;

            var net = NetworkMessageHandler.Instance;
            string localId = (net != null) ? net.LocalPlayerId : null;
            if (string.IsNullOrEmpty(localId))
            {
                Debug.LogWarning("[GameUIPhaseController] phase_completed_notice: LocalPlayerId が未設定のため自他を判別できません。");
                return;
            }
            bool isLocal = (data.player_id == localId);

            // "bet" が仕様だが、phase_change 側の表記は "betting"。どちらで来ても受ける
            switch (data.phase)
            {
                case "hand_selection":
                case "handselection":
                    if (isLocal) _handSelectionReadyLocal = true;
                    else _handSelectionReadyEnemy = true;
                    ApplyPhaseReadyMarks(RoundStatus.HandSelection);
                    break;
                case "bet":
                case "betting":
                    if (isLocal) _betReadyLocal = true;
                    else _betReadyEnemy = true;
                    ApplyPhaseReadyMarks(RoundStatus.Betting);
                    break;
                default:
                    Debug.LogWarning($"[GameUIPhaseController] phase_completed_notice: 未知の phase '{data.phase}'");
                    break;
            }
        }

        /// <summary>
        /// 自分ぶんの「準備完了」を立てる。
        /// 相手ぶんは phase_completed_notice でしか分からないが、自分ぶんは
        /// 既存の受理メッセージ（hand_selection_accepted / bet_accepted）で分かる。
        /// </summary>
        public void MarkLocalPhaseReady(RoundStatus phase)
        {
            if (uiManager == null || uiManager.IsTutorialMode) return;

            if (phase == RoundStatus.HandSelection) _handSelectionReadyLocal = true;
            else if (phase == RoundStatus.Betting) _betReadyLocal = true;
            else return;

            ApplyPhaseReadyMarks(phase);
        }

        /// <summary>
        /// 手牌選択・ベットの「準備完了」を描き直す。
        /// 進行中のフェイズと引数が食い違うときは何もしない（保留から遅れて来た描画で
        /// 別フェイズの箱を出さないため）。
        /// </summary>
        private void ApplyPhaseReadyMarks(RoundStatus phase)
        {
            if (uiManager == null || uiManager.IsTutorialMode) return;
            if (uiManager.CurrentPhaseStatus != phase) return;

            bool localReady;
            bool enemyReady;
            if (phase == RoundStatus.HandSelection)
            {
                localReady = _handSelectionReadyLocal;
                enemyReady = _handSelectionReadyEnemy;
            }
            else if (phase == RoundStatus.Betting)
            {
                localReady = _betReadyLocal;
                enemyReady = _betReadyEnemy;
            }
            else
            {
                return;
            }

            // ShowReadyBox はチェックを外すので、必ず先に呼んでから SetReadyCheck する
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.ShowReadyBox(true);
                uiManager.PlayerInfoUI.SetReadyCheck(localReady);
            }
            if (uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.ShowReadyBox(true);
                uiManager.EnemyInfoUI.SetReadyCheck(enemyReady);
            }

            // 両者そろった瞬間に「選び直す」を引っ込める。ここで描き直さないと、
            // phase_change が届くまでボタンが残って連打で押せてしまう。
            if (phase == RoundStatus.HandSelection && uiManager.HandUI != null)
            {
                uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
            }
        }

        /// <summary>局の頭で印を落とす。手牌選択とベットは1局に1回ずつなのでここだけで足りる。</summary>
        private void ResetPhaseReadyMarks()
        {
            _handSelectionReadyLocal = false;
            _handSelectionReadyEnemy = false;
            _betReadyLocal = false;
            _betReadyEnemy = false;
        }

        /// <summary>「準備完了」の箱を両者ぶん隠す。</summary>
        private void HideReadyBoxes()
        {
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
        }

        /// <summary>ベット中のスマホ拡大に隠れる間だけ、両者の札を伏せる。</summary>
        private void SetReadyBadgesSuppressed(bool suppressed)
        {
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyBoxSuppressed(suppressed);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetReadyBoxSuppressed(suppressed);
        }

        /// <summary>
        /// 賭け金を確定するとスマホが縮む。縮み終わってから札を出し直す。
        /// 拡大中は札がスマホの裏（x197..602・全高・描画順が上）に入って見えないため。
        /// </summary>
        private IEnumerator ShowReadyBadgesAfterZoomOut()
        {
            // 縮み切るのと同じ長さだけ待つと同フレームで競合し、札が「縮んでいる途中の
            // スマホ」に合わせて置かれる（実測で x552..656。正しくは 668..772）。
            // ReadyBadge は表示した瞬間にしか位置を測らないので、必ず後から動く
            yield return new WaitForSeconds(BetZoomOutDuration + 0.05f);

            // 待っている間に相手も賭け終えてフェイズが進んでいたら、出さずに終わる
            if (uiManager.CurrentPhaseStatus != RoundStatus.Betting) yield break;

            SetReadyBadgesSuppressed(false);
            ApplyPhaseReadyMarks(RoundStatus.Betting);
        }
    }
}
