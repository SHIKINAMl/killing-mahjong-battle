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
                if (_reachedFlowEnd) { _scenarioRoutine = null; yield break; }

                PlayerPrefs.SetInt(ProgressKey, i + 1);
                PlayerPrefs.Save();
            }

            // 幕切れで灰をかけているので、必ず抜いてから移る（2026-09-12）。
            // ScreenTint は DontDestroyOnLoad で生き残るため、抜かないとタイトルが灰色のままになる。
            Tutorial.TutorialAudioDirector.ResetVisuals();

            if (_aborted) yield break;
            // 終わったらタイトルではなくデバッグ部屋へ移し、対局を選ぶ画面を開く（2026-10-23 ユーザー指示）
            TutorialNavigation.Complete(TutorialNavigation.OpenMatchMenuInRoom);
            SceneManager.LoadScene(TutorialNavigation.DebugRoomSceneName);
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

        private void SetSeniorIdleBounce(bool on)
        {
            if (gameUIManager != null && gameUIManager.EnemyInfoUI != null)
                gameUIManager.EnemyInfoUI.SetIdleSlimeBounce(on);
        }

        private IEnumerator RunRound(TutorialRoundData data)
        {
            // --- 局の初期化 ---
            // 前の局の対局から説明へ戻るので、曲も説明用へ戻す（対局の曲が鳴っているときだけ。TutorialManager.Board.cs）
            ReturnToTutorialLessonBgm(resumedRound: !IsFirstTutorialRound(data));
            SetSeniorIdleBounce(false); // 前の局を途中で抜けたときに跳ねたまま残さない
            HasClickedAutoMangan = false;
            IsAutoButtonLocked = false;
            _hasRejectedFirstConfirm = false;
            _isWaitingForHandSelectionComplete = true;
            _lastPlayerDiscardBaseId = -1;
            _hasMovedTileThisRound = false;

            // ボルテージは第2局の「新規牌」の説明を終えてから、専用の登場演出で見せる。
            // 第2局のまとめ中は前局の牌・UIを残す。配り直しと表示切替は黒幕の内側で行う。
            _pendingRoundBoard = IsSecondTutorialRound(data) ? data : null;
            if (_pendingRoundBoard == null)
            {
                InitializeRoundBoard(data);
                SetBoardVisible(false);
                yield return null;
            }

            // **どの局も、暗転 →「N局目」→ 明転 で始め、盤面は暗転している間に出しておく**（2026-10-24 ユーザー指示「チュートリアルの対局フェイズでUIをニョキって生やすのは止めてくれ。基本的に画面が暗転しているときにUIは全部出すように」）。
            // 以前は女の子とセリフだけで始め、盤面は導入のセリフの途中で出し、山牌は1枚ずつ起こしていた。
            // 「２局目」の局名は 2026-10-24 のユーザー指示（「２局目入る時は２局目って表示したほうがいい」）。
            // 第2局の頭の暗転は 2026-10-22 のプランナー指摘（「二局目の始まりの演出をカットしてる」）で入れていたが、
            // 暗くなりきった次の瞬間に明け始めていて、局名が見えていなかった
            yield return StartCoroutine(PlayRoundStartRoutine(data));
            if (_aborted) yield break;

            // フロー図シート3の頭。対局フェイズ①が終わって手牌選択フェイズ②へ来たところで、
            // ゲームの流れをまとめ、資料への入口を画像で案内する。
            // **第2局の台本より前。** 第1局の締めくくりなので、盤面を出してから話す。
            if (IsSecondTutorialRound(data))
            {
                yield return StartCoroutine(RunAfterFirstRoundWrapUp());
                if (_aborted) yield break;
                // ふつうは能力の紹介の頭（「それじゃあ第二局目だね」のあと）で出してある。出ていなければここで出す
                if (_pendingRoundBoard != null || !_boardVisible) yield return StartCoroutine(PlayRoundLabelRoutine(2));
                if (_aborted) yield break;
            }

            // **立ち絵が出きってから台詞を出す（2026-09-27 の指示）。**
            // 2026-09-12 のフロー図は「契約書を閉じる → セリフ1行 → 立ち絵」だったが、
            // 順番を入れ替えた。誰もいない画面に声だけが出るのではなく、
            // 先に相手が滲み出てきて、そのあとで喋り出す。
            // 曲も1行目の合図で鳴り始めるので、無音のまま立ち絵が浮かぶことになる。
            //
            // 立ち絵を持っているのはシーン側なので、こちらは合図を出して待つだけ。
            // 合図を受け取る人がいない（局を指定して始めた等）ときは、何も起きずに素通りする。
            var introLines = data.introLines;
            int reveal = data.revealBoardAfterLineIndex;

            if (CharacterRevealRequested != null && introLines != null && introLines.Count > 0)
            {
                var reveal1 = CharacterRevealRequested;
                CharacterRevealRequested = null;     // 出すのは一度きり
                CharacterRevealFinished = false;
                reveal1();

                // **出し終わりを待つ。** 相手がいないところに台詞が出ないように。
                // シーン側が旗を立て損ねても止まらないよう、上限だけ決めておく。
                float revealLimit = Time.time + 5f;
                yield return new WaitUntil(
                    () => CharacterRevealFinished || Time.time > revealLimit);
                if (_aborted) yield break;
            }

            if (reveal >= 0 && reveal < introLines.Count - 1)
            {
                // 盤面と山牌は局の頭の暗転の間に出してある（PlayRoundStartRoutine）。
                // 以前はここで盤面を出し、第1局は山牌を1枚ずつ起こしていた（2026-09-12・09-19 の指示）が、
                // 画面に物を生やさない決まりにしたのでやめた（2026-10-24 ユーザー指示「チュートリアルの対局フェイズでUIをニョキって生やすのは止めてくれ。基本的に画面が暗転しているときにUIは全部出すように」）
                yield return StartCoroutine(PlayLines(introLines.GetRange(0, reveal + 1)));
                yield return StartCoroutine(PlayLines(
                    introLines.GetRange(reveal + 1, introLines.Count - reveal - 1)));
            }
            else
            {
                yield return StartCoroutine(PlayLines(introLines));
                // 第1局の盤面は「１局目」の暗転の間に出す（下）。ここで出すと牌が生えて見える
                if (!IsFirstRound(data)) SetBoardVisible(true);
            }

            // **経験を聞くのはここ（2026-09-12 に位置を移した）。**
            // 以前は立ち絵が出た直後、セリフより前に聞いていた。
            // フロー図では導入の会話がひととおり終わってから分岐する。
            // 頭から始めたときだけ聞く。中身は TutorialManager.Intro.cs。
            yield return StartCoroutine(AskExperienceIfNeeded());
            RestoreIntroSettings();
            // 第1局の「１局目」と盤面は、ふつうは経験を聞く流れの中（「アタシと実践してみよっかー」のあと）で出す。
            // 局を指定して始めたなどで聞かなかったときは、まだ出ていないのでここで出す
            if (IsFirstRound(data) && !_boardVisible) yield return StartCoroutine(PlayRoundLabelRoutine(1));
            if (_aborted) yield break;

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
            bool hideChrome = IsFirstRound(data);
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

                // 第2局は「もう一回の練習」なので、詰まっている人へ段階的に声をかける
                // （20秒 → 満貫以上だと念を押す、1分 → 『おまかせ』へ逃がす）。
                // 2026-09-30 のフロー図改訂で入った。
                if (IsSecondTutorialRound(data))
                    StartCoroutine(RunSecondRoundHandTimerHints(data));

                // 選んでいるあいだ、先輩は手持ち無沙汰なのでスライムのように跳ねる（2026-10-21、ユーザー指示）。
                // 13枚そろった（または決定した）瞬間に元に戻る
                SetSeniorIdleBounce(true);
                yield return new WaitUntil(() =>
                    GetHandTileCount() >= HandSize || !_isWaitingForHandSelectionComplete);
                SetSeniorIdleBounce(false);

                // 13枚目のレイアウト更新で始まる役名表示を、次の案内より先に見せる。
                yield return null;
                yield return WaitForInstantRankCall();
                if (_aborted) yield break;

                if (_isWaitingForHandSelectionComplete)
                {
                    selfMadeMangan = IsSelfMadeManganHand();

                    var lines = selfMadeMangan ? ResolveSelfManganLines(data) : ResolveHandFilledLines(data);

                    if (data.freeHandBuilding)
                    {
                        yield return StartCoroutine(PlayLines(lines));
                    }
                    else
                    {
                        // **フロー図の順番（2026-09-23 のユーザー指示）。**
                        //   「じゃあ、コレ使ってよ」まで → ボタンを出す → 4秒ハイライト
                        //   → 「これはおまかせボタン」以降
                        // ボタンが無い状態で「これはおまかせボタン」と言っても指す先が無い。
                        yield return StartCoroutine(PlayLinesUntilAutoIntro(data, lines));
                    }
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

                // **第2局だけは決定ボタンを指す**（2026-09-30 のフロー図改訂）。
                // 「ほいじゃ決定ボタンを押して」のあとに矢印が続く。
                // ここまで『おまかせ』を指していることがあるので、指し先を差し替える。
                if (IsSecondTutorialRound(data))
                    GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                        ? gameUIManager.HandUI.DecideButtonRect : null);
                else
                    ClearGuide();
            }
            else if (selfMadeMangan)
            {
                // 『おまかせ』の紹介（ボタン表示＋4秒ハイライト＋紹介セリフ）は
                // PlayLinesUntilAutoIntro で済んでいる。ここでは決定へ進めるだけ。
                // HasClickedAutoMangan は待ち牌の公開と決定の解禁に使われているので、ここで立てる。
                HasClickedAutoMangan = true;
                IsAutoButtonLocked = false;   // ここから先は『自動』を出さないので旗も下ろす

                // フロー図どおり、**ハイライトの前に**決定を促す一言を入れる（2026-09-23）。
                // 『おまかせ』を押した経路では ApplyMockAutoMangan が同じ言葉を言う。
                yield return StartCoroutine(PlayLines(new List<TutorialLine>
                {
                    new TutorialLine(DecidePromptLine),
                }));

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

                // 第2局からは**自分で賭け金を決めさせる**（2026-09-30 のフロー図改訂）。
                // 第1局は固定額のまま。まだ額の意味を教えていない。
                if (IsSecondTutorialRound(data))
                    yield return StartCoroutine(RunFreeBettingPhase(data));
                else
                    yield return StartCoroutine(RunBettingPhase(data));

                // 実際に確定した額で積む（UIの表示額と場の額を必ず一致させる）
                int bet = _lastConfirmedBet > 0 ? _lastConfirmedBet : data.betAmount;
                _lastBetAmount = bet;
                PlaceBet(bet, data.enemyBetAmount);
                if (IsSecondTutorialRound(data))
                    yield return RunEnemyAssaultIntroduction(data);
            }

            // **牌以外を戻すのはここ（2026-09-12 の指示）。**
            // 第1局は「牌を選ぶ → 賭け金を決める」まで牌だけを見せ、
            // 賭け金が決まってから残りのUIを出す。
            //
            // **以前は賭け金フェイズの直前で戻していた。** 賭け金がスマホ（体力表示）を
            // 拡大して見せる演出だったころの名残で、伏せたままだと何も出なかったため。
            // いまは賭け金パネルが画面下から出てくる作りなので、伏せたままでも出る。
            // **出すのは打牌フェイズへの暗転（「対局開始」の覆い）の間**（2026-10-24 ユーザー指示「チュートリアルの対局フェイズでUIをニョキって生やすのは止めてくれ。基本的に画面が暗転しているときにUIは全部出すように」）。
            // 以前は賭け金が決まった直後にここで出していて、明るい画面に体力やゲージがぱっと現れていた

            // --- 打牌フェイズ ---
            // **黒帯で隠してから入る（2026-09-28 の指示）。**
            // 本編は賭けが決まると `TriggerBettingAnimationPhase` から
            // `PhaseTransitionUI.PlayTransition` を通り、一本線 →「対局開始」→
            // 市松模様が画面を覆う、という段取りで打牌フェイズへ移る。
            // チュートリアルは `SetPhase` を直接呼んでいたので**そこだけ素通りで、
            // 賭け金の次の瞬間に打牌が始まっていた。** 本編と同じ見え方に揃える。
            //
            // フェイズの切り替えは、本編と同じく**覆い切った時点（onMidpoint）**で行う。
            // 先に切り替えると、盤面が組み替わる様子が見えてしまう。
            yield return StartCoroutine(RunDiscardEntryTransition(
                hideChrome ? () => SetFirstRoundChromeVisible(true) : (System.Action)null));

            // GameUIPhaseController は IsTutorialMode のとき HP パネルを出さないので、
            // フェイズを切り替えたあとに毎回こちらで表示し直す（手順⑦の説明に必要）。
            ApplyHpToUI();
            yield return new WaitForSeconds(phaseSettleTime);
            // 第1局の対局導入は、フロー図どおり山牌／待ち牌の誘導を挟む専用シーケンスで進める。
            // 2局目以降は従来どおり各局の台本をそのまま表示する。
            if (!IsFirstTutorialRound(data))
                yield return StartCoroutine(PlayLines(data.onBattleStartLines));

            // 第2局は**敵が先に打つ**うえ、5打目の手前でボルテージの説明が入る
            // （2026-10-06 のフロー図改訂・4枚目「対局フェイズ②」）。
            // 打つ順番が逆なので、RunBattle に分岐を足さず専用の運びへ渡す。
            if (UsesSecondRoundBattle(data))
                yield return StartCoroutine(RunSecondRoundBattle(data));
            else
                yield return StartCoroutine(RunBattle(data));

            if (_aborted || _reachedFlowEnd) yield break;

            // 流局なら賭け金は場に残したまま次局へ持ち越す
            _prevRoundWasDraw = data.outcome == TutorialOutcome.Draw;

            // 第1局はロン後の40手順を RunPlayerRon 側で完結させる。
            // ここで旧 outroLines を続けると、フロー図に無い別の締めが追加されてしまう。
            if (!IsFirstTutorialRound(data))
                yield return StartCoroutine(PlayLines(data.outroLines));
        }

        /// <summary>
        /// 賭け金を場へ預ける。総体力は動かさず、表示体力だけ預けた分を引く（本編と同じ）。
        /// 減る表示は対局開始の演出で行い、ここでは表示用台帳だけを合わせる。
        /// 相手の額は台本で指定がなければ同額にする。
        /// 残り血より賭け金が大きい場合は、実際に払えた分だけ場に乗る。
        /// </summary>
        private void PlaceBet(int amount, int enemyAmount = 0)
        {
            if (amount <= 0) return;

            _playerHpBeforeBet = PlayerShownHp;
            _enemyHpBeforeBet = EnemyShownHp;
            int playerPaid = Mathf.Min(amount, PlayerShownHp);
            int enemyPaid = Mathf.Min(enemyAmount > 0 ? enemyAmount : amount, EnemyShownHp);
            _playerStake += playerPaid;
            _enemyStake += enemyPaid;
            _stakeRounds++;

            ApplyHpToUI(updateVitals: false);
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
            BoardStateManager.Instance?.ClearStakesOnTable();
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
        /// 台本に設定された持ち越し説明の数値を展開する。空なら会話を挟まない。
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

            return new List<TutorialLine>();
        }


        // ==================== 賭け金フェイズ ====================

        /// <summary>
        /// 賭け金フェイズ。
        ///
        /// 賭け金を決めるのは拡大したスマホの画面なので、順番は
        /// 「セリフを送る → スマホを拡大 → 固定額の賭け金UI → 決定ボタンへ誘導」。
        /// GameUIPhaseController 側はチュートリアル時にこの拡大とベット開始をしない。
        /// </summary>
        /// <summary>局名を出したまま暗転を保つ秒数。暗くなりきってすぐ明けると、局名が読めない</summary>
        private const float RoundLabelHoldSeconds = 1.2f;

        /// <summary>
        /// 局の始まり。本編の配牌と同じ暗転（<see cref="KillingMahjong.UI.PhaseTransitionUI.PlayRoundStartDarken"/>）を
        /// 降ろして「N局目」を出し、**覆っているあいだに盤面と山牌を出しきってから**明ける（2026-10-24 ユーザー指示「チュートリアルの対局フェイズでUIをニョキって生やすのは止めてくれ。基本的に画面が暗転しているときにUIは全部出すように」）。
        /// 暗転の部品が無い・戻ってこないときは、止まらずに盤面を出すだけにする。
        /// </summary>
        private IEnumerator PlayRoundStartRoutine(TutorialRoundData data)
        {
            int number = _scenario != null && _scenario.rounds != null ? _scenario.rounds.IndexOf(data) + 1 : 1;
            if (number < 1) number = 1;
            var phase = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            // チュートリアルでは演出の入れ物が伏せてあることがある。対局開始の演出と同じく起こしてから使う
            // （伏せたまま呼ぶと暗転のコルーチンが始められずにエラーになる）
            if (phase != null && !phase.gameObject.activeSelf) phase.gameObject.SetActive(true);
            if (phase == null || !phase.isActiveAndEnabled)
            {
                ShowBoardWhileDark();
                yield break;
            }

            // **第1局は、頭では暗転も盤面も出さない**（2026-10-29 ユーザー指示「契約書を書くといきなり『第一局』表示が出るので困惑した」
            // 「一局目の演出が出てから自分の牌が出るようにして」）。まだ先輩も出てきておらず、何の1局目なのか分からない。
            // 「１局目」の暗転は「アタシと実践してみよっかー」のあとに出し、その暗いあいだに牌を出す（<see cref="PlayRoundLabelRoutine"/>）
            // **第2局も、局名と盤面は「それじゃあ第二局目だね」のあとに出す**（2026-10-29 ユーザー指示
            // 「それじゃ第二局ね というセリフの後黒帯の演出出てほしい」）。頭で出すと、第1局のまとめの会話の前に「２局目」が出ていた
            if (number == 1 || IsSecondTutorialRound(data)) yield break;
            yield return PlayRoundDarkenRoutine(phase, RoundLabel(number), RoundLabelHoldSeconds);
        }

        private static string RoundLabel(int number) => ToFullWidthDigits(number) + "局目";

        private bool IsFirstRound(TutorialRoundData data) => _scenario != null && _scenario.rounds != null
            && _scenario.rounds.Count > 0 && _scenario.rounds[0] == data;

        /// <summary>
        /// 「N局目」の暗転を出し、暗いあいだに盤面（自分の牌）を出す。第1局の「アタシと実践してみよっかー」のあと
        /// （2026-10-29 ユーザー指示「このセリフの後 １局目の演出を出して」「一局目の演出が出てから自分の牌が出るようにして」）。
        /// 暗転の部品が無いときは、盤面だけ出す。
        /// </summary>
        private IEnumerator PlayRoundLabelRoutine(int number)
        {
            var phase = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            if (phase != null && !phase.gameObject.activeSelf) phase.gameObject.SetActive(true);
            if (phase == null || !phase.isActiveAndEnabled) { ShowBoardWhileDark(); yield break; }
            yield return PlayRoundDarkenRoutine(phase, RoundLabel(number), RoundLabelHoldSeconds);
        }

        /// <summary>暗転して <paramref name="label"/> を出し、覆っているあいだに盤面を出しきって、<paramref name="hold"/> 秒おいて明ける</summary>
        private IEnumerator PlayRoundDarkenRoutine(KillingMahjong.UI.PhaseTransitionUI phase, string label, float hold)
        {
            bool covered = false;
            phase.PlayRoundStartDarken(label, onReady: () => covered = true);
            float limit = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => covered || _aborted || Time.realtimeSinceStartup >= limit);
            if (_aborted) yield break;

            // セリフも黒幕が覆ってから隠す。暗転前にUIだけ消さない。
            if (dialogueUI != null) dialogueUI.HideText();
            ShowBoardWhileDark();
            yield return new WaitForSecondsRealtime(hold);
            if (_aborted) yield break;

            bool light = false;
            phase.PlayRoundStartFadeOut(() => light = true);
            limit = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => light || _aborted || Time.realtimeSinceStartup >= limit);
            if (!light) phase.CancelTransitions();
        }

        /// <summary>盤面と山牌をいちどに出す。山牌は起こす途中の大きさで残っていても等倍に戻す</summary>
        private void ShowBoardWhileDark()
        {
            if (_pendingRoundBoard != null)
            {
                var nextRound = _pendingRoundBoard;
                InitializeRoundBoard(nextRound, preserveRoundCover: true);
                _pendingRoundBoard = null;
            }
            SetBoardVisible(true);
            if (gameUIManager == null || gameUIManager.WallUI == null) return;
            foreach (var t in gameUIManager.WallUI.GetComponentsInChildren<TileInteraction>(true))
                if (t != null) t.transform.localScale = Vector3.one;
        }

        /// <summary>第2局の自由な賭け。選んだ額を変更せず、確定後に強襲の導入へ進む。</summary>
        private IEnumerator RunFreeBettingPhase(TutorialRoundData data)
        {
            SetPhase(RoundStatus.Betting);
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("じゃあこんどは自分で賭け金を決めてみようか"),
            });
            var betting = gameUIManager != null ? gameUIManager.BettingUI : null;
            if (betting == null)
                throw new System.InvalidOperationException("第2局の自由な賭けにはBettingUIが必要です。");
            bool confirmed = false;
            _lastConfirmedBet = data.betAmount;
            betting.ShowFreeBettingPhase(PlayerShownHp, PlayerShownHp, amount => {
                _lastConfirmedBet = amount;
                confirmed = true;
            });
            yield return betting.WaitForSlideAnimation();
            GuideTo(betting.IncreaseButtonRect, useMask: false);
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("賭け金が決まったら決定ボタンを押してね"),
            });
            ClearGuide();
            yield return new WaitUntil(() => confirmed || _aborted);
            if (_aborted) yield break;
            betting.HideBettingPhase();
            yield return betting.WaitForSlideAnimation();
            // 額に応じてセリフだけ分岐し、選んだ掛け金は変更しない。
            yield return PlayLines(FreeBetReaction(data, _lastConfirmedBet));
            yield return PlayLines(data.afterFreeBetLines);
        }

        private static List<TutorialLine> FreeBetReaction(TutorialRoundData data, int amount)
        {
            if (amount >= 4000) return data.onHighBetLines;
            if (amount >= 1000) return data.onMiddleBetLines;
            return data.onLowBetLines;
        }
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
            betting.ShowFixedBettingPhase(PlayerShownHp, PlayerShownHp, data.betAmount, amount =>
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
                        // フロー図の繰り返しループは
                        //   プレイヤーの打牌 → ランダム2行 → 「それじゃあ　あたしはこれ」 → 相手の打牌
                        // の順。**この1行が相手の打牌の合図**なので、ランダム2行だけで打たせない。
                        // 1巡目は上の分岐が同じ並びを直接書いている。
                        var reaction = BuildDiscardReaction(_lastPlayerDiscardBaseId, ref lastReactionIndex);
                        reaction.Add(new TutorialLine("それじゃあ　あたしはこれ"));
                        yield return StartCoroutine(PlayLines(reaction));
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

                // フロー図では、相手が打ってロンにならなかったとき
                // 「プレイヤーの打牌選択フェイズ」へ戻る手前にこの1行が入る。
                // **1巡目には言わない。** あちらは待ち牌の説明のあとの
                // 「じゃ　再開しようかとりま打牌よろしくー」が同じ役目をしている。
                if (isFirstTutorialRound && !isAutoTurn && turn >= 2)
                {
                    yield return StartCoroutine(PlayLines(new List<TutorialLine>
                    {
                        new TutorialLine("次は後輩ちゃんねー"),
                    }));
                }

                if (!isAutoTurn) yield return new WaitForSeconds(0.4f);
            }

            if (data.outcome == TutorialOutcome.Draw)
            {
                yield return StartCoroutine(RunDraw(data));
            }
        }

        /// <summary>
        /// 『おまかせ』の紹介を、フロー図の順番で流す（2026-09-23）。
        ///
        /// 「これはおまかせボタン」と言い出す**手前**でボタンを出し、
        /// そのまま4秒ハイライトしてから残りのセリフを続ける。
        /// 目印の語が見つからない台本（差し替え時など）では、全部言ってからボタンを出す。
        /// </summary>
        private IEnumerator PlayLinesUntilAutoIntro(TutorialRoundData data, List<TutorialLine> lines)
        {
            int introIndex = IndexOfAutoIntro(lines);

            if (lines == null || introIndex < 0)
            {
                // 自力で満貫を作れたときの台本には『おまかせ』の紹介が入っていない。
                // フロー図ではどちらの道も紹介セリフへ合流するので、
                // 手牌が足りないときの台本から**紹介の2行だけ**を借りて言う（2026-09-23）。
                if (lines != null) yield return StartCoroutine(PlayLines(lines));

                // **見せるだけ。押させない。** 自分で組んだ満貫手が置き換わらないようにする。
                // 紹介中は絵の複製しか出ないが、飛び終わって本物が出たあとも押させない
                IsAutoButtonLocked = true;
                yield return StartCoroutine(PresentAutoManganButton(
                    () => PlayLines(BuildAutoIntroLines(data))));
                yield break;
            }

            if (introIndex > 0) yield return StartCoroutine(PlayLines(lines.GetRange(0, introIndex)));

            yield return StartCoroutine(PresentAutoManganButton(
                () => PlayLines(lines.GetRange(introIndex, lines.Count - introIndex))));
        }

        /// <summary>
        /// 『おまかせ』を紹介する（2026-09-29 のユーザー指示）。
        ///
        /// **絵の複製を中央にバッと出す → 紹介セリフ → 本来の位置へ飛ばす → 本物を出す。**
        ///
        /// 以前は本物をその場に出して4秒ハイライトしていた。押せる見た目のまま
        /// 4秒光っているので「押せそうな雰囲気だし、その間押しそうになる」と言われた。
        /// 複製は当たり判定を持たないので、紹介のあいだは押しようがない。
        /// **4秒の無言の間も無くなる** — 紹介セリフを読んでいる間が、そのまま見せる時間になる。
        /// </summary>
        private IEnumerator PresentAutoManganButton(System.Func<IEnumerator> playIntroLines)
        {
            var target = gameUIManager != null && gameUIManager.HandUI != null
                ? gameUIManager.HandUI.AutoManganButtonRect : null;

            // ボタンが取れない場面（台本の差し替えなど）では、今までどおり出して話すだけ。
            // **黙って何も出さないより、押せるボタンが出ているほうがまだ通じる。**
            if (target == null)
            {
                SetHandButtonStage(HandButtonStage.AutoOnly);
                if (playIntroLines != null) yield return StartCoroutine(playIntroLines());
                yield break;
            }

            var intro = UI.TutorialButtonIntroUI.Show(target);
            if (intro != null) yield return StartCoroutine(intro.PopIn());

            if (playIntroLines != null) yield return StartCoroutine(playIntroLines());

            if (intro != null) yield return StartCoroutine(intro.MoveToPlace(target));

            // **飛び終わってから本物を出す。** ここで初めて押せるようになる
            SetHandButtonStage(HandButtonStage.AutoOnly);
        }

        /// <summary>この語を含むセリフの手前で『おまかせ』を出す。</summary>
        private const string AutoButtonIntroKeyword = "おまかせボタン";

        /// <summary>
        /// 決定ボタンへ促す一言（フロー図 y=9360）。
        /// **『おまかせ』を押した場合も、自力で満貫を作れた場合も、同じ言葉で促す。**
        /// </summary>
        internal const string DecidePromptLine = "手牌が決まったら決定ボタンを押してね";

        /// <summary>『おまかせ』の紹介が始まる行。無ければ -1。</summary>
        private static int IndexOfAutoIntro(List<TutorialLine> lines)
        {
            if (lines == null) return -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null && !string.IsNullOrEmpty(lines[i].text)
                    && lines[i].text.Contains(AutoButtonIntroKeyword))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 『おまかせ』の紹介だけを取り出す。
        /// **「これを押して」は入れない。** 自分で満貫を作れた人には押す必要が無いため。
        /// </summary>
        private List<TutorialLine> BuildAutoIntroLines(TutorialRoundData data)
        {
            var source = ResolveHandFilledLines(data);
            int start = IndexOfAutoIntro(source);
            var result = new List<TutorialLine>();
            if (source == null || start < 0) return result;

            for (int i = start; i < source.Count; i++)
            {
                if (source[i] == null || string.IsNullOrEmpty(source[i].text)) continue;
                if (source[i].text.Contains("押して")) continue;
                result.Add(source[i]);
            }
            return result;
        }

        // フロー図の「４秒間オート満貫ボタンハイライト」はここにあったが、
        // 2026-09-29 に <see cref="PresentAutoManganButton"/> へ置き換えて消した。
        // 本物を出して4秒光らせる作りだったので、押せそうに見えて実際に押されかけていた。

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
            // **矢印はフロー図どおり「待ち牌はここに表示されるから」の手前で出す**（2026-09-23）。
            // 図の並びは チキンレース → 待ち牌UIハイライト → 「待ち牌はここに表示されるから」。
            // 以前はこの3行より前から出していたので、まだ話題になっていない枠を指していた。
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine($"とまぁこんな感じでお互い{HighlightOpen}１ターンに１枚{HighlightClose}牌を打っていって"),
                new TutorialLine($"{HighlightOpen}先に{HighlightClose}{HighlightOpen}相手に自分の待ち牌を出させた人の勝ち{HighlightClose}"),
                new TutorialLine("というチキンレースなギャンブルなんだこれは"),
            }));

            GuideTo(GetWaitGuideTarget(), false, new Vector2(0f, 10f));
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
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

        /// <summary>2回目の練習で「満貫以上だぞ」と念を押すまで。フロー図の「20秒以上経過」。</summary>
        private const float SecondRoundManganHintSeconds = 20f;

        /// <summary>『おまかせ』へ逃がすまで。フロー図の「１分以上経過」。</summary>
        private const float SecondRoundAutoHintSeconds = 60f;

        /// <summary>
        /// 2回目の手牌選択の練習で、詰まっている人へ段階的に声をかける
        /// （2026-09-30 のフロー図改訂、シート3）。
        ///
        ///   20秒 …「ちゃんと満貫以上になる13牌だぞー」
        ///   1分  …「……無理そうなら左下のボタンを押してちょ」（『おまかせ』へ逃がす）
        ///
        /// **満貫が出来た時点で打ち切る。** フロー図の
        /// 「内部にタイマーを設けて、プレイヤーが満貫以上を作った時にブレークする処理です」。
        /// 出来ている人に「まだできてないの？」と言わせないための条件。
        ///
        /// **数えるのは選び始めてからの時間で、手が止まっている時間ではない。**
        /// 途中まで組んで悩んでいる人にも同じように声をかけたいため
        /// （手を動かしている間は黙る、だと永久に助けが来ない）。
        /// </summary>
        private IEnumerator RunSecondRoundHandTimerHints(TutorialRoundData data)
        {
            yield return StartCoroutine(WaitWhileBuildingHand(data, SecondRoundManganHintSeconds));
            if (!CanHintSecondRoundHand(data)) yield break;

            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine($"ちゃんと{HighlightOpen}満貫以上{HighlightClose}になる13牌だぞー"),
            }));

            yield return StartCoroutine(WaitWhileBuildingHand(
                data, SecondRoundAutoHintSeconds - SecondRoundManganHintSeconds));
            if (!CanHintSecondRoundHand(data)) yield break;

            // **『おまかせ』を出してから指す。** 逃げ道を指したのにボタンが無い、
            // ということが起きないように
            SetHandButtonStage(HandButtonStage.AutoAndDecide);

            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine($"……無理そうなら{HighlightOpen}左下のボタン{HighlightClose}を押してちょ"),
            }));

            GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                ? gameUIManager.HandUI.AutoManganButtonRect : null);
        }

        /// <summary>満貫が出来たら早めに抜ける待ち。1秒ごとに見る。</summary>
        private IEnumerator WaitWhileBuildingHand(TutorialRoundData data, float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                if (!CanHintSecondRoundHand(data)) yield break;
                yield return new WaitForSeconds(1f);
                waited += 1f;
            }
        }

        /// <summary>まだ声をかけてよいか。局が変わった・満貫が出来た・選択が終わったら黙る。</summary>
        private bool CanHintSecondRoundHand(TutorialRoundData data)
        {
            return _round == data
                   && !_aborted
                   && _isWaitingForHandSelectionComplete
                   && !IsSelfMadeManganHand();
        }

        /// <summary>
        /// プレイヤーの手番を自動で1手打つ。
        ///
        /// チュートリアルの打牌は手牌ではなく山牌から捨てる仕組みなので、
        /// GameUIManager.DiscardSelectedTile のチュートリアル分岐と同じ経路で山から河へ移す。
        /// </summary>
        /// <param name="avoidBases">
        /// 打たない牌種（両方の河にもう出ている牌種）。渡すと、まだ出ていない牌種＝新規牌を山の頭から探して打つ。
        /// **ボルテージを切らさないため**（2026-10-24 ユーザー指摘「女の子との選択肢が出るとこでふつーにボルテージが切れてる
        /// 切れない牌を出すようにして」）。以前は山の頭の牌をそのまま打っていた。全部出ていたら山の頭を打つ。
        /// 呼び出し側の控えに漏れがあっても切らさないよう、ボルテージ自身の記録（<see cref="VoltageSystem.IsNewTileType"/>）も見る
        /// </param>
        private IEnumerator AutoDiscardForPlayer(HashSet<int> avoidBases = null)
        {
            if (gameUIManager == null) yield break;

            var wall = gameUIManager.WallUI;
            var river = gameUIManager.RiverUI;
            if (wall == null || river == null) yield break;

            var slots = wall.GetWallSlots();
            if (slots == null || slots.Count == 0) yield break;

            int tileId = -1;
            foreach (var slot in slots)
            {
                var candidate = slot != null ? slot.GetComponent<TileInteraction>() : null;
                if (candidate == null || candidate.TileId < 0) continue;
                if (tileId < 0) tileId = candidate.TileId;   // 新規牌が無いときに打つ山の頭
                if (avoidBases == null) break;
                if (!avoidBases.Contains(TutorialTiles.BaseOf(candidate.TileId)) && VoltageSystem.IsNewTileType(candidate.TileId))
                {
                    tileId = candidate.TileId;
                    break;
                }
            }
            if (tileId < 0) yield break;

            // どの牌種を捨てたかを残す。第2局は「まだ川に出ていない牌」を
            // 敵に打たせるので、自動で捨てたぶんも数えに入れる必要がある
            _lastAutoDiscardBaseId = TutorialTiles.BaseOf(tileId);

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
