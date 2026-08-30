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
        // TutorialManager: プレイヤー操作の受け口（GameUIManager / HandUI からのコールバック）

        // ==================== UIからのコールバック ====================

        /// <summary>山牌と手牌の間で牌を動かせるか（手順①）。</summary>
        public bool OnTryMoveTile(int tileId, bool toHand)
        {
            if (_round == null) return false;

            // セリフの送り待ち中は触らせない。
            // ここで ShowInterruptMessage を出すと DialogueUI の本文を上書きして
            // 送りボタンの行が消え、進行が止まるので黙って弾く。
            if (_isWaitingForLine) return false;

            if (!_round.allowManualHandSelection)
            {
                ShowInterruptMessage("今は『自動』ボタンを押してね。");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 手牌決定を受け付けるか（手順②③）。
        ///
        /// チュートリアルはサーバーに接続しないため満貫判定を持たない。
        /// 「最初の決定は必ず弾く」スクリプトで手順②を再現し、
        /// 制約『満貫手以下での開始は不可』は requireAutoManganToConfirm で担保する。
        /// </summary>
        public bool OnTryCompleteHandSelection()
        {
            if (_round == null) return false;

            // 押された時点でもう一度手牌を見る。13枚そろった瞬間の判定だけだと、
            // そのあと牌を入れ替えて満貫手に直しても決定できないままになる。
            if (!HasClickedAutoMangan && IsSelfMadeManganHand())
            {
                HasClickedAutoMangan = true;
            }

            if (_round.rejectFirstConfirm && !_hasRejectedFirstConfirm && !HasClickedAutoMangan)
            {
                _hasRejectedFirstConfirm = true;
                ShowInterruptMessage("その手じゃ満貫にも届かないわ。満貫手以上じゃないと始められないのよ。");
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.AutoManganButtonRect : null);
                return false;
            }

            if (_round.requireAutoManganToConfirm && !HasClickedAutoMangan)
            {
                ShowInterruptMessage("『自動』ボタン（オート満貫）を押して、満貫手を作ってね。");

                // 自由に組ませる局では矢印を出さない（自力でも自動でもよいので誘導しない）
                if (!_round.freeHandBuilding)
                {
                    GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                        ? gameUIManager.HandUI.AutoManganButtonRect : null);
                }
                return false;
            }

            // 実際の待機解除は ConfirmationDialogUI での決定後（ConfirmHandSelectionComplete）
            return true;
        }

        /// <summary>
        /// 手牌の決定（確認ダイアログの「決定」）まで済んだか。
        ///
        /// 待ち牌UIを出してよいかの判定に使う。`おまかせ` を押した時点では false のままにしたい。
        /// </summary>
        public bool IsHandSelectionConfirmed => !_isWaitingForHandSelectionComplete;

        public void ConfirmHandSelectionComplete()
        {
            _isWaitingForHandSelectionComplete = false;

            // **ここで初めて待ち牌UIを出す。**
            // 手牌選択フェイズのままなので、この後フェイズ変化は起きない。
            // GameUIPhaseController 側の表示処理は決定前に一度走って抑止されているため、
            // 誰かが明示的に出さないと打牌フェイズまで出ないままになる。
            ShowWaitUiIfReady();
        }

        /// <summary>
        /// 待ち牌が分かっていれば待ち牌UIを出す。
        ///
        /// `おまかせ` を押した直後に出すと、手牌確認のUIと左下で重なる。
        /// 決定を押したあとまで待たせるため、表示のきっかけをここに集約している。
        /// </summary>
        private void ShowWaitUiIfReady()
        {
            if (gameUIManager == null || gameUIManager.WaitUI == null) return;

            var board = BoardStateManager.Instance;
            if (board == null || board.CurrentWaitTiles == null || board.CurrentWaitTiles.Count == 0) return;

            gameUIManager.WaitUI.gameObject.SetActive(true);
            gameUIManager.WaitUI.DisplayWaits(board.CurrentWaitTiles);
        }

        /// <summary>打牌を受け付けるか（手順⑭の禁止牌を含む）。</summary>
        public bool OnTryDiscardTile(int tileId)
        {
            if (!_isWaitingForDiscard) return false;
            if (_round == null) return false;

            int baseId = TutorialTiles.BaseOf(tileId);

            if (_round.lockedTileBaseId >= 0 && baseId == _round.lockedTileBaseId)
            {
                ShowInterruptMessage(_round.lockedTileMessage);
                return false;
            }

            _lastPlayerDiscardBaseId = baseId;
            _isWaitingForDiscard = false;
            return true;
        }

        /// <summary>オート満貫ボタン（手順④）。台本の手牌を盤面へ流し込む。</summary>
        public void ApplyMockAutoMangan()
        {
            if (_round == null) return;
            if (HasClickedAutoMangan) return;

            HasClickedAutoMangan = true;

            SetupBoard(_round, _round.manganHandBaseIds);

            if (gameUIManager != null) gameUIManager.ClearSelection();

            // ここで初めて決定ボタンを出す
            SetHandButtonStage(HandButtonStage.AutoAndDecide);

            // 次は決定ボタンへ誘導する（自由に組ませる局では誘導しない）
            if (_isWaitingForHandSelectionComplete && !_round.freeHandBuilding)
            {
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.DecideButtonRect : null);
            }
        }
    }
}
