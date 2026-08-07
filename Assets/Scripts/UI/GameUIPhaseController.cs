using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUIPhaseController : MonoBehaviour
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

        public void ShowMatchmakingWaiting()
        {
            Debug.Log("[GameUIPhaseController] ShowMatchmakingWaiting called.");
            if (uiManager != null && uiManager.IsTutorialMode) return; // チュートリアル中は非表示

            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting();
            SetMatchUIVisibility(false);
            
            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.gameObject.SetActive(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.gameObject.SetActive(false);
            if (uiManager.BettingUI != null) uiManager.BettingUI.HideBettingPhase(true);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }

        public void ShowMatchCancelled(string reason)
        {
            uiManager.ClearAllTiles();
            BoardStateManager.Instance.ClearAllBoardData();
            uiManager.SetCurrentPhaseStatus(RoundStatus.None);

            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting(reason);
            SetMatchUIVisibility(false);
            
            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.gameObject.SetActive(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            
            if (uiManager.DialogueUI != null) 
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText(reason);
            }
            
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.gameObject.SetActive(false);
            if (uiManager.BettingUI != null) uiManager.BettingUI.HideBettingPhase(true);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }

        public void OnGameStarted()
        {
            _currentRoundIndex = 1;
            ResetPhaseReadyMarks();

            try
            {
                // 血の初期値もサーバーが持っている（game_engine.py:61）。
                // ここではつなぎの値を置くだけにして、本物は status で取り直す。
                // つなぎを置かないと、前の対局の残り血（0 など）がゲージに残る。
                Managers.BoardStateManager.Instance.UpdateHp(
                    Managers.BoardStateManager.PlaceholderInitialHp,
                    Managers.BoardStateManager.PlaceholderInitialHp);
                if (!uiManager.IsTutorialMode)
                {
                    uiManager.SendActionToServer("status", null);
                }
                if (uiManager.BetPotUI != null) uiManager.BetPotUI.Clear();
                // 新しい対局なので獲得も賭け金も引き直す
                if (!uiManager.IsTutorialMode) uiManager.ScoreGauge.ResetScores();
                if (uiManager.PlayerInfoUI != null)
                {
                    uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    // 前の対局で 20000 超えまで血を増やしているとメーターの分母が残るので引き直す
                    uiManager.PlayerInfoUI.ResetHpMeter(20000);
                    uiManager.PlayerInfoUI.SetHP(20000);
                }
                if (uiManager.EnemyInfoUI != null)
                {
                    uiManager.EnemyInfoUI.SetPanelVisible(true);
                    uiManager.EnemyInfoUI.ResetHpMeter(20000);
                    uiManager.EnemyInfoUI.SetHP(20000);
                    uiManager.EnemyInfoUI.ShowReadyBox(false);
                }
                
                if (uiManager.PhaseTransitionUI != null)
                {
                    uiManager.PhaseTransitionUI.PlayRoundStartDarken("対局開始");
                }

                if (ReactionController.Instance != null)
                {
                    ReactionController.Instance.ResetStateForNewGame();
                    ReactionController.Instance.SetPlayerHp(20000);
                    ReactionController.Instance.SetEnemyHp(20000);
                    ReactionController.Instance.HandleRoundStart(1);
                }
                
                if (uiManager.DialogueUI != null) 
                {
                    uiManager.DialogueUI.gameObject.SetActive(true);
                    string introText = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.GameStart) : null;
                    if (string.IsNullOrEmpty(introText)) introText = "Match Found! Game Starting...";
                    uiManager.DialogueUI.ShowText(introText);
                }
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(20000);
                
                SetMatchUIVisibility(true);
                uiManager.SetCurrentPhaseStatus(RoundStatus.None);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(AudioManager.Instance.battleBgm);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameUIPhaseController] OnGameStarted Error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // 何があっても必ずマッチング画面は閉じる
                if (uiManager.MatchmakingUI != null)
                {
                    uiManager.MatchmakingUI.Hide();
                }
            }
        }

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

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmFilter(newStatus != RoundStatus.Discard);
            }

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
                if (!isSettlementPhase) Effects.ScreenFlash.Play();
            }

            if (status != RoundStatus.Betting && uiManager.PlayerInfoUI != null)
            {
                if (uiManager.PlayerInfoUI.gameObject.activeInHierarchy)
                {
                    uiManager.PlayerInfoUI.StartCoroutine(uiManager.PlayerInfoUI.ResetZoomRoutine(0.3f));
                }
                else
                {
                    uiManager.PlayerInfoUI.ResetZoomImmediate();
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

            // 待ち候補の推理は打牌中だけ意味があるので、そのときだけ出す
            if (!uiManager.IsTutorialMode)
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
                case RoundStatus.Betting:
                    SetMatchUIVisibility(true);
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

                    // チュートリアルでは TutorialManager が「セリフ → 拡大 → 賭け金UI」の順で進める。
                    // ここで拡大やベット開始をしてしまうと、セリフを送る前にスマホが拡大してしまう。
                    if (!uiManager.IsTutorialMode)
                    {
                        if (uiManager.PlayerInfoUI != null)
                        {
                            uiManager.PlayerInfoUI.StartCoroutine(uiManager.PlayerInfoUI.ZoomInRoutine(0.4f, 4.5f));
                        }
                        StartBettingPhase(Managers.BoardStateManager.Instance.LocalPlayerHp);

                        // スマホが4.5倍に拡大している間は札がその裏に入る。
                        // 賭け金を確定してスマホが縮んでから出す（OnBetConfirmed）
                        SetReadyBadgesSuppressed(true);
                        ApplyPhaseReadyMarks(RoundStatus.Betting);
                    }
                    break;
                case RoundStatus.Dealing:
                    _hasShownHandSelectionPrompt = false; // 次の局のためにフラグをリセット
                    _hasExecutedRonAnimation = false; // ロン演出の二重再生防止フラグをリセット
                    // 待ち候補の推理は局ごとにやり直す
                    if (!uiManager.IsTutorialMode) uiManager.WaitDeduction.ResetForNewRound();
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
                    ResetPhaseReadyMarks(); // 手牌選択・ベットの印は局ごとに引き直す
                    // 賭け金を確定しないままフェイズが進むと伏せたままになるので、局の頭で戻す
                    SetReadyBadgesSuppressed(false);

                    if (_pendingDrawTransition)
                    {
                        _pendingDrawTransition = false;
                        ExecuteDrawTransitionForDealing();
                    }
                    else
                    {
                        StartNextRoundTransitionForDealing();
                    }
                    break;
                case RoundStatus.HandSelection:
                    SetMatchUIVisibility(true);
                    if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(false);
                    
                    if (!uiManager.IsTutorialMode)
                    {
                        if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                        if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                        if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(true);
                        if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.StartTurnTimer(15f);
                        SetReadyBadgesSuppressed(false); // 手牌選択ではスマホは拡大しない
                        ApplyPhaseReadyMarks(RoundStatus.HandSelection);
                    }

                    // **チュートリアルでは決定を押すまで待ち牌UIを出さない。**
                    // 『おまかせ』は待ち牌を盤面に入れたうえで SetPhase(HandSelection) を通るため、
                    // 素直に書くと押した瞬間に左下へ出て、手牌確認のUIと重なる。
                    // 決定後は TutorialManager.ConfirmHandSelectionComplete が出す。
                    bool waitUiAllowed = !uiManager.IsTutorialMode
                        || (uiManager.TutorialManager != null && uiManager.TutorialManager.IsHandSelectionConfirmed);

                    if (uiManager.WaitUI != null && waitUiAllowed
                        && Managers.BoardStateManager.Instance.CurrentWaitTiles != null
                        && Managers.BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
                    {
                        uiManager.WaitUI.gameObject.SetActive(true);
                        uiManager.WaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentWaitTiles);
                    }
                    else if (uiManager.WaitUI != null)
                    {
                        uiManager.WaitUI.gameObject.SetActive(false);
                    }
                    UpdateDoraDisplay();
                    
                    if (ReactionController.Instance != null && !uiManager.IsTutorialMode)
                    {
                        ReactionController.Instance.StartHandSelectionTimer();
                    }
                    
                    // 黒幕が晴れて手牌フェイズに入った時に表示（1局につき1回のみ）
                    if (uiManager.PhaseTransitionUI != null && !_hasShownHandSelectionPrompt && !uiManager.IsTutorialMode)
                    {
                        uiManager.PhaseTransitionUI.PlayPromptText("手牌を選んでください", 1.5f);
                        _hasShownHandSelectionPrompt = true;
                    }
                    break;
                case RoundStatus.TurnDecision:
                    // ベットの「準備完了」はここで役目を終える。
                    // PlayerInfoUI は非表示にするだけで箱は開いたままなので、明示的に閉じる
                    HideReadyBoxes();
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                    if (uiManager.PlayerInfoUI != null)
                    {
                        uiManager.PlayerInfoUI.gameObject.SetActive(false);
                        uiManager.PlayerInfoUI.StopTurnTimer();
                    }
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    break;
                case RoundStatus.Discard:
                    // TurnDecision が保留で飛ばされた場合に備えて、ここでも閉じておく
                    HideReadyBoxes();
                    if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(true);
                    if (uiManager.HandUI != null) uiManager.HandUI.gameObject.SetActive(true);
                    if (uiManager.WallUI != null) uiManager.WallUI.gameObject.SetActive(true);
                    if (uiManager.EnemyWallUI != null) uiManager.EnemyWallUI.gameObject.SetActive(false);
                    
                    if (uiManager.RiverUI != null) uiManager.RiverUI.UpdateTurnText();
                    if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.UpdateTurnText();
                    
                    if (!uiManager.IsTutorialMode)
                    {
                        if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                        if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                        if (uiManager.PlayerInfoUI != null)
                        {
                            if (Managers.BoardStateManager.Instance.IsLocalTurn)
                            {
                                uiManager.PlayerInfoUI.StartTurnTimer(10f); // 10秒
                            }
                            else
                            {
                                uiManager.PlayerInfoUI.StopTurnTimer();
                            }
                        }
                    }
                    
                    if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
                    {
                        uiManager.WaitUI.gameObject.SetActive(true);
                        uiManager.WaitUI.DisplayWaits(BoardStateManager.Instance.CurrentWaitTiles);
                    }
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    UpdateDoraDisplay();
                    break;
                case RoundStatus.Agari:
                case RoundStatus.Ron:
                case RoundStatus.Result:
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();

                    // チュートリアルではロンボタンを押させてから TutorialManager が演出を出す。
                    // ここで実行すると、ボタンを押す前にロンが走ってしまう。
                    // （LastIsLocalWin はサーバー通信でしか更新されず、チュートリアルでは
                    //   初期値の true のままなので、敵のロンでも自分の勝ちとして走ってしまう）
                    if (!uiManager.IsTutorialMode && uiManager.RonAnimationUI != null)
                    {
                        bool isLocalWin = BoardStateManager.Instance.LastIsLocalWin;

                        if (isLocalWin)
                        {
                            uiManager.ExecuteRonAction();
                        }
                    }
                    break;
                case RoundStatus.Draw:
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    
                    if (uiManager.DialogueUI != null)
                    {
                        uiManager.DialogueUI.gameObject.SetActive(true);
                        uiManager.DialogueUI.ShowText("流局…次の対局へ");
                    }
                    break;
            }
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
        }

        private void StartBettingPhase(int currentHealth)
        {
            if (uiManager.BettingUI != null)
            {
                int svCount = Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount;
                uiManager.BettingUI.ShowBettingPhase(20000, currentHealth, svCount, OnBetConfirmed);
                if (ReactionController.Instance != null)
                {
                    ReactionController.Instance.StartBetPhaseTimer();
                }
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.StartTurnTimer(10f); // 10秒
            }
        }

        private void OnBetConfirmed(int betAmount)
        {
            uiManager.BettingUI.HideBettingPhase();
            
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.StopTurnTimer();
                if (uiManager.PlayerInfoUI.gameObject.activeInHierarchy)
                {
                    uiManager.PlayerInfoUI.StartCoroutine(
                        uiManager.PlayerInfoUI.ResetZoomRoutine(BetZoomOutDuration));
                }
            }

            // スマホが縮んでから「準備完了」を出す。相手が賭けるまではここで待つことになる
            StartCoroutine(ShowReadyBadgesAfterZoomOut());

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(betAmount, Managers.BoardStateManager.Instance.LocalPlayerHp, true);
            }
            uiManager.SendActionToServer("bet", new ActionPayload { bet_amount = betAmount, amount = betAmount });
        }

        public void OnBettingCompleteFromServer(int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
            // 賭け金はここで両者の血から引かれている（BettingMessageHandler）。
            // 流局では決着せず次の局でも同額が賭けられるので、場の表示は積み増していく。
            // 場の血が動くのは決着したときだけなので、クリアはロン演出の完了時に行う。
            if (uiManager.BetPotUI != null) uiManager.BetPotUI.AddStakes(playerBet, enemyBet);
            // 賭けている額はゲージの下にも出す。決着でゲージへ吸い込まれる
            if (!uiManager.IsTutorialMode) uiManager.ScoreGauge.AddStakes(playerBet, enemyBet);

            string roundTitle = $"第{_currentRoundIndex}局目";
            if (_isCarryOverNextRound) 
            {
                roundTitle += "\n自動ベット";
            }

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(enemyBet, enemyHp, false);
                ReactionController.Instance.SetPlayerHp(playerHp);
                ReactionController.Instance.SetEnemyHp(enemyHp);
            }
            
            TriggerBettingAnimationPhase(roundTitle, playerBet, enemyBet, playerHp, enemyHp); 
            _isCarryOverNextRound = false;
        }

        public void TriggerBettingAnimationPhase(string roundString, int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
             // このメソッドは演出だけでなく進行の責務も持っている（onMidpoint で
             // UpdatePhaseStatus(Discard) を呼ぶ）。捨てると打牌フェイズへ進めず Betting で固まる。
             if (uiManager.IsBusyWithTransition)
             {
                 uiManager.DeferUntilIdle("bettingAnimation",
                     () => TriggerBettingAnimationPhase(roundString, playerBet, enemyBet, playerHp, enemyHp));
                 return;
             }

             if (uiManager.PhaseTransitionUI != null)
             {
                 uiManager.SetIsTransitioning(true);
                 
                 // スマホは消さないようにする（ユーザー要望）
                 // if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                 if (uiManager.PlayerInfoUI != null) 
                 {
                     uiManager.PlayerInfoUI.ResetZoomImmediate();
                     // uiManager.PlayerInfoUI.gameObject.SetActive(false);
                 }
                 if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                 if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(false);

                 uiManager.PhaseTransitionUI.PlayTransition(roundString, uiManager.PlayerInfoUI, playerBet, enemyBet, playerHp, enemyHp,
                    onMidpoint: () => {
                         uiManager.SetIsTransitioning(false);
                         
                         // UIのみクリア（BoardStateManagerのデータを消さないようにする）
                         if (uiManager.RiverUI != null) uiManager.RiverUI.Clear();
                         if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.Clear();
                         if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                         
                         // 牌を再構築する前にフェーズをDiscardへ進める
                         if (uiManager.CurrentPhaseStatus == RoundStatus.Betting)
                         {
                             UpdatePhaseStatus(RoundStatus.Discard);
                         }
                         
                         if (uiManager.VisualController != null) uiManager.VisualController.RebuildAllTilesFromState();
                         
                         if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.WallUI != null)
                         {
                             uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                         }
                         
                         SetMatchUIVisibility(true); 
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         
                         uiManager.SetIsTransitioning(true);
                         
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
                    },
                    onComplete: () => {
                         uiManager.SetIsTransitioning(false);
                         
                         // Catch any missed updates
                         if (uiManager.VisualController != null) uiManager.VisualController.RebuildAllTilesFromState();
                         
                         if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.WallUI != null)
                         {
                             uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateDiscardTurnIndicator(BoardStateManager.Instance.IsLocalTurn, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                         }
                         
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(true);
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    }
                 );
             }
        }

        private bool _pendingDrawTransition = false;

        public void HandleDraw(KillingMahjong.EngineData.DrawPlayerData[] drawData = null)
        {
            // 流局は「最後の打牌の直後」に届くので打牌アニメや能力演出と重なりやすい。
            // ここで捨てると _currentRoundIndex++・流局ダイアログ・next_round 送信が全部飛び、
            // サーバーが承認を待ち続けて対局が止まる。捨てずに演出明けまで保留する。
            if (uiManager.IsBusyWithTransition)
            {
                uiManager.DeferUntilIdle("draw", () => HandleDraw(drawData));
                return;
            }

            // 待機中などの表示は消す
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
            
            Debug.Log("[GameUIManager] 流局処理開始");
            _isCarryOverNextRound = true;
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.CheckAndPlayDrawReaction();
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

            // 自分の待ち牌表示
            if (uiManager.WaitUI != null && Managers.BoardStateManager.Instance.CurrentWaitTiles != null && Managers.BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                uiManager.WaitUI.gameObject.SetActive(true);
                uiManager.WaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentWaitTiles);
            }

            // 相手の待ち牌表示
            if (uiManager.EnemyWaitUI != null && Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles != null && Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles.Count > 0)
            {
                uiManager.EnemyWaitUI.gameObject.SetActive(true);
                uiManager.EnemyWaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles);
            }

            // 既存の手牌データソート
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentHandTiles);
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentEnemyHandTiles);
            
            if (uiManager.HandUI != null) uiManager.HandUI.SortHandSlots();
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.SortHandSlots();

            // 相手の手牌強制公開
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.RevealAllHands(uiManager.TileResourceManager);
            }

            // このOKが出ている時点で、準備完了という文字を出してほしい
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            // ダイアログを出してOKボタンを待つ
            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText("流局しました。\nお互いの手牌と待ちを確認してください。");
                uiManager.DialogueUI.ShowNextRoundButton(() => {
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.EnemyWaitUI != null) uiManager.EnemyWaitUI.gameObject.SetActive(false);
                    
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyCheck(true);
                    _pendingDrawTransition = true;
                    SendNextRoundAction();
                });
            }
            else
            {
                _pendingDrawTransition = true;
                SendNextRoundAction();
            }
        }

        private void ExecuteDrawTransitionForDealing()
        {
            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.SetIsTransitioning(true);
                uiManager.PhaseTransitionUI.PlayDrawTransition(
                    onMidpoint: () => {
                        BoardStateManager.Instance.ClearAllBoardData();
                        uiManager.ClearAllTiles();
                        SetMatchUIVisibility(false);
                        
                        if (ReactionController.Instance != null)
                        {
                            ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
                        }
                    },
                    onComplete: () => {
                        uiManager.SetIsTransitioning(false);
                        StartCoroutine(DealingRoutine());
                    }
                );
            }
            else
            {
                StartCoroutine(DrawSequence());
            }
        }

        private IEnumerator DrawSequence()
        {
            yield return new WaitForSeconds(3.0f);
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
            }

            if (uiManager.PlayerInfoUI != null) 
            {
                uiManager.PlayerInfoUI.ShowReadyBox(true);
                uiManager.PlayerInfoUI.SetReadyCheck(true);
            }
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            Debug.Log("[GameUIManager] 流局演出完了 - 次ラウンド待ち承認自動送信");
            SendNextRoundAction();
        }

        private bool _hasExecutedRonAnimation = false;

        public void ExecuteRonAction()
        {
            if (_hasExecutedRonAnimation) return;
            _hasExecutedRonAnimation = true;

            if (uiManager.RonWaitPanel != null) uiManager.RonWaitPanel.SetActive(false);

            bool isLocalWin = true;
            List<int> winningHand = new List<int>(BoardStateManager.Instance.CurrentHandTiles);
            var liq = BoardStateManager.Instance.LastLiquidationData;
            
            List<string> actualYaku = new List<string>();
            string actualFormula = "0飜";
            string actualRank = "満貫";
            
            if (liq != null)
            {
                if (liq.yaku != null) actualYaku = new List<string>(liq.yaku);
                else actualYaku.Add("不明な役");
                
                actualFormula = $"{liq.han}飜";
                
                if (liq.multiplier >= 4.0f) actualRank = "役満";
                else if (liq.multiplier >= 3.0f) actualRank = "三倍満";
                else if (liq.multiplier >= 2.0f) actualRank = "倍満";
                else if (liq.multiplier >= 1.5f) actualRank = "跳満";
                else actualRank = "満貫";

                // 自分の和了なので、自己ベスト打点として通算戦績に記録する
                KillingMahjong.Core.PlayerStatsManager.RecordScore(liq.winner_gain);
            }

            int ronTile = BoardStateManager.Instance.LastDiscardedTileId >= 0
                ? BoardStateManager.Instance.LastDiscardedTileId
                : (winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0);

            // **ronTile を決めたあとに並べ替えること。** 上のフォールバックは
            // 「最後に引いた牌」を取る前提なので、先に並べ替えると別の牌になる。
            SortHandForRonAnimation(winningHand);

            StartCoroutine(PlayRonWithPreDialogue(isLocalWin, winningHand, ronTile, actualYaku, actualFormula, actualRank));
        }

        /// <summary>
        /// ロン演出に出す手牌を並べ替える。**渡す複製だけを並べ替え、盤面の実体には触らない。**
        ///
        /// 演出は受け取った配列をそのままの順で並べる（RonAnimationUI はソートしない）。
        /// 盤面側の並べ替えは OnScoreSettlementComplete で行うが、それが走るのは
        /// 演出が終わったあとなので、ここで揃えないと演出の中だけツモ順のまま出る。
        /// 自分の和了は CurrentHandTiles がたまたま整列しているだけなので、
        /// 両方の経路で明示的に揃えておく。
        /// </summary>
        private static void SortHandForRonAnimation(List<int> hand)
        {
            if (hand == null || hand.Count == 0) return;
            if (BoardStateManager.Instance == null) return;
            BoardStateManager.Instance.SortTileIds(hand);
        }

        public void HandleAgari(bool isLocalWin)
        {
            if (!isLocalWin)
            {
                Debug.Log("[GameUIPhaseController] 相手のロン成立。相手のロン演出を開始します。");

                List<int> winningHand = new List<int>(BoardStateManager.Instance.CurrentEnemyHandTiles);
                var liq = BoardStateManager.Instance.LastLiquidationData;
                
                List<string> actualYaku = new List<string>();
                string actualFormula = "0飜";
                string actualRank = "満貫";
                
                if (liq != null)
                {
                    if (liq.yaku != null) actualYaku = new List<string>(liq.yaku);
                    else actualYaku.Add("不明な役");
                    
                    actualFormula = $"{liq.han}飜";
                    
                    if (liq.multiplier >= 4.0f) actualRank = "役満";
                    else if (liq.multiplier >= 3.0f) actualRank = "三倍満";
                    else if (liq.multiplier >= 2.0f) actualRank = "倍満";
                    else if (liq.multiplier >= 1.5f) actualRank = "跳満";
                    else actualRank = "満貫";
                }
                
                int ronTile = BoardStateManager.Instance.LastDiscardedTileId >= 0
                    ? BoardStateManager.Instance.LastDiscardedTileId
                    : (winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0);

                // 相手の手牌はツモ順のまま届く。ここで揃えないと演出中だけバラバラに見える
                SortHandForRonAnimation(winningHand);

                StartCoroutine(PlayRonWithPreDialogue(isLocalWin, winningHand, ronTile, actualYaku, actualFormula, actualRank));
            }
        }

        private bool _isStartingNextRound = false;

        public void HandleNextRoundWaitingReceived(NextRoundWaitingData data = null)
        {
            Debug.Log("[GameUIPhaseController] HandleNextRoundWaitingReceived: 相手が次ラウンド準備完了（またはロンボタン押下）しました。");
            
            if (data != null && data.ready_players != null)
            {
                // 自分以外のIDが ready_players に含まれているか確認
                string localId = NetworkMessageHandler.Instance.LocalPlayerId;
                bool enemyIsReady = false;
                bool localIsReady = false;
                foreach (var playerId in data.ready_players)
                {
                    if (playerId != localId) enemyIsReady = true;
                    if (playerId == localId) localIsReady = true;
                }
                
                if (uiManager.EnemyInfoUI != null)
                {
                    uiManager.EnemyInfoUI.SetReadyCheck(enemyIsReady);
                }
                
                if (uiManager.PlayerInfoUI != null)
                {
                    uiManager.PlayerInfoUI.SetReadyCheck(localIsReady);
                }
            }
        }

        /// <summary>
        /// "phase_completed_notice"：手牌選択・ベットを確定したプレイヤーの通知。
        /// 確定した本人ぶんが1通ずつ届くので、届いた側にだけ印を立てて描き直す。
        /// </summary>
        public void HandlePhaseCompletedNotice(PhaseCompletedNoticeData data)
        {
            if (data == null || uiManager == null) return;
            // チュートリアルはサーバーに繋がず待ち合わせも無いので、印は出さない
            if (uiManager.IsTutorialMode) return;

            var net = NetworkMessageHandler.Instance;
            string localId = (net != null) ? net.LocalPlayerId : null;
            if (string.IsNullOrEmpty(localId))
            {
                Debug.LogWarning("[GameUIPhaseController] phase_completed_notice: LocalPlayerId が未設定のため自他を判別できません。");
                return;
            }
            bool isLocal = (data.player_id == localId);

            // "bet" が仕様だが、phase_change 側の表記は "betting"。どちらで来ても受ける
            switch (data.phase)
            {
                case "hand_selection":
                case "handselection":
                    if (isLocal) _handSelectionReadyLocal = true;
                    else _handSelectionReadyEnemy = true;
                    ApplyPhaseReadyMarks(RoundStatus.HandSelection);
                    break;
                case "bet":
                case "betting":
                    if (isLocal) _betReadyLocal = true;
                    else _betReadyEnemy = true;
                    ApplyPhaseReadyMarks(RoundStatus.Betting);
                    break;
                default:
                    Debug.LogWarning($"[GameUIPhaseController] phase_completed_notice: 未知の phase '{data.phase}'");
                    break;
            }
        }

        /// <summary>
        /// 自分ぶんの「準備完了」を立てる。
        /// 相手ぶんは phase_completed_notice でしか分からないが、自分ぶんは
        /// 既存の受理メッセージ（hand_selection_accepted / bet_accepted）で分かる。
        /// </summary>
        public void MarkLocalPhaseReady(RoundStatus phase)
        {
            if (uiManager == null || uiManager.IsTutorialMode) return;

            if (phase == RoundStatus.HandSelection) _handSelectionReadyLocal = true;
            else if (phase == RoundStatus.Betting) _betReadyLocal = true;
            else return;

            ApplyPhaseReadyMarks(phase);
        }

        /// <summary>
        /// 手牌選択・ベットの「準備完了」を描き直す。
        /// 進行中のフェイズと引数が食い違うときは何もしない（保留から遅れて来た描画で
        /// 別フェイズの箱を出さないため）。
        /// </summary>
        private void ApplyPhaseReadyMarks(RoundStatus phase)
        {
            if (uiManager == null || uiManager.IsTutorialMode) return;
            if (uiManager.CurrentPhaseStatus != phase) return;

            bool localReady;
            bool enemyReady;
            if (phase == RoundStatus.HandSelection)
            {
                localReady = _handSelectionReadyLocal;
                enemyReady = _handSelectionReadyEnemy;
            }
            else if (phase == RoundStatus.Betting)
            {
                localReady = _betReadyLocal;
                enemyReady = _betReadyEnemy;
            }
            else
            {
                return;
            }

            // ShowReadyBox はチェックを外すので、必ず先に呼んでから SetReadyCheck する
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.ShowReadyBox(true);
                uiManager.PlayerInfoUI.SetReadyCheck(localReady);
            }
            if (uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.ShowReadyBox(true);
                uiManager.EnemyInfoUI.SetReadyCheck(enemyReady);
            }

            // 両者そろった瞬間に「選び直す」を引っ込める。ここで描き直さないと、
            // phase_change が届くまでボタンが残って連打で押せてしまう。
            if (phase == RoundStatus.HandSelection && uiManager.HandUI != null)
            {
                uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
            }
        }

        /// <summary>局の頭で印を落とす。手牌選択とベットは1局に1回ずつなのでここだけで足りる。</summary>
        private void ResetPhaseReadyMarks()
        {
            _handSelectionReadyLocal = false;
            _handSelectionReadyEnemy = false;
            _betReadyLocal = false;
            _betReadyEnemy = false;
        }

        /// <summary>「準備完了」の箱を両者ぶん隠す。</summary>
        private void HideReadyBoxes()
        {
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
        }

        /// <summary>ベット中のスマホ拡大に隠れる間だけ、両者の札を伏せる。</summary>
        private void SetReadyBadgesSuppressed(bool suppressed)
        {
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyBoxSuppressed(suppressed);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetReadyBoxSuppressed(suppressed);
        }

        /// <summary>
        /// 賭け金を確定するとスマホが縮む。縮み終わってから札を出し直す。
        /// 拡大中は札がスマホの裏（x197..602・全高・描画順が上）に入って見えないため。
        /// </summary>
        private IEnumerator ShowReadyBadgesAfterZoomOut()
        {
            // 縮み切るのと同じ長さだけ待つと同フレームで競合し、札が「縮んでいる途中の
            // スマホ」に合わせて置かれる（実測で x552..656。正しくは 668..772）。
            // ReadyBadge は表示した瞬間にしか位置を測らないので、必ず後から動く
            yield return new WaitForSeconds(BetZoomOutDuration + 0.05f);

            // 待っている間に相手も賭け終えてフェイズが進んでいたら、出さずに終わる
            if (uiManager.CurrentPhaseStatus != RoundStatus.Betting) yield break;

            SetReadyBadgesSuppressed(false);
            ApplyPhaseReadyMarks(RoundStatus.Betting);
        }

        private void StartNextRoundTransitionForDealing()
        {
            if (_isStartingNextRound) return;
            _isStartingNextRound = true;

            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.PhaseTransitionUI.PlayRoundStartDarken($"第{_currentRoundIndex}局...", () => {
                    BoardStateManager.Instance.ClearAllBoardData();
                    uiManager.ClearAllTiles();
                    StartCoroutine(DealingRoutine());
                });
            }
            else
            {
                BoardStateManager.Instance.ClearAllBoardData();
                uiManager.ClearAllTiles();
                StartCoroutine(DealingRoutine());
            }
        }

        /// <summary>
        /// ロン演出に出す計算式を、サーバーの清算結果から組み立てる。
        ///
        /// **勝者の獲得額の式にしている。**演出に出している数字（score）が winner_gain なので、
        /// 式と答えを揃えるため。GameRules の定義どおり:
        ///
        ///   勝者が得る額 = 勝者自身の賭け金 × 勝者の役の倍率
        ///
        /// 敗者の損失（賭け金 × 倍率 × 単騎倍率）は、勝者の獲得とは別計算なので式が違う。
        /// 混ぜると答えが合わなくなるのでここでは扱わない。
        ///
        /// 内訳が無いときは null を返す。演出側は式を伏せて額だけ見せる。
        /// </summary>
        private static string BuildScoreFormula(LiquidationData liq)
        {
            if (liq == null) return null;
            if (liq.winner_bet <= 0 || liq.multiplier <= 0f) return null;

            // 1.0 は「1」、1.5 は「1.5」と出す。末尾の 0 を引きずらない
            string mult = liq.multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return $"{liq.winner_bet} × {mult}";
        }

        private IEnumerator PlayRonWithPreDialogue(bool isLocalWin, List<int> winningHand, int ronTile, List<string> yaku, string formula, string rank)
        {
            // ロンの一撃を予感させる合図。自分のロン（ExecuteRonAction）も
            // 相手のロン（HandleAgari）もここを通るので、1箇所で両方に効く。
            Effects.ScreenFlash.Play();

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.ClearReactions();
                
                bool isYakuman = rank == "役満";
                bool isDoraBaku = false;
                foreach (var y in yaku) if (y.Contains("ドラ") && y.Contains("3") || y.Contains("4") || y.Contains("5")) isDoraBaku = true;
                bool isCheap = formula == "1飜" || formula == "2飜";
                
                ReactionController.Instance.HandleAgari(isLocalWin, isYakuman, isDoraBaku, isCheap);
                ReactionController.Instance.SetPlayerLostLastRound(!isLocalWin);
            }

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);

            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (uiManager.RonAnimationUI != null)
            {
                uiManager.RonAnimationUI.gameObject.SetActive(true);

                var liq = BoardStateManager.Instance.LastLiquidationData;
                int score = liq != null ? liq.winner_gain : 0;

                int newLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                int newEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                int loserLoss = liq != null ? liq.loser_loss : 0;
                int prevLocalHp = isLocalWin ? (newLocalHp - score) : (newLocalHp + loserLoss);
                int prevEnemyHp = isLocalWin ? (newEnemyHp + loserLoss) : (newEnemyHp - score);

                uiManager.RonAnimationUI.PlayRonSequence(winningHand, ronTile, yaku, formula, rank, score, isLocalWin,
                    uiManager.PlayerInfoUI, uiManager.EnemyInfoUI, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp,
                    () => OnRonAnimationComplete(isLocalWin),
                    BuildScoreFormula(liq));
            }
            yield break;
        }

        private void OnRonAnimationComplete(bool isLocalWin)
        {
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.HandleGameEnd(isLocalWin);
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

            // HPのアニメーションはRonAnimationUIで完了しているため、
            // そのまま結果エフェクトを表示して次局へ進む
            OnScoreSettlementComplete(isLocalWin);
        }

        private void OnScoreSettlementComplete(bool isLocalWin)
        {
            // 決着したので場の血は勝者へ移った。表示を空にする（流局のときはここを通らない）
            if (uiManager.BetPotUI != null) uiManager.BetPotUI.Clear();

            // 賭け金がゲージへ吸い込まれ、そのあとゲージが伸びる。
            // 勝敗の判定はサーバーの担当で、ここは表示のために積むだけ。
            // サーバーが「獲得で勝利」へ移行するまでは、この表示だけが先行する。
            var liq = BoardStateManager.Instance.LastLiquidationData;
            if (liq != null && liq.winner_gain > 0 && !uiManager.IsTutorialMode)
            {
                uiManager.ScoreGauge.AbsorbStakesIntoGauge(isLocalWin, liq.winner_gain);
            }

            // 相手の手牌を並べ直して公開する。
            // これは流局（HandleDraw）にしか無く、ロンで決着したときは打牌フェイズのまま
            // ＝ツモ順でバラバラ・大半が伏せたままの状態が残っていた（要望9）。
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentHandTiles);
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentEnemyHandTiles);

            if (uiManager.HandUI != null) uiManager.HandUI.SortHandSlots();
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.SortHandSlots();
                uiManager.EnemyHandUI.RevealAllHands(uiManager.TileResourceManager);
            }

            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.gameObject.SetActive(true);
                uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
            }
            if (uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.SetPanelVisible(true);
                uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
            }

            if (isLocalWin)
            {
                if (uiManager.VictoryEffectPrefab != null && uiManager.PlayerInfoUI != null) 
                    Instantiate(uiManager.VictoryEffectPrefab, uiManager.PlayerInfoUI.transform.position, Quaternion.identity);
                if (uiManager.DamageEffectPrefab != null && uiManager.EnemyInfoUI != null) 
                    Instantiate(uiManager.DamageEffectPrefab, uiManager.EnemyInfoUI.transform.position, Quaternion.identity);
            }
            else
            {
                if (uiManager.VictoryEffectPrefab != null && uiManager.EnemyInfoUI != null) 
                    Instantiate(uiManager.VictoryEffectPrefab, uiManager.EnemyInfoUI.transform.position, Quaternion.identity);
                if (uiManager.DamageEffectPrefab != null && uiManager.PlayerInfoUI != null) 
                    Instantiate(uiManager.DamageEffectPrefab, uiManager.PlayerInfoUI.transform.position, Quaternion.identity);
            }

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.ShowNextRoundButton(() => {
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyCheck(true);

                    if (uiManager.IsGameOver)
                    {
                        uiManager.ShowGameResult();
                    }
                    else
                    {
                        SendNextRoundAction();
                    }
                });
            }
            else
            {
                if (uiManager.IsGameOver)
                {
                    uiManager.ShowGameResult();
                }
                else
                {
                    SendNextRoundAction();
                }
            }
        }

        private void SendNextRoundAction()
        {
            if (uiManager.IsGameOver) return;

            // ゲームプレイ中のフェーズでは次局への進行リクエストを送らない
            // （遅延コルーチンが新局開始後に発火して二重送信になるのを防ぐため）
            if (uiManager.CurrentPhaseStatus == RoundStatus.Dealing ||
                uiManager.CurrentPhaseStatus == RoundStatus.HandSelection ||
                uiManager.CurrentPhaseStatus == RoundStatus.Betting ||
                uiManager.CurrentPhaseStatus == RoundStatus.Discard ||
                uiManager.CurrentPhaseStatus == RoundStatus.TurnDecision)
            {
                Debug.Log($"[GameUIPhaseController] SendNextRoundAction aborted. Current phase is {uiManager.CurrentPhaseStatus}");
                return;
            }

            if (!_hasSentNextRoundForCurrentPhase)
            {
                _hasSentNextRoundForCurrentPhase = true;
                NetworkMessageHandler.Instance.SendActionToServer("next_round", new ActionPayload());
            }
        }

        public void HandleSpecialVictoryWon(string playerId)
        {
            bool isLocalPlayer = (playerId == NetworkMessageHandler.Instance.LocalPlayerId);
            
            if (uiManager.VictoryUI != null)
            {
                uiManager.VictoryUI.PlayAnimation(isLocalPlayer ? VictoryType.SpecialVictory : VictoryType.SpecialDefeat);
            }
            else if (uiManager.PhaseTransitionUI != null)
            {
                string msg = isLocalPlayer ? "特殊勝利条件を達成しました！" : "相手が特殊勝利条件を達成しました...";
                uiManager.PhaseTransitionUI.PlayCenterTextAnim(msg, 3.0f, null);
            }
        }

        private IEnumerator DealingRoutine()
        {
            _isStartingNextRound = false;

            if (uiManager.PhaseTransitionUI != null)
            {
                while (uiManager.PhaseTransitionUI.IsDarkenTransitioning)
                {
                    yield return null;
                }
                uiManager.PhaseTransitionUI.ChangeDarkenText($"第{_currentRoundIndex}局進行中...");
            }
            
            uiManager.ClearAllTiles();
            SetMatchUIVisibility(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.PlayDealingReaction();
            }
        }
    }
}
