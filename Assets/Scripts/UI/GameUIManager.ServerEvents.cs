using UnityEngine;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    // サーバーから届くイベントの受け口と、ロン待ちパネルまわり。GameUIManager から分離（partial）。
    // クラス・namespace・[SerializeField] は変えていないのでシーン参照には影響しない。
    public partial class GameUIManager
    {
        private void HandleStatusReceived(KillingMahjong.EngineData.StatusData data)
        {
            if (yakuListUI != null)
            {
                yakuListUI.UpdateBoostData(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus);
            }
        }

        private void HandleOpeningBoostAssigned()
        {
            if (yakuListUI != null)
            {
                yakuListUI.UpdateBoostData(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus);
            }
        }

        private bool _isAgariPending = false;

        private void HandleAgariPendingReceived(KillingMahjong.EngineData.AgariPendingData data)
        {
            Debug.Log($"[GameUIManager] HandleAgariPendingReceived called. winner_id: {data.winner_id}, loser_id: {data.loser_id}, tile: {data.tile}");

            if (data.winner_id == NetworkMessageHandler.Instance.LocalPlayerId)
            {
                // 保留するかどうかに関わらず、自動打牌だけは先に止める。
                // AutoDiscardController は RonWaitPanel の表示有無でロン猶予を判定しているので、
                // パネルを出す前に保留すると、その隙に自動で打ってロンを取り逃す。
                var autoDiscard = GetComponent<AutoDiscardController>();
                if (autoDiscard != null) autoDiscard.CancelAutoDiscard();

                // 賭け金演出などの最中にロン猶予が届くと、演出を突き抜けてロンボタンだけが先に出る。
                // サーバーはロン入力を待ち続ける（手番のタイムアウトは無い）ので、
                // 演出が明けてから出しても取りこぼしにはならない。
                if (IsBusyWithTransition)
                {
                    DeferUntilIdle("agariPending", () => HandleAgariPendingReceived(data));
                    return;
                }

                if (BoardStateManager.Instance.NonManganWaitTiles.Contains(data.tile))
                {
                    Debug.Log($"[GameUIManager] Ignored agari_pending because tile {data.tile} is non-mangan.");
                    SendActionToServer("agari", new KillingMahjong.Network.ActionPayload { accept = false });
                    return;
                }

                Debug.Log("[GameUIManager] I am the winner! Showing RonWaitPanel.");
                _isAgariPending = true;

                ShowRonWaitPanel();
            }
        }

        /// <summary>
        /// ロン待ちパネルを最前面に出す。
        /// 対局とチュートリアルで同じボタンを見せたいので、両方からここを通す。
        /// </summary>
        private void ShowRonWaitPanel()
        {
            if (RonWaitPanel == null) return;

            RonWaitPanel.SetActive(true);
            RonWaitPanel.transform.SetAsLastSibling();

            // **画面を落として、ロンできる牌だけを光らせる**（2026-10-04、`ロン演出.pdf`）。
            // 光らせるのは敵がいま捨てた牌＝敵の河のいちばん最後。
            // 暗幕はクリックを吸わないので、ロンボタンはこれまでどおり押せる。
            Effects.RonChanceEffect.Show(EnemyRiverUI != null ? EnemyRiverUI.LastTile : null);

            // 最前面に表示するためにCanvasを追加してソート順を強制する
            Canvas canvas = RonWaitPanel.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = RonWaitPanel.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.RonWaitPanel;

            if (RonWaitPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                RonWaitPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            var images = RonWaitPanel.GetComponentsInChildren<UnityEngine.UI.Image>();
            foreach (var img in images)
            {
                if (img.GetComponent<UnityEngine.UI.Button>() == null &&
                    !img.gameObject.name.ToLower().Contains("button"))
                {
                    var c = img.color;
                    c.a = 0.1f;
                    img.color = c;
                }
            }
        }

        private System.Action _tutorialRonCallback;

        /// <summary>
        /// チュートリアルで、対局とまったく同じロンボタンを出して押されるのを待つ。
        /// サーバーには何も送らず、押されたら渡されたコールバックを呼ぶだけ。
        /// </summary>
        public void ShowRonWaitPanelForTutorial(System.Action onPressed)
        {
            _tutorialRonCallback = onPressed;
            ShowRonWaitPanel();
        }

        private void HandleTurnChanged(bool isLocalTurn)
        {
            PhaseController?.HandleDiscardTurnChanged(); // 演出中の手番変更も記録する。
            if (IsTransitioning) return; // アニメーション演出中は矢印を消さない
            if (wallUI != null)
            {
                wallUI.UpdateDiscardTurnIndicator(isLocalTurn, currentPhaseStatus == RoundStatus.Discard);
            }

            // 手番の側の体力表示を光らせ直す。
            // UpdateTurnIndicatorVisibility はフェイズ・演出の切り替えでしか呼ばれないので、
            // 打牌フェイズ中の手番交代はここで拾う
            UpdateTurnIndicatorVisibility();
        }

        // Methods invoked via Unity Events (e.g. Inspector Buttons)
        public void ExecuteRonAction()
        {
            Debug.Log($"[GameUIManager] ExecuteRonAction called. _isAgariPending={_isAgariPending}");

            // 暗幕と牌の点滅をここで畳む。押したのに光ったままだと、
            // 「まだ押せていない」のか分からない
            Effects.RonChanceEffect.HideCurrent();

            // チュートリアルはサーバーに繋がっていないので、進行役へ返すだけ
            if (_tutorialRonCallback != null)
            {
                var cb = _tutorialRonCallback;
                _tutorialRonCallback = null;
                if (RonWaitPanel != null) RonWaitPanel.SetActive(false);
                StartCoroutine(RonImpactThen(cb));
                return;
            }

            if (_isAgariPending)
            {
                _isAgariPending = false;
                if (RonWaitPanel != null) RonWaitPanel.SetActive(false);
                StartCoroutine(RonImpactThen(() =>
                {
                    SendActionToServer("agari", new KillingMahjong.Network.ActionPayload { accept = true });
                    Debug.Log("[GameUIManager] Sent 'agari' action to server. Waiting for server response to play animation.");
                }));
                return; // サーバーからの確定（役のデータ等）を待ってからアニメーションを再生するため、ここでは抜ける
            }

            PhaseController?.ExecuteRonAction();
        }

        /// <summary>
        /// 手の三段階を見せ、指差しのあとに吹き出しと体力表示だけを揺らして次へ渡す。
        ///
        /// サーバーへはこの短い入力演出の後に送る。返答で始まる既存の決着カットインと
        /// 重ならず、入力が受理されたことも盤面上で読み取れる。
        /// </summary>
        private System.Collections.IEnumerator RonImpactThen(System.Action next)
        {
            var hpPanels = new System.Collections.Generic.List<RectTransform>();
            if (PlayerInfoUI != null && PlayerInfoUI.HpAnchor != null) hpPanels.Add(PlayerInfoUI.HpAnchor);
            if (EnemyInfoUI != null && EnemyInfoUI.HpAnchor != null) hpPanels.Add(EnemyInfoUI.HpAnchor);

            yield return Effects.RonImpactEffect.Play(this, hpPanels);

            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayVoice(KillingMahjong.Managers.AudioManager.Instance.ronVoice);
            }

            next?.Invoke();
        }
    }
}
