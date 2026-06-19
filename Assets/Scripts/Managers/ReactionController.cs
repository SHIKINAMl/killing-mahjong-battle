using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 追加: 新しいInputSystem用
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
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

        private Queue<Action> reactionQueue = new Queue<Action>();
        private bool isProcessingReactions = false;

        private void Awake()
        {
            if (Instance == null) {
                Instance = this;
            } else {
                Destroy(gameObject);
            }
        }

        public void Setup(DialogueUI dialogueUI, EnemyInfoUI enemyInfoUI, PlayerInfoUI playerInfoUI)
        {
            this.dialogueUI = dialogueUI;
            this.enemyInfoUI = enemyInfoUI;
            this.playerInfoUI = playerInfoUI;
            reactionQueue.Clear();
            isProcessingReactions = false;
        }

        public void ClearReactions()
        {
            reactionQueue.Clear();
            isProcessingReactions = false;
            StopAllCoroutines();
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
                reactionQueue.Enqueue(() => StartCoroutine(ProcessLegacyDiscardEvent(tileId, isLocalPlayer, tileName)));
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
                    reactionQueue.Enqueue(() => StartCoroutine(ProcessCSVDialogue(safeText1, safePose, safeExpr, isLast ? onComplete : null)));
                }

                if (!string.IsNullOrEmpty(entry.Dialogue2))
                {
                    string safeText2 = string.Format(entry.Dialogue2, formatArg);
                    string safeExpr = entry.Expression;
                    string safePose = entry.Pose;
                    reactionQueue.Enqueue(() => StartCoroutine(ProcessCSVDialogue(safeText2, safePose, safeExpr, onComplete)));
                }
                
                if (!isProcessingReactions) ProcessNextReaction();
            }
        }

        public void EnqueueCSVDialogue(string condition, bool clearPrevious = true)
        {
            EnqueueFormattedCSVDialogue(condition, "", clearPrevious);
        }

        private IEnumerator ProcessCSVDialogue(string text, string poseName, string expressionName, Action onComplete = null)
        {
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

            yield return StartCoroutine(WaitWhileLogIsOpen(reactionDisplayDuration));
            onComplete?.Invoke();
            ProcessNextReaction();
        }

        private IEnumerator ProcessLegacyDiscardEvent(int tileId, bool isLocalPlayer, string tileName)
        {
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

            yield return StartCoroutine(WaitWhileLogIsOpen(reactionDisplayDuration));

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

        public void SetCurrentRound(int round)
        {
            _currentRound = round;
        }

        public void SetPlayerHp(int hp) { _playerHp = hp; }
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
        }

        public void CheckAndPlayBetReaction(int betAmount, int maxHp, bool isLocalPlayer)
        {
            if (isLocalPlayer)
            {
                if (betAmount >= 5000)
                {
                    if (_betPhaseTimerActive && (Time.time - _betPhaseStartTime) < 2.0f)
                    {
                        EnqueueCSVDialogue("プレイヤーが即座に限度額を賭けた時");
                    }
                    else if (_playerLostLastRound)
                    {
                        EnqueueCSVDialogue("プレイヤーが前の局で負けたのに限度額を賭けた時");
                    }
                    else if (!_firstMaxBetPlayed)
                    {
                        _firstMaxBetPlayed = true;
                        EnqueueCSVDialogue("初めて限度額いっぱいまで賭けた時の開幕のセリフ");
                    }
                }
                else if (betAmount <= 500)
                {
                    if (!_firstMinBetPlayed)
                    {
                        _firstMinBetPlayed = true;
                        EnqueueCSVDialogue("初めて最小単位で賭けた時の開幕のセリフ");
                    }
                    else
                    {
                        EnqueueCSVDialogue("プレイヤーが少額しか賭けなかった時");
                    }
                }
            }
            else
            {
                if (betAmount >= 5000)
                {
                    EnqueueCSVDialogue("自分が限度額を賭けた時");
                }
            }
            
            _betPhaseTimerActive = false;
        }

        public void CheckAndPlayDrawReaction()
        {
            _drawCount++;
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
                if (duration > 15.0f) EnqueueCSVDialogue("プレイヤーが手牌決定に時間をかけている時");
                else if (duration < 3.0f) EnqueueCSVDialogue("プレイヤーが手牌を即決した時");
            }
            _handSelectionTimerActive = false;
        }

        public void StartBetPhaseTimer()
        {
            _betPhaseStartTime = Time.time;
            _betPhaseTimerActive = true;
        }

        public void HandleEnemyHandSelection(bool isYakuman, bool isMangan, bool isCheap)
        {
            if (isYakuman) EnqueueCSVDialogue("敵の手が役満の時");
            else if (isMangan) EnqueueCSVDialogue("敵の手が満貫以上の時");
            else if (isCheap) EnqueueCSVDialogue("敵の手が安い時");
        }

        public void HandleAgari(bool isLocalPlayerWin, bool isYakuman, bool isDoraBaku, bool isCheap)
        {
            if (isLocalPlayerWin)
            {
                if (isYakuman) EnqueueCSVDialogue("敵が役満に放銃した時");
                else if (isDoraBaku) EnqueueCSVDialogue("ドラ爆でアガった時");
                else if (isCheap) EnqueueCSVDialogue("敵が安い手に放銃した時");
                else EnqueueCSVDialogue("敵が放銃した時");
            }
            else
            {
                if (isYakuman) EnqueueCSVDialogue("プレイヤーが役満に放銃した時");
                else if (isCheap) EnqueueCSVDialogue("プレイヤーが安い手に放銃した時");
                else EnqueueCSVDialogue("プレイヤーが放銃した時");
            }
        }

        public void HandleGameEnd(bool isLocalPlayerWin)
        {
            if (isLocalPlayerWin) EnqueueCSVDialogue("敵のHPが0になった時");
            else EnqueueCSVDialogue("プレイヤーのHPが0になった時");
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
                
                bool isSameAsBefore = _playerDiscardHistory.Count > 0 && 
                                      new KillingMahjong.TileData(_playerDiscardHistory[_playerDiscardHistory.Count - 1]).Category == tData.Category && 
                                      new KillingMahjong.TileData(_playerDiscardHistory[_playerDiscardHistory.Count - 1]).Number == tData.Number;

                if (tData.IsRedDora) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーが赤ドラを切った時"); }
                else if (isHonor && tData.Number >= 1 && tData.Number <= 4) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーがオタ風を切った時"); }
                else if (isHonor && tData.Number >= 5 && tData.Number <= 7) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーが役牌を切った時"); }
                else if (isMiddle) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーがド真ん中の牌を切った時"); }
                else if (isSameAsBefore) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーが前の捨て牌と同じ牌を切った時"); }
                else if (isSuji) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーがスジ牌を切った時"); }

                if (isHonor) _playerConsecutiveHonorCount++;
                else _playerConsecutiveHonorCount = 0;

                if (_playerConsecutiveHonorCount >= 3) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーが字牌を連続で切った時"); }
                
                _playerDiscardHistory.Add(tileId);
            }
            else
            {
                if (tData.IsRedDora) { playedSpecial = true; EnqueueCSVDialogue("敵が赤ドラを切る時"); }
                
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
