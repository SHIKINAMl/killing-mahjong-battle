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
    ///
    /// このファイルにはフィールド宣言と公開エントリポイントのみを置き、
    /// 責務ごとの本体は同じ Tutorial/ フォルダの partial ファイルに分けている:
    ///   TutorialManager.Progression.cs   … シナリオ進行・局の運び・賭け金・打牌ターン・流局
    ///   TutorialManager.Settlement.cs    … ロン決着・清算パネル・ロン演出
    ///   TutorialManager.AbilityShowcase.cs … 敵の能力デモ
    ///   TutorialManager.Board.cs         … 盤面のセットアップと表示
    ///   TutorialManager.Dialogue.cs      … セリフ表示・誘導
    ///   TutorialManager.Input.cs         … プレイヤー操作の受け口
    /// </summary>
    public partial class TutorialManager : MonoBehaviour
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
        // 賭け金は確定した時点で両者の血から引かれ、場に積まれる（＝賭けた分は先に払う）。
        // 決着したときの増減はこれとは別に GameRules の式で決まる:
        //   勝者が得る額 = 勝者自身の賭け金 × 勝者の役の倍率
        //   敗者が失う額 = 敗者自身の賭け金 × 勝者の役の倍率（単騎で上がられたら2倍）
        // したがって満貫（1倍）で上がった勝者は、払った賭け金と同額が戻る＝差し引き0になる。
        // 流局では決着しないので、賭け金は次の局へ積み増される（＝次の決着が大きくなる）。
        // 場の表示（BetPotUI）は積まれている額の情報表示で、表示額そのものが移動するわけではない。
        private int _playerStake;      // 自分が賭けている額
        private int _enemyStake;       // 相手が賭けている額
        private int Pot => _playerStake + _enemyStake;

        /// <summary>
        /// いま場に積まれているのが何局ぶんか。**清算パネルの「素点（持ち越しN局ぶん）」に使う。**
        /// 流局のたびに `PlaceBet` がもう1局ぶん積むので、その回数を数えるのが正確。
        /// **賭け金の割り算で求めてはいけない**（局ごとに額が変わりうるし、
        /// 血が足りなくて満額払えなかった局があると割り切れない）。
        /// </summary>
        private int _stakeRounds;
        private int _lastBetAmount;    // 直前に賭けた額（流局の次の局はこれと同額が自動で賭けられる）
        private int _lastConfirmedBet; // 賭け金UIで実際に確定した額
        private bool _prevRoundWasDraw;

        private bool _boardVisible = true;

        /// <summary>セリフの送り待ち中か。待っている間は牌を触らせない。</summary>
        private bool _isWaitingForLine;

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

            _playerStake = 0;
            _enemyStake = 0;
            _stakeRounds = 0;
            _lastBetAmount = 0;
            _prevRoundWasDraw = false;
            if (gameUIManager != null) gameUIManager.ScoreGauge.ResetScores();
            ApplyPotToUI();

            // メーターの分母（到達最高HP）は前回のプレイの値が残るので引き直す
            if (gameUIManager != null)
            {
                if (gameUIManager.PlayerInfoUI != null)
                    gameUIManager.PlayerInfoUI.ResetHpMeter(_scenario.playerStartHp);
                if (gameUIManager.EnemyInfoUI != null)
                    gameUIManager.EnemyInfoUI.ResetHpMeter(_scenario.enemyStartHp);
            }

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
            if (dialogueUI != null)
            {
                dialogueUI.HideNextRoundButton();
                // 全画面のクリック受けが残るとタイトルへ戻ったあとも操作を食う
                dialogueUI.HideAdvanceOnAnyClick();
            }

            string sceneName = _scenario != null ? _scenario.titleSceneName : "タイトルシーン";
            SceneManager.LoadScene(sceneName);
        }

    }
}
