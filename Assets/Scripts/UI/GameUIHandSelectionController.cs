using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUIHandSelectionController : MonoBehaviour
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
        private bool _ignoreNextRankResponse;
        private HandRankCallUI _rankCallUI;

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
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;
            if (uiManager.DialogueUI != null && uiManager.DialogueUI.IsLogOpen) return;

            StopInstantRankCallForSubmission();
            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

            if (!TryCaptureCurrentHandSelection(out _pendingHandIndexes, out _pendingHandTiles)) return;

            if (uiManager.IsTutorialMode)
            {
                // チュートリアル用のモックを直接返す（サーバー通信をスキップ）。
                //
                // 台本の手牌（オート満貫）を組んだときだけ聴牌として応答し、
                // プレイヤーが自由に選んだ13枚に対してはノーテンを返す。
                // これにより手順②「判定ではじかれる」が正しく成立する。
                var tm = uiManager.TutorialManager;
                var waitIds = tm != null ? tm.GetCurrentWaitTileIds() : new List<int>();

                var waitList = new List<WaitData>();
                if (tm != null)
                {
                    string[] yaku = tm.CurrentHandYaku.ToArray();
                    foreach (int tileId in waitIds)
                    {
                        waitList.Add(new WaitData
                        {
                            tile = tileId,
                            yaku = yaku,
                            han = tm.CurrentHandHan,
                            mangan_or_more = true
                        });
                    }
                }

                HandleIsTenpaiReceived(new IsTenpaiData { waits = waitList.ToArray() });
            }
            else
            {
                uiManager.SendActionToServer("is_tenpai", new KillingMahjong.Network.ActionPayload { wall_indexes = _pendingHandIndexes });
            }
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
            // **出ている役名は途中で消さない。**（2026-09-20）
            // 牌を1枚戻して13枚を割ったときに消していたが、仕様書のフロー図で
            // 表示が消えるのは「2秒後のフェードアウト」と「次の役名が出るとき」だけ。
            if (!CanShowInstantRankCall())
            {
                _hasRankResult = false;
                return;
            }

            if (!TryCaptureCurrentHandSelection(out List<int> currentIndexes, out _))
            {
                _hasRankResult = false;
                return;
            }

            if (_rankRequestInFlight)
            {
                if (!SameHandIndexes(currentIndexes, _rankRequestIndexes)) _hasRankResult = false;
                return;
            }

            if (_hasRankResult && SameHandIndexes(currentIndexes, _rankResultIndexes)) return;

            _rankRequestIndexes = new List<int>(currentIndexes);
            _rankRequestInFlight = true;
            _hasRankResult = false;
            // 待っている間は何も出さない。フロー図に「確認中」の表示は無い
            uiManager.SendActionToServer("is_tenpai", new KillingMahjong.Network.ActionPayload
            {
                wall_indexes = _rankRequestIndexes
            });
        }

        private bool CanShowInstantRankCall()
        {
            return uiManager != null
                && !uiManager.IsTutorialMode
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

            GetRankCallUI().ShowRank(rank);
        }

        private void StopInstantRankCallForSubmission()
        {
            if (_rankRequestInFlight)
            {
                // 直後に確定用の is_tenpai をもう一度送る。先に返るプレビュー応答だけを
                // 捨て、確定用の応答は従来どおり確認ダイアログへ渡す。
                _ignoreNextRankResponse = true;
                _rankRequestInFlight = false;
            }
            _hasRankResult = false;
            if (_rankCallUI != null) _rankCallUI.HideImmediate();
        }

        private bool TryHandleInstantRankCall(IsTenpaiData data)
        {
            if (_ignoreNextRankResponse)
            {
                _ignoreNextRankResponse = false;
                return true;
            }
            if (!_rankRequestInFlight) return false;

            _rankRequestInFlight = false;
            if (!CanShowInstantRankCall()
                || !TryCaptureCurrentHandSelection(out List<int> currentIndexes, out _))
            {
                // 返事が来た時にはもう13枚ではない。出ているものはそのまま消えさせる
                return true;
            }

            if (!SameHandIndexes(currentIndexes, _rankRequestIndexes))
            {
                UpdateInstantRankCallForCurrentHand();
                return true;
            }

            _rankResultIndexes = new List<int>(currentIndexes);
            _hasRankResult = true;
            ShowRankCall(data != null ? data.waits : null);
            return true;
        }

        private bool TryHandleInstantNotTenpai(string reason)
        {
            if (_ignoreNextRankResponse)
            {
                _ignoreNextRankResponse = false;
                return true;
            }
            if (!_rankRequestInFlight) return false;

            _rankRequestInFlight = false;
            if (!CanShowInstantRankCall()
                || !TryCaptureCurrentHandSelection(out List<int> currentIndexes, out _))
            {
                // 返事が来た時にはもう13枚ではない。出ているものはそのまま消えさせる
                return true;
            }

            if (!SameHandIndexes(currentIndexes, _rankRequestIndexes))
            {
                UpdateInstantRankCallForCurrentHand();
                return true;
            }

            _rankResultIndexes = new List<int>(currentIndexes);
            _hasRankResult = true;
            // 聴牌していないなら、フロー図どおり「なにもしない」
            return true;
        }


        public void CancelHandSelection()
        {
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;
            if (uiManager.IsTransitioning) return;
            // ボタン側でも隠しているが、連打で滑り込まれると手牌が消えるのでここでも弾く
            if (IsSelectionLockedIn) return;

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
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;

            // **応答を待っている間に「選び直す」を押されていたら、もう出さない。**
            // `is_tenpai` はサーバーとの往復なので、その間も取り下げは押せる。
            // ここで確認ダイアログを出すと、取り下げ済みの
            // `_pendingHandIndexes`（＝画面の手牌とは違うかもしれない）を
            // OK が送ってしまう。
            if (uiManager.HandUI != null && !uiManager.HandUI.IsSubmitted) return;

            // 役名はここには並べない。牌にカーソルを合わせたときだけ
            // ConfirmationDialogUI がオーバーレイで出す（要望18）。
            string message = "【待ち牌】\n";
            int[] waitTileIds = new int[0];
            ConfirmationDialogUI.WaitInfo[] waitInfos = new ConfirmationDialogUI.WaitInfo[0];

            if (data.waits != null && data.waits.Length > 0)
            {
                // 牌そのものは ConfirmationDialogUI が並べるので、ここは場所を空けるだけ
                message += "\n\n\n\n";

                var ids = new System.Collections.Generic.List<int>();
                var infos = new System.Collections.Generic.List<ConfirmationDialogUI.WaitInfo>();
                foreach (var wait in data.waits)
                {
                    ids.Add(wait.tile);

                    string yakuText = (wait.yaku != null && wait.yaku.Length > 0) ? string.Join(" / ", wait.yaku) : "役なし";

                    // 翻数(han)とmangan_or_moreに基づいてランクを決定する。
                    // 「以上」は付けない（要望18）。上限の役名がそのまま出るほうが読みやすい。
                    string rankText = "満貫未満";
                    if (wait.yaku != null && System.Array.Exists(wait.yaku, y => y.Contains("役満"))) rankText = "役満";
                    else if (wait.han >= 13) rankText = "数え役満";
                    else if (wait.han >= 11) rankText = "三倍満";
                    else if (wait.han >= 8) rankText = "倍満";
                    else if (wait.han >= 6) rankText = "跳満";
                    else if (wait.han >= 5 || wait.mangan_or_more) rankText = "満貫";

                    infos.Add(new ConfirmationDialogUI.WaitInfo
                    {
                        TileId = wait.tile,
                        YakuText = yakuText,
                        RankText = rankText,
                    });
                }
                waitTileIds = ids.ToArray();
                waitInfos = infos.ToArray();

                message += "\n牌にカーソルを合わせると手牌と役が出ます";
            }
            message += "\n\nこの手牌で決定しますか？";

            // WaitUI を中央へ移す処理は廃止（ConfirmationDialogUI が中に待ち牌を出す）。
            // **2026-08-29 に WaitUI.MoveToCenter / MoveToOriginalPosition ごと削除した。**
            if (uiManager.ConfirmationDialogUI != null)
            {
                uiManager.ConfirmationDialogUI.ShowDialogWithWaits(
                    message,
                    waitInfos,
                    waitTileIds,
                    () => {
                        if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                        _autoConfirmNextHandSelection = true;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                        if (uiManager.PhaseTransitionUI != null)
                        {
                            uiManager.SetIsTransitioning(true);
                            uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                            {
                                uiManager.SetIsTransitioning(false);
                                
                                // 手牌決定演出が終わったタイミングで、左下のプレイヤー情報UIに待ち牌を表示する
                                BoardStateManager.Instance.SetLocalState(null, null, new System.Collections.Generic.List<int>(waitTileIds));
                                BoardStateManager.Instance.FireRebuildEvent();

                                if (uiManager.IsTutorialMode && uiManager.TutorialManager != null)
                                {
                                    uiManager.TutorialManager.ConfirmHandSelectionComplete();
                                }
                                else
                                {
                                    uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                                }
                            });
                        }
                        else
                        {
                            BoardStateManager.Instance.SetLocalState(null, null, new System.Collections.Generic.List<int>(waitTileIds));
                            BoardStateManager.Instance.FireRebuildEvent();

                            if (uiManager.IsTutorialMode && uiManager.TutorialManager != null)
                            {
                                uiManager.TutorialManager.ConfirmHandSelectionComplete();
                            }
                            else
                            {
                                uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                            }
                        }
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
                        BoardStateManager.Instance.ClearWaitTiles();
                        if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
                    }
                );
            }
            else
            {
                if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                _autoConfirmNextHandSelection = true;
                if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                if (uiManager.PhaseTransitionUI != null)
                {
                    uiManager.SetIsTransitioning(true);
                    uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                    {
                        uiManager.SetIsTransitioning(false);
                        uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                    });
                }
                else
                {
                    uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                }
            }
        }

        public void HandleNotTenpaiReceived(string reason)
        {
            if (TryHandleInstantNotTenpai(reason)) return;
            if (uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return;

            string message = $"ノーテン（聴牌していません）\n\nこのまま決定しますか？";
            if (uiManager.ConfirmationDialogUI != null)
            {
                uiManager.ConfirmationDialogUI.ShowDialog(
                    message,
                    () => {
                        if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                        _autoConfirmNextHandSelection = true;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                        if (uiManager.PhaseTransitionUI != null)
                        {
                            uiManager.SetIsTransitioning(true);
                            uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                            {
                                uiManager.SetIsTransitioning(false);
                                uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                            });
                        }
                        else
                        {
                            uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                        }
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(false);
                        if (uiManager.PhaseController != null) uiManager.PhaseController.SetMatchUIVisibility(true);
                    }
                );
            }
            else
            {
                if (ReactionController.Instance != null) ReactionController.Instance.StopHandSelectionTimer(true);
                _autoConfirmNextHandSelection = true;
                if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);

                if (uiManager.PhaseTransitionUI != null)
                {
                    uiManager.SetIsTransitioning(true);
                    uiManager.PhaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                    {
                        uiManager.SetIsTransitioning(false);
                        uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                    });
                }
                else
                {
                    uiManager.SendActionToServer("select", new KillingMahjong.Network.ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                }
            }
        }

        public void HandleHandSelectionConfirmation(HandSelectionConfirmationData data)
        {
            _autoConfirmNextHandSelection = false;
            
            // 満貫未満の警告ダイアログを表示せず、自動で確定を送信する
            if (uiManager.HandUI != null) uiManager.HandUI.SetSubmittedState(true);
            uiManager.SendActionToServer("select_confirm", new KillingMahjong.Network.ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
        }

        public void OnHandSelectionAccepted()
        {
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
