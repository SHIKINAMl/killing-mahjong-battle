using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // 対局フェイズ②。
        //
        // 出どころはフロー図（km-docs/tutorial/flow_20261008.drawio）の4枚目「対局フェイズ②」。
        //   2026-10-06 … 13 → 44 ノード（Discord の指示 1557038206350794772）
        //   2026-10-08 … 44 → 71 ノード（Discord の指示 1557407361621033092）
        //                 ボルテージの説明が「フリテン」の話から「新規牌とボーナス」の話へ
        //                 書き換わり、ゲージを実際に見せる運びになった。
        //                 敵の10打目に、制限時間の話が足された。
        //
        // 流れ（矢印はフロー図のとおり）
        //
        //   対局フェイズ②開始 → プレイヤー後攻から
        //     →「じゃあ早速対局開始！」→「今度はアタシから」      … 台本アセットの onBattleStartLines
        //     → 普通の対局
        //          敵はプレイヤーがロンできる牌を一切打たない
        //          敵はまだ川に出ていない牌を打ち続ける
        //     → if 敵5打目
        //     →「さてと 後輩ちゃんも慣れてきたことだし」→ フラッシュ
        //     → ボルテージの説明用会話（RunVoltageIntroduction。途中で敵が5打目を打つ）
        //     → 敵10打目まで汎用対局会話
        //     → if 敵10打目 → フラッシュ → 制限時間の話（RunTimeLimitTalk）
        //     → 打牌数UI表示＆ハイライト
        //
        // **フロー図はここで途切れている。** 「流出説明」の枠は本線から外して脇に置かれたまま。
        // なので打牌数UIのハイライトより先は **これまでの第2局のまま**
        // （自動で流す → 残り2手を打たせる → 流局）にしてある。
        // 描き足されたら、<see cref="SecondBattleResumeLines"/> から先を差し替えること。
        //
        // 「敵10打目まで汎用対局会話」の枠には、プレイヤーがアワセをした／ボルテージの段階を
        // 初めて上げたときの特殊セリフを流す、という条件だけが描いてある。
        // **セリフそのものがまだ図に無い**ので、ここでは何も流していない。

        /// <summary>
        /// 何打目の手前でボルテージの話を始めるか。フロー図の「if 敵5打目」。
        ///
        /// **5打目を打つ前**に話し始め、説明の途中で5打目を打つ（図の「敵、5打目を打つ」）。
        /// 打つ前の時点でゲージには4打ぶんが溜まっている（図の注記のとおり、
        /// 隠していただけで数えてはいる）。
        /// </summary>
        private const int VoltageIntroEnemyTurn = 5;

        /// <summary>何打目の手前で制限時間の話をするか。フロー図の「if 敵10打目」。</summary>
        private const int TimeLimitTalkEnemyTurn = 10;

        /// <summary>
        /// 自動打牌（<see cref="AutoDiscardForPlayer"/>）が最後に捨てた牌種。無ければ -1。
        /// 手で打ったぶんは <see cref="_lastPlayerDiscardBaseId"/> に入るが、
        /// 自動のほうはどこにも残っていなかったので、ここで受ける。
        /// </summary>
        private int _lastAutoDiscardBaseId = -1;

        /// <summary>
        /// ボルテージのゲージを見せてよいか（2026-10-08）。
        ///
        /// フロー図の「初期非表示UI：ボルテージUI」→ 第2局の「ボルテージUI表示」。
        /// 先輩が紹介するまで伏せておき、紹介したあとは打牌フェイズのたびに出す。
        /// 出し入れそのものは GameUIPhaseController が行い、ここは「もう見せてよい」の旗だけ持つ。
        /// </summary>
        public bool IsVoltageUiRevealed { get; private set; }

        private static readonly List<TutorialLine> VoltageIntroLeadLines = new List<TutorialLine>
        {
            new TutorialLine("さてと\n後輩ちゃんも慣れてきたことだし"),
        };

        private static readonly List<TutorialLine> VoltageIntroOpeningLines = new List<TutorialLine>
        {
            new TutorialLine("後輩ちゃんに対局を盛り上げる機能のご案内～"),
            new TutorialLine("危険なコトをすればするほどボーナスがもらえる……"),
            new TutorialLine("そんなチキンレースを生み出すシステムね"),
            new TutorialLine("とりまアタシの捨てた牌をみてー"),
        };

        private static readonly List<TutorialLine> VoltageIntroEnemyRiverLines = new List<TutorialLine>
        {
            new TutorialLine("アタシの捨てた牌さ\nまだ場にでたことがない牌ばっかりなんだよね"),
        };

        private static readonly List<TutorialLine> VoltageIntroNewTileLines = new List<TutorialLine>
        {
            new TutorialLine("こういうまだ場に出たことのない牌のことを新規牌っていうんだけど……"),
            new TutorialLine("この新規牌を連続で出しつづけているとボーナスがもらえるんだよね"),
        };

        private static readonly List<TutorialLine> VoltageIntroNameLines = new List<TutorialLine>
        {
            new TutorialLine("これはボルテージ"),
        };

        private static readonly List<TutorialLine> VoltageIntroGaugeLines = new List<TutorialLine>
        {
            new TutorialLine("本当はアタシたちの昂ぶりを見抜く魔法具なんだけど"),
            new TutorialLine("今回は新規牌の連続を感知してくれるんだよね"),
            new TutorialLine("例えばアタシがこんな風に新規牌を打つと―"),
        };

        private static readonly List<TutorialLine> VoltageIntroChargedLines = new List<TutorialLine>
        {
            new TutorialLine("こんな風にボルテージが溜まってボーナスがもらえるってワケ"),
            new TutorialLine("ボーナスを持ったままアガれたら……"),
        };

        private static readonly List<TutorialLine> VoltageIntroBonusLines = new List<TutorialLine>
        {
            new TutorialLine("ボーナスの数だけ獲得点が増えるんだ！"),
        };

        private static readonly List<TutorialLine> VoltageIntroKeepGoingLines = new List<TutorialLine>
        {
            new TutorialLine("連続で新規牌を出せば出すほどボーナスは増えるから\nどんどん出して行こう！"),
            new TutorialLine("逆にチキって既出の牌出したらボーナスは減るから\nそこはヨロシクね"),
        };

        private static readonly List<TutorialLine> VoltageIntroAfterPanelLines = new List<TutorialLine>
        {
            new TutorialLine("まっ…こういう危険をボーナスに変える仕組みがあるから活用してみてよ"),
        };

        private static readonly List<TutorialLine> VoltageIntroWhyLines = new List<TutorialLine>
        {
            new TutorialLine("なんで新規牌を出し続けることが危険なのかって？"),
            new TutorialLine("うーん\nそれは自分で考えてみてって\n感じかなぁ"),
        };

        private static readonly List<TutorialLine> VoltageIntroYourTurnLines = new List<TutorialLine>
        {
            new TutorialLine("まっ…ひとまず君の番だから打って！"),
        };

        /// <summary>
        /// 「ボルテージ説明用UI」の**仮の板**。
        ///
        /// フロー図は説明用のUIを出して「閉じる」を押させるが、**その絵がまだ無い。**
        /// 文面は企画の領分なのでこちらでは書かず、17歩ルールのときと同じく
        /// 「ここに入ります」とだけ出している。絵が届いたら
        /// <see cref="VoltagePanelSpritePath"/> に置き場所を書けば差し替わる。**AIで絵を描かないこと。**
        /// </summary>
        private const string VoltagePanelTitle = "ボルテージの説明";
        private static readonly string[] VoltagePanelPlaceholder =
        {
            "（ここにボルテージ説明用UIが入ります）",
            "画像が届いたら差し替えます",
        };

        /// <summary>ボルテージ説明用UIの絵。Resources 直下からの相対パス（拡張子なし）。まだ無いので null。</summary>
        private const string VoltagePanelSpritePath = null;

        private static readonly List<TutorialLine> TimeLimitTalkLinesA = new List<TutorialLine>
        {
            new TutorialLine("うん？\nこんなにちんたらしていていいのかって？"),
            new TutorialLine("……そうだねー\nここは悪いヤツのアジト\nちんたらしてると殺される"),
        };

        private static readonly List<TutorialLine> TimeLimitTalkLinesB = new List<TutorialLine>
        {
            new TutorialLine("……そういう設定かもだ"),
            new TutorialLine("フフフ"),
            new TutorialLine("ご察しの通り実際にアタシたちには制限時間が設けられてる"),
        };

        /// <summary>打牌数UIを囲んで見せておく秒数。図はここで終わっていて、消す合図が無い。</summary>
        private const float TurnCountHighlightSeconds = 2.5f;

        /// <summary>
        /// 敵が牌を置いてから、光がゲージへ届いて段が上がりきるまで待つ秒数。
        /// 図の「ボルテージが貯まる演出」。これより短いと、溜まる前に次のセリフが出る。
        /// </summary>
        private const float VoltageChargeWaitSeconds = 1.8f;

        /// <summary>
        /// 制限時間の話のあと、自動で流し始める前の一言。
        ///
        /// **フロー図に無い。** 図が打牌数UIのハイライトで途切れているので、
        /// その先はこれまでの第2局をそのまま続けている。このセリフも旧台本の
        /// 対局開始時のもの（口調が前の人格のまま）で、黙って操作を取り上げないために
        /// 残してある。続きが描かれたら、ここから先を差し替えること。
        /// </summary>
        private static readonly List<TutorialLine> SecondBattleResumeLines = new List<TutorialLine>
        {
            new TutorialLine("しばらく黙って見ていなさい。勝手に進めるわ。"),
        };

        /// <summary>
        /// 第2局の対局をこの専用の運びで進めるか。
        ///
        /// **流局で終わる局のときだけ。** ロンで決着する台本に差し替えられた場合、
        /// ここは決着の処理を持っていないので、従来の <see cref="RunBattle"/> へ任せる。
        /// </summary>
        private bool UsesSecondRoundBattle(TutorialRoundData data)
        {
            return IsSecondTutorialRound(data) && data.outcome == TutorialOutcome.Draw;
        }

        /// <summary>
        /// 対局フェイズ②。**敵が先に打つ**（フロー図「プレイヤー後攻から」「今度はアタシから」）。
        ///
        /// <see cref="RunBattle"/> は「プレイヤー → 敵」の順で、ロンの判定もその順に
        /// 組んであるので、向きを変える分岐を足すより別に書いたほうが読める。
        /// </summary>
        private IEnumerator RunSecondRoundBattle(TutorialRoundData data)
        {
            var board = BoardStateManager.Instance;
            int turns = data.enemyDiscardBaseIds.Count;
            int autoTurns = Mathf.Clamp(data.autoDiscardTurns, 0, turns);
            int lastReactionIndex = -1;

            // 両方の河に出た牌種。「まだ川に出ていない牌」を選ぶのに使う
            var seen = new HashSet<int>();

            // 河の「先：／後：」の表示をフロー図に合わせる（プレイヤーは後攻）
            if (board != null) board.SetLocalPlayerFirstRound(false);

            for (int turn = 1; turn <= turns; turn++)
            {
                if (_aborted) yield break;

                bool enemyAlreadyDiscarded = false;

                if (turn == VoltageIntroEnemyTurn)
                {
                    // --- ボルテージの説明。**この中で敵が5打目を打つ** ---
                    yield return StartCoroutine(RunVoltageIntroduction(data, turn, seen));
                    if (_aborted) yield break;
                    enemyAlreadyDiscarded = true;
                }
                else if (turn == TimeLimitTalkEnemyTurn)
                {
                    // --- 制限時間の話（敵の10打目の手前） ---
                    yield return StartCoroutine(RunTimeLimitTalk());
                    if (_aborted) yield break;

                    // ここから先はフロー図に無いので、これまでの第2局のまま流す
                    if (turn <= autoTurns)
                        yield return StartCoroutine(PlayLines(SecondBattleResumeLines));
                }

                // 制限時間の話までは自分で打たせる（図の「普通の対局」「敵10打目まで汎用対局会話」）。
                // そのあとは従来どおり、autoDiscardTurns までを自動で流す
                bool isAutoTurn = turn >= TimeLimitTalkEnemyTurn && turn <= autoTurns;

                // --- 敵の打牌（先攻） ---
                if (!enemyAlreadyDiscarded)
                {
                    if (board != null) board.SetLocalTurn(false);
                    DiscardForEnemyInSecondBattle(data, turn, seen);
                    yield return new WaitForSeconds(isAutoTurn ? autoDiscardInterval : discardInterval);
                }

                // 自動打牌が終わってプレイヤーの番になる境目でセリフを挟む。
                // **プレイヤーが打つ直前に言う。** 「好きなのを捨てなさい」のあとに
                // 相手の打牌が挟まると、誰に言ったのか分からなくなる
                if (autoTurns > 0 && turn == autoTurns + 1)
                {
                    yield return StartCoroutine(PlayLines(data.beforeManualDiscardLines));
                }

                // --- プレイヤーの打牌（後攻） ---
                if (board != null) board.SetLocalTurn(true);

                if (isAutoTurn)
                {
                    _lastAutoDiscardBaseId = -1;
                    yield return StartCoroutine(AutoDiscardForPlayer());
                    if (_lastAutoDiscardBaseId >= 0) seen.Add(_lastAutoDiscardBaseId);
                }
                else
                {
                    _isWaitingForDiscard = true;
                    _lastPlayerDiscardBaseId = -1;

                    yield return new WaitUntil(() => !_isWaitingForDiscard || _aborted);
                    if (_aborted) yield break;

                    if (_lastPlayerDiscardBaseId >= 0) seen.Add(_lastPlayerDiscardBaseId);
                }

                yield return new WaitForSeconds(isAutoTurn ? autoDiscardInterval : discardInterval);

                // 制限時間の話までは、打った牌にひとこと返す
                // （図の「普通の対局」と「敵10打目まで汎用対局会話」）。
                //
                // フロー図は「先輩用の汎用対局反応を流す　先輩の反応一覧は後記」で、
                // **一覧そのものはまだ図に無い。** 第1局で使っている汎用の反応
                // （<see cref="BuildDiscardReaction"/>）を先輩の反応として流用している。
                // 一覧が届いたら差し替えること
                if (!isAutoTurn && turn < TimeLimitTalkEnemyTurn)
                {
                    yield return StartCoroutine(PlayLines(
                        BuildDiscardReaction(_lastPlayerDiscardBaseId, ref lastReactionIndex)));
                }

                if (!isAutoTurn) yield return new WaitForSeconds(0.4f);
            }

            if (data.outcome == TutorialOutcome.Draw)
            {
                yield return StartCoroutine(RunDraw(data));
            }
        }

        /// <summary>敵が1枚打つ。河へ置き、打牌音を鳴らす。打った牌種を返す。</summary>
        private int DiscardForEnemyInSecondBattle(TutorialRoundData data, int turn, HashSet<int> seen)
        {
            int discardBase = PickEnemyDiscardForSecondBattle(data, turn, seen);
            int discardId = TutorialTiles.Encode(discardBase, discardBase == data.doraBaseId);
            seen.Add(discardBase);

            if (gameUIManager != null && gameUIManager.EnemyRiverUI != null)
                gameUIManager.EnemyRiverUI.AddTile(discardId);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDiscardSE();
            return discardBase;
        }

        /// <summary>
        /// 敵が打つ牌を決める。フロー図の仕様は2つ。
        ///
        ///   ・プレイヤーがロンできる牌を一切打たない
        ///   ・まだ川に出ていない牌を打ち続ける
        ///
        /// **台本の並び（enemyDiscardBaseIds）をそのまま打つだけでは守れない。**
        /// この局はプレイヤーが山から好きな牌を捨てるので、台本の次の牌を
        /// プレイヤーが先に捨てていることがある。そこで台本の並びを優先順位として使い、
        /// どちらの河にもまだ出ていない牌種を前から探す。
        ///
        /// 牌種には限りがあるので、探しても無いときは台本の牌へ戻る
        /// （ロンできる牌だけは、そのときも避ける）。ボルテージの説明は5打目なので、
        /// そこまでに尽きることは無い。
        /// </summary>
        private int PickEnemyDiscardForSecondBattle(TutorialRoundData data, int turn, HashSet<int> seen)
        {
            var waits = data.waitBaseIds ?? new List<int>();

            // 1) 台本の並びの中から、まだ出ていない牌種
            foreach (int candidate in data.enemyDiscardBaseIds)
            {
                if (seen.Contains(candidate)) continue;
                if (waits.Contains(candidate)) continue;
                return candidate;
            }

            // 2) 台本に無い牌種も含めて、まだ出ていないもの
            for (int candidate = 0; candidate <= TutorialTiles.MaxKind; candidate++)
            {
                if (seen.Contains(candidate)) continue;
                if (waits.Contains(candidate)) continue;
                return candidate;
            }

            // 3) もう未出の牌種が無い。台本の牌をそのまま打つ（ロンできる牌は避ける）
            int scripted = data.enemyDiscardBaseIds[Mathf.Clamp(turn - 1, 0, data.enemyDiscardBaseIds.Count - 1)];
            if (!waits.Contains(scripted)) return scripted;

            foreach (int candidate in data.enemyDiscardBaseIds)
            {
                if (!waits.Contains(candidate)) return candidate;
            }
            return scripted;
        }

        /// <summary>
        /// ボルテージのゲージを出す。図の「ボルテージUI表示」。
        /// 以後、打牌フェイズのあいだは出たままになる（<see cref="IsVoltageUiRevealed"/>）。
        /// </summary>
        private void RevealVoltageUi()
        {
            IsVoltageUiRevealed = true;
            UI.VoltageUI.EnsureCreated();
            UI.VoltageUI.SetCanvasVisible(true);
        }

        /// <summary>
        /// ボルテージの説明ひとまとまり。フロー図4枚目の「ボルテージの説明用会話」。
        /// **途中で敵が5打目を打つ。** 呼んだ側は、この巡の敵の打牌を飛ばすこと。
        /// </summary>
        /// <param name="turn">いまの巡（＝敵がこれから打つのが何打目か）。セリフの「○回連続」に入る</param>
        private IEnumerator RunVoltageIntroduction(TutorialRoundData data, int turn, HashSet<int> seen)
        {
            var board = BoardStateManager.Instance;

            yield return StartCoroutine(PlayLines(VoltageIntroLeadLines));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(VoltageIntroOpeningLines));

            // 敵の川ハイライト
            var enemyRiver = gameUIManager != null ? gameUIManager.EnemyRiverUI : null;
            GuideTo(enemyRiver != null ? enemyRiver.GetTilesBoundsRect() : null,
                false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(VoltageIntroEnemyRiverLines));

            // プレイヤーの川ハイライト。「新規牌」の話のあいだ囲んでおく
            // （図は消す場所を描いていない。次にゲージを出す所で外している）
            var playerRiver = gameUIManager != null ? gameUIManager.RiverUI : null;
            GuideTo(playerRiver != null ? playerRiver.GetTilesBoundsRect() : null,
                false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(VoltageIntroNewTileLines));
            ClearGuide();

            // ボルテージUI表示 →「これはボルテージ」
            RevealVoltageUi();
            yield return new WaitForSeconds(0.4f);
            yield return StartCoroutine(PlayLines(VoltageIntroNameLines));

            // ボルテージUIハイライト。**自分のと相手のと、両方を囲む。**
            // 図は「ボルテージUI」とだけ書いている（相手だけを指すのは、あとの
            // 「敵ボルテージハイライト」）。離れているので枠は2つ出す
            GuideTo(UI.VoltageUI.GetGaugeRect(isEnemy: false), false, null, UI.TutorialHighlightUI.Style.Frame);
            UI.TutorialHighlightUI.ShowSecond(UI.VoltageUI.GetGaugeRect(isEnemy: true),
                UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(VoltageIntroGaugeLines));
            ClearGuide();

            // 敵、5打目を打つ → ボルテージが貯まる演出
            // （河に置くと RiverUI が光を飛ばし、届いた所でゲージが進む）
            if (board != null) board.SetLocalTurn(false);
            DiscardForEnemyInSecondBattle(data, turn, seen);
            yield return new WaitForSeconds(VoltageChargeWaitSeconds);

            yield return StartCoroutine(PlayLines(VoltageIntroChargedLines));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(VoltageIntroBonusLines));

            // X は「今の敵のボルテージのボーナス倍率」（図の注記）。回数も実際の数を入れる。
            // 実行時に決まる文なので、強調の印（⟦ ⟧）はここで直接書く
            string multiplier = VoltageSystem.GetMultiplier(isEnemy: true).ToString("0.#");
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine(
                    $"ちなみにアタシは{ToFullWidthDigits(turn)}回連続で新規牌を出したから…\n"
                    + $"{Tutorial.TutorialEmphasis.Open}獲得点は{multiplier}倍{Tutorial.TutorialEmphasis.Close}かな"),
            }));

            // 敵ボルテージハイライト
            GuideTo(UI.VoltageUI.GetGaugeRect(isEnemy: true), false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(VoltageIntroKeepGoingLines));
            ClearGuide();

            // ボルテージ説明用UI表示 → プレイヤーが「閉じる」を押す
            if (dialogueUI != null) dialogueUI.HideText();
            yield return ShowRulePanelRoutine(BorrowJapaneseFont(), VoltagePanelTitle,
                VoltagePanelPlaceholder, VoltagePanelSpritePath);

            yield return StartCoroutine(PlayLines(VoltageIntroAfterPanelLines));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(VoltageIntroWhyLines));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(VoltageIntroYourTurnLines));
        }

        /// <summary>
        /// 制限時間の話。フロー図4枚目の「if 敵10打目」から「打牌数UI表示＆ハイライト」まで。
        /// </summary>
        private IEnumerator RunTimeLimitTalk()
        {
            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(TimeLimitTalkLinesA));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(TimeLimitTalkLinesB));

            // 打牌数UI表示＆ハイライト。打牌数（「後：十打目」）は河が持っている
            RectTransform turnCount = null;
            if (gameUIManager != null)
            {
                if (gameUIManager.RiverUI != null) turnCount = gameUIManager.RiverUI.TurnTextRect;
                if (turnCount == null && gameUIManager.EnemyRiverUI != null)
                    turnCount = gameUIManager.EnemyRiverUI.TurnTextRect;
            }
            if (turnCount != null)
            {
                turnCount.gameObject.SetActive(true);
                GuideTo(turnCount, false, null, UI.TutorialHighlightUI.Style.Frame);
                yield return new WaitForSeconds(TurnCountHighlightSeconds);
                ClearGuide();
            }
        }

        /// <summary>
        /// 数字を全角にする。フロー図のセリフは回数を「５回」と全角で書いているので、
        /// 表記をそこへ合わせるためだけに使う。
        /// </summary>
        private static string ToFullWidthDigits(int value)
        {
            var chars = value.ToString().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '0' && chars[i] <= '9') chars[i] = (char)('０' + (chars[i] - '0'));
            }
            return new string(chars);
        }
    }
}
