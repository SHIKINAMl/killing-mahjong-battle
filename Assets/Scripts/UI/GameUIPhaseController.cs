using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 対局のフェイズ進行と、それに伴う画面の出し入れ。
    ///
    /// このファイルにはフィールドと <see cref="Setup"/> だけを置き、
    /// 責務ごとの本体は同じフォルダの partial ファイルに分けている:
    ///   GameUIPhaseController.Matchmaking.cs    … マッチング待ち・中止
    ///   GameUIPhaseController.RoundFlow.cs      … 対局開始・局から局への繋ぎ
    ///   GameUIPhaseController.Visibility.cs     … フェイズ切り替え時の表示（共通部分）
    ///   GameUIPhaseController.VisibilityCases.cs … 同・フェイズごとの中身
    ///   GameUIPhaseController.ReadyMarks.cs     … 「準備完了」の印と札
    ///   GameUIPhaseController.Betting.cs        … 賭け金
    ///   GameUIPhaseController.Draw.cs           … 流局
    ///   GameUIPhaseController.Ron.cs            … ロン演出の起動と後始末
    ///   GameUIPhaseController.Settlement.cs     … 清算パネルの中身の組み立て
    /// </summary>
    [RequireComponent(typeof(GameUIManager))]
    public partial class GameUIPhaseController : MonoBehaviour
    {
        private GameUIManager uiManager;
        
        private bool _hasShownHandSelectionPrompt = false;
        private bool _hasSentNextRoundForCurrentPhase = false;
        private int _currentRoundIndex = 1;
        private bool _isCarryOverNextRound = false;

        // 手牌選択・ベットの「準備完了」印。phase_completed_notice で立ち、局の頭（Dealing）で落ちる。
        // 印を状態として持っておくのは、通知が HandlePhaseVisibility の保留より先に届くことがあるため。
        // 受け取った瞬間に SetReadyCheck するだけだと、あとから走る ShowReadyBox(true) に消される。
        /// <summary>賭け金確定でスマホが縮むまでの秒数。OnBetConfirmed の ResetZoomRoutine と揃えること</summary>
        private const float BetZoomOutDuration = 0.3f;

        private bool _handSelectionReadyLocal = false;
        private bool _handSelectionReadyEnemy = false;
        private bool _betReadyLocal = false;
        private bool _betReadyEnemy = false;

        /// <summary>
        /// 両者とも手牌を確定し終えたか。**掛け金フェイズへ移る直前**がこの状態。
        ///
        /// 「選び直す」は相手を待っている間は押せてよいが、ここまで来たら手遅れで、
        /// 押されると受理済みの手牌に `select_cancel` が飛んでしまう。
        /// `phase_change` が届くまでの隙間を連打で抜けられるのを、これで塞ぐ。
        /// </summary>
        public bool IsHandSelectionSettledForBoth => _handSelectionReadyLocal && _handSelectionReadyEnemy;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }
    }
}
