using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.UI;
using KillingMahjong.EngineData;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// チュートリアルの進行管理。
    ///
    /// サーバーには一切接続せず、Unity 側だけで完結する。
    /// 局ごとの内容（配牌・敵の打牌・結末・セリフ・賭け金）は TutorialScenario に外出しし、
    /// このクラスは「台本を順に再生する」ことに専念する。
    ///
    /// 外部から呼ばれる API（GameUIManager / HandUI / GameUIHandSelectionController /
    /// OpeningSequenceManager が使用）は従来と同じシグネチャを維持している:
    ///   StartTutorial / OnTryMoveTile / OnTryCompleteHandSelection /
    ///   ConfirmHandSelectionComplete / OnTryDiscardTile / ApplyMockAutoMangan
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameUIManager gameUIManager;
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private TutorialArrowUI arrowUI;
        [SerializeField] private TutorialMaskUI maskUI;

        [Header("Scenario")]
        [Tooltip("未設定の場合は TutorialScenario.BuildDefault() の内容が使われる")]
        [SerializeField] private TutorialScenario scenario;

        [Header("Timing")]
        [SerializeField] private float interruptMessageDuration = 2.0f;
        [SerializeField] private float discardInterval = 0.5f;

        [Tooltip("自動で打ち進めるときの1手あたりの間隔。手動より短くしないと待たされる。")]
        [SerializeField] private float autoDiscardInterval = 0.14f;
        [SerializeField] private float phaseSettleTime = 0.5f;

        [Tooltip("能力の実演で、発動SEを鳴らしてから次のセリフに移るまでの間（秒）")]
        [SerializeField] private float abilityShowcaseInterval = 0.8f;

        /// <summary>旧実装との互換のために残している局の識別子。</summary>
        public enum TutorialRound
        {
            Round1_BasicRon,
            Round2_Draw,
            Round3_FakeHint,
            Round4_Ability,
            Round5_Final
        }

        private TutorialRound currentRound;
        public TutorialRound CurrentRound => currentRound;

        // --- 進行状態 ---
        private TutorialScenario _scenario;
        private TutorialRoundData _round;

        private int _playerHp;
        private int _enemyHp;

        // --- 賭け金 ---
        // 流局では賭け金が決着しないので、次の局へ持ち越される。
        // 持ち越された分は、次にロンが出た局でまとめて血の移動に加算される。
        private int _pot;              // いま場に乗っている賭け金の総額
        private int _lastBetAmount;    // 直前に賭けた額（流局の次の局はこれと同額が自動で賭けられる）
        private bool _prevRoundWasDraw;

        private bool _boardVisible = true;
        private bool _isWaitingForDiscard;
        private bool _isWaitingForHandSelectionComplete;
        private bool _hasRejectedFirstConfirm;
        private int _lastPlayerDiscardBaseId = -1;

        private Coroutine _scenarioRoutine;
        private Coroutine _interruptRoutine;
        private bool _aborted;

        /// <summary>オート満貫ボタンが押されたか。局ごとにリセットされる。</summary>
        public bool HasClickedAutoMangan { get; set; }

        /// <summary>手牌構築フェイズで13枚とみなす枚数。</summary>
        private const int HandSize = 13;

        /// <summary>
        /// 手牌構築フェイズのボタン開放段階。
        /// 「13枚選ぶ（両方隠す）→ 自動だけ出す → 自動を押したら決定も出す」の順に進む。
        /// </summary>
        private enum HandButtonStage
        {
            Hidden,
            AutoOnly,
            AutoAndDecide,

            /// <summary>自力で満貫手を組めた場合。オートは不要なので決定だけ出す。</summary>
            DecideOnly
        }

        private HandButtonStage _handButtonStage = HandButtonStage.Hidden;

        /// <summary>『自動』ボタンを出してよいか。HandUI.UpdateLayout から参照される。</summary>
        public bool IsAutoButtonVisible =>
            _handButtonStage == HandButtonStage.AutoOnly ||
            _handButtonStage == HandButtonStage.AutoAndDecide;

        /// <summary>『決定』ボタンを出してよいか。HandUI.UpdateLayout から参照される。</summary>
        public bool IsDecideButtonVisible =>
            _handButtonStage == HandButtonStage.AutoAndDecide ||
            _handButtonStage == HandButtonStage.DecideOnly;

        /// <summary>既定のセリフ。台本側の onHandFilledLines が空のときに使う。</summary>
        private static readonly List<TutorialLine> DefaultHandFilledLines = new List<TutorialLine>
        {
            new TutorialLine("今回は自動で選んであげるわ。"),
        };

        /// <summary>
        /// 現在の局で打牌が禁止されている牌種（-1 でなし）。
        /// HandUI 側でグレーアウト表示するために公開している。
        /// </summary>
        public int LockedTileBaseId => _round != null ? _round.lockedTileBaseId : -1;

        /// <summary>チュートリアル中、プレイヤーは能力を使えない（制約）。</summary>
        public bool IsAbilityUsableByPlayer => false;

        /// <summary>
        /// 現在の局の待ち牌（実牌ID）。オート満貫を押す前は空を返す。
        /// サーバーに繋がないチュートリアルでは聴牌判定を持たないため、
        /// 「台本の手牌を組んだときだけ聴牌」として扱う。
        /// </summary>
        public List<int> GetCurrentWaitTileIds()
        {
            if (_round == null || !HasClickedAutoMangan) return new List<int>();
            return TutorialTiles.EncodeAll(_round.waitBaseIds, _round.doraBaseId);
        }

        /// <summary>台本の手牌の役名（聴牌チェックのモック応答用）。</summary>
        public List<string> CurrentHandYaku =>
            _round != null ? _round.manganHandYaku : new List<string>();

        /// <summary>台本の手牌の翻数（聴牌チェックのモック応答用）。</summary>
        public int CurrentHandHan => _round != null ? _round.manganHandHan : 0;

        // ==================== 開始 / 中断 ====================

        public void StartTutorial()
        {
            StartTutorialFrom(0);
        }

        public void StartTutorialFrom(int roundIndex)
        {
            _scenario = scenario != null ? scenario : TutorialScenario.BuildDefault();
            _scenario.Validate();

            _playerHp = _scenario.playerStartHp;
            _enemyHp = _scenario.enemyStartHp;
            _aborted = false;

            _pot = 0;
            _lastBetAmount = 0;
            _prevRoundWasDraw = false;

            if (_scenarioRoutine != null) StopCoroutine(_scenarioRoutine);
            _scenarioRoutine = StartCoroutine(ScenarioRoutine(Mathf.Max(0, roundIndex)));
        }

        /// <summary>チュートリアルを打ち切ってタイトルへ戻す（スキップボタン用）。</summary>
        public void SkipTutorial()
        {
            _aborted = true;

            if (_scenarioRoutine != null) { StopCoroutine(_scenarioRoutine); _scenarioRoutine = null; }
            if (_interruptRoutine != null) { StopCoroutine(_interruptRoutine); _interruptRoutine = null; }

            if (arrowUI != null) arrowUI.Hide();
            if (maskUI != null) maskUI.Hide();
            if (dialogueUI != null) dialogueUI.HideNextRoundButton();

            string sceneName = _scenario != null ? _scenario.titleSceneName : "タイトルシーン";
            SceneManager.LoadScene(sceneName);
        }

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

            yield return StartCoroutine(PlayLines(_scenario.endingLines));
            SceneManager.LoadScene(_scenario.titleSceneName);
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

            // 前局の透視マークがプールの牌に残らないようにする
            ClearPerspectiveMarks();

            // 13枚そろうまでは『自動』も『決定』も出さない
            SetHandButtonStage(HandButtonStage.Hidden);

            // 手牌は空の状態で開始する（プレイヤーが山牌から選ぶ）
            SetupBoard(data, null);

            // 開幕は女の子とセリフだけ。盤面はイントロの途中で出す。
            SetBoardVisible(false);
            yield return null;

            int reveal = data.revealBoardAfterLineIndex;
            if (reveal >= 0 && reveal < data.introLines.Count - 1)
            {
                yield return StartCoroutine(PlayLines(data.introLines.GetRange(0, reveal + 1)));
                SetBoardVisible(true);
                yield return StartCoroutine(PlayLines(
                    data.introLines.GetRange(reveal + 1, data.introLines.Count - reveal - 1)));
            }
            else
            {
                yield return StartCoroutine(PlayLines(data.introLines));
                SetBoardVisible(true);
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

            if (data.allowManualHandSelection)
            {
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

            if (selfMadeMangan)
            {
                // 自力で満貫手を組めたなら『自動』を挟ませる理由がない。そのまま決定へ通す。
                // HasClickedAutoMangan は「台本の満貫手が盤面にそろっている」ことを表すフラグとして
                // 待ち牌の公開と決定の解禁に使われているので、ここでも立てておく。
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
                _pot += inherited;

                yield return StartCoroutine(PlayLines(ResolveInheritedBetLines(data, inherited)));
            }
            else
            {
                yield return StartCoroutine(PlayLines(data.beforeBetLines));
                yield return StartCoroutine(RunBettingPhase(data));

                _lastBetAmount = data.betAmount;
                _pot += data.betAmount;
            }

            // --- 打牌フェイズ ---
            // GameUIPhaseController は IsTutorialMode のとき HP パネルを出さないので、
            // フェイズを切り替えたあとに毎回こちらで表示し直す（手順⑦の説明に必要）。
            SetPhase(RoundStatus.Discard);
            ApplyHpToUI();
            yield return new WaitForSeconds(phaseSettleTime);
            yield return StartCoroutine(PlayLines(data.onBattleStartLines));

            yield return StartCoroutine(RunBattle(data));

            // 流局なら賭け金は場に残したまま次局へ持ち越す
            _prevRoundWasDraw = data.outcome == TutorialOutcome.Draw;

            yield return StartCoroutine(PlayLines(data.outroLines));
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
                        string.Format(line.text, inherited, _pot), line.speaker));
                }
                return resolved;
            }

            return new List<TutorialLine>
            {
                new TutorialLine($"前の局は流局だったから、賭け金は{inherited}円のまま持ち越しよ。改めて賭ける必要はないわ。"),
            };
        }

        // ==================== 盤面セットアップ ====================

        private void SetupBoard(TutorialRoundData data, List<int> handBaseIds)
        {
            if (gameUIManager == null || gameUIManager.HandUI == null || gameUIManager.WallUI == null) return;

            gameUIManager.IsTutorialMode = true;
            gameUIManager.TutorialManager = this;

            if (gameUIManager.PhaseTransitionUI != null)
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
        /// この局の山牌は『台本の満貫手（同一色13枚）＋ 筒子9枚 ＋ 索子9枚 ＋ 字牌3枚』で、
        /// 台本の色の牌はその13枚しか入っていない。他の色は9枚しかなく13枚に届かないので、
        /// この山から作れる満貫以上の手は台本の13枚だけ。よってこの一致判定で過不足がない。
        ///
        /// 山牌の構成を変えるときは、この前提が崩れていないか確認すること。
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

        private static readonly List<TutorialLine> DefaultSelfManganLines = new List<TutorialLine>
        {
            new TutorialLine("あら、自分で満貫手を組めたのね。やるじゃない。"),
            new TutorialLine("それなら『自動』は要らないわ。そのまま決定しなさい。"),
        };

        private static List<TutorialLine> ResolveSelfManganLines(TutorialRoundData data)
        {
            return (data.onSelfManganLines != null && data.onSelfManganLines.Count > 0)
                ? data.onSelfManganLines
                : DefaultSelfManganLines;
        }

        private static List<TutorialLine> ResolveHandFilledLines(TutorialRoundData data)
        {
            return (data.onHandFilledLines != null && data.onHandFilledLines.Count > 0)
                ? data.onHandFilledLines
                : DefaultHandFilledLines;
        }

        /// <summary>ボタンの開放段階を変えて、HandUI に即座に反映させる。</summary>
        private void SetHandButtonStage(HandButtonStage stage)
        {
            _handButtonStage = stage;

            if (gameUIManager != null && gameUIManager.HandUI != null)
                gameUIManager.HandUI.UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        private void SetPhase(RoundStatus status)
        {
            if (gameUIManager == null) return;

            if (gameUIManager.PhaseController != null)
                gameUIManager.PhaseController.UpdatePhaseStatus(status);
            else
                gameUIManager.SetCurrentPhaseStatus(status);
        }

        private void ApplyHpToUI()
        {
            var board = BoardStateManager.Instance;
            if (board != null) board.UpdateHp(_playerHp, _enemyHp);

            if (gameUIManager == null) return;

            // 盤面がまだ隠れている段階（女の子とセリフだけの状態）では表示を戻さない
            // 「準備完了」ボックスは次局待ち合わせ用。チュートリアルには待ち合わせがないので常に隠す
            if (gameUIManager.PlayerInfoUI != null)
            {
                gameUIManager.PlayerInfoUI.gameObject.SetActive(_boardVisible);
                gameUIManager.PlayerInfoUI.SetMaxHP(_scenario.playerStartHp);
                gameUIManager.PlayerInfoUI.SetHP(_playerHp);
                gameUIManager.PlayerInfoUI.ShowReadyBox(false);
            }
            if (gameUIManager.EnemyInfoUI != null)
            {
                gameUIManager.EnemyInfoUI.SetPanelVisible(_boardVisible);
                gameUIManager.EnemyInfoUI.SetMaxHP(_scenario.enemyStartHp);
                gameUIManager.EnemyInfoUI.SetHP(_enemyHp);
                gameUIManager.EnemyInfoUI.ShowReadyBox(false);
            }
        }

        /// <summary>
        /// 盤面まわりの表示を一括で切り替える。
        ///
        /// false のとき画面に残るのは「敵の立ち絵」と「セリフ」だけ。
        /// EnemyInfoUI.SetPanelVisible(false) はHPパネルのみを消し、立ち絵は残す。
        /// </summary>
        private void SetBoardVisible(bool visible)
        {
            _boardVisible = visible;
            if (gameUIManager == null) return;

            // 手牌 / 山牌 / 敵山牌 / 役一覧 / 待ち牌
            if (gameUIManager.PhaseController != null)
                gameUIManager.PhaseController.SetMatchUIVisibility(visible);

            if (gameUIManager.TurnIndicatorUI != null)
                gameUIManager.TurnIndicatorUI.gameObject.SetActive(visible);

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

            // --- ② セリフのあとにスマホを拡大する ---
            var info = gameUIManager.PlayerInfoUI;
            if (info != null)
            {
                info.gameObject.SetActive(true);
                yield return info.StartCoroutine(info.ZoomInRoutine(0.4f, 4.5f));
            }

            // --- ③ 賭け金は固定額。増減ボタンは押せないので決定するしかない ---
            bool confirmed = false;
            betting.ShowFixedBettingPhase(_playerHp, _playerHp, data.betAmount, _ => confirmed = true);

            GuideTo(betting.ConfirmButtonRect);
            yield return new WaitUntil(() => confirmed);
            ClearGuide();

            betting.HideBettingPhase();

            // --- ④ 拡大を戻す ---
            if (info != null)
            {
                yield return info.StartCoroutine(info.ResetZoomRoutine(0.3f));
            }

            yield return new WaitForSeconds(phaseSettleTime);
        }

        // ==================== 対局 ====================

        private IEnumerator RunBattle(TutorialRoundData data)
        {
            var board = BoardStateManager.Instance;
            int turns = data.enemyDiscardBaseIds.Count;
            int autoTurns = Mathf.Clamp(data.autoDiscardTurns, 0, turns);

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
                    _isWaitingForDiscard = true;
                    _lastPlayerDiscardBaseId = -1;

                    yield return new WaitUntil(() => !_isWaitingForDiscard);
                }

                yield return new WaitForSeconds(isAutoTurn ? autoDiscardInterval : discardInterval);

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

        private IEnumerator RunPlayerRon(TutorialRoundData data, int ronTileId)
        {
            SetPhase(RoundStatus.Agari);

            // ロンボタンを押させる
            if (gameUIManager != null && gameUIManager.AgariSelectionUI != null)
            {
                bool selected = false;
                gameUIManager.AgariSelectionUI.Show(() => selected = true);
                yield return new WaitUntil(() => selected);

                if (gameUIManager.RonWaitPanel != null) gameUIManager.RonWaitPanel.SetActive(false);
            }

            // ロンは血の奪い合い。相手から減った分がそのまま自分の血になる。
            // 流局で持ち越されてきた賭け金もここでまとめて決着する。
            // 相手の残り血より総額が大きい場合は、実際に奪えた分だけ増える。
            int prevEnemyHp = _enemyHp;
            int prevPlayerHp = _playerHp;

            int settlement = data.score + _pot;
            _pot = 0;

            int drained = Mathf.Min(settlement, prevEnemyHp);
            _enemyHp = prevEnemyHp - drained;
            _playerHp = prevPlayerHp + drained;

            // RonAnimationUI は handTiles を並べたあとに ronTile を別枠で追加描画する。
            // したがって handTiles にはアタリ牌を含めない13枚を渡すこと。
            var hand = TutorialTiles.EncodeAll(data.manganHandBaseIds, data.doraBaseId);

            yield return StartCoroutine(PlayRonAnimation(
                hand, ronTileId, data, isLocalPlayerWin: true,
                prevLocalHp: prevPlayerHp, newLocalHp: _playerHp,
                prevEnemyHp: prevEnemyHp, newEnemyHp: _enemyHp,
                displayScore: settlement));

            ApplyHpToUI();
        }

        private IEnumerator RunEnemyRon(TutorialRoundData data, int playerDiscardBaseId)
        {
            SetPhase(RoundStatus.Agari);

            // 手順⑯: 打点に加えて、流局から持ち越された賭け金もまとめて奪われる
            int prevPlayerHp = _playerHp;
            int prevEnemyHp = _enemyHp;

            int settlement = data.score + _pot;
            _pot = 0;

            // drawDamageToPlayer は賭け金とは別枠の流局ペナルティ。血の移動には含めない。
            int totalDamage = settlement + data.drawDamageToPlayer;
            _playerHp = Mathf.Max(0, prevPlayerHp - totalDamage);

            _enemyHp = prevEnemyHp + Mathf.Min(settlement, prevPlayerHp);

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
                displayScore: settlement));

            ApplyHpToUI();
        }

        /// <param name="displayScore">
        /// 演出に出す金額。持ち越された賭け金を含む「この局で動いた総額」を渡すこと。
        /// data.score をそのまま出すと、表示額と実際のHPの増減が食い違って見える。
        /// </param>
        private IEnumerator PlayRonAnimation(
            List<int> handTiles, int ronTileId, TutorialRoundData data, bool isLocalPlayerWin,
            int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp, int displayScore)
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
                () => done = true);

            yield return new WaitUntil(() => done);
        }

        private IEnumerator RunDraw(TutorialRoundData data)
        {
            SetPhase(RoundStatus.Draw);

            if (data.drawDamageToPlayer > 0)
            {
                _playerHp = Mathf.Max(0, _playerHp - data.drawDamageToPlayer);
                ApplyHpToUI();
            }

            yield return new WaitForSeconds(2.0f);
        }

        /// <summary>
        /// 手順⑱: 敵が能力を順に使ってみせる。
        ///
        /// チュートリアルはサーバーに繋がないので実際のスキル処理は走らせない。
        /// 能力欄を開いて対象の行を指し示し、その能力のSEと相手の反応で「使った」ことを見せる。
        /// プレイヤーは能力を使えない制約（IsAbilityUsableByPlayer）があるため、ここは実演のみ。
        /// </summary>
        private IEnumerator RunEnemyAbilityShowcase(TutorialRoundData data)
        {
            var ability = gameUIManager != null ? gameUIManager.AbilityUI : null;

            if (ability != null)
            {
                ability.gameObject.SetActive(true);

                // 非アクティブから有効化した直後は AbilityUI の Start() がまだ走っていない。
                // 先に開くとウィンドウ位置の初期化と開く演出がぶつかるので1フレーム待つ。
                yield return null;

                ability.OpenWindow();

                // 実演中は押しても何も起きないようにする。
                // 押せてしまうと DialogueUI がチュートリアルのセリフを上書きし、
                // 送りボタン待ちのまま進めなくなる。
                ability.IsDisplayOnly = true;

                // 行が生成されてレイアウトが確定するまでさらに1フレーム待つ
                yield return null;
            }

            var showcases = data.abilityShowcases;
            if (showcases == null || showcases.Count == 0)
            {
                // 台本に能力が並んでいない場合は、従来どおり軽く見せるだけにする
                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                foreach (var showcase in showcases)
                {
                    if (showcase == null || string.IsNullOrEmpty(showcase.skillType)) continue;

                    // どの能力の話かを矢印で指しながら説明する。
                    // マスクは使わないこと。穴の外側のクリックを全て食べるので、
                    // 出したままセリフ待ちに入ると送りボタンが押せなくなる。
                    RectTransform itemRt = ability != null
                        ? ability.GetAbilityItemRect(showcase.skillType)
                        : null;
                    if (itemRt != null) GuideTo(itemRt, useMask: false);

                    yield return StartCoroutine(PlayLines(showcase.beforeLines));

                    ClearGuide();
                    if (ability != null) ability.CloseWindow(false);

                    // ここから実際の発動。本編と同じ手順を踏む。
                    yield return StartCoroutine(RunEnemySkillActivation(showcase));

                    yield return StartCoroutine(PlayLines(showcase.afterLines));

                    // 次の能力の説明のために開き直す
                    if (ability != null && showcase != showcases[showcases.Count - 1])
                    {
                        ability.OpenWindow();
                        yield return null;
                    }
                }
            }

            ClearGuide();
            if (ability != null)
            {
                ability.IsDisplayOnly = false;
                ability.CloseWindow(false);
            }
        }

        /// <summary>
        /// 敵が能力を1つ実際に発動する。GameUISkillController が本編で行う手順に合わせている。
        /// カットイン → コスト（血）の支払い → 能力ごとの効果、の順。
        /// </summary>
        private IEnumerator RunEnemySkillActivation(TutorialAbilityShowcase showcase)
        {
            string skillName = SkillNames.GetDisplayName(showcase.skillType);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySkillSE(showcase.skillType);

            // 1. カットイン演出（立ち絵＋巨大テキスト）
            if (gameUIManager != null && gameUIManager.PhaseTransitionUI != null)
            {
                var cData = gameUIManager.EnemyInfoUI != null
                    ? gameUIManager.EnemyInfoUI.CurrentCharacterData
                    : null;

                yield return gameUIManager.PhaseTransitionUI.PlaySkillCutinAnimationRoutine(
                    skillName, isLocalPlayer: false, characterData: cData, duration: 2.0f);
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }

            // 2. コストの支払い。能力は血を削って使うものなので、敵のHPも実際に減らす。
            int cost = GameRules.GetSkillCost(showcase.skillType, 0);
            if (cost > 0 && cost < 99999)
            {
                _enemyHp = Mathf.Max(0, _enemyHp - cost);
                ApplyHpToUI();

                if (gameUIManager != null && gameUIManager.EnemyInfoUI != null)
                    gameUIManager.EnemyInfoUI.PlayBounceAnimation(0.4f);

                // 体力が減る様子を見せるためのタメ
                yield return new WaitForSeconds(1.0f);
            }

            // 3. 能力ごとの効果
            if (showcase.skillType == SkillNames.Perspective)
            {
                ApplyPerspectiveMarks(showcase.perspectiveTileCount);
            }

            if (showcase.skillType == SkillNames.BoostHand && !string.IsNullOrEmpty(showcase.boostYakuName))
            {
                // 役強化は結果が役一覧に残る。直後の手順⑳でプレイヤーに確認させる。
                var board = BoardStateManager.Instance;
                if (board != null)
                {
                    if (board.EnemyBoostHandBonus == null)
                        board.EnemyBoostHandBonus = new Dictionary<string, int>();
                    board.EnemyBoostHandBonus[showcase.boostYakuName] = showcase.boostHan;

                    if (gameUIManager != null && gameUIManager.YakuListUI != null)
                        gameUIManager.YakuListUI.UpdateBoostData(
                            board.LocalBoostHandBonus, board.EnemyBoostHandBonus);
                }
            }

            yield return new WaitForSeconds(abilityShowcaseInterval);
        }

        /// <summary>今この局で透視マークを立てた牌。局が変わるときに消すために覚えておく。</summary>
        private readonly List<TileVisual> _perspectiveMarked = new List<TileVisual>();

        /// <summary>
        /// 敵の『透視』の効果。プレイヤーの牌のうち指定枚数に透視マークを出す。
        ///
        /// 能力の実演は手牌を組む前に入るため、その時点では手牌が空のことがある。
        /// その場合はプレイヤーが選ぶ対象である山牌に付ける。
        /// </summary>
        private void ApplyPerspectiveMarks(int count)
        {
            if (count <= 0 || gameUIManager == null) return;

            var candidates = new List<TileVisual>();

            if (gameUIManager.HandUI != null) CollectTileVisuals(gameUIManager.HandUI.GetHandSlots(), candidates);
            if (candidates.Count < count && gameUIManager.WallUI != null)
                CollectTileVisuals(gameUIManager.WallUI.GetWallSlots(), candidates);

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] 透視マークを付ける牌が見つかりませんでした。");
                return;
            }

            // 端に固まらないよう、候補全体に散らして選ぶ
            int picked = Mathf.Min(count, candidates.Count);
            int step = Mathf.Max(1, candidates.Count / picked);

            for (int i = 0; i < picked; i++)
            {
                var visual = candidates[Mathf.Min(i * step, candidates.Count - 1)];
                if (visual == null || _perspectiveMarked.Contains(visual)) continue;

                visual.SetExposed(true);
                _perspectiveMarked.Add(visual);
            }
        }

        private static void CollectTileVisuals<T>(IEnumerable<T> slots, List<TileVisual> into) where T : Component
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var visual = slot.GetComponent<TileVisual>();
                if (visual != null && !into.Contains(visual)) into.Add(visual);
            }
        }

        /// <summary>局が切り替わるときに透視マークを消す。プールの牌に状態が残るのを防ぐ。</summary>
        private void ClearPerspectiveMarks()
        {
            foreach (var visual in _perspectiveMarked)
            {
                if (visual != null) visual.SetExposed(false);
            }
            _perspectiveMarked.Clear();
        }

        /// <summary>
        /// 手順⑳: 役一覧（役表）を実際に開かせる。
        /// 開くボタンが見つからない場合は誘導を諦めて先へ進む（進行が止まらないようにする）。
        /// </summary>
        private IEnumerator RunYakuListGuide(TutorialRoundData data)
        {
            var yakuList = gameUIManager != null ? gameUIManager.YakuListUI : null;
            if (yakuList == null) yield break;

            yakuList.gameObject.SetActive(true);

            // 画面右上の「役一覧」画像そのものを指す（開くボタン単体だと何を見ればよいか分からない）
            RectTransform guideRt = yakuList.GuideTargetRect;
            if (guideRt == null)
            {
                Debug.LogWarning("[TutorialManager] YakuListUI の開くボタンが未設定です。役一覧への誘導をスキップします。");
                yield break;
            }

            GuideTo(guideRt);

            // 開くまで待つ。すでに開いていればそのまま進む。
            yield return new WaitUntil(() => yakuList.IsOpen || _aborted);
            ClearGuide();

            if (_aborted) yield break;

            yield return StartCoroutine(PlayLines(data.onYakuListOpenedLines));

            yakuList.CloseYakuList();
        }

        // ==================== セリフ表示 ====================

        /// <summary>
        /// 台本のセリフを1行ずつ送る。シナリオ用コルーチンからのみ呼ばれるため、
        /// 同時に複数走ることはない（旧実装の多重起動によるコルーチン残留を防いでいる）。
        /// </summary>
        private IEnumerator PlayLines(List<TutorialLine> lines)
        {
            if (lines == null) yield break;

            foreach (var line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.text)) continue;

                bool clicked = false;
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(true);
                    dialogueUI.ShowText(Decorate(line));
                    dialogueUI.ShowNextRoundButton(() => clicked = true);
                }
                else
                {
                    clicked = true;
                }

                yield return new WaitUntil(() => clicked);

                if (dialogueUI != null) dialogueUI.HideNextRoundButton();
            }
        }

        /// <summary>
        /// 操作を弾いたときの一言。送りボタンを使わないので PlayLines と競合しない。
        /// 連打されても直前のものを止めるだけで、コルーチンは残らない。
        /// </summary>
        private void ShowInterruptMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_interruptRoutine != null) StopCoroutine(_interruptRoutine);
            _interruptRoutine = StartCoroutine(InterruptRoutine(text));
        }

        private IEnumerator InterruptRoutine(string text)
        {
            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText($"「{text}」");
            }
            yield return new WaitForSeconds(interruptMessageDuration);
            _interruptRoutine = null;
        }

        private static string Decorate(TutorialLine line)
        {
            switch (line.speaker)
            {
                case TutorialSpeaker.System:
                    return line.text;
                case TutorialSpeaker.Senpai:
                    // TODO: 立ち絵の切り替え。現状はテキストで話者を明示するのみ。
                    return $"あずにゃん先輩\n「{line.text}」";
                default:
                    return line.text.Contains("「") ? line.text : $"「{line.text}」";
            }
        }

        // ==================== 誘導（矢印＋マスク） ====================

        /// <param name="useMask">
        /// false にすると矢印だけで指し示す。マスクは穴の外側のクリックを全て食べるので、
        /// セリフ送りと併用したい場面（説明しながら指す）では必ず false にすること。
        /// </param>
        private void GuideTo(RectTransform target, bool useMask = true)
        {
            if (target == null) return;

            if (arrowUI != null) arrowUI.ShowAt(target, new Vector2(0, 50f));

            if (useMask)
            {
                if (maskUI != null) maskUI.Show(target);
            }
            else if (maskUI != null)
            {
                maskUI.Hide();
            }
        }

        private void ClearGuide()
        {
            if (arrowUI != null) arrowUI.Hide();
            if (maskUI != null) maskUI.Hide();
        }

        // ==================== UIからのコールバック ====================

        /// <summary>山牌と手牌の間で牌を動かせるか（手順①）。</summary>
        public bool OnTryMoveTile(int tileId, bool toHand)
        {
            if (_round == null) return false;

            if (!_round.allowManualHandSelection)
            {
                ShowInterruptMessage("今は『自動』ボタンを押してね。");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 手牌決定を受け付けるか（手順②③）。
        ///
        /// チュートリアルはサーバーに接続しないため満貫判定を持たない。
        /// 「最初の決定は必ず弾く」スクリプトで手順②を再現し、
        /// 制約『満貫手以下での開始は不可』は requireAutoManganToConfirm で担保する。
        /// </summary>
        public bool OnTryCompleteHandSelection()
        {
            if (_round == null) return false;

            if (_round.rejectFirstConfirm && !_hasRejectedFirstConfirm && !HasClickedAutoMangan)
            {
                _hasRejectedFirstConfirm = true;
                ShowInterruptMessage("その手じゃ満貫にも届かないわ。満貫手以上じゃないと始められないのよ。");
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.AutoManganButtonRect : null);
                return false;
            }

            if (_round.requireAutoManganToConfirm && !HasClickedAutoMangan)
            {
                ShowInterruptMessage("『自動』ボタン（オート満貫）を押して、満貫手を作ってね。");
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.AutoManganButtonRect : null);
                return false;
            }

            // 実際の待機解除は ConfirmationDialogUI での決定後（ConfirmHandSelectionComplete）
            return true;
        }

        public void ConfirmHandSelectionComplete()
        {
            _isWaitingForHandSelectionComplete = false;
        }

        /// <summary>打牌を受け付けるか（手順⑭の禁止牌を含む）。</summary>
        public bool OnTryDiscardTile(int tileId)
        {
            if (!_isWaitingForDiscard) return false;
            if (_round == null) return false;

            int baseId = TutorialTiles.BaseOf(tileId);

            if (_round.lockedTileBaseId >= 0 && baseId == _round.lockedTileBaseId)
            {
                ShowInterruptMessage(_round.lockedTileMessage);
                return false;
            }

            _lastPlayerDiscardBaseId = baseId;
            _isWaitingForDiscard = false;
            return true;
        }

        /// <summary>オート満貫ボタン（手順④）。台本の手牌を盤面へ流し込む。</summary>
        public void ApplyMockAutoMangan()
        {
            if (_round == null) return;
            if (HasClickedAutoMangan) return;

            HasClickedAutoMangan = true;

            SetupBoard(_round, _round.manganHandBaseIds);

            if (gameUIManager != null) gameUIManager.ClearSelection();

            // ここで初めて決定ボタンを出す
            SetHandButtonStage(HandButtonStage.AutoAndDecide);

            // 次は決定ボタンへ誘導する
            if (_isWaitingForHandSelectionComplete)
            {
                GuideTo(gameUIManager != null && gameUIManager.HandUI != null
                    ? gameUIManager.HandUI.DecideButtonRect : null);
            }
        }
    }
}
