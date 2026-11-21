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

        private TutorialRoundData _pendingRoundBoard;

        private void InitializeRoundBoard(TutorialRoundData data, bool preserveRoundCover = false)
        {
            if (IsFirstTutorialRound(data)) IsVoltageUiRevealed = false;
            else if (IsSecondTutorialRound(data)) IsVoltageUiRevealed = false;
            ClearPerspectiveMarks();
            SetHandButtonStage(HandButtonStage.Hidden);
            SetupBoard(data, null, preserveRoundCover);
            if (data.enemyUsesAbility) _abilityBellRevealed = true;
            SetAbilityBellVisible(_abilityBellRevealed);
        }

        private void SetupBoard(TutorialRoundData data, List<int> handBaseIds, bool preserveRoundCover = false)
        {
            _tutorialEnemyAssaultActive = false;
            if (gameUIManager == null || gameUIManager.HandUI == null || gameUIManager.WallUI == null) return;

            gameUIManager.IsTutorialMode = true;
            gameUIManager.TutorialManager = this;

            if (!preserveRoundCover && gameUIManager.PhaseTransitionUI != null)
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

                // **相手の山は、透視の実演がある第2局だけ伏せて置く**（2026-10-24 プランナー指摘「透視スキルの効果が
                // わかるようにチュートリアルでも相手の手牌があった方がいいかも」）。以前は相手の牌が画面に1枚も無く、
                // 透視を押してもめくれた牌が見えなかった。盤面ごと局の頭の暗転の間に出るので、画面に生えない。
                // 自分の山を除いた予備牌から置き、同種4枚を超えないようにする。ほかの局は今までどおり置かない。
                board.OriginalEnemyWallTiles.Clear();
                board.CurrentEnemyWallTiles.Clear();
                board.ExposedEnemyHandWallIndexes.Clear();
                if (IsSecondTutorialRound(data))
                {
                    var stock = TutorialSkillStock.Remaining(wall, null, data.doraBaseId);
                    foreach (int tile in TutorialTiles.EncodeAll(data.mulliganReplacementBaseIds, data.doraBaseId))
                        stock.Remove(tile);
                    board.SetEnemyState(TutorialSkillStock.Draw(stock, wall.Count, _introSkillRandom), new List<int>());
                }
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
        /// 台本以外の組み方を網羅する判定ではない。正解例の配牌・待ち・役を変更するときは、
        /// サーバーの判定で実際に満貫になることを確認し、台本の表示もそろえること。
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

        private static List<TutorialLine> ResolveSelfManganLines(TutorialRoundData data)
        {
            return data.onSelfManganLines;
        }

        private static List<TutorialLine> ResolveHandFilledLines(TutorialRoundData data)
        {
            return data.onHandFilledLines;
        }

        /// <summary>ボタンの開放段階を変えて、HandUI に即座に反映させる。</summary>
        private void SetHandButtonStage(HandButtonStage stage)
        {
            _handButtonStage = stage;

            if (gameUIManager != null && gameUIManager.HandUI != null)
                gameUIManager.HandUI.UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        /// <summary>
        /// 打牌フェイズへ入るときの黒帯（2026-09-28 の指示）。
        ///
        /// 本編と**同じ演出を同じ手順で**通す。`PhaseTransitionUI.PlayTransition` は
        /// 一本線 →「対局開始」→ 市松模様が画面を覆う、と進み、覆い切った時点で
        /// `onMidpoint` を呼ぶ。**フェイズの切り替えはそこで行う。**
        /// 先に切り替えると、盤面が組み替わる様子が黒幕の外で見えてしまう。
        ///
        /// 賭け額と血は本編ではサーバーから来るが、チュートリアルは繋がないので
        /// こちらの台帳（`_playerHp` / `_enemyHp` / `_lastBetAmount`）から作る。
        /// **null を渡してはいけない。** 演出の中で中身を読んでいる。
        ///
        /// 演出そのものが出せない場面（UIが無い等）では、**待たずにフェイズだけ進める。**
        /// ここで待つと、盤面が揃っていない入り方をしたときに止まってしまう。
        /// </summary>
        // ==================== 対局のあいだの曲（2026-10-23） ====================
        //
        // ユーザーが採用した対局用の曲（`tut_lesson_battle`、144 BPM）を、チュートリアルで
        // 実際に対局を進める区間にだけ流す。説明の区間は今までどおり `tut_lesson`（135 BPM）。
        //
        //   入る: 「対局開始」の黒帯が画面を覆い切ったところ（フェイズが打牌に切り替わるのと同時）
        //         DEADEND から対局へ戻ったところ
        //   戻る: 次の局の頭（手牌選択の説明に戻る）。**対局の曲が鳴っているときだけ。**
        //         第1局の頭は無音から立ち絵が浮かぶ作りなので、ここで曲を鳴らし始めない。
        //         続きから（第2局以降を指定して）始めたときは何も鳴っていないので、説明の曲を鳴らす
        //
        // 同じ曲を続けて指定しても頭出しはしない（AudioManager.PlayTutorialBgm）。
        // 台本で断ち切った（「BGM止まる」など）あとは、ここから勝手に鳴らし直さない。

        private const string TutorialLessonBgm = "tut_lesson";
        private const string TutorialBattleBgm = "tut_lesson_battle";

        private static void PlayTutorialBattleBgm()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayTutorialBgm(TutorialBattleBgm);
        }

        /// <param name="resumedRound">
        /// 第2局以降の頭か。局を指定して始め直したとき（続きから）は曲が何も鳴っていないので、説明の曲を鳴らす。
        /// 第1局の頭は台本の1行目の合図で鳴らすので、ここでは鳴らさない
        /// </param>
        private static void ReturnToTutorialLessonBgm(bool resumedRound = false)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (audio.IsTutorialBgmPlaying(TutorialBattleBgm) || (resumedRound && !audio.IsBgmPlaying))
                audio.PlayTutorialBgm(TutorialLessonBgm);
        }

        /// <param name="whileCovered">覆いきった間に一緒に出す物（第1局の体力・ゲージなど）。画面に物をぱっと出さないため（2026-10-24 ユーザー指示「チュートリアルの対局フェイズでUIをニョキって生やすのは止めてくれ。基本的に画面が暗転しているときにUIは全部出すように」）</param>
        private IEnumerator RunDiscardEntryTransition(System.Action whileCovered = null)
        {
            // **黒帯に入る前にセリフ欄を空にする**（2026-10-29 バグ報告「第二局開始時に前のセリフが残っている」
            // 「開始時は一旦セリフを空にして、始まった時にしゃべらせたい」）。
            // 賭け金の話のセリフが、明けたあとの対局の頭まで残っていた。次のセリフは明けてから出る
            if (dialogueUI != null) dialogueUI.HideText();

            var transition = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            if (transition != null && !transition.gameObject.activeSelf) transition.gameObject.SetActive(true);
            // 親ごと消えていると演出が始まらない。来ない合図を待たずに進める
            if (transition == null || !transition.isActiveAndEnabled)
            {
                whileCovered?.Invoke();
                SetPhase(RoundStatus.Discard);
                PlayTutorialBattleBgm();
                yield break;
            }

            int bet = _lastBetAmount > 0 ? _lastBetAmount : 0;
            var info = new EngineData.BettingCompletedInfo
            {
                LocalBet = bet,
                EnemyBet = _enemyStake,
                LocalHpBefore = _playerHpBeforeBet,
                EnemyHpBefore = _enemyHpBeforeBet,
                LocalHpAfter = PlayerShownHp,
                EnemyHpAfter = EnemyShownHp,
                HasServerHealth = true,
            };

            bool midpointDone = false;
            bool finished = false;
            transition.PlayTransition(
                "対局開始",
                gameUIManager.PlayerInfoUI,
                info,
                onMidpoint: () => { whileCovered?.Invoke(); SetPhase(RoundStatus.Discard); PlayTutorialBattleBgm(); midpointDone = true; },
                onComplete: () => finished = true);

            // **覆い切るまでは必ず待つ。** ここを待たないと、黒帯が降りている最中に
            // 次の台詞が走り、見えないところで喋ることになる。
            float limit = Time.time + 8f;
            yield return new WaitUntil(() => midpointDone || Time.time > limit);
            if (!midpointDone) { whileCovered?.Invoke(); SetPhase(RoundStatus.Discard); PlayTutorialBattleBgm(); }

            // 晴れるまでも待つ。上限を置いてあるので、演出が壊れても止まらない
            float limit2 = Time.time + 8f;
            yield return new WaitUntil(() => finished || Time.time > limit2);
        }

        private void SetPhase(RoundStatus status)
        {
            if (gameUIManager == null) return;

            if (gameUIManager.PhaseController != null)
                gameUIManager.PhaseController.UpdatePhaseStatus(status);
            else
                gameUIManager.SetCurrentPhaseStatus(status);
        }

        private void ApplyHpToUI(bool updateVitals = true)
        {
            var board = BoardStateManager.Instance;
            if (board != null)
            {
                board.ClearStakesOnTable();
                board.UpdateHp(_playerHp, _enemyHp);
                board.AddStakesOnTable(_playerStake, _enemyStake);
            }

            if (gameUIManager == null || !updateVitals) return;

            // 盤面がまだ隠れている段階（女の子とセリフだけの状態）では表示を戻さない
            // 「準備完了」ボックスは次局待ち合わせ用。チュートリアルには待ち合わせがないので常に隠す
            if (gameUIManager.PlayerInfoUI != null)
            {
                gameUIManager.PlayerInfoUI.gameObject.SetActive(_boardVisible);
                gameUIManager.PlayerInfoUI.SetMaxHP(_scenario.playerStartHp);
                gameUIManager.PlayerInfoUI.SetHP(PlayerShownHp);
                gameUIManager.PlayerInfoUI.ShowReadyBox(false);
            }
            if (gameUIManager.EnemyInfoUI != null)
            {
                // **第1局で伏せている間は、点滴（相手の体力）を出し直さない**
                // （2026-09-17 のユーザー指摘）。
                //
                // ここは `SetVitalsVisible(false)` が消したのとまったく同じ
                // GameObject を起こしてしまう。そのため、導入のセリフの途中で
                // 盤面が出た瞬間に相手の体力だけが現れ、牌を選ぶ場面に入ると
                // （伏せ直しが走って）また消える、という挙動になっていた。
                //
                // 自分側は `PlayerInfoUI.gameObject` を起こしているだけで、
                // 体力を消しているのは中の別の物なので、この症状が出ない。
                gameUIManager.EnemyInfoUI.SetPanelVisible(_boardVisible && !_firstRoundChromeHidden);
                gameUIManager.EnemyInfoUI.SetMaxHP(_scenario.enemyStartHp);
                gameUIManager.EnemyInfoUI.SetHP(EnemyShownHp);
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
        /// <summary>
        /// いま牌以外を伏せているか。**フェイズ側から見るために公開している。**
        ///
        /// 伏せるのは一度きりでは足りない。`GameUIPhaseController` はフェイズが変わるたびに
        /// 表示を組み直すので、**賭け金フェイズに入った瞬間に役一覧が戻ってしまう**
        /// （`ApplyBettingVisibility` が `SetMatchUIVisibility(true)` を通るため。2026-09-12 に確認）。
        /// そちらから毎回この旗を見て、伏せ直してもらう。
        /// </summary>
        public bool FirstRoundChromeHidden { get { return _firstRoundChromeHidden; } }

        private bool _firstRoundChromeHidden;

        private void SetFirstRoundChromeVisible(bool visible)
        {
            _firstRoundChromeHidden = !visible;
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

        // 牌を1枚ずつ起こす演出（DealTilesRoutine。2026-09-12 のフロー図「牌を配る演出」）は 2026-10-24 に外した。
        // 画面に物を生やさず、局の頭の暗転の間に盤面ごと出す（TutorialManager.Progression の PlayRoundStartRoutine）。

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

            // **第1局で牌以外を伏せている間は、出し直さない（2026-09-14）。**
            // ここは盤面を出すときに場の血と獲得ゲージも一緒に出していたが、
            // 牌を配る演出はまさに「牌だけを見せる」場面なので、
            // ここで出すと伏せた意味が無くなる。実際、導入の途中で
            // 上のゲージだけが出てしまっていた。
            bool keepHidden = _firstRoundChromeHidden;

            // 場の血は額を保持したまま表示だけ消す（持ち越し分が消えて見えないように）
            if (gameUIManager.BetPotUI != null)
                gameUIManager.BetPotUI.SetVisible(visible && !keepHidden);

            // 獲得ゲージも盤面と一緒に出し入れする
            gameUIManager.ScoreGauge.SetVisible(visible && !keepHidden);

            // **チュートリアルではタイマーを使わないので、枠ごと出さない**（2026-09-12 の指示）。
            // 数字は `PlayerInfoUI.StartTurnTimer` 側でも弾いてあるが、
            // それだけだと王冠の下に空の枠が残る。
            gameUIManager.ScoreGauge.SetTimerVisible(false);

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
