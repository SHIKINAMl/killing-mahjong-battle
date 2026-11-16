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
            // 役強化のログに、強化した役を書き足す（2026-10-25）
            DialogueUI?.ResolveBoostLog(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus);
            // 強化が変わっていたら、13枚の役の予想を問い合わせ直す（2026-10-29）
            HandSelectionController?.RefreshRankCallAfterBoost();
        }

        private void HandleOpeningBoostAssigned()
        {
            if (yakuListUI != null)
            {
                yakuListUI.UpdateBoostData(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus);
            }
            // 局の初めに配られる強化はログに出さない。見た一覧として覚えておくだけ
            DialogueUI?.ResolveBoostLog(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus, openingBoosts: true);
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

                // **満貫未満の待ち（NonManganWaitTiles）でも、ここでは断らない。**
                // サーバーは満貫以上で上がれるとき（can_agari）にしか agari_pending を送らない。
                // 手牌を決めたときの判定には一発・河底が入っていないので、そこでは満貫未満だった待ちが
                // 和了のときに満貫に届くことがある。以前はその一覧で自動辞退していて、
                // 「当たり牌が出たのにロンできない」になっていた（2026-10-17）
                if (BoardStateManager.Instance.NonManganWaitTiles.Contains(data.tile))
                {
                    Debug.Log($"[GameUIManager] tile {data.tile} was non-mangan at hand selection, but the server allows agari (situational yaku). Showing RonWaitPanel.");
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

            YakuListUI?.CloseYakuList();
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

            if (IsRonInputPlaying) return;

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
                StartCoroutine(RonImpactThen(null, sendToServer: true));
                return; // サーバーからの確定（役のデータ等）を待ってからアニメーションを再生するため、ここでは抜ける
            }

            Effects.RonChanceEffect.HideCurrent();
            PhaseController?.ExecuteRonAction();
        }

        internal bool IsRonInputPlaying { get; private set; }
        internal bool RonResponseReceived { private get; set; }
        private PresentationScope ronInputScope;

        /// <summary>
        /// 手の三段階を見せ、指差しのあとに吹き出しと体力表示だけを揺らして次へ渡す。
        ///
        /// サーバーの確定は吹き出しが出た時点で送り、結果表示は入力演出が明けるまで待つ。
        /// </summary>
        private System.Collections.IEnumerator RonImpactThen(System.Action next, bool sendToServer = false)
        {
            var hpPanels = new System.Collections.Generic.List<RectTransform>();
            if (PlayerInfoUI != null && PlayerInfoUI.HpAnchor != null) hpPanels.Add(PlayerInfoUI.HpAnchor);
            if (EnemyInfoUI != null && EnemyInfoUI.HpAnchor != null) hpPanels.Add(EnemyInfoUI.HpAnchor);

            // 声「それ、ロン」は RonImpactEffect の中で、手振りと吹き出しに合わせて鳴らす（2026-10-15）。
            // 前はここ（演出を見せ終えた後）でまるごと鳴らしていて、「ロン」が大きく遅れていた
            // 押された雫のボタンの位置から、指先・弾ける所・吹き出しを揃える（2026-11-15）。
            // 本物のボタンは押した時点で隠し、演出の画面に押された絵を写して続ける
            var ronButton = RonWaitPanel != null ? RonWaitPanel.GetComponentInChildren<UnityEngine.UI.Button>(true) : null;
            IsRonInputPlaying = true;
            RonResponseReceived = false;
            System.Action onImpact = null;
            System.Func<bool> waitingForResponse = null;
            if (sendToServer)
            {
                onImpact = () => SendActionToServer("agari",
                    new KillingMahjong.Network.ActionPayload { accept = true });
                waitingForResponse = () => !RonResponseReceived;
            }

            using var scope = new PresentationScope(Debug.LogException);
            ronInputScope = scope;
            using (BeginTransition("ron-input"))
            {
                try
                {
                    yield return Effects.RonImpactEffect.Play(this, hpPanels,
                        ronButton != null ? (RectTransform)ronButton.transform : null,
                        onImpact, waitingForResponse, scope);
                }
                finally
                {
                    IsRonInputPlaying = false;
                    ronInputScope = null;
                }
            }
            if (scope.IsActive) next?.Invoke();
        }
    }
}
