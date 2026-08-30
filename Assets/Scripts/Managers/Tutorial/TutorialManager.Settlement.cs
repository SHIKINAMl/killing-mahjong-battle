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
                settlement: BuildSettlementInfo(
                    data, isLocalWin: true, han: han, myBet: myBet, theirBet: theirBet,
                    carryRounds: carryRounds, myDelta: gain, theirDelta: -loss,
                    prevLocalHp: prevPlayerHp, newLocalHp: _playerHp,
                    prevEnemyHp: prevEnemyHp, newEnemyHp: _enemyHp)));

            ApplyHpToUI();

            // 賭け金の数字がゲージへ吸い込まれ、そのあとゲージが伸びる。
            // ロン演出のあとに呼ぶこと。演出中は盤面ごと隠れていて見えない
            if (gameUIManager != null) gameUIManager.ScoreGauge.AbsorbStakesIntoGauge(true, gain);
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
            string scoreFormula = null, UI.RonSettlementInfo settlement = null)
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
                settlement);

            yield return new WaitUntil(() => done);
        }

    }
}
