using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // 対局フェイズ②（2026-10-07）。
        //
        // 出どころはフロー図（km-docs/tutorial/flow_20261006.drawio）の
        // 4枚目「対局フェイズ②」。2026-10-06 に 13 → 44 ノードへ描き足された
        // （Discord の指示 1557038206350794772「フロー図を更新しました。
        // チュートリアルの更新お願いします」）。
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
        //     → ボルテージの説明用会話
        //          「後輩ちゃんに対局を盛り上げる機能のご案内～」…
        //          「とりまアタシの捨てた牌をみてー」→ 敵の川ハイライト
        //          「アタシの捨てた牌さ まだ場にでたことがない牌ばっかりなんだよね」
        //          → プレイヤーの川ハイライト →「これがどんだけ危険かというと」…
        //          「例えば……後輩ちゃんがさっきXを捨ててロンされなかったから」
        //             X はプレイヤーが直前に捨てた牌
        //          … → フラッシュ
        //          →「アタシはロンされるかもしれない牌種を４回も続けて打ったんだ！」
        //          →「スゴイ勇気でしょー」
        //
        // **フロー図はここで途切れている。** 続き（また何打かやる → 流局の説明 → …）は
        // 注記に方針が書いてあるだけで、ノードも矢印もまだ無い。「流出説明」の枠は
        // 本線から外して脇に置かれている。なので「スゴイ勇気でしょー」より先は
        // **これまでの第2局のまま**（自動で流す → 残り2手を打たせる → 流局）にしてある。
        // 描き足されたら、<see cref="SecondBattleResumeLines"/> から先を差し替えること。

        /// <summary>
        /// 何打目の手前でボルテージの話を始めるか。フロー図の「if 敵5打目」。
        ///
        /// **5打目を打つ前**に話す。この時点で敵は4枚捨てているので、
        /// セリフの「4回もあった」「４回も続けて打った」と数が合う。
        /// 5枚目を打ってから話すと5回になり、セリフと食い違う。
        /// </summary>
        private const int VoltageIntroEnemyTurn = 5;

        /// <summary>
        /// 自動打牌（<see cref="AutoDiscardForPlayer"/>）が最後に捨てた牌種。無ければ -1。
        /// 手で打ったぶんは <see cref="_lastPlayerDiscardBaseId"/> に入るが、
        /// 自動のほうはどこにも残っていなかったので、ここで受ける。
        /// </summary>
        private int _lastAutoDiscardBaseId = -1;

        private static readonly List<TutorialLine> VoltageIntroLeadLines = new List<TutorialLine>
        {
            new TutorialLine("さてと 後輩ちゃんも慣れてきたことだし"),
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
            new TutorialLine("アタシの捨てた牌さ まだ場にでたことがない牌ばっかりなんだよね"),
        };

        private static readonly List<TutorialLine> VoltageIntroClosingLines = new List<TutorialLine>
        {
            new TutorialLine("スゴイ勇気でしょー"),
        };

        /// <summary>
        /// 説明のあと、自動で流し始める前の一言。
        ///
        /// **フロー図に無い。** 図が「スゴイ勇気でしょー」で途切れているので、
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

                // --- ボルテージの説明（敵の5打目の手前） ---
                if (turn == VoltageIntroEnemyTurn)
                {
                    yield return StartCoroutine(RunVoltageIntroduction(turn - 1, _lastPlayerDiscardBaseId));
                    if (_aborted) yield break;

                    // ここから先はフロー図に無いので、これまでの第2局のまま流す
                    if (turn <= autoTurns)
                        yield return StartCoroutine(PlayLines(SecondBattleResumeLines));
                }

                // 説明が済むまでは自分で打たせる（フロー図の「普通の対局」）。
                // そのあとは従来どおり、autoDiscardTurns までを自動で流す
                bool isAutoTurn = turn >= VoltageIntroEnemyTurn && turn <= autoTurns;

                // --- 敵の打牌（先攻） ---
                if (board != null) board.SetLocalTurn(false);

                int discardBase = PickEnemyDiscardForSecondBattle(data, turn, seen);
                int discardId = TutorialTiles.Encode(discardBase, discardBase == data.doraBaseId);
                seen.Add(discardBase);

                if (gameUIManager != null && gameUIManager.EnemyRiverUI != null)
                    gameUIManager.EnemyRiverUI.AddTile(discardId);
                if (AudioManager.Instance != null) AudioManager.Instance.PlayDiscardSE();

                yield return new WaitForSeconds(isAutoTurn ? autoDiscardInterval : discardInterval);

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

                // 説明までの「普通の対局」では、打った牌にひとこと返す。
                //
                // フロー図は「先輩用の汎用対局反応を流す　先輩の反応一覧は後記」で、
                // **一覧そのものはまだ図に無い。** 第1局で使っている汎用の反応
                // （<see cref="BuildDiscardReaction"/>）を先輩の反応として流用している。
                // 一覧が届いたら差し替えること
                if (!isAutoTurn && turn < VoltageIntroEnemyTurn)
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
        /// （ロンできる牌だけは、そのときも避ける）。説明は5打目の手前なので、
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
        /// ボルテージの説明ひとまとまり。フロー図4枚目の「ボルテージの説明用会話」。
        /// </summary>
        /// <param name="enemyDiscardCount">ここまでに敵が捨てた枚数。セリフの「○回」に入る</param>
        /// <param name="lastPlayerDiscardBaseId">プレイヤーが直前に捨てた牌。セリフの X に入る</param>
        private IEnumerator RunVoltageIntroduction(int enemyDiscardCount, int lastPlayerDiscardBaseId)
        {
            string x = GetTileName(lastPlayerDiscardBaseId);

            yield return StartCoroutine(PlayLines(VoltageIntroLeadLines));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(VoltageIntroOpeningLines));

            // 敵の川ハイライト
            var enemyRiver = gameUIManager != null ? gameUIManager.EnemyRiverUI : null;
            GuideTo(enemyRiver != null ? enemyRiver.GetTilesBoundsRect() : null,
                false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(VoltageIntroEnemyRiverLines));

            // プレイヤーの川ハイライト。
            // **X の話が終わるまで出しておく。** 図は消す場所を描いていないが、
            // 「後輩ちゃんがさっきXを捨てて」はこの河の牌を指しているので、
            // その間は囲んだままにする
            var playerRiver = gameUIManager != null ? gameUIManager.RiverUI : null;
            GuideTo(playerRiver != null ? playerRiver.GetTilesBoundsRect() : null,
                false, null, UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("これがどんだけ危険かというと"),
                new TutorialLine($"アタシはロンされる可能性が{enemyDiscardCount}回もあったってコト"),
                new TutorialLine("どういうことかというと"),
                new TutorialLine("フリテンって言えば伝わるかなー？"),
                new TutorialLine("麻雀はもう既に捨ててある牌種からはロンできないんだよね"),
                new TutorialLine($"例えば……後輩ちゃんがさっき{x}を捨ててロンされなかったから"),
                new TutorialLine($"アタシはこの局では{x}と同じ種類の牌ではロンできないんだよね"),
                new TutorialLine($"後輩ちゃん視点から見れば{x}は絶対にロンされない牌ってことになるね"),
            }));
            ClearGuide();

            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("さて……アタシの言った危険の意味がわかったかな？"),
                new TutorialLine("アタシはまだ捨てられてない牌を連続で打った つまり……"),
            }));

            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine(
                    $"アタシはロンされるかもしれない牌種を{ToFullWidthDigits(enemyDiscardCount)}回も続けて打ったんだ！"),
            }));
            yield return StartCoroutine(PlayLines(VoltageIntroClosingLines));
        }

        /// <summary>
        /// 数字を全角にする。フロー図の最後の決めゼリフだけ「４回」と全角で書いてあるので、
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
