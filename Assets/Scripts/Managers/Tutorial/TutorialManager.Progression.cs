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
        // TutorialManager: 進行（シナリオ全体の進行・局の運び・賭け金フェイズ・打牌ターン・流局）

        // ==================== シナリオ本体 ====================

        private IEnumerator ScenarioRoutine(int startIndex)
        {
            for (int i = startIndex; i < _scenario.rounds.Count; i++)
            {
                _round = _scenario.rounds[i];
                currentRound = IndexToRound(i);

                yield return StartCoroutine(RunRound(_round));

                if (_aborted) yield break;

                PlayerPrefs.SetInt(ProgressKey, i + 1);
                PlayerPrefs.Save();
            }

            // 相手の血が尽きているなら、倒れる演出を挟んでから締めのセリフへ
            yield return StartCoroutine(RunEnemyDeath());

            yield return StartCoroutine(PlayLines(_scenario.endingLines));

            // 幕切れで灰をかけているので、必ず抜いてから移る（2026-09-12）。
            // ScreenTint は DontDestroyOnLoad で生き残るため、抜かないとタイトルが灰色のままになる。
            Tutorial.TutorialAudioDirector.ResetVisuals();

            SceneManager.LoadScene(_scenario.titleSceneName);
        }

        /// <summary>
        /// 決着したあと、相手が倒れるところを見せる。
        ///
        /// 盤面（手牌・山牌・河・HPパネル）を先に片付けて立ち絵とセリフだけにする。
        /// 牌が残ったまま立ち絵が落ちると何が起きたのか分からない。
        /// </summary>
        private IEnumerator RunEnemyDeath()
        {
            if (_enemyHp > 0) yield break;

            var enemy = gameUIManager != null ? gameUIManager.EnemyInfoUI : null;
            if (enemy == null) yield break;

            ClearGuide();
            SetBoardVisible(false);
            yield return new WaitForSeconds(phaseSettleTime);

            yield return StartCoroutine(enemy.PlayDeathRoutine());

            // 倒れ切ってから沈黙（endingLines の「…………」）を出す
            yield return new WaitForSeconds(0.4f);
        }

        private const string ProgressKey = "Tutorial_LastCompletedRound";

        /// <summary>前回どこまで進んだか（0 = 未プレイ）。</summary>
        public static int GetSavedProgress() => PlayerPrefs.GetInt(ProgressKey, 0);

        private static TutorialRound IndexToRound(int index)
        {
            switch (index)
            {
                case 0: return TutorialRound.Round1_BasicRon;
                case 1: return TutorialRound.Round2_Draw;
                case 2: return TutorialRound.Round3_FakeHint;
                case 3: return TutorialRound.Round4_Ability;
                default: return TutorialRound.Round5_Final;
            }
        }

        private IEnumerator RunRound(TutorialRoundData data)
        {
            // --- 局の初期化 ---
            HasClickedAutoMangan = false;
            _hasRejectedFirstConfirm = false;
            _isWaitingForHandSelectionComplete = true;
            _lastPlayerDiscardBaseId = -1;
            _hasMovedTileThisRound = false;

            // 前局の透視マークがプールの牌に残らないようにする
            ClearPerspectiveMarks();

            // 13枚そろうまでは『自動』も『決定』も出さない
            SetHandButtonStage(HandButtonStage.Hidden);

            // 手牌は空の状態で開始する（プレイヤーが山牌から選ぶ）
            SetupBoard(data, null);

            // 開幕は女の子とセリフだけ。盤面はイントロの途中で出す。
            SetBoardVisible(false);
            yield return null;

            // **立ち絵は1行目のセリフの後に出す（2026-09-12、フロー図どおり）。**
            // 契約書を閉じた直後は誰もいない画面で、最初の一言だけが聞こえる。
            // 立ち絵を持っているのはシーン側なので、こちらは合図を出すだけ。
            // 合図を受け取る人がいない（局を指定して始めた等）ときは、何も起きずに素通りする。
            var introLines = data.introLines;
            int reveal = data.revealBoardAfterLineIndex;

            if (CharacterRevealRequested != null && introLines != null && introLines.Count > 0)
            {
                yield return StartCoroutine(PlayLines(introLines.GetRange(0, 1)));

                var reveal1 = CharacterRevealRequested;
                CharacterRevealRequested = null;     // 出すのは一度きり
                reveal1();

                // 1行ぶん先に送ったので、残りと「盤面を出す行」の番号をずらす
                introLines = introLines.GetRange(1, introLines.Count - 1);
                if (reveal >= 0) reveal -= 1;
            }

            if (reveal >= 0 && reveal < introLines.Count - 1)
            {
                yield return StartCoroutine(PlayLines(introLines.GetRange(0, reveal + 1)));

                // **第1局だけ、牌を配る演出を挟む**（2026-09-12、フロー図どおり）。
                // 2局目以降は同じ卓が続いているので、配り直す絵は出さない。
                bool isFirstRound = _scenario != null && _scenario.rounds != null
                                    && _scenario.rounds.Count > 0
                                    && ReferenceEquals(data, _scenario.rounds[0]);
                if (isFirstRound && _askExperienceAfterIntro)
                {
                    // **最初から始めたときは、山牌を「じゃーん！これが君の山牌ね」まで出さない**
                    // （2026-09-19 のユーザー指示）。起こすのは AskExperienceIfNeeded の中
                    _wallRevealDeferred = true;
                }
                else if (isFirstRound) yield return StartCoroutine(DealTilesRoutine());
                else SetBoardVisible(true);

                yield return StartCoroutine(PlayLines(
                    introLines.GetRange(reveal + 1, introLines.Count - reveal - 1)));
            }
            else
            {
                yield return StartCoroutine(PlayLines(introLines));
                SetBoardVisible(true);
            }

            // **経験を聞くのはここ（2026-09-12 に位置を移した）。**
            // 以前は立ち絵が出た直後、セリフより前に聞いていた。
            // フロー図では導入の会話がひととおり終わってから分岐する。
            // 頭から始めたときだけ聞く。中身は TutorialManager.Intro.cs。
            yield return StartCoroutine(AskExperienceIfNeeded());

            // 念のため: 山牌を遅らせたのに「じゃーん」の行を通らなかったら、ここで出す
            if (_wallRevealDeferred)
            {
                _wallRevealDeferred = false;
                yield return StartCoroutine(DealTilesRoutine());
            }

            // --- 能力の実演と説明（手順⑱〜⑳） ---
            // 能力は手牌フェイズでしか使えない仕様なので、実演もこのフェイズのうちに行う。
            // さらに手牌を決めてしまう前（『自動』ボタンを押す前）に済ませる。
            // 手牌が決まったあとに能力の話を始めると、説明を聞いても試す余地がなくなる。
            if (data.enemyUsesAbility)
            {
                yield return StartCoroutine(PlayLines(data.abilityIntroLines));
                yield return StartCoroutine(RunEnemyAbilityShowcase(data));
                yield return StartCoroutine(PlayLines(data.abilityExplainLines));
                yield return StartCoroutine(PlayLines(data.enhanceExplainLines));

                if (data.guideToYakuList)
                {
                    yield return StartCoroutine(RunYakuListGuide(data));
                }
            }

            // --- 手牌構築フェイズ（手順①〜④ / ⑧ / ⑫ / ㉑） ---
            // 手動で組ませる局は、13枚そろってからセリフを挟んで『自動』を開放する。
            // 手動選択を許さない局は組みようがないので、最初から『自動』を出す。
            bool selfMadeMangan = false;

            // 第1局の牌選択中だけ、牌以外を伏せる。賭け金フェイズの直前で戻す
            bool hideChrome = _scenario != null && _scenario.rounds != null
                              && _scenario.rounds.Count > 0 && _scenario.rounds[0] == data;
            if (hideChrome) SetFirstRoundChromeVisible(false);

            // **自由に組ませる局は、13枚そろう前から『おまかせ』を出す。**
            // この局の『おまかせ』は「自分で組むのが大変なら任せてよい」という逃げ道なのに、
            // 13枚そろうまで隠していると、組み終えてからしか現れない＝逃げ道にならなかった。
            // 誘導する局（第1局など）は今までどおり「13枚 → セリフ → おまかせ → 決定」の順のまま。
            if (data.allowManualHandSelection && data.freeHandBuilding)
            {
                SetHandButtonStage(HandButtonStage.AutoOnly);
            }

            if (data.allowManualHandSelection)
            {
                // 第1局は、フロー図どおり4秒間まったく牌を取らなかったときだけ
                // 「クリックすると手牌に登録できる」ことを補足する。
                if (IsFirstTutorialRound(data))
                    StartCoroutine(RunFirstRoundHandIdleHint(data));

                yield return new WaitUntil(() =>
                    GetHandTileCount() >= HandSize || !_isWaitingForHandSelectionComplete);

                if (_isWaitingForHandSelectionComplete)
                {
                    selfMadeMangan = IsSelfMadeManganHand();

                    yield return StartCoroutine(PlayLines(selfMadeMangan
                        ? ResolveSelfManganLines(data)
                        : ResolveHandFilledLines(data)));
                }
            }

            if (data.freeHandBuilding)
            {
                // 自力で組んでも『自動』に任せてもよい局。
                // 両方のボタンを出したままにし、どちらかへ誘導もしない。
                // 決定が押された時点でもう一度手牌を見る（OnTryCompleteHandSelection）ので、
                // ここで満貫手が出来ていなくても、牌を入れ替えて直せる。
                if (selfMadeMangan) HasClickedAutoMangan = true;

                SetHandButtonStage(HandButtonStage.AutoAndDecide);
                ClearGuide();
            }
            else if (selfMadeMangan)
            {
                // 自力で満貫手を組めた場合も、フロー図どおり『おまかせ』を4秒だけ紹介する。
                // その後は手牌を保ったまま決定へ進める。HasClickedAutoMangan は待ち牌の公開と
                // 決定の解禁に使われているので、紹介が終わった時点で立てる。
                SetHandButtonStage(HandButtonStage.AutoOnly);
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.AutoManganButtonRect : null);
                yield return new WaitForSeconds(4f);
                ClearGuide();
                HasClickedAutoMangan = true;

                SetHandButtonStage(HandButtonStage.DecideOnly);
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.DecideButtonRect : null);
            }
            else
            {
                SetHandButtonStage(HandButtonStage.AutoOnly);
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.AutoManganButtonRect : null);
            }

            yield return new WaitUntil(() => !_isWaitingForHandSelectionComplete);
            ClearGuide();

            // --- 賭け金フェイズ ---
            if (_prevRoundWasDraw)
            {
                // 流局では賭け金が決着しない。次の局は前局と同額が自動で賭けられる仕様なので、
                // ここでプレイヤーに賭け金を指示してはいけない。
                int inherited = _lastBetAmount > 0 ? _lastBetAmount : data.betAmount;
                _lastBetAmount = inherited;
                PlaceBet(inherited);

                yield return StartCoroutine(PlayLines(ResolveInheritedBetLines(data, inherited)));
            }
            else
            {
                yield return StartCoroutine(PlayLines(data.beforeBetLines));
                yield return StartCoroutine(RunBettingPhase(data));

                // 実際に確定した額で積む（UIの表示額と場の額を必ず一致させる）
                int bet = _lastConfirmedBet > 0 ? _lastConfirmedBet : data.betAmount;
                _lastBetAmount = bet;
                PlaceBet(bet);
            }

            // **牌以外を戻すのはここ（2026-09-12 の指示）。**
            // 第1局は「牌を選ぶ → 賭け金を決める」まで牌だけを見せ、
            // 賭け金が決まってから残りのUIを出す。
            //
            // **以前は賭け金フェイズの直前で戻していた。** 賭け金がスマホ（体力表示）を
            // 拡大して見せる演出だったころの名残で、伏せたままだと何も出なかったため。
            // いまは賭け金パネルが画面下から出てくる作りなので、伏せたままでも出る。
            if (hideChrome) SetFirstRoundChromeVisible(true);

            // --- 打牌フェイズ ---
            // GameUIPhaseController は IsTutorialMode のとき HP パネルを出さないので、
            // フェイズを切り替えたあとに毎回こちらで表示し直す（手順⑦の説明に必要）。
            SetPhase(RoundStatus.Discard);
            ApplyHpToUI();
            yield return new WaitForSeconds(phaseSettleTime);
            // 第1局の対局導入は、フロー図どおり山牌／待ち牌の誘導を挟む専用シーケンスで進める。
            // 2局目以降は従来どおり各局の台本をそのまま表示する。
            if (!IsFirstTutorialRound(data))
                yield return StartCoroutine(PlayLines(data.onBattleStartLines));

            yield return StartCoroutine(RunBattle(data));

            // 流局なら賭け金は場に残したまま次局へ持ち越す
            _prevRoundWasDraw = data.outcome == TutorialOutcome.Draw;

            yield return StartCoroutine(PlayLines(data.outroLines));
        }

        /// <summary>
        /// 賭け金を積む。確定した時点で両者の血から引く（賭けた分は先に払う）。
        /// 決着時の増減はこれとは別に GameRules の式で決まる。
        /// チュートリアルでは相手も同額を賭ける。
        /// 残り血より賭け金が大きい場合は、実際に払えた分だけ場に乗る。
        /// </summary>
        private void PlaceBet(int amount)
        {
            if (amount <= 0) return;

            int playerPaid = Mathf.Min(amount, _playerHp);
            int enemyPaid = Mathf.Min(amount, _enemyHp);

            _playerHp -= playerPaid;
            _enemyHp -= enemyPaid;
            _playerStake += playerPaid;
            _enemyStake += enemyPaid;
            _stakeRounds++;

            ApplyHpToUI();
            ApplyPotToUI();
        }

        /// <summary>
        /// 決着したので賭け金を精算（0に戻す）する。
        /// ゲージ側の数字はここでは消さない。ロン演出のあとに吸収演出で消える。
        /// </summary>
        private void ClearStakes()
        {
            _playerStake = 0;
            _enemyStake = 0;
            _stakeRounds = 0;
            ApplyPotToUI(includeGauge: false);
        }

        /// <summary>この局の勝者の役の飜数。倍率は GameRules.GetMultiplier がここから決める。</summary>
        private int GetWinnerHan(TutorialRoundData data, bool isPlayerWin)
        {
            // プレイヤーの上がりは台本の手牌（manganHandHan）がそのまま勝者の役になる
            return isPlayerWin ? data.manganHandHan : data.enemyWinningHan;
        }

        /// <param name="includeGauge">
        /// 上の獲得ゲージにも反映するか。決着の精算では false にすること。
        /// ここで 0 を流し込むと、ロン演出のあとに吸い込ませる数字が先に消えてしまう。
        /// </param>
        private void ApplyPotToUI(bool includeGauge = true)
        {
            var potUI = gameUIManager != null ? gameUIManager.BetPotUI : null;
            if (potUI != null) potUI.SetStakes(_playerStake, _enemyStake);

            // 上の獲得ゲージにも同じ額を出す。対局と同じ見た目にするため
            if (includeGauge && gameUIManager != null)
                gameUIManager.ScoreGauge.SetStakes(_playerStake, _enemyStake);
        }

        /// <summary>
        /// 賭け金が持ち越されたことを伝えるセリフ。台本が空なら既定文を使う。
        /// </summary>
        private List<TutorialLine> ResolveInheritedBetLines(TutorialRoundData data, int inherited)
        {
            if (data.inheritedBetLines != null && data.inheritedBetLines.Count > 0)
            {
                var resolved = new List<TutorialLine>(data.inheritedBetLines.Count);
                foreach (var line in data.inheritedBetLines)
                {
                    if (line == null) continue;
                    resolved.Add(new TutorialLine(
                        string.Format(line.text, inherited, Pot), line.speaker));
                }
                return resolved;
            }

            return new List<TutorialLine>
            {
                new TutorialLine($"前の局は流局だったから、賭け金は{inherited}円のまま持ち越しよ。改めて賭ける必要はないわ。"),
            };
        }


        // ==================== 賭け金フェイズ ====================

        /// <summary>
        /// 賭け金フェイズ。
        ///
        /// 賭け金を決めるのは拡大したスマホの画面なので、順番は
        /// 「セリフを送る → スマホを拡大 → 固定額の賭け金UI → 決定ボタンへ誘導」。
        /// GameUIPhaseController 側はチュートリアル時にこの拡大とベット開始をしない。
        /// </summary>
        private IEnumerator RunBettingPhase(TutorialRoundData data)
        {
            SetPhase(RoundStatus.Betting);

            // --- ① セリフ。OKされるまで待つ ---
            if (!string.IsNullOrEmpty(data.betPromptText))
            {
                var prompt = new List<TutorialLine>
                {
                    new TutorialLine(string.Format(data.betPromptText, data.betAmount))
                };
                yield return StartCoroutine(PlayLines(prompt));
            }

            var betting = gameUIManager != null ? gameUIManager.BettingUI : null;
            if (betting == null)
            {
                Debug.LogWarning("[TutorialManager] BettingUI が未設定のため賭け金フェイズをスキップします。");
                yield break;
            }

            // --- ② セリフのあとにスマホ型の賭け金パネルを下から出す ---
            // 賭け金は固定額。増減ボタンは押せないので決定するしかない。
            // 実際に賭ける額は UI が確定した値を使う。data.betAmount をそのまま使うと、
            // 表示されている額と場に積まれる額が食い違う余地が残る。
            bool confirmed = false;
            _lastConfirmedBet = data.betAmount;
            betting.ShowFixedBettingPhase(_playerHp, _playerHp, data.betAmount, amount =>
            {
                _lastConfirmedBet = amount;
                confirmed = true;
            });

            yield return betting.WaitForSlideAnimation();
            GuideTo(betting.ConfirmButtonRect);
            yield return new WaitUntil(() => confirmed);
            ClearGuide();

            betting.HideBettingPhase();
            yield return betting.WaitForSlideAnimation();

            yield return new WaitForSeconds(phaseSettleTime);
        }

        // ==================== 対局 ====================

        private bool IsFirstTutorialRound(TutorialRoundData data)
        {
            return _scenario != null && _scenario.rounds != null
                   && _scenario.rounds.Count > 0 && _scenario.rounds[0] == data;
        }

        private IEnumerator RunBattle(TutorialRoundData data)
        {
            var board = BoardStateManager.Instance;
            int turns = data.enemyDiscardBaseIds.Count;
            int autoTurns = Mathf.Clamp(data.autoDiscardTurns, 0, turns);
            bool isFirstTutorialRound = IsFirstTutorialRound(data);
            int lastReactionIndex = -1;

            for (int turn = 1; turn <= turns; turn++)
            {
                bool isAutoTurn = turn <= autoTurns;

                // 自動打牌が終わってプレイヤーの番になる境目でセリフを挟む
                if (autoTurns > 0 && turn == autoTurns + 1)
                {
                    yield return StartCoroutine(PlayLines(data.beforeManualDiscardLines));
                }

                // --- プレイヤーの打牌 ---
                if (board != null) board.SetLocalTurn(true);

                if (isAutoTurn)
                {
                    yield return StartCoroutine(AutoDiscardForPlayer());
                }
                else
                {
                    if (isFirstTutorialRound && turn == 1)
                        yield return StartCoroutine(RunFirstRoundOpening());

                    if (isFirstTutorialRound && (turn == 1 || turn == 2))
                        GuideTo(GetWallGuideTarget(), true, new Vector2(0f, 10f));

                    _isWaitingForDiscard = true;
                    _lastPlayerDiscardBaseId = -1;

                    yield return new WaitUntil(() => !_isWaitingForDiscard);

                    if (isFirstTutorialRound && (turn == 1 || turn == 2))
                        ClearGuide();
                }

                yield return new WaitForSeconds(isAutoTurn ? autoDiscardInterval : discardInterval);

                if (isFirstTutorialRound && !isAutoTurn)
                {
                    if (turn == 1)
                    {
                        yield return StartCoroutine(PlayLines(new List<TutorialLine>
                        {
                            new TutorialLine($"おっ{GetTileName(_lastPlayerDiscardBaseId)}かぁ"),
                            new TutorialLine("それじゃあ　あたしはこれ"),
                        }));
                    }
                    else
                    {
                        yield return StartCoroutine(PlayLines(BuildDiscardReaction(_lastPlayerDiscardBaseId, ref lastReactionIndex)));
                    }
                }

                // --- 敵のロン（プレイヤーの打牌に反応する。手順⑮） ---
                if (data.outcome == TutorialOutcome.EnemyRon && turn >= data.enemyRonOnPlayerDiscardTurn)
                {
                    yield return StartCoroutine(RunEnemyRon(data, _lastPlayerDiscardBaseId));
                    yield break;
                }

                // --- 敵の打牌 ---
                if (board != null) board.SetLocalTurn(false);

                int discardBase = data.enemyDiscardBaseIds[turn - 1];
                int discardId = TutorialTiles.Encode(discardBase, discardBase == data.doraBaseId);

                if (gameUIManager != null && gameUIManager.EnemyRiverUI != null)
                    gameUIManager.EnemyRiverUI.AddTile(discardId);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDiscardSE();

                yield return new WaitForSeconds(isAutoTurn ? autoDiscardInterval : discardInterval);

                if (isFirstTutorialRound && turn == 1)
                {
                    yield return StartCoroutine(PlayLines(new List<TutorialLine>
                    {
                        new TutorialLine($"フフフ　あたしは{GetTileName(discardBase)}を打ったよ"),
                    }));
                    yield return StartCoroutine(RunFirstRoundWaitExplanation());
                }

                // --- プレイヤーのロン（手順⑥ / ㉓） ---
                if (data.outcome == TutorialOutcome.PlayerRon && discardBase == data.playerWinningTileBaseId)
                {
                    yield return StartCoroutine(RunPlayerRon(data, discardId));
                    yield break;
                }

                if (!isAutoTurn) yield return new WaitForSeconds(0.4f);
            }

            if (data.outcome == TutorialOutcome.Draw)
            {
                yield return StartCoroutine(RunDraw(data));
            }
        }

        private IEnumerator RunFirstRoundOpening()
        {
            GuideTo(GetWallGuideTarget(), false, new Vector2(0f, 10f));
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("よっし　対局だね"),
                new TutorialLine($"対局では交互に牌をこの{HighlightOpen}山牌{HighlightClose}から打ってくよ"),
                new TutorialLine("試しに適当に打ってみな"),
            }));
            ClearGuide();
        }

        private IEnumerator RunFirstRoundWaitExplanation()
        {
            GuideTo(GetWaitGuideTarget(), false, new Vector2(0f, 10f));
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine($"とまぁこんな感じでお互い{HighlightOpen}１ターンに１枚{HighlightClose}牌を打っていって"),
                new TutorialLine($"{HighlightOpen}先に{HighlightClose}{HighlightOpen}相手に自分の待ち牌を出させた人の勝ち{HighlightClose}"),
                new TutorialLine("というチキンレースなギャンブルなんだこれは"),
                new TutorialLine($"{HighlightOpen}待ち牌はここに表示{HighlightClose}されるから"),
                new TutorialLine("相手がその牌を出すのを祈りながら牌を打っていってね"),
            }));
            ClearGuide();

            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("じゃ　再開しようか"),
                new TutorialLine("とりま打牌よろしくー"),
            }));
        }

        private RectTransform GetWallGuideTarget()
        {
            return gameUIManager != null && gameUIManager.WallUI != null
                ? gameUIManager.WallUI.GuideTargetRect
                : null;
        }

        private RectTransform GetWaitGuideTarget()
        {
            return gameUIManager != null && gameUIManager.WaitUI != null
                ? gameUIManager.WaitUI.GuideTargetRect
                : null;
        }

        private static string GetTileName(int tileId)
        {
            return tileId >= 0 ? new TileData(tileId).GetTileName() : "その牌";
        }

        private static List<TutorialLine> BuildDiscardReaction(int tileId, ref int previousIndex)
        {
            string tileName = GetTileName(tileId);
            int reactionIndex = UnityEngine.Random.Range(0, 3);
            if (previousIndex >= 0 && reactionIndex >= previousIndex) reactionIndex++;
            previousIndex = reactionIndex;

            switch (reactionIndex)
            {
                case 0:
                    return new List<TutorialLine>
                    {
                        new TutorialLine($"おっ{tileName}かぁ"),
                        new TutorialLine("あたし的にはセーフ！"),
                    };
                case 1:
                    return new List<TutorialLine>
                    {
                        new TutorialLine($"うーん{tileName}ね"),
                        new TutorialLine("くぅーおしいっ！"),
                    };
                case 2:
                    return new List<TutorialLine>
                    {
                        new TutorialLine($"はぁ{tileName}…？"),
                        new TutorialLine("ぜんぜんロンできんなぁ"),
                    };
                default:
                    return new List<TutorialLine>
                    {
                        new TutorialLine($"へぇ{tileName}？"),
                        new TutorialLine("あたしも同じの打と"),
                    };
            }
        }

        private IEnumerator RunFirstRoundHandIdleHint(TutorialRoundData data)
        {
            yield return new WaitForSeconds(4f);

            if (!IsFirstTutorialRound(data) || _round != data || _hasMovedTileThisRound ||
                !_isWaitingForHandSelectionComplete)
            {
                yield break;
            }

            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("そうそう　牌をクリックすると手牌に登録できるよ"),
            }));
        }

        /// <summary>
        /// プレイヤーの手番を自動で1手打つ。
        ///
        /// チュートリアルの打牌は手牌ではなく山牌から捨てる仕組みなので、
        /// GameUIManager.DiscardSelectedTile のチュートリアル分岐と同じ経路で山から河へ移す。
        /// </summary>
        private IEnumerator AutoDiscardForPlayer()
        {
            if (gameUIManager == null) yield break;

            var wall = gameUIManager.WallUI;
            var river = gameUIManager.RiverUI;
            if (wall == null || river == null) yield break;

            var slots = wall.GetWallSlots();
            if (slots == null || slots.Count == 0) yield break;

            var interaction = slots[0] != null ? slots[0].GetComponent<TileInteraction>() : null;
            int tileId = interaction != null ? interaction.TileId : -1;
            if (tileId < 0) yield break;

            RectTransform tileRt = wall.GrabTileById(tileId);
            if (tileRt != null) river.AddExistingTile(tileRt, tileId);
            else river.AddTile(tileId);

            if (BoardStateManager.Instance != null)
                wall.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, true);

            if (AudioManager.Instance != null) AudioManager.Instance.PlayDiscardSE();

            yield return null;
        }

        // ==================== 流局 ====================

        private IEnumerator RunDraw(TutorialRoundData data)
        {
            SetPhase(RoundStatus.Draw);

            // 流局では場の血は動かさない（決着していないので次の局へ持ち越す）。
            // 賭け金は既に両者の血から引かれているので、流局そのもので血は減っている。
            // drawDamageToPlayer はそれとは別枠の追加ペナルティ。既定シナリオでは 0。
            if (data.drawDamageToPlayer > 0)
            {
                _playerHp = Mathf.Max(0, _playerHp - data.drawDamageToPlayer);
                ApplyHpToUI();
            }

            yield return new WaitForSeconds(2.0f);
        }

    }
}
