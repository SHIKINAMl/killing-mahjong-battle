using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.UI;
using KillingMahjong.EngineData;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // TutorialManager: 盤面のセットアップと表示

        // ==================== 盤面セットアップ ====================

        private void SetupBoard(TutorialRoundData data, List<int> handBaseIds)
        {
            if (gameUIManager == null || gameUIManager.HandUI == null || gameUIManager.WallUI == null) return;

            gameUIManager.IsTutorialMode = true;
            gameUIManager.TutorialManager = this;

            if (gameUIManager.PhaseTransitionUI != null)
                gameUIManager.PhaseTransitionUI.gameObject.SetActive(false);

            gameUIManager.ClearAllTiles();

            var board = BoardStateManager.Instance;
            if (board != null)
            {
                board.SetLocalTurn(true);
                board.CurrentDoraId = data.doraBaseId >= 0
                    ? TutorialTiles.Encode(data.doraBaseId, isDora: true)
                    : -1;
            }

            SetPhase(RoundStatus.HandSelection);

            // 牌種 → 実牌ID へ変換（ドラフラグ付与）
            List<int> wall = TutorialTiles.EncodeAll(data.wallBaseIds, data.doraBaseId);
            List<int> hand = handBaseIds != null
                ? TutorialTiles.EncodeAll(handBaseIds, data.doraBaseId)
                : new List<int>();
            List<int> waits = TutorialTiles.EncodeAll(data.waitBaseIds, data.doraBaseId);

            if (board != null)
            {
                // 手牌が空のときは待ち牌も出さない（まだ何も組んでいないため）
                board.SetLocalState(wall, hand, hand.Count > 0 ? waits : new List<int>());
            }

            if (gameUIManager.VisualController != null)
                gameUIManager.VisualController.RebuildAllTilesFromState();
        }

        /// <summary>現在の手牌枚数。13枚そろったかの判定に使う。</summary>
        private static int GetHandTileCount()
        {
            var board = BoardStateManager.Instance;
            return board != null && board.CurrentHandTiles != null ? board.CurrentHandTiles.Count : 0;
        }

        /// <summary>
        /// プレイヤーが自力で満貫以上の手を組めたか。
        ///
        /// チュートリアルはサーバーに繋がないので手牌を評価できない。
        /// そこで「台本の満貫手と同じ13枚がそろっているか」で判定する。
        /// この局の山牌は『台本の満貫手（同一色13枚）＋ 筒子9枚 ＋ 索子9枚 ＋ 字牌3枚』で、
        /// 台本の色の牌はその13枚しか入っていない。他の色は9枚しかなく13枚に届かないので、
        /// この山から作れる満貫以上の手は台本の13枚だけ。よってこの一致判定で過不足がない。
        ///
        /// 山牌の構成を変えるときは、この前提が崩れていないか確認すること。
        /// </summary>
        private bool IsSelfMadeManganHand()
        {
            if (_round == null || _round.manganHandBaseIds == null) return false;

            var board = BoardStateManager.Instance;
            if (board == null || board.CurrentHandTiles == null) return false;
            if (board.CurrentHandTiles.Count != _round.manganHandBaseIds.Count) return false;

            // 牌種で多重集合として比較する（同じ牌が複数あるので枚数まで見る）
            var remaining = new List<int>(_round.manganHandBaseIds);
            foreach (int tileId in board.CurrentHandTiles)
            {
                int idx = remaining.IndexOf(TutorialTiles.BaseOf(tileId));
                if (idx < 0) return false;
                remaining.RemoveAt(idx);
            }
            return remaining.Count == 0;
        }

        private static readonly List<TutorialLine> DefaultSelfManganLines = new List<TutorialLine>
        {
            new TutorialLine("あら、自分で満貫手を組めたのね。やるじゃない。"),
            new TutorialLine("それなら『自動』は要らないわ。そのまま決定しなさい。"),
        };

        private static List<TutorialLine> ResolveSelfManganLines(TutorialRoundData data)
        {
            return (data.onSelfManganLines != null && data.onSelfManganLines.Count > 0)
                ? data.onSelfManganLines
                : DefaultSelfManganLines;
        }

        private static List<TutorialLine> ResolveHandFilledLines(TutorialRoundData data)
        {
            return (data.onHandFilledLines != null && data.onHandFilledLines.Count > 0)
                ? data.onHandFilledLines
                : DefaultHandFilledLines;
        }

        /// <summary>ボタンの開放段階を変えて、HandUI に即座に反映させる。</summary>
        private void SetHandButtonStage(HandButtonStage stage)
        {
            _handButtonStage = stage;

            if (gameUIManager != null && gameUIManager.HandUI != null)
                gameUIManager.HandUI.UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        private void SetPhase(RoundStatus status)
        {
            if (gameUIManager == null) return;

            if (gameUIManager.PhaseController != null)
                gameUIManager.PhaseController.UpdatePhaseStatus(status);
            else
                gameUIManager.SetCurrentPhaseStatus(status);
        }

        private void ApplyHpToUI()
        {
            var board = BoardStateManager.Instance;
            if (board != null) board.UpdateHp(_playerHp, _enemyHp);

            if (gameUIManager == null) return;

            // 盤面がまだ隠れている段階（女の子とセリフだけの状態）では表示を戻さない
            // 「準備完了」ボックスは次局待ち合わせ用。チュートリアルには待ち合わせがないので常に隠す
            if (gameUIManager.PlayerInfoUI != null)
            {
                gameUIManager.PlayerInfoUI.gameObject.SetActive(_boardVisible);
                gameUIManager.PlayerInfoUI.SetMaxHP(_scenario.playerStartHp);
                gameUIManager.PlayerInfoUI.SetHP(_playerHp);
                gameUIManager.PlayerInfoUI.ShowReadyBox(false);
            }
            if (gameUIManager.EnemyInfoUI != null)
            {
                gameUIManager.EnemyInfoUI.SetPanelVisible(_boardVisible);
                gameUIManager.EnemyInfoUI.SetMaxHP(_scenario.enemyStartHp);
                gameUIManager.EnemyInfoUI.SetHP(_enemyHp);
                gameUIManager.EnemyInfoUI.ShowReadyBox(false);
            }
        }

        /// <summary>
        /// 盤面まわりの表示を一括で切り替える。
        ///
        /// false のとき画面に残るのは「敵の立ち絵」と「セリフ」だけ。
        /// EnemyInfoUI.SetPanelVisible(false) はHPパネルのみを消し、立ち絵は残す。
        /// </summary>
        /// <summary>
        /// **第1局の牌を選んでいる間だけ、牌以外の表示物を伏せる（2026-08-14 の指示）。**
        ///
        /// 最初の画面で必要なのは「山牌から13枚選ぶ」ことだけなのに、
        /// 体力・点滴・獲得ゲージ・場の血・役一覧・待ち牌まで一度に出ていて、
        /// 何を見ればいいのか分からなかった。
        ///
        /// **立ち絵とセリフ枠は伏せない。** 説明しているのは女の子なので、
        /// これを消すと誰の話を聞いているのか分からなくなる。
        /// 手牌・山牌・おまかせ・決定もそのまま残す。
        ///
        /// ドラは `SetBoardVisible` が常に隠しているのでここでは触らない。
        /// </summary>
        private void SetFirstRoundChromeVisible(bool visible)
        {
            if (gameUIManager == null) return;

            if (gameUIManager.PlayerInfoUI != null)
                gameUIManager.PlayerInfoUI.SetVitalsVisible(visible);
            if (gameUIManager.EnemyInfoUI != null)
                gameUIManager.EnemyInfoUI.SetVitalsVisible(visible);

            if (gameUIManager.YakuListUI != null)
            {
                if (!visible) gameUIManager.YakuListUI.CloseYakuList();
                gameUIManager.YakuListUI.gameObject.SetActive(visible);
            }

            if (!visible && gameUIManager.WaitUI != null)
                gameUIManager.WaitUI.gameObject.SetActive(false);

            gameUIManager.ScoreGauge.SetVisible(visible);

            if (gameUIManager.BetPotUI != null)
                gameUIManager.BetPotUI.SetVisible(visible);
        }

        private void SetBoardVisible(bool visible)
        {
            _boardVisible = visible;
            if (gameUIManager == null) return;

            // 手牌 / 山牌 / 敵山牌 / 役一覧 / 待ち牌
            if (gameUIManager.PhaseController != null)
                gameUIManager.PhaseController.SetMatchUIVisibility(visible);

            // ターン表示（YOUR TURN / ENEMY TURN）は打牌フェイズだけのもの。
            // 盤面をまとめて出すここで無条件に付けると、手牌フェイズでも出てしまう。
            if (gameUIManager.TurnIndicatorUI != null)
            {
                bool isDiscardPhase = gameUIManager.CurrentPhaseStatus == RoundStatus.Discard;
                gameUIManager.TurnIndicatorUI.gameObject.SetActive(visible && isDiscardPhase);
            }

            // 場の血は額を保持したまま表示だけ消す（持ち越し分が消えて見えないように）
            if (gameUIManager.BetPotUI != null)
                gameUIManager.BetPotUI.SetVisible(visible);

            // 獲得ゲージも盤面と一緒に出し入れする
            gameUIManager.ScoreGauge.SetVisible(visible);

            // ドラ表示牌（3Dグランドライト含む）はチュートリアルの説明に入らないので常に隠す
            if (gameUIManager.DoraDisplayUI != null)
                gameUIManager.DoraDisplayUI.Hide();

            if (!visible)
            {
                // 河は打牌フェイズ側で出すので、隠すときだけ触る
                if (gameUIManager.RiverUI != null)
                    gameUIManager.RiverUI.gameObject.SetActive(false);
                if (gameUIManager.EnemyRiverUI != null)
                    gameUIManager.EnemyRiverUI.gameObject.SetActive(false);
                if (gameUIManager.AbilityUI != null)
                    gameUIManager.AbilityUI.gameObject.SetActive(false);
                if (gameUIManager.WaitUI != null)
                    gameUIManager.WaitUI.gameObject.SetActive(false);
            }

            ApplyHpToUI();
        }

    }
}
