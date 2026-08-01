# マルチプレイ不具合 調査メモ（2026-07-31 → 08-01 修正）

調査時のコミット: `efee0f1` / ブランチ `UI16`
状態: **修正案(A)を適用済み（2026-08-01）・Play モードでの動作確認は未実施**

適用内容: `RoundLifecycleMessageHandler.cs` から `"round_start"` を削除（`Types` 配列と `case` の両方）。
`dotnet build Assembly-CSharp.csproj` は 0 エラー。未登録の type は
`NetworkMessageHandler.ProcessServerMessage:242` で黙って無視されるため、ログ汚染も起きない。

---

## 症状

第1局が終わって次の局が始まるとき、**「対局開始」が表示されて第1局がまた始まる。**

---

## 根本原因

サーバーからの `round_start`（次の局の開始）を、クライアントが**対局そのものの開始**として処理している。

`Assets/Scripts/Network/Handlers/RoundLifecycleMessageHandler.cs:31-33`

```csharp
case "round_start":
    network.RaiseGameStarted();   // ← 対局開始と同じイベントを投げている
    break;
```

`Assets/Scripts/Network/Handlers/MatchLifecycleMessageHandler.cs:34-35` の `game_started`
（本物の対局開始）も**まったく同じ `RaiseGameStarted()`** を呼ぶ。
つまり **「局の開始」と「対局の開始」が区別されていない。**

### 伝播経路

```
round_start
  → NetworkMessageHandler.RaiseGameStarted()      (Managers/NetworkMessageHandler.cs:156)
  → OnGameStarted イベント
  → GameUINetworkHandler.HandleGameStarted()      (UI/GameUINetworkHandler.cs:85)
  → GameUIPhaseController.OnGameStarted()         (UI/GameUIPhaseController.cs:95)
```

### `OnGameStarted()` は対局まるごとのリセット

| 行 | 処理 | 症状として見えるもの |
| :--- | :--- | :--- |
| `:97` | `_currentRoundIndex = 1` | **第1局に戻る** |
| `:120` | `PlayRoundStartDarken("対局開始")` | **「対局開始」が出る** |
| `:101` | `UpdateHp(20000, 20000)` | 体力が全快に戻る |
| `:102` | `BetPotUI.Clear()` | 場の血が消える |
| `:107-114` | `ResetHpMeter(20000)` / `SetHP(20000)` | HPメーターの分母も引き直し |
| `:125-128` | `ResetStateForNewGame()` / `HandleRoundStart(1)` | 敵リアクション状態も初期化 |
| `:145` | `PlayBGM(battleBgm)` | BGMが頭から鳴り直す |

> **報告された症状は「対局開始の表示」と「第1局に戻る」だけだが、
> 体力・場の血・BGMも同時に巻き戻っているはず。次回そこも確認すること。**

---

## いつ壊れたか

**2026-07-21 `c4fe0b8「webGL版の認証」**。当該ファイルの差分はこれだけ。

```diff
             "game_end"
+            "round_start"
...
+                case "round_start":
+                    network.RaiseGameStarted();
+                    break;
```

`_currentRoundIndex = 1` 側は 2026-06-07 `94a3dc8「透視能力」` からある正しいコード。
**7/21 に一方的に持ち込まれた回帰**であり、認証作業に紛れ込んだもので設計意図には見えない。

---

## 正しい次局処理は既に別にある

局の進行はクライアント側で完結している。`round_start` は何も担っていない。

| 場所 | 役割 |
| :--- | :--- |
| `GameUIPhaseController.HandleDraw()` → `:604` | 流局後に `_currentRoundIndex++` |
| `GameUIPhaseController.OnRonAnimationComplete()` → `:889` | 和了後に `_currentRoundIndex++` |
| `GameUIPhaseController.StartNextRoundTransitionForDealing()` → `:833` | `第{_currentRoundIndex}局...` を表示 |

---

## 修正案

### (A) `round_start` の処理を削除する ← 推奨・次回これをやる

`RoundLifecycleMessageHandler.cs` の `Types` 配列から `"round_start"` を外し、`case` を削除する。
7/21 以前に戻すだけ。上記のとおり進行は別経路が担っているので機能欠落は起きない。

### (B) `round_start` を正しく使う（後日）

`EngineData/ServerMessages.cs:110` に `RoundStartMessage { type, round }` が定義済みで、
**サーバーは局番号を送ってきているのにクライアントが捨てている。**
本来はサーバーが権威なので `_currentRoundIndex` をこの値へ同期させるのが正しい。

ただし新しいイベント（`OnRoundStarted`）の追加が必要で、演出のタイミングと競合する危険がある
（既知の注意点: 演出が先走る不具合は `GameUIPhaseController` を疑う）。**(A) と同時にやらないこと。**

---

## 副次的に疑っていたこと → **サーバーのコードで裏付け完了（2026-08-01）**

マッチ開始時に「対局開始」演出が**2回**走っていた。サーバー側の送信順がこうなっている:

```
game_session.py:243  "game_started" を broadcast
game_session.py:271  engine.start_game(1000)
  → game_engine.py:70  _start_round()
  → game_engine.py:75  on_round_start コールバック
  → game_session.py:978  "round_start" (round=1) を broadcast
