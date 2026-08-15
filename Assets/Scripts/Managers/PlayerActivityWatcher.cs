using UnityEngine;
using UnityEngine.InputSystem;
using KillingMahjong.UI;
using KillingMahjong.Managers.Reactions;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// プレイヤーの操作を見張って `Meta_*` と `Tile_*` のリアクションを鳴らす。
    ///
    /// **これらは「何回・何秒」でしか判定できない**ので、盤面の処理からは出せない。
    /// 賭けや打牌のように結果が1つ届く出来事と違い、放置・連打・迷いは
    /// 時間の経過そのものが条件になる。それを盤面側に書くと本筋が読めなくなるので分けた。
    ///
    /// **シーンには置かない。** 対局シーンが2つある（`UIテストシーン` と `OpeningScene`）ため、
    /// 片方だけに置く事故が起きる。`ReactionController` が実行時に作る。
    ///
    /// ここから鳴らすのは**すべて `Ambient`**。`ReactionController` の作りにより、
    /// 演出中とキューに何か入っている間は無条件に捨てられ、全体クールダウンもかかる。
    /// つまり「連打してうるさい」状態にはならない。
    /// </summary>
    public class PlayerActivityWatcher : MonoBehaviour
    {
        public static PlayerActivityWatcher Instance { get; private set; }

        // --- 放置 ---
        private const float Idle1Seconds = 20f;
        private const float Idle2Seconds = 60f;
        /// <summary>これ以下のマウス移動は「動かしていない」とみなす（手ぶれで放置が解けないように）</summary>
        private const float MouseMoveEpsilon = 2f;

        // --- 連打・迷い。いずれも Window 秒のあいだに Count 回 ---
        private const float SpamWindow = 5f;
        private const int ScreenSpamCount = 6;
        private const int CharacterSpamCount = 5;
        private const int TileSpamCount = 8;
        private const int WallPokeCount = 3;
        private const float HoverWindow = 6f;
        private const int HoverHesitationCount = 10;
        private const int PeekCount = 3;

        // --- 打牌の速さ ---
        private const float InstantDiscardSeconds = 1.5f;
        /// <summary>打牌の持ち時間は 10 秒（GameUIManager:839）。切れる前に急かす</summary>
        private const float ThinkTimeoutSeconds = 7.5f;

        // --- ウィンドウ復帰 ---
        /// <summary>これより短く離れただけなら「浮気」扱いしない</summary>
        private const float RefocusAwaySeconds = 5f;

        private float _lastInputAt;
        private bool _idle1Fired, _idle2Fired;

        // 「誰も拾わなかったクリック」を数えるための1フレームぶんの記録。
        // Update の実行順はコンポーネント間で保証されないので、LateUpdate で判定する
        private bool _clickThisFrame;
        private bool _clickClaimed;
        private bool _clickWasOverUI;

        private readonly Counter _screenClicks = new Counter();
        private readonly Counter _characterClicks = new Counter();
        private readonly Counter _tileClicks = new Counter();
        private readonly Counter _wallPokes = new Counter();
        private readonly Counter _hovers = new Counter();
        private readonly Counter _peeks = new Counter();

        private float _turnStartedAt = -1f;
        private bool _thinkTimeoutFired;
        private bool _wasLocalTurn;

        /// <summary>直近 SpamWindow 秒に女の子を押した回数。ルールの条件で使う</summary>
        private int _clickStreak;
        private float _clickStreakStartedAt = -999f;

        private bool _wasMuted;
        private float _lostFocusAt = -1f;

        /// <summary>直近 window 秒に何回起きたかだけを数える。履歴は要らないので個数と先頭時刻で足りる</summary>
        private class Counter
        {
            private float _windowStartedAt = -999f;
            private int _count;

            /// <summary>1回数えて、window 秒以内に threshold 回に達したら true（達した瞬間だけ）</summary>
            public bool Hit(float window, int threshold)
            {
                float now = Time.unscaledTime;
                if (now - _windowStartedAt > window)
                {
                    _windowStartedAt = now;
                    _count = 0;
                }
                _count++;
                if (_count == threshold)
                {
                    // 数え直す。連打を続けているあいだ threshold ごとに1回だけ鳴る
                    _windowStartedAt = now;
                    _count = 0;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                _windowStartedAt = -999f;
                _count = 0;
            }
        }

        /// <summary>`ReactionController` から作られる。シーンには置かない</summary>
        public static PlayerActivityWatcher Create(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PlayerActivityWatcher");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<PlayerActivityWatcher>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _lastInputAt = Time.unscaledTime;
        }

        private void OnDestroy()
        {
            // シーン再読込時に破棄済みオブジェクトを指したままにしない
            if (Instance == this) Instance = null;
        }

        /// <summary>局が変わったら数え直す。前の局の連打を持ち越さない</summary>
        public void ResetForNewRound()
        {
            _screenClicks.Reset();
            _characterClicks.Reset();
            _tileClicks.Reset();
            _wallPokes.Reset();
            _hovers.Reset();
            _peeks.Reset();
            _turnStartedAt = -1f;
            _thinkTimeoutFired = false;
            _wasLocalTurn = false;
            _clickStreak = 0;
            _clickStreakStartedAt = -999f;
            _lastInputAt = Time.unscaledTime;
            _idle1Fired = false;
            _idle2Fired = false;
        }

        // ------------------------------------------------------------------
        // 外から呼ばれる通知
        // ------------------------------------------------------------------

        /// <summary>このフレームのクリックは自分が処理した、という申告。画面の余白連打と区別するため</summary>
        public void ClaimClick()
        {
            _clickClaimed = true;
        }

        /// <summary>
        /// 女の子をクリックした。連打しているなら `Meta_ClickSpam` を出す。
        /// **戻り値が true のときは、呼び出し側は自前のセリフを出さないこと**（二重に喋る）。
        /// </summary>
        public bool NotifyCharacterClick(string areaName)
        {
            ClaimClick();
            MarkInput();

            var rc = ReactionController.Instance;
            if (rc == null) return false;

            // 連打の回数は窓を切って数え直す。1時間ぶんが積み上がると条件が意味を失う
            float now = Time.unscaledTime;
            if (now - _clickStreakStartedAt > SpamWindow) { _clickStreakStartedAt = now; _clickStreak = 0; }
            _clickStreak++;

            bool spam = _characterClicks.Hit(SpamWindow, CharacterSpamCount);

            // プランナーのルールを先に見る。連打かどうかは「短時間に押した回数」で書ける
            if (rc.Publish(ReactionEvent.CharacterClick, new ReactionContext().WithCommon()
                    .Set(ReactionVars.ClickArea, areaName ?? "")
                    .Set(ReactionVars.ClickStreak, _clickStreak)))
            {
                return true;
            }

            return spam && rc.Trigger(ReactionTrigger.Meta_ClickSpam, ReactionPriority.Ambient);
        }

        /// <summary>
        /// 部位のセリフが CSV に無かったときの受け皿。
        ///
        /// **CSV を先に引くのは、そちらの方が本数が多いから。** 部位ごとに CSV が5本、
        /// トリガーは3本しかない。抽選は1〜20なので、部位専用が当たるのは 25%。
        /// 残りをここで拾って、全体のセリフへ落ちる前に部位の反応を出す。
        /// </summary>
        public bool TryPartReaction(string areaName)
        {
            var rc = ReactionController.Instance;
            if (rc == null) return false;

            if (areaName == "Head") return rc.Trigger(ReactionTrigger.Meta_ClickHead, ReactionPriority.Ambient);
            if (areaName == "Chest") return rc.Trigger(ReactionTrigger.Meta_ClickChest, ReactionPriority.Ambient);
            return false;
        }

        /// <summary>牌をクリックした（実際に動く操作だったとき）</summary>
        public void NotifyTileClick()
        {
            ClaimClick();
            MarkInput();

            if (_tileClicks.Hit(SpamWindow, TileSpamCount))
            {
                TriggerAmbient(ReactionTrigger.Tile_SpamClick, "牌連打", 0f, TileSpamCount);
            }
        }

        /// <summary>牌をクリックしたが何も起きなかった（＝つついただけ）</summary>
        public void NotifyWallPoke()
        {
            ClaimClick();
            MarkInput();

            if (_wallPokes.Hit(SpamWindow, WallPokeCount))
            {
                TriggerAmbient(ReactionTrigger.Tile_WallPoke, "牌つつき", 0f, WallPokeCount);
            }
        }

        /// <summary>牌の上にカーソルが乗った。行ったり来たりを見る</summary>
        public void NotifyTileHover()
        {
            if (_hovers.Hit(HoverWindow, HoverHesitationCount))
            {
                TriggerAmbient(ReactionTrigger.Tile_HoverHesitation, "牌の上で迷う", 0f, HoverHesitationCount);
            }
        }

        /// <summary>「手牌を見る」を押した</summary>
        public void NotifyPeek()
        {
            ClaimClick();
            MarkInput();

            if (_peeks.Hit(SpamWindow * 4f, PeekCount))
            {
                TriggerAmbient(ReactionTrigger.Tile_PeekHold, "手牌を覗く", 0f, PeekCount);
            }
        }

        /// <summary>自分が牌を切った。ターンが来てからの速さを見る</summary>
        public void NotifyLocalDiscard()
        {
            MarkInput();

            if (_turnStartedAt >= 0f && Time.unscaledTime - _turnStartedAt < InstantDiscardSeconds)
            {
                TriggerAmbient(ReactionTrigger.Tile_InstantDiscard, "即切り", TurnElapsedSeconds);
            }
            _turnStartedAt = -1f;
            _thinkTimeoutFired = false;
        }

        // ------------------------------------------------------------------
        // 毎フレームの監視
        // ------------------------------------------------------------------

        /// <summary>
        /// 自分の手番になった瞬間を拾う。
        ///
        /// **`BoardStateManager.OnTurnChanged` は購読していない。** この監視役は実行時に生成され、
        /// `BoardStateManager` より先に立つことも後になることもある。購読し損ねても気づけないので、
        /// 毎フレーム見て自分で差分を取る方が確実（1フレームに bool 1つの比較しかしない）。
        /// </summary>
        private void DetectTurnChange()
        {
            var board = BoardStateManager.Instance;
            bool isLocalTurn = board != null && board.IsLocalTurn;
            if (isLocalTurn == _wasLocalTurn) return;

            _wasLocalTurn = isLocalTurn;
            _turnStartedAt = isLocalTurn ? Time.unscaledTime : -1f;
            _thinkTimeoutFired = false;
        }

        private void Update()
        {
            DetectTurnChange();
            DetectInput();
            DetectIdle();
            DetectThinkTimeout();
            DetectMute();
        }

        private void LateUpdate()
        {
            // Update の実行順に関わらず、全員の Update が終わってから判定する。
            // 誰も名乗り出ず、UI の上でもなかったクリック＝画面の余白を叩いた。
            //
            // **UI を除くのを忘れると、賭け金の＋ボタン連打が「余白の連打」になる。**
            // ボタン類はいちいち ClaimClick を呼ばないので、ここでまとめて弾く
            if (_clickThisFrame && !_clickClaimed && !_clickWasOverUI)
            {
                if (_screenClicks.Hit(SpamWindow, ScreenSpamCount))
                {
                    TriggerAmbient(ReactionTrigger.Meta_ScreenClickSpam, "画面連打", 0f, ScreenSpamCount);
                }
            }
            _clickThisFrame = false;
            _clickClaimed = false;
            _clickWasOverUI = false;
        }

        private void DetectInput()
        {
            bool moved = false;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                {
                    _clickThisFrame = true;
                    // 押した瞬間に見ておく。LateUpdate まで待つと、
                    // その間にパネルが開閉して結果が変わることがある
                    _clickWasOverUI = IsPointerOverUI();
                    moved = true;
                }
                if (mouse.delta.ReadValue().sqrMagnitude > MouseMoveEpsilon * MouseMoveEpsilon) moved = true;
                if (mouse.scroll.ReadValue().sqrMagnitude > 0f) moved = true;
            }

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                _clickThisFrame = true;
                _clickWasOverUI = IsPointerOverUI();
                moved = true;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) moved = true;

            if (moved) MarkInput();
        }

        private void MarkInput()
        {
            _lastInputAt = Time.unscaledTime;
            _idle1Fired = false;
            _idle2Fired = false;
        }

        private void DetectIdle()
        {
            float idle = Time.unscaledTime - _lastInputAt;

            // 長い方から見る。60 秒に達したときに 20 秒の方を出さない
            if (!_idle2Fired && idle >= Idle2Seconds)
            {
                _idle2Fired = true;
                _idle1Fired = true;
                TriggerAmbient(ReactionTrigger.Meta_Idle60s, "放置", idle);
                return;
            }
            if (!_idle1Fired && idle >= Idle1Seconds)
            {
                _idle1Fired = true;
                TriggerAmbient(ReactionTrigger.Meta_Idle20s, "放置", idle);
            }
        }

        private void DetectThinkTimeout()
        {
            if (_thinkTimeoutFired || _turnStartedAt < 0f) return;
            if (Time.unscaledTime - _turnStartedAt < ThinkTimeoutSeconds) return;

            _thinkTimeoutFired = true;
            TriggerAmbient(ReactionTrigger.Tile_ThinkTimeout, "長考", TurnElapsedSeconds);
        }

        private void DetectMute()
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;

            // BGM も SE も鳴らない状態だけを「消した」とみなす。
            // マスターだけ見ると、片方だけ絞っている人にも出てしまう
            bool muted = audio.masterVolume <= 0.001f
                || (audio.bgmVolume <= 0.001f && audio.seVolume <= 0.001f);

            if (muted && !_wasMuted) TriggerAmbient(ReactionTrigger.Meta_MuteAudio, "ミュート");
            _wasMuted = muted;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                _lostFocusAt = Time.unscaledTime;
                return;
            }

            // 戻ってきた瞬間は「操作した」とみなす。放置と二重に鳴らさない
            MarkInput();

            if (_lostFocusAt < 0f) return;
            float away = Time.unscaledTime - _lostFocusAt;
            _lostFocusAt = -1f;
            if (away < RefocusAwaySeconds) return;

            TriggerAmbient(ReactionTrigger.Meta_WindowRefocus, "ウィンドウ復帰", away);
        }

        /// <summary>ボタンやパネルの上を押しているか。EventSystem が無い場面では false</summary>
        private static bool IsPointerOverUI()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        /// <summary>自分の手番が来てからの秒数。まだ手番でなければ 0</summary>
        public float TurnElapsedSeconds
        {
            get { return _turnStartedAt < 0f ? 0f : Time.unscaledTime - _turnStartedAt; }
        }

        /// <summary>
        /// プランナーのルールを先に試し、当たらなければ従来のトリガーへ落とす。
        /// **`Meta_*` / `Tile_*` はすべてここを通る。**
        /// </summary>
        private static void TriggerAmbient(ReactionTrigger trigger, string kind,
                                           float seconds = 0f, int count = 0)
        {
            var rc = ReactionController.Instance;
            if (rc == null) return;

            if (!string.IsNullOrEmpty(kind))
            {
                var ctx = new ReactionContext().WithCommon()
                    .Set(ReactionVars.ActivityKind, kind)
                    .Set(ReactionVars.ActivitySeconds, seconds)
                    .Set(ReactionVars.ActivityCount, count);
                if (rc.Publish(ReactionEvent.PlayerActivity, ctx)) return;
            }

            rc.Trigger(trigger, ReactionPriority.Ambient);
        }
    }
}
