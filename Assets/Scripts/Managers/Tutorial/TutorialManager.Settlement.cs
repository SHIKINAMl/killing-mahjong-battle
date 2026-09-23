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
        // TutorialManager: 決着（プレイヤー／敵のロン、清算パネル、ロン演出）


        private IEnumerator RunPlayerRon(TutorialRoundData data, int ronTileId)
        {
            SetPhase(RoundStatus.Agari);

            // ロンボタンを押させる。対局と同じ RonWaitPanel を出す（要望15）。
            // 以前は AgariSelectionUI を使っていて、本編と見た目が違っていた。
            if (gameUIManager != null && gameUIManager.RonWaitPanel != null)
            {
                bool selected = false;
                gameUIManager.ShowRonWaitPanelForTutorial(() => selected = true);
                yield return new WaitUntil(() => selected);
            }

            // 増減は GameRules の式で決まる。得る額と失う額は別計算なので一致しない。
            int prevEnemyHp = _enemyHp;
            int prevPlayerHp = _playerHp;

            int han = GetWinnerHan(data, isPlayerWin: true);

            // **賭け金は ClearStakes() の前に控える。** 清算パネルの素点も計算式も
            // ここの額から作るので、0 になったフィールドを後から読むと式が消える
            // （実際に消えていた。2026-08-29 に発覚。§BuildScoreFormula は stake<=0 で null を返す）
            int myBet = _playerStake;
            int theirBet = _enemyStake;
            int carryRounds = Mathf.Max(1, _stakeRounds);

            int gain = GameRules.CalculateWinnerGain(myBet, han);
            int loss = GameRules.CalculateLoserLoss(theirBet, han, data.isTankiWin);
            ClearStakes();

            _playerHp = prevPlayerHp + gain;
            _enemyHp = Mathf.Max(0, prevEnemyHp - loss);

            var settlementInfo = BuildSettlementInfo(
                data, isLocalWin: true, han: han, myBet: myBet, theirBet: theirBet,
                carryRounds: carryRounds, myDelta: gain, theirDelta: -loss,
                prevLocalHp: prevPlayerHp, newLocalHp: _playerHp,
                prevEnemyHp: prevEnemyHp, newEnemyHp: _enemyHp);
            bool isFirstTutorialRound = IsFirstTutorialRound(data);

            // 累計ゲージはこの局のロン後に初めて説明する。ロン演出中に見せると、
            // フロー図の「獲得表示 → 累計獲得点数バー」の順番が崩れる。
            if (isFirstTutorialRound && gameUIManager != null)
                gameUIManager.ScoreGauge.SetVisible(false);

            // 演出に出す額は「自分の血がどれだけ動いたか」に合わせる
            int settlement = gain;

            // RonAnimationUI は handTiles を並べたあとに ronTile を別枠で追加描画する。
            // したがって handTiles にはアタリ牌を含めない13枚を渡すこと。
            var hand = TutorialTiles.EncodeAll(data.manganHandBaseIds, data.doraBaseId);

            yield return StartCoroutine(PlayRonAnimation(
                hand, ronTileId, data, isLocalPlayerWin: true,
                prevLocalHp: prevPlayerHp, newLocalHp: _playerHp,
                prevEnemyHp: prevEnemyHp, newEnemyHp: _enemyHp,
                displayScore: settlement,
                scoreFormula: BuildScoreFormula(myBet, han),
                settlement: isFirstTutorialRound ? null : settlementInfo,
                suppressSettlementPanel: isFirstTutorialRound,
                deferHpUpdate: isFirstTutorialRound));

            if (isFirstTutorialRound)
            {
                yield return StartCoroutine(RunFirstRoundRonAftermath(
                    data, settlementInfo, myBet, theirBet, carryRounds, han, gain,
                    prevPlayerHp, prevEnemyHp));
                yield break;
            }

            ApplyHpToUI();

            // 賭け金の数字がゲージへ吸い込まれ、そのあとゲージが伸びる。
            // ロン演出のあとに呼ぶこと。演出中は盤面ごと隠れていて見えない
            if (gameUIManager != null) gameUIManager.ScoreGauge.AbsorbStakesIntoGauge(true, gain);
        }

        /// <summary>
        /// 第1局のロン後だけに続く40手順。結果の判定や数値はすでに <see cref="RunPlayerRon"/> が
        /// 台本と <see cref="GameRules"/> から確定しており、ここはその既存結果を順番どおりに見せるだけ。
        /// </summary>
        private IEnumerator RunFirstRoundRonAftermath(TutorialRoundData data, UI.RonSettlementInfo settlement,
            int myBet, int theirBet, int carryRounds, int han, int gain,
            int prevPlayerHp, int prevEnemyHp)
        {
            var ronUI = gameUIManager != null ? gameUIManager.RonAnimationUI : null;
            var gauge = gameUIManager != null ? gameUIManager.ScoreGauge : null;

            // 1〜3. ロンの直後に、清算の説明へ入る。
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("その牌かぁぁ！！！"),
                new TutorialLine("や…やるじゃーん"),
                new TutorialLine("…それじゃあ賭け金を精算しようか"),
            }));

            // 4〜5. 既存の清算表を出してから左へ寄せ、立ち絵の顔を隠さない。
            if (ronUI != null)
            {
                ronUI.ShowTutorialSettlement(settlement);
                yield return null;
                ronUI.MoveTutorialSettlementLeft();
            }

            // 6〜7. 表全体を指してから、何を読む表なのかを説明する。
            GuideTo(ronUI != null ? ronUI.TutorialSettlementGuideTarget : null, false, new Vector2(0f, 18f),
                UI.TutorialHighlightUI.Style.Frame);
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("これが点数計算表ね"),
                new TutorialLine("ここには君がどれだけ報酬をもらえるかの計算式が書かれているの"),
            }));
            ClearGuide();

            // 8〜11. 払い戻しの計算欄を指して、満貫での増え方を説明する。
            GuideTo(ronUI != null ? ronUI.TutorialRefundFormulaGuideTarget : null, false, new Vector2(0f, 18f),
                UI.TutorialHighlightUI.Style.Band);
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("今回君は2000を賭けたから"),
                new TutorialLine("賭けた分の2000が帰ってきて、さらに役の分のボーナスがもらえる"),
                new TutorialLine("今回は満貫だから2000儲けたって感じかな"),
                new TutorialLine("役満とかもっと強い役を作るとガンガン儲けられるよ"),
            }));
            ClearGuide();

            // 12. 表を消すのと同時に、通常清算と同じ既存の血移動で獲得を見せる。
            if (ronUI != null)
            {
                yield return StartCoroutine(ronUI.PlayTutorialSettlementTransfer(
                    settlement, gameUIManager.PlayerInfoUI, gameUIManager.EnemyInfoUI,
                    prevPlayerHp, _playerHp, prevEnemyHp, _enemyHp));
            }
            else
            {
                ApplyHpToUI();
            }

            // 表を読む間に残しておいた賭け金を、既存の累計ゲージへ吸収する。
            if (gauge != null) gauge.AbsorbStakesIntoGauge(true, gain);

            // 13〜16. 初めて累計ゲージを出し、勝利ラインを説明する。
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("あ…大事なことを言い忘れてた！"),
            }));

            if (gauge != null) gauge.SetVisible(true);
            // 14. 累計バーも面で示す。画面の上辺に貼りついた細い帯なので、
            //     矢印を下から当てても「どこからどこまでがバーなのか」が伝わらない。
            GuideTo(gauge != null ? gauge.GuideTargetRect : null, false, new Vector2(0f, -36f),
                UI.TutorialHighlightUI.Style.Band);
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("点数を獲得すると儲けた分だけここに蓄積されていくよ"),
                new TutorialLine("累計の獲得点数が30000を越えると……"),
            }));
            ClearGuide();

            // 17〜20. 勝利条件を示す。
            KillingMahjong.UI.Effects.ScreenFlash.Play();
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("なんとゲームに勝利！晴れて脱出だ！"),
                new TutorialLine("だから後輩ちゃんも対局に勝って点数を増やしまくろう！"),
                new TutorialLine("…っと　これが対局に勝った時のやり方ね"),
            }));

            // 21〜22. 同じ対局でも負けたときの説明へ切り替える。
            KillingMahjong.UI.Effects.ScreenFlash.Play();
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("次は対局に負けた…ロンされちゃった時のやり方ね"),
            }));

            // 23. 実際の勝敗は変えず、同額で負けた場合の既存表だけを説明用に組み立てる。
            int previewLoss = GameRules.CalculateLoserLoss(myBet, han, data.isTankiWin);
            int previewGain = GameRules.CalculateWinnerGain(theirBet, han);
            var lossSettlement = BuildSettlementInfo(
                data, isLocalWin: false, han: han, myBet: myBet, theirBet: theirBet,
                carryRounds: carryRounds, myDelta: -previewLoss, theirDelta: previewGain,
                prevLocalHp: prevPlayerHp, newLocalHp: Mathf.Max(0, prevPlayerHp - previewLoss),
                prevEnemyHp: prevEnemyHp, newEnemyHp: prevEnemyHp + previewGain);

            if (ronUI != null)
            {
                ronUI.ShowTutorialSettlement(lossSettlement);
                yield return null;
                ronUI.MoveTutorialSettlementLeft();
            }
            GuideTo(ronUI != null ? ronUI.TutorialDamageFormulaGuideTarget : null, false, new Vector2(0f, 18f),
                UI.TutorialHighlightUI.Style.Band);

            // 24〜26. 負けた場合の差し引きを説明する。
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("負けると逆に賭け金分持ち点を引かれてしまう"),
                new TutorialLine("最初に賭けた額も帰ってこないから…マイナス分は2倍！"),
                new TutorialLine("例えば…あたしは2000賭けたから…2000のダメージ"),
            }));
            ClearGuide();

            // 27〜28. 例として相手側へ既存のダメージ表示だけを出す。実際のHPは変えない。
            if (ronUI != null)
            {
                ronUI.HideTutorialSettlement();
                ronUI.ShowTutorialDamagePreview(gameUIManager.EnemyInfoUI, previewLoss);
            }
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("ちなみに相手が満貫以上の役を作ってたらもっとダメージは増える……"),
            }));

            // 29. この1枚は閉じる操作そのものを体験させるため、セリフ送りでは代用しない。
            var damageExplanation = TutorialDamageExplanationUI.Create();
            bool closedDamageExplanation = false;
            damageExplanation.Open(() => closedDamageExplanation = true);
            yield return new WaitUntil(() => closedDamageExplanation);
            if (damageExplanation != null) Destroy(damageExplanation.gameObject);

            // 30〜32. 賭け金と敗北条件をまとめる。
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("まぁ単純に賭けた分だけリスクを負うって認識でいーよ"),
                new TutorialLine("ただ…ダメージを受けて賭けられる点数が０になったら負け……"),
                new TutorialLine("それは即ち……"),
            }));

            // 33〜39. 最後のフラッシュのあと、対局フェイズ①を締める。
            KillingMahjong.UI.Effects.ScreenFlash.Play();
            yield return StartCoroutine(PlayLines(new List<TutorialLine>
            {
                new TutorialLine("死を意味する……！"),
                new TutorialLine("……なんて冗談冗談！"),
                new TutorialLine("とりまこんな風に対局を繰り返していって"),
                new TutorialLine("先に累計で30000点獲得するか"),
                new TutorialLine("相手の持ち点を０にした方が勝ち！"),
                new TutorialLine("って感じのギャンブルね"),
            }));
        }

        private IEnumerator RunEnemyRon(TutorialRoundData data, int playerDiscardBaseId)
        {
            SetPhase(RoundStatus.Agari);

            // 手順⑯: 単騎で上がられると失う額が2倍になる（GameRules.CalculateLoserLoss）
            int prevPlayerHp = _playerHp;
            int prevEnemyHp = _enemyHp;

            int han = GetWinnerHan(data, isPlayerWin: false);

            // **賭け金は ClearStakes() の前に控える。** 理由は RunPlayerRon 側と同じ
            int myBet = _playerStake;
            int theirBet = _enemyStake;
            int carryRounds = Mathf.Max(1, _stakeRounds);

            int gain = GameRules.CalculateWinnerGain(theirBet, han);
            int loss = GameRules.CalculateLoserLoss(myBet, han, data.isTankiWin);
            ClearStakes();

            _playerHp = Mathf.Max(0, prevPlayerHp - loss);
            _enemyHp = prevEnemyHp + gain;

            // 演出に出す額は「自分の血がどれだけ動いたか」に合わせる
            int settlement = loss;

            // 単騎待ちなので、実際に打たれた牌がそのままアタリ牌になる
            int ronTileBase = playerDiscardBaseId >= 0 ? playerDiscardBaseId : TutorialTiles.Ton;
            int ronTileId = TutorialTiles.Encode(ronTileBase, ronTileBase == data.doraBaseId);

            // 単騎待ちなので、面子12枚＋単騎の1枚（= アタリ牌と同じ牌）で13枚。
            // RonAnimationUI がアタリ牌をもう1枚追加描画し、雀頭が揃った14枚が表示される。
            var hand = TutorialTiles.EncodeAll(data.enemyRonMeldBaseIds, data.doraBaseId);
            hand.Add(ronTileId);

            yield return StartCoroutine(PlayRonAnimation(
                hand, ronTileId, data, isLocalPlayerWin: false,
                prevLocalHp: prevPlayerHp, newLocalHp: _playerHp,
                prevEnemyHp: prevEnemyHp, newEnemyHp: _enemyHp,
                displayScore: settlement,
                // ここで出しているのは自分の損失なので、損失側の式にする
                scoreFormula: BuildScoreFormula(myBet, han, data.isTankiWin),
                settlement: BuildSettlementInfo(
                    data, isLocalWin: false, han: han, myBet: myBet, theirBet: theirBet,
                    carryRounds: carryRounds, myDelta: -loss, theirDelta: gain,
                    prevLocalHp: prevPlayerHp, newLocalHp: _playerHp,
                    prevEnemyHp: prevEnemyHp, newEnemyHp: _enemyHp)));

            ApplyHpToUI();

            // 相手が勝ったので、賭け金は相手側（左）のゲージへ吸い込まれる。
            // ロン演出のあとに呼ぶこと。演出中は盤面ごと隠れていて見えない
            if (gameUIManager != null) gameUIManager.ScoreGauge.AbsorbStakesIntoGauge(false, gain);
        }

        /// <param name="displayScore">
        /// 演出に出す金額。持ち越された賭け金を含む「この局で動いた総額」を渡すこと。
        /// data.score をそのまま出すと、表示額と実際のHPの増減が食い違って見える。
        /// </param>
        /// <summary>
        /// ロン演出に出す計算式を作る。**演出に出している額と式が一致するようにすること。**
        ///
        ///   勝った側 … 自分の賭け金 × 自分の役の倍率
        ///   負けた側 … 自分の賭け金 × 相手の役の倍率（相手が単騎で上がっていれば さらに ×2）
        ///
        /// 勝者の獲得と敗者の損失は別計算なので、どちらを出しているかで式が変わる。
        /// 混ぜると答えが合わなくなる。
        /// </summary>
        /// <summary>
        /// 清算パネルの中身を台本から組む。**本編の <c>GameUIPhaseController.BuildSettlementInfo</c> と対で読むこと。**
        ///
        /// あちらはサーバーの `liquidation` をそのまま写すだけだが、
        /// **チュートリアルはサーバーに繋がないので、正は台本（<see cref="TutorialRoundData"/>）と
        /// <see cref="GameRules"/> になる。** ここで別の式を書くと、同じ局の HP の増減
        /// （`CalculateWinnerGain` / `CalculateLoserLoss` で出したもの）と表の数字が食い違うので、
        /// **増減は呼び出し側で出した値をそのまま受け取る。ここでは計算し直さない。**
        ///
        /// 役の行は本編と同じ <c>GameUIPhaseController.FillYakuRows</c> に任せる。
        /// 台本の役名と翻数が噛み合っていなければ、あちらが行ごとの翻数を伏せて合計だけ出す。
        ///
        /// **強襲はチュートリアルに出てこない**ので常に無し。
        /// </summary>
        private static UI.RonSettlementInfo BuildSettlementInfo(
            TutorialRoundData data, bool isLocalWin, int han, int myBet, int theirBet, int carryRounds,
            int myDelta, int theirDelta, int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp)
        {
            if (data == null) return null;

            var info = new UI.RonSettlementInfo
            {
                RankName = data.rankText,
                TotalHan = han,
                Multiplier = GameRules.GetMultiplier(han),
                CarryRounds = Mathf.Max(1, carryRounds),
                IsTankiWait = data.isTankiWin,
                AssaultApplied = false,
                AssaultBonusDamage = 0,
                LocalWon = isLocalWin,

                // **「自分」「相手」はローカル基準。** 勝った側基準ではない（本編と同じ約束）
                MyBet = myBet,
                TheirBet = theirBet,
                MyDelta = myDelta,
                TheirDelta = theirDelta,

                MyHpBefore = prevLocalHp,
                MyHpAfter = newLocalHp,
                TheirHpBefore = prevEnemyHp,
                TheirHpAfter = newEnemyHp,
            };

            UI.GameUIPhaseController.FillYakuRows(
                info, Common.YakuNameUtil.Summarize(data.yakuList), han);

            return info;
        }

        private static string BuildScoreFormula(int stake, int han, bool tankiDouble = false)
        {
            if (stake <= 0) return null;

            float mult = GameRules.GetMultiplier(han);
            string m = mult.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return tankiDouble ? $"{stake} × {m} × 2" : $"{stake} × {m}";
        }

        /// <param name="scoreFormula">
        /// 「2000 × 1.5」のような計算式。本編はサーバーの liquidation から作るが、
        /// チュートリアルはサーバーに繋がないので GameRules の値から作って渡す。
        /// </param>
        private IEnumerator PlayRonAnimation(
            List<int> handTiles, int ronTileId, TutorialRoundData data, bool isLocalPlayerWin,
            int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, int displayScore,
            string scoreFormula = null, UI.RonSettlementInfo settlement = null,
            bool suppressSettlementPanel = false, bool deferHpUpdate = false)
        {
            var ronUI = gameUIManager != null ? gameUIManager.RonAnimationUI : null;
            if (ronUI == null)
            {
                yield return new WaitForSeconds(2.0f);
                yield break;
            }

            bool done = false;
            ronUI.PlayRonSequence(
                handTiles,
                ronTileId,
                data.yakuList,
                data.formulaText,
                data.rankText,
                displayScore,
                isLocalPlayerWin,
                gameUIManager.PlayerInfoUI,
                gameUIManager.EnemyInfoUI,
                prevLocalHp, newLocalHp,
                prevEnemyHp, newEnemyHp,
                () => done = true,
                scoreFormula,
                settlement,
                suppressSettlementPanel,
                deferHpUpdate);

            yield return new WaitUntil(() => done);
        }

    }
}