```

つまり第1局でも必ず `round_start` が飛ぶので、`OnGameStarted()` は毎回2回実行されていた。
(A) でこちらも同時に解消される。

---

## 次回やること

1. ~~(A) を適用する~~ → **2026-08-01 適用済み**
2. ~~2クライアントで対局して第2局を確認~~ → **2026-08-01 実機で確認済み。第2局へ正しく進むようになった**
3. 通ったら、残りの不具合の切り分けに進む

### 実機検証の環境（再現手順）

サーバーは Render の本番（`wss://jongpire.onrender.com/ws`）。**トークン認証が必要**で、
無しだと WebSocket ハンドシェイクが **HTTP 403** で弾かれる（`wsgi.py` の `TOKEN`）。
Unity 側は `WebSocketGameClientSample.authToken` をシーンに直接シリアライズしており、
`Assets/Scenes/UIテストシーン.unity` の `authToken:` がその値。クエリ `?token=` で渡す。

対戦相手は使い捨ての自動ボットで通した（`mahjong_engine/` は読むだけで未変更）。
ボットは `dealing_completed` の `tenpai_examples` をそのまま手牌に使い、賭け金は最小200、
自分の手番で山牌を順に打つ。**ロンは必ず受けること**（このゲームに「辞退」は仕様として無い。
`accept:false` を送る実装にしたら対局が壊れた）。

ボット実装上の注意: 打牌を送った直後に `agari_pending` が届くと、サーバーが打牌だけを
「和了入力待ち中のため打牌できません」で弾く。打牌を投機的に消費済み扱いにすると
1手を借金したまま止まる（**サーバーに手番のタイムアウトは無いので自然復旧しない**）。
`discard_accepted` を受けるまで確定させないこと。

一方のクライアントが切断すると、サーバーは残ったプレイヤーを待機列へ戻して再マッチさせる
（`websocket_server.py:226`）。**Unity の Play を止めずにボットだけ繋ぎ直せる。**

---

# 演出タイミング系の洗い出し（2026-08-01・コード調査のみ／未修正）

ユーザーの申告カテゴリ＝**演出のタイミング**。`GameUIPhaseController` を中心に静的解析した結果。

## 共通の構造欠陥

**ネットワーク由来のイベントを「演出中（`IsTransitioning`）だから」と早期 return で捨てており、
あとで拾い直す仕組みが無い。** サーバーメッセージは再送されないので、演出と重なった瞬間に永久に失われる。

`IsTransitioning` を長時間 true にするのは主に:
- `GameUISkillController.HandleSkillCastedRoutine():160` … 能力演出。数秒。**打牌フェイズ中に走る**
- `TileMoveAnimator.PlayTransitionAnimationRoutine():233` … 0.2秒
- `GameUIPhaseController.TriggerBettingAnimationPhase():522` / `ExecuteDrawTransitionForDealing():668`

## 個別の指摘

| # | 深刻度 | 場所 | 内容 |
| :--- | :--- | :--- | :--- |
| 1 | **重大** | `GameUIPhaseController.HandleDraw()` | 流局処理の消失 → **進行停止**。**2026-08-01 修正済み** |
| 2 | **重大** | `GameUIPhaseController.TriggerBettingAnimationPhase()` | 賭け金演出の消失 → **Betting で停止**。**2026-08-01 修正済み** |
| 3 | 中 | `GameUIPhaseController.HandlePhaseVisibility()` | フェイズ演出が飛び復帰しない。**2026-08-01 修正済み** |
| 4 | ~~中~~ → **誤判定** | `GameUIPhaseController.cs:18,24,789` | デッドコード。**2026-08-01 削除済み** |
| 5 | 小 | `GameUIPhaseController.cs:546,560,581` | Discard の演出が1回の遷移で3回走る |

### 1. 流局処理の消失 → 進行停止

`HandleDraw()` の冒頭で `IsTransitioning` と `IsDarkenTransitioning` の二重の早期 return。
呼び出し元は `OnDraw` イベント（`GameUINetworkHandler.cs:157`）**のみ**で再試行が無い。
落ちると `_currentRoundIndex++`・流局ダイアログ・`SendNextRoundAction()` が全部飛ぶ。
サーバーは `next_round` 承認を待ち続けるので**そのままハングする**。

