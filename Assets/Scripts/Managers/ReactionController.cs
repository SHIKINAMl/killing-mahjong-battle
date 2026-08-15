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
    public class ReactionController : MonoBehaviour
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

        private IEnumerator WaitWhileLogIsOpen(float duration)
        {
            float elapsed = 0f;
            yield return null; // 最初のフレームでのクリック誤爆を防ぐ

            while (elapsed < duration)
            {
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;

                // 指定時間経過後、かつスキップが許可されている場合のみクリック判定を行う
                if (allowClickSkip && elapsed >= minWaitBeforeSkip)
                {
                    bool isClicked = false;
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) isClicked = true;
                    if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) isClicked = true;

                    if (isClicked)
                    {
                        break;
                    }
                }

                yield return null;
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
            if (enemyInfoUI == null) return false;

            // **セリフが1本も無いトリガーはここで断る。**
            // 積んでしまうと ProcessTriggerReaction が空振りして即 Dequeue するだけなのに、
            // 呼び出し側には true が返る。すると「トリガーで喋ったから CSV は要らない」と
            // 誤解して、CSV に書いてあるセリフまで出なくなる
            if (!enemyInfoUI.HasReaction(trigger)) return false;

            float now = Time.unscaledTime;

            // 同じセリフの連発を防ぐ。進行に関わるものは止めない
            if (priority != ReactionPriority.Progress)
            {
                float last;
                if (_lastFiredAt.TryGetValue(trigger, out last) && now - last < perTriggerCooldown)
                {
                    return false;
                }
            }

            if (priority == ReactionPriority.Ambient)
            {
                // ここでキューに積んではいけない。積むと待ち時間ぶん遅れて
                // 「もう終わった操作」に対して喋り出す
                if (isProcessingReactions || reactionQueue.Count > 0) return false;
                if (now - _lastAmbientAt < ambientGlobalCooldown) return false;
            }
            else if (priority == ReactionPriority.Situation)
            {
                if (_queuedTriggers.Contains(trigger)) return false;
            }

            _lastFiredAt[trigger] = now;
            if (priority == ReactionPriority.Ambient) _lastAmbientAt = now;

            _queuedTriggers.Add(trigger);
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessTriggerReaction(trigger, formatArg)));
            if (!isProcessingReactions) ProcessNextReaction();
            return true;
        }

        private IEnumerator ProcessTriggerReaction(ReactionTrigger trigger, string formatArg)
        {
            float duration = reactionDisplayDuration;
            string text = enemyInfoUI.PlayReaction(trigger, duration, formatArg ?? "");

            // データが無いトリガーで待ち時間を潰さない。
            // 5秒間なにも出ないまま後続を止めてしまうため
            if (string.IsNullOrEmpty(text))
            {
                _queuedTriggers.Remove(trigger);
                _currentReaction = null;
                ProcessNextReaction();
                yield break;
            }

            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(text.Contains("「") ? text : $"「{text}」");
            }

            yield return WaitWhileLogIsOpen(duration);

            _queuedTriggers.Remove(trigger);
            _pendingOnComplete = null;
            _currentReaction = null;
            ProcessNextReaction();
        }

        public void EnqueueDiscardReaction(int tileId, bool isLocalPlayer, string tileName)
        {
            string conditionBase = isLocalPlayer ? "プレイヤーが打牌した時" : "相手が打牌した時";
            int randomIdx = UnityEngine.Random.Range(1, 6);
            string condition = $"{conditionBase}{randomIdx}";
            
            var entry = Managers.DialogueManager.Instance.GetDialogueEntry(condition);
            if (entry == null)
            {
                entry = Managers.DialogueManager.Instance.GetDialogueEntry(conditionBase);
                condition = conditionBase;
            }

            if (entry != null && (!string.IsNullOrEmpty(entry.Dialogue1) || !string.IsNullOrEmpty(entry.Dialogue2)))
            {
                if (isLocalPlayer)
                {
                    EnqueueFormattedCSVDialogue(condition, tileName, false);
                }
                else
                {
                    if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(true);
                    EnqueueFormattedCSVDialogue(condition, tileName, false, () => {
                        if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);
                    });
                }
            }
            else
            {
                reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessLegacyDiscardEvent(tileId, isLocalPlayer, tileName)));
                if (!isProcessingReactions)
                {
                    ProcessNextReaction();
                }
            }
        }

        public void EnqueueFormattedCSVDialogue(string condition, string formatArg, bool clearPrevious = true, Action onComplete = null)
        {
            var entry = Managers.DialogueManager.Instance.GetDialogueEntry(condition);
            if (entry != null && (!string.IsNullOrEmpty(entry.Dialogue1) || !string.IsNullOrEmpty(entry.Dialogue2)))
            {
                if (clearPrevious) ClearReactions();

                if (!string.IsNullOrEmpty(entry.Dialogue1))
                {
                    string safeText1 = string.Format(entry.Dialogue1, formatArg);
                    string safeExpr = entry.Expression;
                    string safePose = entry.Pose;
                    bool isLast = string.IsNullOrEmpty(entry.Dialogue2);
                    reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessCSVDialogue(safeText1, safePose, safeExpr, isLast ? onComplete : null)));
                }

                if (!string.IsNullOrEmpty(entry.Dialogue2))
                {
                    string safeText2 = string.Format(entry.Dialogue2, formatArg);
                    string safeExpr = entry.Expression;
                    string safePose = entry.Pose;
                    reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessCSVDialogue(safeText2, safePose, safeExpr, onComplete)));
                }
                
                if (!isProcessingReactions) ProcessNextReaction();
            }
        }

        public void EnqueueCustomDialogue(string text, string poseName = "", string expressionName = "", bool clearPrevious = true)
        {
            if (clearPrevious) ClearReactions();
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessCSVDialogue(text, poseName, expressionName, null)));
            if (!isProcessingReactions) ProcessNextReaction();
        }

        public void EnqueueCSVDialogue(string condition, bool clearPrevious = true)
        {
            EnqueueFormattedCSVDialogue(condition, "", clearPrevious);
        }

        private IEnumerator ProcessCSVDialogue(string text, string poseName, string expressionName, Action onComplete = null)
        {
            _pendingOnComplete = onComplete;

            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(text.Contains("「") ? text : $"「{text}」");
            }
            if (enemyInfoUI != null)
            {
                if (!string.IsNullOrEmpty(expressionName) || !string.IsNullOrEmpty(poseName))
                {
                    enemyInfoUI.PlayReactionWithVisualId(poseName, expressionName, reactionDisplayDuration);
                }
                else
                {
                    enemyInfoUI.PlayBounceAnimation(reactionDisplayDuration);
                }
            }

            // StartCoroutine で包まないこと。包むと親を StopCoroutine しても
            // 子コルーチンが生き残り、演出が二重に進む。
            yield return WaitWhileLogIsOpen(reactionDisplayDuration);

            _pendingOnComplete = null;
            _currentReaction = null;
            onComplete?.Invoke();
            ProcessNextReaction();
        }

        private IEnumerator ProcessLegacyDiscardEvent(int tileId, bool isLocalPlayer, string tileName)
        {
            // 中断されても打牌モーションが解除されるようにしておく
            _pendingOnComplete = () => { if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false); };

            if (isLocalPlayer)
            {
                if (dialogueUI != null)
                {
                    string text = enemyInfoUI != null ? enemyInfoUI.PlayReaction(ReactionTrigger.PlayerDiscard, reactionDisplayDuration) : null;
                    if (string.IsNullOrEmpty(text)) text = "「プレイヤーが何かを捨てたな…」";
                    dialogueUI.ShowText(text);
                }
                if (enemyInfoUI != null)
                {
                    enemyInfoUI.PlayBounceAnimation(reactionDisplayDuration);
                }
            }
            else
            {
                if (dialogueUI != null)
                {
                    string text = enemyInfoUI != null ? enemyInfoUI.PlayReaction(ReactionTrigger.EnemyDiscard, reactionDisplayDuration, tileName) : null;
                    if (string.IsNullOrEmpty(text)) text = $"「{tileName}を切るわ！」";
                    dialogueUI.ShowText(text);
                }
                
                if (enemyInfoUI != null) 
                {
                    enemyInfoUI.SetDiscardingState(true);
                    enemyInfoUI.PlayBounceAnimation(reactionDisplayDuration);
                }
            }

            yield return WaitWhileLogIsOpen(reactionDisplayDuration);

            _pendingOnComplete = null;
            _currentReaction = null;
            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);

            ProcessNextReaction();
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
            _firstTerminalHonorPlayed = false;
            _firstMiddleTilePlayed = false;
            _firstMaxBetPlayed = false;
            _firstMinBetPlayed = false;
            _firstDrawPlayed = false;
            _round1DiscardCount = 0;
            _lastDiscardedTileId = -1;
            _firstPlayerAwasePlayed = false;
            _firstEnemyAwasePlayed = false;
            _currentRound = 1;

            _drawCount = 0;
            _playerConsecutiveHonorCount = 0;
            _handSelectionTimerActive = false;
            _betPhaseTimerActive = false;
            _playerHp = 10000;
            _enemyHp = 10000;
            _playerLostLastRound = false;
            _playerDiscardHistory.Clear();
            _enemyDiscardHistory.Clear();

            _betChangeCount = 0;
            _playerNearDeathPlayed = false;

            // ルールの「1対局に1回」と クールダウンもここで白紙に戻す
            ReactionRuleEngine.ResetMatch(ReactionRuleSet.Load());

            if (PlayerActivityWatcher.Instance != null)
                PlayerActivityWatcher.Instance.ResetForNewRound();
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
            if (enemyInfoUI == null) return false;

            var set = ReactionRuleSet.Load();
            if (set == null) return false;

            var rule = ReactionRuleEngine.Match(set, ev, ctx);
            if (rule == null) return false;

            var line = ReactionRuleEngine.PickLine(rule);
            if (line == null) return false;

            float now = Time.unscaledTime;

            if (rule.priority == ReactionPriority.Ambient)
            {
                // 積むと待ち時間ぶん遅れて「もう終わった操作」に対して喋り出す
                if (isProcessingReactions || reactionQueue.Count > 0) return false;
                if (now - _lastAmbientAt < ambientGlobalCooldown) return false;
                _lastAmbientAt = now;
            }
            else if (rule.priority == ReactionPriority.Situation)
            {
                if (_queuedRules.Contains(rule)) return false;
            }

            ReactionRuleEngine.MarkFired(rule);
            _queuedRules.Add(rule);

            var captured = rule;
            string text = line.text;
            string face = line.faceId;
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessRuleLine(captured, text, face)));
            if (!isProcessingReactions) ProcessNextReaction();
            return true;
        }

        private IEnumerator ProcessRuleLine(ReactionRule rule, string text, string faceId)
        {
            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(text.Contains("「") ? text : $"「{text}」");
            }
            if (enemyInfoUI != null && !string.IsNullOrEmpty(faceId))
            {
                enemyInfoUI.PlayReactionWithVisualId("", faceId, reactionDisplayDuration);
            }

            yield return WaitWhileLogIsOpen(reactionDisplayDuration);

            _queuedRules.Remove(rule);
            _pendingOnComplete = null;
            _currentReaction = null;
            ProcessNextReaction();
        }

        /// <summary>共通の値を詰めた入れ物を作る。呼び出し側はここに固有の値を足す</summary>
        private ReactionContext NewContext()
        {
            return new ReactionContext().WithCommon();
        }

        /// <summary>
        /// トリガーを試して、セリフが無ければ CSV に落とす。
        ///
        /// **この順番でなければならない。** 逆にすると `EnqueueCSVDialogue` が
        /// 既定で `ClearReactions()` を呼ぶため、先に積んだトリガーが消える。
        /// </summary>
        /// <returns>どちらかを流したら true</returns>
        private bool PlayOrFallback(ReactionTrigger trigger, ReactionPriority priority, string csvCondition)
        {
            if (Trigger(trigger, priority)) return true;
            if (string.IsNullOrEmpty(csvCondition)) return false;
            EnqueueCSVDialogue(csvCondition);
            return true;
        }

        /// <summary>
        /// 自分がテンパイしているか。**サーバーが返した `is_tenpai` が正**なので、
        /// 待ちの一覧が空でないかで見る（`BettingUI.UpdateUI` と同じ判定）。
        ///
        /// これは**自分の手の情報**なので、反応に出しても駆け引きは壊れない。
        /// 相手の手や待ちを使う反応は `CharacterData` のコメントどおり入れていない。
        /// </summary>
        private static bool IsLocalTenpai()
        {
            var b = BoardStateManager.Instance;
            return b != null && b.LocalWaitDataList != null && b.LocalWaitDataList.Count > 0;
        }

        /// <summary>
        /// 賭け金を上げ下げするたびに呼ぶ。`Bet_FidgetSpam` と `Bet_HesitateMax` の材料。
        /// **確定ではなく操作のたび**に呼ばれるので、ここで喋らせるのは Ambient に限る。
        /// </summary>
        public void NotifyBetAmountChanged()
        {
            _betChangeCount++;
            if (_betChangeCount == betFidgetCount)
            {
                Trigger(ReactionTrigger.Bet_FidgetSpam, ReactionPriority.Ambient);
            }
        }

        public void CheckAndPlayBetReaction(int betAmount, int maxHp, bool isLocalPlayer)
        {
            bool max = betAmount >= maxBetThreshold;
            bool min = betAmount > 0 && betAmount <= minBetThreshold;

            if (Publish(ReactionEvent.BetConfirmed, NewContext()
                    .Set(ReactionVars.IsMyBet, isLocalPlayer)
                    .Set(ReactionVars.BetAmount, betAmount)
                    .Set(ReactionVars.BetMax, maxBetThreshold)
                    .Set(ReactionVars.IsMaxBet, max)
                    .Set(ReactionVars.IsMinBet, min)
                    .Set(ReactionVars.IsTenpai, IsLocalTenpai())
                    .Set(ReactionVars.BetChangeCount, _betChangeCount)
                    .Set(ReactionVars.BetDecideSeconds,
                         _betPhaseTimerActive ? Time.time - _betPhaseStartTime : 0f)))
            {
                // 「初めて」の記録だけは進めておく。次に CSV へ落ちたとき辻褄が合うように
                if (isLocalPlayer)
                {
                    if (max) _firstMaxBetPlayed = true;
                    if (min) _firstMinBetPlayed = true;
                }
                _betPhaseTimerActive = false;
                return;
            }

            if (isLocalPlayer)
            {
                // 「初めて」の記録はトリガーが喋ったかに関わらず進める。
                // ここを分岐の中に置くと、トリガーで喋った局が数えられず、
                // あとから「初めて限度額」の CSV が場違いなタイミングで出る
                bool isMax = betAmount >= maxBetThreshold;
                bool isMin = betAmount > 0 && betAmount <= minBetThreshold;
                bool firstMax = isMax && !_firstMaxBetPlayed;
                bool firstMin = isMin && !_firstMinBetPlayed;
                if (isMax) _firstMaxBetPlayed = true;
                if (isMin) _firstMinBetPlayed = true;

                bool instant = _betPhaseTimerActive && (Time.time - _betPhaseStartTime) < 2.0f;

                if (betAmount <= 0)
                {
                    // 現状 BettingUI は最小でも1単位を賭けるので、ここには来ない。
                    // サーバーが 0 を許すようになったときのために残してある
                    Trigger(ReactionTrigger.Bet_ZeroGiveUp, ReactionPriority.Situation);
                }
                else if (isMax)
                {
                    // 強い順に見る。仕返し > 迷った末 > テンパイ > ハッタリ。
                    // 落ちる先の CSV は元の分岐をそのまま残している
                    string csv = instant ? "プレイヤーが即座に限度額を賭けた時"
                               : _playerLostLastRound ? "プレイヤーが前の局で負けたのに限度額を賭けた時"
                               : firstMax ? "初めて限度額いっぱいまで賭けた時の開幕のセリフ"
                               : null;

                    if (_playerLostLastRound) PlayOrFallback(ReactionTrigger.Bet_RevengeMax, ReactionPriority.Situation, csv);
                    else if (_betChangeCount >= betHesitateCount) PlayOrFallback(ReactionTrigger.Bet_HesitateMax, ReactionPriority.Situation, csv);
                    else if (IsLocalTenpai()) PlayOrFallback(ReactionTrigger.Bet_TenpaiMax, ReactionPriority.Situation, csv);
                    else PlayOrFallback(ReactionTrigger.Bet_BluffMax, ReactionPriority.Situation, csv);
                }
                else if (isMin)
                {
                    string csv = firstMin ? "初めて最小単位で賭けた時の開幕のセリフ"
                                          : "プレイヤーが少額しか賭けなかった時";

                    if (IsLocalTenpai()) PlayOrFallback(ReactionTrigger.Bet_TenpaiMin, ReactionPriority.Situation, csv);
                    else PlayOrFallback(ReactionTrigger.Bet_NoTenMin, ReactionPriority.Situation, csv);
                }
                // 501〜4999 は元から無言。ここに反応を足すと毎局喋ることになるので触らない
            }
            else
            {
                if (betAmount >= maxBetThreshold)
                {
                    EnqueueCSVDialogue("自分が限度額を賭けた時");
                }
            }

            _betPhaseTimerActive = false;
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
            if (Publish(ReactionEvent.SkillCast, NewContext()
                    .Set(ReactionVars.SkillType, skillType)
                    .Set(ReactionVars.IsMySkill, isLocalPlayer)
                    .Set(ReactionVars.SkillCost, costPaid)
                    .Set(ReactionVars.HpAfterSkill, hpAfter))) return;

            if (isLocalPlayer)
            {
                switch (skillType)
                {
                    case "perspective":
                        Trigger(ReactionTrigger.Skill_PlayerClairvoyance, ReactionPriority.Situation);
                        break;
                    case "boost_hand":
                        Trigger(ReactionTrigger.Skill_PlayerEnhance, ReactionPriority.Situation);
                        break;
                    case "special_victory":
                        Trigger(ReactionTrigger.Skill_PlayerSpecialWin, ReactionPriority.Progress);
                        break;
                }
                return;
            }

            if (hpAfter > 0 && hpAfter <= nearDeathHp
                && Trigger(ReactionTrigger.Skill_NearDeathByCost, ReactionPriority.Progress)) return;
            if (costPaid >= highSkillCost
                && Trigger(ReactionTrigger.Skill_HighCostPaid, ReactionPriority.Situation)) return;
            if (skillType == "perspective")
                Trigger(ReactionTrigger.Skill_EnemyClairvoyance, ReactionPriority.Situation);
        }

        /// <summary>
        /// 血が更新されたときに呼ぶ。プレイヤーが瀕死になった瞬間だけ、**1 対局に 1 回**喋る。
        /// 局ごとに戻すと、瀕死のまま何局か続いたときに毎局同じことを言い出す。
        /// `SetPlayerHp` から呼ばれるので、呼び出し側に追加の配線は要らない。
        /// </summary>
        private void CheckPlayerNearDeath()
        {
            if (_playerNearDeathPlayed) return;
            if (_playerHp <= 0 || _playerHp > nearDeathHp) return;
            if (Trigger(ReactionTrigger.Result_PlayerNearDeath, ReactionPriority.Situation))
            {
                _playerNearDeathPlayed = true;
            }
        }

        public void CheckAndPlayDrawReaction()
        {
            _drawCount++;

            if (Publish(ReactionEvent.Draw, NewContext()
                    .Set(ReactionVars.DrawCount, _drawCount))) return;

            if (_drawCount >= 2)
            {
                EnqueueCSVDialogue("流局が2回以上続いた時");
            }
            else if (!_firstDrawPlayed)
            {
                _firstDrawPlayed = true;
                EnqueueCSVDialogue("初めて流局した時の最後のセリフ");
            }
        }

        public void PlayDealingReaction()
        {
            EnqueueCSVDialogue("山牌構築中のセリフ", false);
        }

        public void HandleRoundStart(int round)
        {
            SetCurrentRound(round);

            // 局が変わったので「1局に1回」の枠を戻す
            ReactionRuleEngine.ResetRound(ReactionRuleSet.Load());

            if (Publish(ReactionEvent.RoundStart, NewContext()
                    .Set(ReactionVars.PrevWasDraw, _drawCount > 0 && round > 1)
                    .Set(ReactionVars.PrevWasLoss, _playerLostLastRound)))
                return;

            if (round == 1)
            {
                EnqueueCSVDialogue("1局目のゲーム開始時");
            }
            else
            {
                if (_playerHp <= 2000) EnqueueCSVDialogue("プレイヤーのHPが残りわずかな時の開幕");
                else if (_enemyHp <= 2000) EnqueueCSVDialogue("敵のHPが残りわずかな時の開幕");
                else if (_playerHp >= _enemyHp + 5000) EnqueueCSVDialogue("プレイヤーが圧倒的有利な時の開幕");
                else if (_enemyHp >= _playerHp + 5000) EnqueueCSVDialogue("敵が圧倒的有利な時の開幕");
                else EnqueueCSVDialogue("2局目以降の開幕時");
            }
        }

        public void StartHandSelectionTimer()
        {
            _handSelectionStartTime = Time.time;
            _handSelectionTimerActive = true;
        }
        
        public void StopHandSelectionTimer(bool isLocalPlayer)
        {
            if (isLocalPlayer && _handSelectionTimerActive)
            {
                float duration = Time.time - _handSelectionStartTime;

                if (Publish(ReactionEvent.HandConfirmed, NewContext()
                        .Set(ReactionVars.HandDecideSeconds, duration)))
                {
                    _handSelectionTimerActive = false;
                    return;
                }

                if (duration > 15.0f) EnqueueCSVDialogue("プレイヤーが手牌決定に時間をかけている時");
                else if (duration < 3.0f) EnqueueCSVDialogue("プレイヤーが手牌を即決した時");
            }
            _handSelectionTimerActive = false;
        }

        public void StartBetPhaseTimer()
        {
            _betPhaseStartTime = Time.time;
            _betPhaseTimerActive = true;
            // 迷った回数は局ごとに数え直す。持ち越すと2局目以降が必ず「散々迷った」になる
            _betChangeCount = 0;
        }

        public void HandleEnemyHandSelection(bool isYakuman, bool isMangan, bool isCheap)
        {
            if (isYakuman) EnqueueCSVDialogue("敵の手が役満の時");
            else if (isMangan) EnqueueCSVDialogue("敵の手が満貫以上の時");
            else if (isCheap) EnqueueCSVDialogue("敵の手が安い時");
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
            if (Publish(ReactionEvent.Agari, NewContext()
                    .Set(ReactionVars.IsMyWin, isLocalPlayerWin)
                    .Set(ReactionVars.IsYakuman, isYakuman)
                    .Set(ReactionVars.IsDoraBomb, isDoraBaku)
                    .Set(ReactionVars.IsCheapHand, isCheap))) return;

            if (isLocalPlayerWin)
            {
                if (isYakuman) PlayOrFallback(ReactionTrigger.Result_EnemyHitYakuman, ReactionPriority.Progress, "敵が役満に放銃した時");
                else if (isDoraBaku) EnqueueCSVDialogue("ドラ爆でアガった時");
                else if (isCheap) EnqueueCSVDialogue("敵が安い手に放銃した時");
                else EnqueueCSVDialogue("敵が放銃した時");
            }
            else
            {
                if (isYakuman) PlayOrFallback(ReactionTrigger.Result_PlayerHitYakuman, ReactionPriority.Progress, "プレイヤーが役満に放銃した時");
                else if (isDoraBaku) PlayOrFallback(ReactionTrigger.Result_EnemyDoraBomb, ReactionPriority.Progress, "プレイヤーが放銃した時");
                else if (isCheap) EnqueueCSVDialogue("プレイヤーが安い手に放銃した時");
                else EnqueueCSVDialogue("プレイヤーが放銃した時");
            }
        }

        public void HandleGameEnd(bool isLocalPlayerWin)
        {
            if (Publish(ReactionEvent.MatchEnd, NewContext()
                    .Set(ReactionVars.IsMyWin, isLocalPlayerWin))) return;

            if (isLocalPlayerWin)
            {
                // 女の子が倒れる場面。Result_EnemyKO が無ければ旧 Lose、それも無ければ CSV
                if (Trigger(ReactionTrigger.Result_EnemyKO, ReactionPriority.Progress)) return;
                PlayOrFallback(ReactionTrigger.Lose, ReactionPriority.Progress, "敵のHPが0になった時");
            }
            else
            {
                PlayOrFallback(ReactionTrigger.Win, ReactionPriority.Progress, "プレイヤーのHPが0になった時");
            }
        }

        /// <summary>牌の種類をルールの条件で使う日本語名にする（`ReactionVariableCatalog` の選択肢と揃える）</summary>
        private static string SuitName(KillingMahjong.TileCategory category)
        {
            switch (category)
            {
                case KillingMahjong.TileCategory.Manzu: return "萬子";
                case KillingMahjong.TileCategory.Pinzu: return "筒子";
                case KillingMahjong.TileCategory.Souzu: return "索子";
                default: return "字牌";
            }
        }

        public void CheckDiscardConditions(int tileId, bool isLocalPlayer)
        {
            bool playedSpecial = false;
            var tData = new KillingMahjong.TileData(tileId);

            if (_currentRound == 1)
            {
                _round1DiscardCount++;
                if (_round1DiscardCount == 1 && !isLocalPlayer) { playedSpecial = true; EnqueueCSVDialogue("相手が第１局目で先行"); }
                else if (_round1DiscardCount == 2 && !isLocalPlayer) { playedSpecial = true; EnqueueCSVDialogue("相手が第一局目で後攻"); }
                
                if (_lastDiscardedTileId >= 0)
                {
                    var lastTile = new KillingMahjong.TileData(_lastDiscardedTileId);
                    if (tData.Category == lastTile.Category && tData.Number == lastTile.Number)
                    {
                        if (isLocalPlayer && !_firstPlayerAwasePlayed) { _firstPlayerAwasePlayed = true; playedSpecial = true; EnqueueCSVDialogue("自分が第一局目で初めて合わせを行う"); }
                        else if (!isLocalPlayer && !_firstEnemyAwasePlayed) { _firstEnemyAwasePlayed = true; playedSpecial = true; EnqueueCSVDialogue("相手が第一局目で合わせ(敵の直前の打牌同じ牌を打つこと)を行う"); }
                    }
                }
            }

            bool isHonor = tData.Category == KillingMahjong.TileCategory.Honor;
            bool isMiddle = !isHonor && tData.Number >= 4 && tData.Number <= 6;
            bool isTerminalOrHonor = isHonor || tData.Number == 1 || tData.Number == 9;
            bool isSuji = false;
            bool isSameAsBefore = false;

            // **スジと同一牌の判定を、分岐の外へ出してある。**
            // ルールへ渡すには Publish の前に値が要るため。中身は元の計算そのまま
            if (isLocalPlayer)
            {
                if (!isHonor)
                {
                    int suji1 = tData.Number - 3;
                    int suji2 = tData.Number + 3;
                    foreach (var histId in _playerDiscardHistory)
                    {
                        var hTile = new KillingMahjong.TileData(histId);
                        if (hTile.Category == tData.Category && (hTile.Number == suji1 || hTile.Number == suji2))
                        {
                            isSuji = true; break;
                        }
                    }
                }

                isSameAsBefore = _playerDiscardHistory.Count > 0 &&
                                 new KillingMahjong.TileData(_playerDiscardHistory[_playerDiscardHistory.Count - 1]).Category == tData.Category &&
                                 new KillingMahjong.TileData(_playerDiscardHistory[_playerDiscardHistory.Count - 1]).Number == tData.Number;
            }

            // 字牌の連続数は「この牌を数に入れた」値で渡す。
            // 実際の加算は下で行うので、ここでは1つ先を読んでいる
            int honorStreakForRule = isLocalPlayer
                ? (isHonor ? _playerConsecutiveHonorCount + 1 : 0)
                : 0;

            float turnElapsed = 0f;
            if (isLocalPlayer && PlayerActivityWatcher.Instance != null)
                turnElapsed = PlayerActivityWatcher.Instance.TurnElapsedSeconds;

            bool ruleHandled = Publish(ReactionEvent.Discard, NewContext()
                .Set(ReactionVars.IsMyDiscard, isLocalPlayer)
                .Set(ReactionVars.TileSuit, SuitName(tData.Category))
                .Set(ReactionVars.TileNumber, tData.Number)
                .Set(ReactionVars.IsRedDora, tData.IsRedDora)
                .Set(ReactionVars.IsYakuhai, isHonor && tData.Number >= 5 && tData.Number <= 7)
                .Set(ReactionVars.IsOtakaze, isHonor && tData.Number >= 1 && tData.Number <= 4)
                .Set(ReactionVars.IsCenterTile, isMiddle)
                .Set(ReactionVars.IsSameAsPrev, isSameAsBefore)
                .Set(ReactionVars.IsSuji, isSuji)
                .Set(ReactionVars.HonorStreak, honorStreakForRule)
                .Set(ReactionVars.TurnElapsedSeconds, turnElapsed));

            if (ruleHandled) playedSpecial = true;

            if (isLocalPlayer)
            {
                // Discard_* のトリガーを先に試し、無ければ従来の CSV へ落とす。
                // 対応は REACTION_LINES.tsv のセリフから読み取ったもの:
                //   Discard_SafeTile … 「まずは無難な字牌から？」→ オタ風
                //   Discard_RawYakuhai … 「生牌の字牌を切るなんて」→ 役牌
                // スジ牌に対応するトリガーは無いので CSV のまま
                if (!ruleHandled)
                {
                    if (tData.IsRedDora) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_RedDora, ReactionPriority.Situation, "プレイヤーが赤ドラを切った時"); }
                    else if (isHonor && tData.Number >= 1 && tData.Number <= 4) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_SafeTile, ReactionPriority.Situation, "プレイヤーがオタ風を切った時"); }
                    else if (isHonor && tData.Number >= 5 && tData.Number <= 7) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_RawYakuhai, ReactionPriority.Situation, "プレイヤーが役牌を切った時"); }
                    else if (isMiddle) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_CenterTile, ReactionPriority.Situation, "プレイヤーがド真ん中の牌を切った時"); }
                    else if (isSameAsBefore) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_SameTileStreak, ReactionPriority.Situation, "プレイヤーが前の捨て牌と同じ牌を切った時"); }
                    else if (isSuji) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーがスジ牌を切った時"); }
                }

                if (isHonor) _playerConsecutiveHonorCount++;
                else _playerConsecutiveHonorCount = 0;

                if (!ruleHandled && _playerConsecutiveHonorCount >= 3) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_HonorStreak, ReactionPriority.Situation, "プレイヤーが字牌を連続で切った時"); }

                _playerDiscardHistory.Add(tileId);
            }
            else
            {
                if (!ruleHandled && tData.IsRedDora) { playedSpecial = true; EnqueueCSVDialogue("敵が赤ドラを切る時"); }
                
                bool isTsumogiri = _enemyDiscardHistory.Count > 0 && tileId == _lastDiscardedTileId; // 厳密なツモ切り判定はサーバーから来る情報に依存するため簡易化
                
                _enemyDiscardHistory.Add(tileId);
            }

            if (isTerminalOrHonor && !_firstTerminalHonorPlayed)
            {
                _firstTerminalHonorPlayed = true;
                if (!playedSpecial) { playedSpecial = true; EnqueueCSVDialogue("初めて一九字牌を切った時のセリフ"); }
            }
            else if (!isHonor && tData.Number >= 2 && tData.Number <= 8 && !_firstMiddleTilePlayed)
            {
                _firstMiddleTilePlayed = true;
                if (!playedSpecial) { playedSpecial = true; EnqueueCSVDialogue("初めて2-8の牌をを切った時のセリフ"); }
            }

            _lastDiscardedTileId = tileId;

            if (!playedSpecial)
            {
                string tileName = tData.GetTileName();
                EnqueueDiscardReaction(tileId, isLocalPlayer, tileName);
            }
        }
    }
}
