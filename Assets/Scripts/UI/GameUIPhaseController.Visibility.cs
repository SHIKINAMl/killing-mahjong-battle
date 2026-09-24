using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // フェイズが切り替わったときの表示の出し入れ。どのフェイズでも共通の部分だけを置く。
    // フェイズごとの中身は GameUIPhaseController.VisibilityCases.cs。
    public partial class GameUIPhaseController
    {

        public void UpdatePhaseStatus(RoundStatus newStatus)
        {
            if (uiManager.CurrentPhaseStatus == newStatus) return;

            // フェイズ遷移時に現在のHPを戦況グラフ用に記録
            if (Managers.BoardStateManager.Instance != null)
            {
                uiManager.RecordHpHistory(
                    Managers.BoardStateManager.Instance.LocalPlayerHp,
                    Managers.BoardStateManager.Instance.EnemyPlayerHp);
            }

            _hasSentNextRoundForCurrentPhase = false;

            uiManager.SetCurrentPhaseStatus(newStatus);
            if (PhaseManager.Instance != null) PhaseManager.Instance.ChangeRoundStatus(newStatus);

            // BGM のこもりは上の `SetCurrentPhaseStatus` が当てている。
            // **ここで二重に呼ばない（2026-08-24 に削除）。**
            // duration が 1.5 と 1.0 で食い違ううえ、あちらにある
            // 「チュートリアル中はかけない」の条件をこちらは持っていなかった。

            if (newStatus == RoundStatus.HandSelection && uiManager.HandUI != null)
            {
                uiManager.HandUI.SetSubmittedState(false);
            }

            bool isGameEndPhase = newStatus == RoundStatus.Agari || 
                                  newStatus == RoundStatus.Ron || 
                                  newStatus == RoundStatus.Result || 
                                  newStatus == RoundStatus.Draw;

            if (!isGameEndPhase && !uiManager.IsTransitioning)
            {
                // コンテナ切り替えはBetting時にHandBaseUI.UpdateLayout内部でガードされる。
                // ここでは呼び出しをスキップしない（ボタン表示の更新のために必要）。
                if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);

                if (uiManager.WallUI != null)
                {
                    uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                    uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                }
            }
            
            // ここは :177 の同値ガードを抜けた先＝本当に段が進んだときだけ通る。
            // フラッシュの可否をここで決め、実際に光らせるのは表示が切り替わる瞬間に任せる。
            _flashOnNextPhaseVisibility = true;

            HandlePhaseVisibility(newStatus);
        }

        /// <summary>
        /// 次の HandlePhaseVisibility でフラッシュを出すか。UpdatePhaseStatus だけが立てる。
        /// 表示の作り直し目的の呼び出しでは立たないので、透視の公開後などには光らない。
        /// </summary>
        private bool _flashOnNextPhaseVisibility = false;

        public void HandlePhaseVisibility(RoundStatus status)
        {
            // UpdatePhaseStatus は :176 で先に status を確定させてからここへ来るため、
            // ここで捨てると「status だけ進んで演出が出ない」状態になる。
            // しかも同じ status の再通知は :164 の同値ガードで弾かれるので二度と復帰しない。
            // 保留して演出明けに実行する。
            //
            // キーに status を含めて畳まないこと。フェイズごとに本体の処理が違い、しかも
            // 冪等ではない（Dealing は _hasShownHandSelectionPrompt / _hasExecutedRonAnimation の
            // リセットと次局の暗転開始を担っている）。1つのキーで畳むと Dealing が
            // HandSelection に上書きされて消え、次局が始まらなくなる。
            // 到着順に積んでおけば、演出が無かった場合と同じ順序で再生される。
            if (uiManager.IsBusyWithTransition)
            {
                uiManager.DeferUntilIdle($"phaseVisibility:{status}", () => HandlePhaseVisibility(status));
                return;
            }

            // フェイズが切り替わる合図として一瞬だけ光らせる。
            //
            // **このメソッドは「フェイズが変わった」ときだけでなく「今のフェイズの表示を
            // 作り直す」ときにも呼ばれる**（透視演出の後の ExposedTileEffectPlayer:91、
            // 手牌決定後の GameUIHandSelectionController:345 など）。
            // 無条件に光らせると、透視で3枚公開したあとにも光ってしまう。
            // 実際に段が進んだときだけ立つ印を見て、その場合に限って光らせる。
            //
            // 印はフラグで持ち、引数では渡さない。保留は同じ key で後勝ちに上書きされるので、
            // ラムダに焼き込むと「進行」の保留が後から来た「作り直し」の保留に潰される。
            //
            // **保留から復帰した場合も、捨てずにここで光らせる。** 上の分岐より前に置くと
            // 別の演出で画面が覆われている最中に光ることになり、何の合図か分からなくなる。
            if (_flashOnNextPhaseVisibility)
            {
                _flashOnNextPhaseVisibility = false;

                // 決着系（Agari / Ron / Result / Draw）は除く。それぞれロン演出・流局演出という
                // 専用の入りを持っていて、そちらでも光らせるため、ここで光らせると二度光る。
                bool isSettlementPhase = status == RoundStatus.Agari ||
                                         status == RoundStatus.Ron ||
                                         status == RoundStatus.Result ||
                                         status == RoundStatus.Draw;
                if (!isSettlementPhase)
                {
                    // この経路は対局中に繰り返し出るため、毎回の「ピン！」で盤面への集中を妨げないよう音は鳴らさない。
                    Effects.ScreenFlash.Play(playSound: false);
                }
            }

            if (status != RoundStatus.Betting && uiManager.BettingUI != null)
            {
                uiManager.BettingUI.HideBettingPhase(true);
            }

            bool showBoardElements = status == RoundStatus.Discard || 
                                     status == RoundStatus.Agari || 
                                     status == RoundStatus.Ron || 
                                     status == RoundStatus.Result || 
                                     status == RoundStatus.Draw;

            bool isGameEndPhase = status == RoundStatus.Agari || 
                                  status == RoundStatus.Ron || 
                                  status == RoundStatus.Result || 
                                  status == RoundStatus.Draw;

            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(showBoardElements);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(showBoardElements);

            // 待ち候補UIは実行時生成を止めている。再有効化時も打牌中だけ表示する。
            if (uiManager.IsWaitDeductionUIEnabled)
            {
                uiManager.WaitDeduction.SetVisible(status == RoundStatus.Discard);
            }
            if (uiManager.EnemyHandUI != null)
            {
                if (isGameEndPhase)
                {
                    var layoutGroup = uiManager.EnemyHandUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
                    if (layoutGroup != null) layoutGroup.enabled = false;
                }
                uiManager.EnemyHandUI.gameObject.SetActive(showBoardElements);
            }
            if (uiManager.EnemyWallUI != null)
            {
                if (isGameEndPhase)
                {
                    var layoutGroup = uiManager.EnemyWallUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
                    if (layoutGroup != null) layoutGroup.enabled = false;
                    uiManager.EnemyWallUI.gameObject.SetActive(false);
                }
                else
                {
                    uiManager.EnemyWallUI.gameObject.SetActive(false);
                }
            }

            switch (status)
            {
                case RoundStatus.Betting:      ApplyBettingVisibility();       break;
                case RoundStatus.Dealing:      ApplyDealingVisibility();       break;
                case RoundStatus.HandSelection: ApplyHandSelectionVisibility(); break;
                case RoundStatus.TurnDecision: ApplyTurnDecisionVisibility();  break;
                case RoundStatus.Discard:      ApplyDiscardVisibility();       break;
                case RoundStatus.Agari:
                case RoundStatus.Ron:
                case RoundStatus.Result:       ApplyAgariVisibility();         break;
                case RoundStatus.Draw:         ApplyDrawVisibility();          break;
            }

            // **ボルテージゲージの出し入れ（2026-09-15 のユーザー指示）。**
            //   チュートリアル: **一度も出さない。**
            //   対局:           打牌フェイズのときだけ出す。
            //
            // 牌を選んでいる間や賭け金を決めている間はゲージが動かないので、
            // 出していても意味が無く、画面が混むだけだった。
            //
            // `TurnDecision` も出す側に入れてある。**あれは打牌のあいだに毎ターン
            // 挟まる**ので、外すと1手ごとにゲージが点いたり消えたりする。
            bool duringDiscard = status == RoundStatus.Discard
                              || status == RoundStatus.TurnDecision;
            VoltageUI.SetCanvasVisible(duringDiscard && !uiManager.IsTutorialMode);

            ReHideTutorialChrome();
        }

        /// <summary>
        /// チュートリアル第1局で牌以外を伏せている間は、**フェイズが変わっても伏せ直す**
        /// （2026-09-12 の指示）。
        ///
        /// 上の `Apply*Visibility` は素直に「そのフェイズで出すべきもの」を出すので、
        /// 賭け金フェイズに入った瞬間に役一覧が戻ってしまう。
        /// **伏せる指示のほうが後から来る**形にして、取りこぼしを無くしている。
        ///
        /// **賭け金パネルそのものは伏せない。** あれは `BettingUI` で別物なので、
        /// 体力表示を伏せたままでも画面下から出てくる（実機で確認済み）。
        /// </summary>
        private void ReHideTutorialChrome()
        {
            if (!uiManager.IsTutorialMode) return;
            if (uiManager.TutorialManager == null) return;
            if (!uiManager.TutorialManager.FirstRoundChromeHidden) return;

            if (uiManager.YakuListUI != null)
            {
                uiManager.YakuListUI.CloseYakuList();
                uiManager.YakuListUI.gameObject.SetActive(false);
            }
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetVitalsVisible(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetVitalsVisible(false);
            if (uiManager.ScoreGauge != null) uiManager.ScoreGauge.SetVisible(false);
            if (uiManager.BetPotUI != null) uiManager.BetPotUI.SetVisible(false);

            // ボルテージゲージも、第1局で伏せているあいだは出さない
            VoltageUI.SetCanvasVisible(false);
        }

        private void UpdateDoraDisplay()
        {
            if (uiManager.DoraDisplayUI == null) return;

            // チュートリアルではドラ表示牌を扱わないので常に隠す
            if (uiManager.IsTutorialMode)
            {
                uiManager.DoraDisplayUI.Hide();
                return;
            }

            int doraId = Managers.BoardStateManager.Instance.CurrentDoraId;
            if (doraId >= 0)
            {
                uiManager.DoraDisplayUI.ShowDora(doraId);
            }
            else
            {
                uiManager.DoraDisplayUI.Hide();
            }
        }

        public void SetMatchUIVisibility(bool visible)
        {
            if (uiManager.HandUI != null) uiManager.HandUI.gameObject.SetActive(visible);
            if (uiManager.WallUI != null) uiManager.WallUI.gameObject.SetActive(visible);
            if (uiManager.EnemyWallUI != null) uiManager.EnemyWallUI.gameObject.SetActive(visible);
            if (uiManager.YakuListUI != null) 
            {
                uiManager.YakuListUI.gameObject.SetActive(visible);
                uiManager.YakuListUI.CloseYakuList();
            }
            
            if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                if (!visible) uiManager.WaitUI.gameObject.SetActive(false);
            }

            // **ここでも伏せ直す（2026-09-14）。**
            // この関数は10箇所以上から呼ばれていて、そのうちのいくつかは
            // チュートリアルの導入中にも通る。呼ぶ側を1つずつ直すより、
            // **出したあとに必ず伏せ直す**ほうが取りこぼしが無い。
            // 伏せているあいだ以外は何もしない（FirstRoundChromeHidden を見る）。
            ReHideTutorialChrome();
        }
    }
}