流局は「最後の打牌の直後」に届くため、打牌アニメや能力演出と**構造的に重なりやすい**。

### 2. 賭け金演出の消失 → Betting で停止

`TriggerBettingAnimationPhase():518` の早期 return。
このメソッドの `onMidpoint` が `UpdatePhaseStatus(RoundStatus.Discard):546` を呼んでいるため、
落ちると**打牌フェイズへ進めず Betting のまま固まる**。演出だけでなく進行の責務を負っている。

### 3. フェイズ演出が丸ごと飛ぶ

`HandlePhaseVisibility():212` の早期 return。
`UpdatePhaseStatus()` は `:176` で先に status を確定させてから `:207` で呼ぶので、
**status だけ進んで演出が飛ぶ**。同じ status の再送は `:164` の同値ガードで弾かれ、二度と復帰しない。

### 4. ロン進行フォールバック → **不具合ではなくデッドコードだった（2026-08-01 訂正・削除済み）**

当初「代入が消えた回帰」と判断したが**誤り**。`39ea9d9「debugコードとロン後の演出」`は
意図的な設計変更で、削り残しが放置されていただけだった。

- **旧設計**: 敵ロン時は `_waitingForOpponentRonAnimation = true` にして
  「相手がロンボタンを押す（`next_round_waiting`）」のを**待ってから**演出を再生していた。
  この待ちがハングする保険として `Update()` の5秒フォールバックがあった
- **新設計**: `HandleAgari():783` が `PlayRonWithPreDialogue` を**即座に**開始する。待ちが無いので保険も不要

したがって代入を復活させるのは**誤り**で、即時再生と衝突して演出が二重に走る。
正しい対処は削除。対局終了の実処理は `GameUIManager.cs:233` の別サブスクライバが持っているため、
機能欠落は無い。

**削除したもの:**
- `GameUIPhaseController`: `_waitingForOpponentRonAnimation` / `_fallbackTimer` / `Update()` / `HandleGameEnded()`
- `GameUINetworkHandler`: `OnGameEnded` の購読・解除と、空になった `HandleGameEnded(int,int)`

`dotnet build` 0エラー、Unity 側もコンパイル警告なし。

### 5. Discard の演出が3回走る

賭け金演出の midpoint で `UpdatePhaseStatus(Discard):546` → 内部で `HandlePhaseVisibility`、
直後に `:560` でもう一度、`onComplete` の `:581` でさらに一度。
`PlayerInfoUI.StartTurnTimer(10f)` が3回張り直される。

## 1〜3 の修正（2026-08-01 実装済み・**実機で発火を確認**）

**検証結果:** 実対局中に Unity コンソールへ以下が出た。強制実行の警告は出ていない
（＝タイムアウト前に正常にフラッシュされた）。

```
[GameUIManager] 演出中のため 'phaseVisibility:Dealing' を保留しました。演出完了後に実行します。
[GameUIManager] 演出中のため 'phaseVisibility:Discard' を保留しました。演出完了後に実行します。
```

**修正前はこの2件が黙って捨てられていた。** 3 が机上の空論ではなく実在の不具合だったことの裏付け。
とくに `Dealing` が保留対象になっている点が重要で、キーを `phaseVisibility` 単一にして畳んでいたら
`Dealing` の本体（次局の暗転開始とフラグのリセット）が消えて**次局が始まらなくなっていた**。


原因が共通なので個別に潰さず、**演出中に届いたイベントを保留して演出明けに実行する仕組み**を
`GameUIManager` に入れ、3箇所の早期 return を差し替えた。

### 追加した API（`GameUIManager`）

| メンバ | 役割 |
| :--- | :--- |
| `IsBusyWithTransition` | `isTransitioning` と `PhaseTransitionUI.IsDarkenTransitioning` を合わせた「演出中」判定 |
| `DeferUntilIdle(key, action)` | 演出明けまで処理を保留する。同じ key は元の位置で後勝ち上書き |
| `FlushDeferredActionsRoutine()` | 演出明けを待って到着順に実行するコルーチン |

### 設計上の注意（触るときはここを壊さないこと）

- **`SetIsTransitioning(false)` での同期フラッシュにはしていない。**
  `TriggerBettingAnimationPhase` は onMidpoint で一瞬 false に戻してから true に戻す（`:536`→`:562`）ため、
  同期フラッシュだと演出の途中で保留処理が走ってしまう。必ず1フレーム待ってから判定する。
- **`phaseVisibility` のキーには status を含めてある。** フェイズごとに本体の処理が違い冪等でもない。
  1つのキーで畳むと `Dealing`（`_hasShownHandSelectionPrompt` / `_hasExecutedRonAnimation` のリセットと
  次局の暗転開始を担う）が `HandSelection` に上書きされて消え、**次局が始まらなくなる**。
