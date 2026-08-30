using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 追加: 新しいInputSystem用
using KillingMahjong.UI;
using KillingMahjong.Managers.Reactions;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// リアクションの重要度。**この区別が無いとキューが破綻する。**
    ///
    /// 1件あたり reactionDisplayDuration（既定5秒）を直列に消化する作りなので、
    /// ホバーや連打のような高頻度のものまで同じキューに流すと、
    /// キューが伸び続けて「30秒前の出来事」を今喋り出す。
    /// </summary>
    public enum ReactionPriority
    {
        /// <summary>進行に関わる。必ず出す（ロン・流局・被弾・局開始など）</summary>
        Progress,
        /// <summary>状況の説明。出したいが、同じものが並んでいるなら1件でよい（ベット確定・打牌傾向など）</summary>
        Situation,
        /// <summary>環境。出なくてもよい。**演出中は無条件で捨てる**（ホバー・連打・放置・つつき）</summary>
        Ambient,
    }

    /// <summary>
    /// キャラクターのリアクション、ログの表示待ち、シーケンシャルな演出などを管理するクラス
    /// </summary>
    public partial class ReactionController : MonoBehaviour
    {
        public static ReactionController Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("リアクションを表示したまま待つ最大時間")]
        [SerializeField] private float reactionDisplayDuration = 5.0f;
        [Tooltip("セリフが表示されてからクリックでスキップできるようになるまでの最低待機時間（誤爆防止）")]
        [SerializeField] private float minWaitBeforeSkip = 0.5f;
        [Tooltip("クリック（タップ）でセリフをスキップできるかどうか")]
        [SerializeField] private bool allowClickSkip = true;

        // UIの参照（シーン上でセットアップするか自動取得する）
        public DialogueUI dialogueUI;
        public EnemyInfoUI enemyInfoUI;
        public PlayerInfoUI playerInfoUI;

        [Header("発火制御")]
        [Tooltip("Ambient を連発させない最短間隔(秒)。トリガー個別のクールダウンとは別に全体へ効く")]
        [SerializeField] private float ambientGlobalCooldown = 6.0f;
        [Tooltip("同じトリガーを再び出せるようになるまでの秒数。Progress は免除される")]
        [SerializeField] private float perTriggerCooldown = 20.0f;

        private Queue<Action> reactionQueue = new Queue<Action>();
        private bool isProcessingReactions = false;

        // 発火制御用。Time.unscaledTime で測る（演出で timeScale を触っても効くように）
        private readonly Dictionary<ReactionTrigger, float> _lastFiredAt = new Dictionary<ReactionTrigger, float>();
        private readonly HashSet<ReactionTrigger> _queuedTriggers = new HashSet<ReactionTrigger>();
        private readonly HashSet<ReactionRule> _queuedRules = new HashSet<ReactionRule>();
        private float _lastAmbientAt = -999f;

        // 実行中の演出コルーチンと、その完了時に必ず走らせたい後始末。
        // 中断時に onComplete を取りこぼすと SetDiscardingState(false) が呼ばれず、
        // 敵が打牌モーションのまま固まる。
        private Coroutine _currentReaction;
        private Action _pendingOnComplete;

        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
                // 放置・連打・迷いを見る監視役。**シーンには置かない**
                // （対局シーンが2つあり、片方だけに置く事故が起きる）
                PlayerActivityWatcher.Create(transform);
            } else {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            // シーン再読込時に破棄済みオブジェクトを指したままにしない
            if (Instance == this) Instance = null;
        }

        public void Setup(DialogueUI dialogueUI, EnemyInfoUI enemyInfoUI, PlayerInfoUI playerInfoUI)
        {
            this.dialogueUI = dialogueUI;
            this.enemyInfoUI = enemyInfoUI;
            this.playerInfoUI = playerInfoUI;
            reactionQueue.Clear();
            isProcessingReactions = false;
            _currentReaction = null;
            _pendingOnComplete = null;
        }

        /// <summary>
        /// 演出キューを空にして進行中の演出を打ち切る。
        ///
        /// StopAllCoroutines() は使わない。演出コルーチンを問答無用で殺すと
        /// 末尾の onComplete / ProcessNextReaction に到達せず、
        /// 敵の打牌モーションが解除されないままキューが止まるため。
        /// </summary>
        public void ClearReactions()
        {
            reactionQueue.Clear();
            _queuedTriggers.Clear();
            _queuedRules.Clear();
            isProcessingReactions = false;

            if (_currentReaction != null)
            {
                StopCoroutine(_currentReaction);
                _currentReaction = null;
            }

            // 中断された演出の後始末は必ず実行する
            Action pending = _pendingOnComplete;
            _pendingOnComplete = null;
            if (pending != null) pending.Invoke();

            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);
        }

        public void ProcessNextReaction()
        {
            if (reactionQueue.Count > 0)
            {
                // ログが開かれている間はキューの消化を止める
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    isProcessingReactions = false;
                    return;
                }

                isProcessingReactions = true;
                var action = reactionQueue.Dequeue();
                action.Invoke();
            }
            else
            {
                isProcessingReactions = false;
            }
        }


        /// <summary>
        /// トリガーでリアクションを起こす。**新しいリアクションはすべてこの入口を通す。**
        /// セリフと表情は `CharacterData.reactions`（ScriptableObject）から引く。
        /// CSV（DialogueManager）経由の既存フローはそのまま残してある。
        ///
        /// 重要度で扱いが変わる:
        ///   Progress  … 必ずキューへ積む。クールダウン免除
        ///   Situation … 同じトリガーが既に並んでいれば捨てる
        ///   Ambient   … **演出中・キューに何かある間は捨てる**。全体クールダウンもかかる
        /// </summary>
        /// <returns>実際に積んだら true。捨てたら false</returns>
        public bool Trigger(ReactionTrigger trigger, ReactionPriority priority, string formatArg = "")
        {
            return TriggerCore(trigger, priority, formatArg);
        }


        public void EnqueueDiscardReaction(int tileId, bool isLocalPlayer, string tileName)
        {
            EnqueueDiscardReactionCore(tileId, isLocalPlayer, tileName);
        }

        public void EnqueueFormattedCSVDialogue(string condition, string formatArg, bool clearPrevious = true, Action onComplete = null)
        {
            EnqueueFormattedCSVDialogueCore(condition, formatArg, clearPrevious, onComplete);
        }

        public void EnqueueCustomDialogue(string text, string poseName = "", string expressionName = "", bool clearPrevious = true)
        {
            EnqueueCustomDialogueCore(text, poseName, expressionName, clearPrevious);
        }

        public void EnqueueCSVDialogue(string condition, bool clearPrevious = true)
        {
            EnqueueCSVDialogueCore(condition, clearPrevious);
        }



        // --- CSV Dialogue & State Tracking ---
        private bool _firstTerminalHonorPlayed = false;
        private bool _firstMiddleTilePlayed = false;
        private bool _firstMaxBetPlayed = false;
        private bool _firstMinBetPlayed = false;
        private bool _firstDrawPlayed = false;
        
        private int _lastDiscardedTileId = -1;
        private int _round1DiscardCount = 0;
        private bool _firstPlayerAwasePlayed = false;
        private bool _firstEnemyAwasePlayed = false;
        
        private int _currentRound = 1;

        // --- 2026-08-15 追加。CharacterData のトリガーを発火させるための状態 ---
        //
        // ここまで `ReactionTrigger` は 48 種のうち 5 種しか鳴っていなかった。
        // `Trigger()` は実装済みで呼び出し元が無いだけだったので、判定に要る材料を
        // ここへ足して繋いでいる。**CSV を消していない**のがこの実装の要点で、
        // トリガーにセリフが無ければ従来どおり CSV のセリフが出る（`PlayOrFallback`）。

        [Header("トリガーの発火条件")]
        [Tooltip("この額以上を賭けたら「限度額」とみなす")]
        [SerializeField] private int maxBetThreshold = 5000;
        [Tooltip("この額以下を賭けたら「最小」とみなす")]
        [SerializeField] private int minBetThreshold = 500;
        [Tooltip("賭け金をこの回数以上いじったら「散々迷った」とみなす")]
        [SerializeField] private int betHesitateCount = 4;
        [Tooltip("賭けフェイズ中に上げ下げがこの回数を超えたら Bet_FidgetSpam を出す")]
        [SerializeField] private int betFidgetCount = 8;
        [Tooltip("スキルのコストがこの額以上なら Skill_HighCostPaid")]
        [SerializeField] private int highSkillCost = 3000;
        [Tooltip("この血以下なら瀕死とみなす（Skill_NearDeathByCost / Result_PlayerNearDeath）")]
        [SerializeField] private int nearDeathHp = 3000;

        /// <summary>賭けフェイズ中に賭け金をいじった回数。`StartBetPhaseTimer` で 0 に戻る</summary>
        private int _betChangeCount = 0;
        /// <summary>Result_PlayerNearDeath を1局に1回だけにするための記録</summary>
        private bool _playerNearDeathPlayed = false;

        // --- New State Tracking Variables ---
        private int _drawCount = 0;
        private int _playerConsecutiveHonorCount = 0;
        private float _handSelectionStartTime = 0f;
        private bool _handSelectionTimerActive = false;
        private float _betPhaseStartTime = 0f;
        private bool _betPhaseTimerActive = false;
        private int _playerHp = 10000;
        private int _enemyHp = 10000;
        private bool _playerLostLastRound = false;
        private List<int> _playerDiscardHistory = new List<int>();
        private List<int> _enemyDiscardHistory = new List<int>();

        /// <summary>いま何局目か。ルールの共通変数として配るために公開している</summary>
        public int CurrentRound { get { return _currentRound; } }

        public void SetCurrentRound(int round)
        {
            _currentRound = round;
        }

        public void SetPlayerHp(int hp)
        {
            _playerHp = hp;
            CheckPlayerNearDeath();
        }
        public void SetEnemyHp(int hp) { _enemyHp = hp; }
        public void SetPlayerLostLastRound(bool lost) { _playerLostLastRound = lost; }

        public void ResetStateForNewGame()
        {
            ResetStateForNewGameCore();
        }

        /// <summary>
        /// プランナーが作ったルール（`ReactionRuleSet`）を試す。**すべての反応の最初の関門。**
        ///
        /// 3層のうちの1層目で、順番は **ルール → トリガー → CSV**。
        /// ルールが当たればそこで終わり、当たらなければ従来どおりの動きに落ちる。
        /// **アセットが無くても動く**ので、ルールを1つも作っていない状態でも今までと同じ。
        ///
        /// 出す・出さないの間引き（優先度）はトリガー側と同じ考え方で揃えてある。
        /// </summary>
        /// <returns>実際に積んだら true</returns>
        public bool Publish(ReactionEvent ev, ReactionContext ctx)
        {
            return PublishCore(ev, ctx);
        }


        /// <summary>
        /// 賭け金を上げ下げするたびに呼ぶ。`Bet_FidgetSpam` と `Bet_HesitateMax` の材料。
        /// **確定ではなく操作のたび**に呼ばれるので、ここで喋らせるのは Ambient に限る。
        /// </summary>
        public void NotifyBetAmountChanged()
        {
            NotifyBetAmountChangedCore();
        }

        public void CheckAndPlayBetReaction(int betAmount, int maxHp, bool isLocalPlayer)
        {
            CheckAndPlayBetReactionCore(betAmount, maxHp, isLocalPlayer);
        }

        /// <summary>
        /// スキルが発動したときの反応。`GameUISkillController.HandleSkillCastedRoutine` から呼ぶ。
        ///
        /// **`costPaid` は「発動した側が払った血」**で、`hpAfter` はその後の血。
        /// どちらもサーバー由来の値をそのまま渡してもらう（クライアントで逆算しない）。
        ///
        /// 相手（女の子）が撃った場合は、スキルの種類より**自分の消耗を優先**する。
        /// 瀕死なのに余裕のある透視のセリフが出ると、演出と血の残量が食い違って見えるため。
        /// </summary>
        public void HandleSkillCast(string skillType, bool isLocalPlayer, int costPaid, int hpAfter)
        {
            HandleSkillCastCore(skillType, isLocalPlayer, costPaid, hpAfter);
        }

        public void CheckAndPlayDrawReaction()
        {
            CheckAndPlayDrawReactionCore();
        }

        public void PlayDealingReaction()
        {
            PlayDealingReactionCore();
        }

        public void HandleRoundStart(int round)
        {
            HandleRoundStartCore(round);
        }

        public void StartHandSelectionTimer()
        {
            StartHandSelectionTimerCore();
        }
        
        public void StopHandSelectionTimer(bool isLocalPlayer)
        {
            StopHandSelectionTimerCore(isLocalPlayer);
        }

        public void StartBetPhaseTimer()
        {
            StartBetPhaseTimerCore();
        }

        public void HandleEnemyHandSelection(bool isYakuman, bool isMangan, bool isCheap)
        {
            HandleEnemyHandSelectionCore(isYakuman, isMangan, isCheap);
        }

        /// <summary>
        /// 局の決着。
        ///
        /// **`Result_*` の名前は「誰が撃ったか」ではなく「誰が食らったか」で付いている。**
        /// 名前だけ見ると逆に読めるので、セリフから読み取った対応をここに残す:
        ///   Result_EnemyHitYakuman  … 女の子が役満に放銃（「バカな……役満ですって!?」）
        ///   Result_PlayerHitYakuman … 女の子が役満で和了（「役・満・よ♡ …人生終了！」）
        ///   Result_EnemyDoraBomb    … 女の子がドラ爆で和了（「ドラがたっぷりの極上の一撃」）
        /// 並べ替えるときは REACTION_LINES.tsv のセリフを読んでからにすること。
        /// </summary>
        public void HandleAgari(bool isLocalPlayerWin, bool isYakuman, bool isDoraBaku, bool isCheap)
        {
            HandleAgariCore(isLocalPlayerWin, isYakuman, isDoraBaku, isCheap);
        }

        public void HandleGameEnd(bool isLocalPlayerWin)
        {
            HandleGameEndCore(isLocalPlayerWin);
        }

        public void CheckDiscardConditions(int tileId, bool isLocalPlayer)
        {
            CheckDiscardConditionsCore(tileId, isLocalPlayer);
        }
    }
}
