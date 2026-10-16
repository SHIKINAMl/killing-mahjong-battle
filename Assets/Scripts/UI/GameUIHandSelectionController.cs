using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public partial class GameUIHandSelectionController : MonoBehaviour
    {
        private GameUIManager uiManager;

        private bool _autoConfirmNextHandSelection = false;
        public bool AutoConfirmNextHandSelection => _autoConfirmNextHandSelection;

        private List<int> _pendingHandIndexes;
        private List<int> _pendingHandTiles;

        // 即席の役名表示は確定処理とは別の、13枚選択中だけの問い合わせ。
        // 応答に request id はないため、送信時の山牌indexと現在の13枚を照合して
        // 選び直し後の古い応答を画面に出さない。
        private List<int> _rankRequestIndexes;
        private List<int> _rankResultIndexes;
        private bool _rankRequestInFlight;
        private bool _hasRankResult;
        private WaitData[] _rankWaits;
        private HandWaitRevealUI _waitReveal;
        private HandWeakConfirmationUI _weakConfirmation;
        private string _rankSignature, _rankRequestSignature;
        private int _rankRequestGeneration;
        private HandRankCallUI _rankCallUI;

        /// <summary>
        /// 役名表示を出してよいか。**自分の手で13枚目を入れたときだけ立つ**（2026-10-11 のユーザー指示）。
        ///
        /// それまでは「手牌が13枚で、まだ問い合わせていない並び」なら毎回出していた。
        /// 手牌の並べ直し（HandUI.UpdateLayout）は牌を入れたとき以外にも通るので、
        /// 選びなおしで手牌が戻ってきたときや、スキルを使ったあとにも役名が出ていた。
        ///
        /// 立てるのは <see cref="ArmInstantRankCall"/>（牌をクリックして手牌へ入れる経路）と、
        /// <see cref="RequestInstantRankCallNow"/>（「おまかせ」を押して13枚がそろったとき）。
        /// 選びなおし・スキル・サーバーからの組み直しは呼ばないので、出ない。
        /// 問い合わせを送ったら降ろす。13枚でなくなったとき・確定したときも降ろす。
        /// </summary>
        private bool _rankCallArmed;

        /// <summary>
        /// 自分の手で13枚目を入れる直前に呼ぶ。次の並べ直しで、役名の問い合わせを1回だけ通す。
        /// </summary>
        public void ArmInstantRankCall()
        {
            _rankCallArmed = true;
        }

        /// <summary>
        /// 「おまかせ」で13枚がそろった直後に呼ぶ。役名の問い合わせを、その場で1回通す
        /// （2026-10-11 のユーザー指示「おまかせでも反応するようにして」）。
        /// おまかせは牌を1枚ずつ動かすので、動かす前に印を立てても、13枚に届く前に降りてしまう。
        /// だから、そろったあとに呼ぶ。同じ並びを前に問い合わせていても、押したのだからもう一度出す。
        /// </summary>
        public void RequestInstantRankCallNow()
        {
            _hasRankResult = false;
            _rankCallArmed = true;
            UpdateInstantRankCallForCurrentHand();
        }

        /// <summary>入れようとした牌が入らなかったときに呼ぶ。</summary>
        public void DisarmInstantRankCall()
        {
            _rankCallArmed = false;
        }

        /// <summary>
        /// もう取り下げられないか。**相手を待っている間は取り下げてよい**（`select_cancel` は
        /// そのためにある）。手遅れになるのは相手も確定して掛け金フェイズへ移る直前だけ。
        /// 判定は `phase_completed_notice` で両者の確定を知る PhaseController に持たせている。
        /// </summary>
        public bool IsSelectionLockedIn =>
            uiManager != null && uiManager.PhaseController != null
            && uiManager.PhaseController.IsHandSelectionSettledForBoth;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
            _waitReveal = HandWaitRevealUI.Ensure(manager.transform);
            _waitReveal.BindHoverTargets(manager.HandUI);
            _waitReveal.Hide();
            _weakConfirmation = HandWeakConfirmationUI.Ensure(manager.transform, manager.HandUI != null ? manager.HandUI.DecideButtonRect : null);
            _weakConfirmation.Hide();
            SceneFirst.Report(nameof(HandWaitRevealUI));
        }

        private void OnDestroy()
        {
            // HandRankCallUI は既存Canvasの座標を継承しない独立Canvas。
            // コントローラだけが途中で破棄される場合にも残さない。
            if (_rankCallUI != null)
            {
                Destroy(_rankCallUI.gameObject);
            }
        }

        public void CompleteHandSelection()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection
                || _confirming || uiManager.IsTransitioning || (uiManager.HandUI != null && uiManager.HandUI.IsSubmitted)) return;
            if (uiManager.DialogueUI != null && uiManager.DialogueUI.IsLogOpen) return;
            if (!TryCaptureCurrentHandSelection(out _pendingHandIndexes, out _pendingHandTiles)) return;
            var cached = _hasRankResult && SameHandIndexes(_pendingHandIndexes, _rankResultIndexes)
                && _rankSignature == CurrentSelectionSignature()
                ? _rankWaits : null;
            submissionVersion++;
            StopInstantRankCallForSubmission();
            BeginImmediateConfirmation();
            if (uiManager.IsTutorialMode)
                ResolveImmediateConfirmation(BuildTutorialWaits());
            else if (cached != null)
                ResolveImmediateConfirmation(new IsTenpaiData { waits = cached });
            else if (!_rankRequestInFlight || !SameHandIndexes(_pendingHandIndexes, _rankRequestIndexes)
                || _rankRequestSignature != CurrentSelectionSignature()
                || _rankRequestGeneration != (uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0))
                SendTenpaiProbe(_pendingHandIndexes, true);
            // 同じ13枚の問い合わせが進行中なら、その返事を確定用にも使う。二重に送らない。
        }

        /// <summary>
        /// 手牌の増減後に HandUI.UpdateLayout から呼ばれる。
        /// 本編ではサーバーへ問い合わせ、返ってきた待ちの中で**いちばん上の格**を役名として出す
        /// （2026-09-20 の仕様書「即席満貫以上判定システム」のフロー図）。
        /// チュートリアルにはサーバーが存在しないため、誤って通信エラーを出さないよう出さない
        /// （確定時の既存チュートリアル処理は変更しない）。
        /// </summary>
        public void UpdateInstantRankCallForCurrentHand()
        {
            if (_confirming) return; // 提出状態のレイアウト更新で、移送中の候補を消さない。
            if (uiManager != null && uiManager.IsTutorialMode && _rankCallArmed
                && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection
                && TryCaptureCurrentHandSelection(out List<int> tutorialIndexes, out _))
            {
                _rankCallArmed = false;
                _rankResultIndexes = tutorialIndexes;
                _rankSignature = CurrentSelectionSignature();
                _rankWaits = BuildTutorialWaits().waits;
                _hasRankResult = true;
                ShowRankCall(_rankWaits);
                return;
            }
            // **出ている役名は途中で消さない。**（2026-09-20）
            // 牌を1枚戻して13枚を割ったときに消していたが、仕様書のフロー図で
            // 表示が消えるのは「2秒後のフェードアウト」と「次の役名が出るとき」だけ。
            if (!CanShowInstantRankCall())
            {
                _hasRankResult = false;
                _rankWaits = null;
                _waitReveal?.Hide();
                _rankCallArmed = false;
                // **手牌選択を抜けたら片付ける。** 13枚を割っただけなら消さない（自分の2秒で消える）が、
                // フェイズが変わったら役名は用済み。残すと決着画面まで出続ける（2026-09-20）
                if (uiManager != null && uiManager.CurrentPhaseStatus != RoundStatus.HandSelection
                    && _rankCallUI != null) _rankCallUI.HideImmediate();
                return;
            }

            if (!TryCaptureCurrentHandSelection(out List<int> currentIndexes, out _))
            {
                _hasRankResult = false;
                return;
            }

            if (_waitReveal != null && _hasRankResult && !SameHandIndexes(currentIndexes, _rankResultIndexes)) _waitReveal.Hide();
            if (_hasRankResult && _rankSignature != CurrentSelectionSignature())
            {
                _hasRankResult = false; _rankWaits = null; _waitReveal?.Hide();
            }
            if (_rankRequestInFlight)
            {
                if (!SameHandIndexes(currentIndexes, _rankRequestIndexes)) _hasRankResult = false;
                return;
            }

            if (_hasRankResult && SameHandIndexes(currentIndexes, _rankResultIndexes)
                && _rankSignature == CurrentSelectionSignature()) return;

            // **自分の手で13枚目を入れたときだけ問い合わせる。**
            // 選びなおしで戻ってきた13枚や、スキルのあとの13枚では出さない
            if (!_rankCallArmed) return;
            _rankCallArmed = false;

            _rankRequestIndexes = new List<int>(currentIndexes);
            _rankRequestSignature = CurrentSelectionSignature();
            _rankRequestGeneration = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            _rankRequestInFlight = true;
            _hasRankResult = false;
            // 待っている間は何も出さない。フロー図に「確認中」の表示は無い
            SendTenpaiProbe(_rankRequestIndexes, false);
        }

        private bool CanShowInstantRankCall()
        {
            return uiManager != null
                && uiManager.CurrentPhaseStatus == RoundStatus.HandSelection
                && (uiManager.HandUI == null || !uiManager.HandUI.IsSubmitted)
                && BoardStateManager.Instance != null
                && BoardStateManager.Instance.CurrentHandTiles != null
                && BoardStateManager.Instance.CurrentHandTiles.Count == 13;
        }

        private bool TryCaptureCurrentHandSelection(out List<int> handIndexes, out List<int> handTiles)
        {
            handIndexes = new List<int>();
            handTiles = new List<int>();

            if (BoardStateManager.Instance == null || BoardStateManager.Instance.CurrentHandTiles == null)
            {
                return false;
            }

            handTiles = new List<int>(BoardStateManager.Instance.CurrentHandTiles);
            if (handTiles.Count != 13) return false;

            var board = BoardStateManager.Instance;
            if (BoardStateManager.HandIndexesMatch(board.OriginalWallTiles, handTiles, board.LocalHandIndexes))
            {
                handIndexes = new List<int>(board.LocalHandIndexes);
                handIndexes.Sort();
                return true;
            }

            if (BoardStateManager.Instance.TargetHandIndexes != null
                && BoardStateManager.Instance.TargetHandIndexes.Count == 13)
            {
                handIndexes = new List<int>(BoardStateManager.Instance.TargetHandIndexes);
                return true;
            }

            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles == null) return false;

            HashSet<int> usedIndexes = new HashSet<int>();
            foreach (int tileId in handTiles)
            {
                int index = -1;
                for (int i = 0; i < wallTiles.Count; i++)
                {
                    if (wallTiles[i] == tileId && !usedIndexes.Contains(i))
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0) return false;
                handIndexes.Add(index);
                usedIndexes.Add(index);
            }

            return handIndexes.Count == 13;
        }

        private static bool SameHandIndexes(List<int> first, List<int> second)
        {
            if (first == null || second == null || first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                if (first[i] != second[i]) return false;
            }
            return true;
        }

        private HandRankCallUI GetRankCallUI()
        {
            if (_rankCallUI == null)
            {
                _rankCallUI = HandRankCallUI.Create();
            }
            return _rankCallUI;
        }

        /// <summary>
        /// 待ちの一覧から、**届きうる中でいちばん上の格**を役名として出す（2026-09-20 の仕様書）。
        ///
        /// 満貫に届かないなら何も出さない（フロー図の「if それが満貫以上であるか → NO → なにもしない」）。
        /// 倍率は 満貫1 / 跳満1.5 / 倍満2 / 三倍満3 / 役満4（<see cref="Managers.GameRules.GetMultiplier"/>）。
        /// **フロー図は三倍満を分けていない**ので、3倍は「倍満以上」に含めて倍満と出す。
        /// </summary>
        private void ShowRankCall(EngineData.WaitData[] waits)
        {
            // 待ち牌は役名のスタンプと独立。満貫未満・待ちなしも表示できる。
            _waitReveal?.Show(waits ?? System.Array.Empty<WaitData>(), uiManager.TileResourceManager, false);
            if (waits == null || waits.Length == 0) return;

            float best = 0f;
            bool manganOrMore = false;
            for (int i = 0; i < waits.Length; i++)
            {
                if (waits[i] == null) continue;
                if (waits[i].mangan_or_more) manganOrMore = true;
                if (waits[i].multiplier > best) best = waits[i].multiplier;
            }

            if (!manganOrMore) return;

            string rank;
            if (best >= 4f) rank = "役満";
            else if (best >= 2f) rank = "倍満";
            else if (best >= 1.5f) rank = "跳満";
            else rank = "満貫";

            // **役名表示は、カットしてよい演出として順番待ちへ入れる**（2026-10-11）。
            // ほかの演出（スキルなど）が流れていれば終わるのを待ち、出ている最中にほかの演出が来たら消える。
            // 順番が来たときに手牌が変わっていたら、もう出さない
            var shownFor = _rankResultIndexes != null ? new List<int>(_rankResultIndexes) : null;
            var shownSignature = _rankSignature;
            var queue = Effects.EffectQueue.Instance;
            if (queue == null || uiManager.IsTutorialMode)
            {
                GetRankCallUI().ShowRank(rank);
                // 候補は上で構築済み。同じ牌を消して再登場させない。
                return;
            }
            queue.Enqueue("rank-call", () => ShowRankCallRoutine(rank, waits), cuttable: true,
                onCut: () => { if (_rankCallUI != null) _rankCallUI.HideImmediate(); },
                stillValid: () => this != null && CanShowInstantRankCall()
                    && shownSignature == CurrentSelectionSignature()
                    && TryCaptureCurrentHandSelection(out List<int> now, out _)
                    && (shownFor == null || SameHandIndexes(now, shownFor)));
        }

        private System.Collections.IEnumerator ShowRankCallRoutine(string rank, WaitData[] waits)
        {
            var ui = GetRankCallUI();
            ui.ShowRank(rank);
            // 順番待ちの前に表示した候補は、そのまま保つ。
            if (_waitReveal != null && !_waitReveal.HasCandidates(waits))
                _waitReveal.Show(waits, uiManager.TileResourceManager);
            while (ui != null && ui.IsShowing) yield return null;
        }

        private void StopInstantRankCallForSubmission()
        {
            _rankCallArmed = false;
            if (_rankCallUI != null) _rankCallUI.HideImmediate();
        }

        private bool TryHandleInstantRankCall(IsTenpaiData data) => HandleTenpaiProbe(data);
        private bool TryHandleInstantNotTenpai(string reason) =>
            HandleTenpaiProbe(new IsTenpaiData { waits = System.Array.Empty<WaitData>() });

        public void CancelHandSelection()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;
            if (uiManager.IsTransitioning) return;
            // ボタン側でも隠しているが、連打で滑り込まれると手牌が消えるのでここでも弾く
            if (IsSelectionLockedIn) return;
            CancelPendingConfirmation();
            _hasRankResult = false;
            _rankWaits = null;

            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            KillingMahjong.Managers.BoardStateManager.Instance.ClearWaitTiles();
            if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);

            // **手牌はそのまま残す。** 「選び直す」は確定を取り下げるだけで、
            // 選んだ牌まで捨てさせると、13枚を一から選び直すことになる。
            // 取り下げると SetSubmittedState(false) で IsSubmitted が false に戻るので、
            // ここから手牌の牌をクリックして山に返す／別の牌を足す、という調整ができる。
            //
            // **サーバーの select_cancel は player.hand を空にするが、それで問題ない。**
            // 選択中の増減はクライアント内だけの話で、サーバーに手牌が伝わるのは
            // 「決定」で select / select_confirm を送るときにまとめて一度だけ。
            // 次の決定で全13枚を送り直すので、ここで空になっていても食い違わない。
            uiManager.SendActionToServer("select_cancel", new KillingMahjong.Network.ActionPayload());
        }

        public void HandleIsTenpaiReceived(IsTenpaiData data)
        {
            if (TryHandleInstantRankCall(data)) return;
            if (uiManager.IsTutorialMode && _confirming) ResolveImmediateConfirmation(data);
        }

        public void HandleNotTenpaiReceived(string reason)
        {
            TryHandleInstantNotTenpai(reason);
        }

        public void HandleHandSelectionConfirmation(HandSelectionConfirmationData data)
        {
            HandleServerWeakHandConfirmation(data);
        }

        public void OnHandSelectionAccepted()
        {
            _serverConfirmationPending = false; _weakConfirmation?.Hide();
            _autoConfirmNextHandSelection = false;
            if (uiManager.HandUI != null && uiManager.HandUI.IsSubmitted) 
            {
                if (uiManager.DialogueUI != null) 
                {
                    string text = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.HandSelection) : null;
                    if (string.IsNullOrEmpty(text)) text = "相手の手牌選択を待っています...";
                    uiManager.DialogueUI.ShowText(text);
                }
            }
            if (uiManager.PhaseController != null) uiManager.PhaseController.HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
            if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
        }
    }
}