- **8秒のタイムアウトで強制実行する。** 演出フラグが立ちっぱなしになると保留が永久に実行されず、
  取りこぼしと同じ進行停止になるため。強制時は `ignoreBusyForForcedFlush` でガードを一時的に無効化する。
  これが無いと各処理が冒頭で再び「演出中」と判定して保留し直し、**警告だけ出して永久に実行されない**。
- フラッシュ中に先行処理が新しい演出を始めた場合、後続は冒頭の判定で自然に再保留される（通常時のみ）。

### 残り

5（Discard の演出が3回走る）は未着手。

---

# 見た目の3件（2026-08-01・実機で確認して修正済み）

シーンの変更4行のみ。**Play モード中の変更は破棄されるので、必ず Play を抜けてから触ること。**

| 対象 | 変更 |
| :--- | :--- |
| `Canvas/EnemyHandCanvas/EnemyHandPanel` | `anchoredPosition` (2,-50) → **(2,-145)** |
| `マージャン卓（仮）/DoraCyberEffect_MCP` | `localPosition` (-0.2,0.42,-0.1) → **(-0.4092,2.3919,-0.0721)** |
| `Canvas/役Canvas/常時役一覧` | `localScale` (0.7,0.7,1) → **(1,1,1)** |
| 同上 | `anchoredPosition` (260,200) → **(215,200)** |

## 画面座標の基準値（800x600・Canvas は scaleFactor=1 なので単位＝px）

| 要素 | 画面 Y |
| :--- | :--- |
| 卓の緑（フェルト） | 55..210 |
| 自分の山牌（手牌選択） | 65..155 |
| 自分の山牌（打牌） | 0..90 |
| 自分の手牌 | 93..178 |
| `YOUR TURN` | 400..450 |

### 1. 敵の「山牌」が浮く → **実体は敵の手牌だった**

**`Canvas/EnemyWallUI` は `activeInHierarchy=False` で一度も表示されていない。**
`GameUIPhaseController.HandlePhaseVisibility` が全経路で `SetActive(false)` している。
浮いていたのは `EnemyHandCanvas/EnemyHandPanel/HandSlotContainer` の**敵の手牌**。
牌が Y 255..295（卓の奥端 210 より上＝空中）にあった。`anchoredPosition.y` を -145 にして Y 160..200 へ。

> **`EnemyWallUI` と `EnemyWallUI.cs` は触っても画面が変わらない。** 調査で一度そこを直して空振りした。
> `EnemyWallUI.cs:55` に `startPosition.y` を無視する `actualStartY = 150f` の直書きが残っているが、
> 表示されないので実害なし（直す場合は表示するかどうかから決めること）。

### 2. ドラが自分の山牌と被る → 卓外へ移動

ドラは `マージャン卓（仮）` 配下の **WorldSpace の3Dオブジェクト**で、山牌は ScreenSpaceOverlay の UI。
座標系が別なので静的な測定では重なりを再現できなかった。
ユーザーの指定により**卓の外（`YOUR TURN` の下）へ独立配置**した。
移動は `localPosition` を直接指定。画面位置から決めたいときは
`Camera.main.ScreenToWorldPoint(new Vector3(sx, sy, 現在のdepth))` で world を求めるのが早い。

> `DoraFloatAnimator.floatAmplitude` はコードの既定値が `0.5` だが**シーンで 0.08 に上書きされている**。
> 既定値を見て「±63px 揺れる」と誤判断した。**シリアライズ値を必ず確認すること。**

### 3. 役一覧の文字が潰れる

`常時役一覧` の `localScale` が **(0.7, 0.7, 1.0)** と非等倍かつ 0.7倍の縮小で、
fontSize 20 が実効 14px になっていた。等倍にして **20px**（PixelMplus10 の原寸10pxの2倍）へ。
板ごと 1.43倍になるので、右へのはみ出し分だけ `anchoredPosition` を X-45 して補正した。

- `役Canvas` の `localScale=0.0019` は **ScreenSpaceCamera で Unity が自動設定する正常値**。触らないこと
- フォントは SDF（`pointSize=90` / SDFAA）。**これ以上くっきりさせるには板ごと拡大するかフォント資産を変えるしかない**

---

## 未調査

- 演出タイミング 5（Discard の `HandlePhaseVisibility` が1遷移で3回走る）は未着手
- `HandleDraw`（演出タイミング 1）は**流局に到達していないため未検証**
- サーバー側（`mahjong_engine/` と `.py`）は他の人の担当のため触っていない。
  実際にどのメッセージがどの順で飛んでいるかはログを見て確認する必要がある
