# チュートリアル関連の変更まとめ（2026-07-28 / Claude Code 側）

Unity側のチュートリアル（OpeningScene）に対して行った変更。
別セッションで行われた音量・フォント・ボイス関連の変更とは範囲が重複していない。

---

## 全体を貫く一つのパターン

今回の不具合の大半は同じ原因だった。

**`GameUIPhaseController.HandlePhaseVisibility()` が、フェイズを切り替えた瞬間に演出まで実行してしまう。**

チュートリアルは `TutorialManager` が「セリフ → 演出 → 入力待ち」の順番を持っているのに、
`SetPhase()` を呼んだ時点で演出が先に走るため、順番が壊れていた。

対処は一貫して「該当ケースを `if (!uiManager.IsTutorialMode)` で囲み、
チュートリアル側に順番を持たせる」。既存の `HandSelection` / `Discard` ケースと同じ書き方に揃えた。

適用箇所: `Betting` / `Agari・Ron・Result`

---

## 1. ドラ表示牌と「準備完了」がチュートリアルに出る

チュートリアルの説明に入らないUIが表示されていた。

| ファイル | 変更 |
|---|---|
| `UI/GameUIPhaseController.cs` | `UpdateDoraDisplay()` にチュートリアルガード。フェイズが変わるたびドラが復活するのを防ぐ |
| `Managers/TutorialManager.cs` | `SetBoardVisible()` のドラ表示ロジックを削除し常に `Hide()` |
| `Managers/TutorialManager.cs` | `ApplyHpToUI()` で `ShowReadyBox(false)` を両者に |
| `UI/DoraDisplayUI.cs` | `doraLabelObject` を追加。卓上の「ドラ」ラベル（`DoraText`）が誰からも制御されていなかったため、表示切替に連動させた |

「準備完了」は `PlayerInfoUI / EenemyInfoUI` 配下の `ReadyBoxContainer`。
シーン上で `m_IsActive: 1` なので、実行時に消している（シーンの値は変更していない）。

---

## 2. 手牌選択のボタンを3段階で開放

「13枚選んでいる最中にAutoとDecideが両方見えている」状態を変更。

```
Hidden        : 13枚選んでいる最中     … Auto 非表示 / Decide 非表示
AutoOnly      : 13枚そろってセリフの後 … Auto 表示   / Decide 非表示
AutoAndDecide : Auto を押した後        … Auto 表示   / Decide 表示
```

| ファイル | 変更 |
|---|---|
| `Managers/TutorialManager.cs` | `HandButtonStage` enum、`IsAutoButtonVisible` / `IsDecideButtonVisible`、`SetHandButtonStage()` |
| `UI/HandUI.cs` | `UpdateLayout()` がこの段階を見てボタンを出し分ける。チュートリアル以外は従来通り |
| `Managers/Tutorial/TutorialScenario.cs` | `onHandFilledLines` を追加（13枚そろった時のセリフ。空なら既定文） |

**副作用**: `rejectFirstConfirm`（最初の決定を弾く手順②）が手動選択の局では到達不能になった。
Auto を押すまで決定ボタンが出ないため。セリフは第1局の `onHandFilledLines` に移してある。

---

## 3. チュートリアルマスクの作り直し（シーン変更あり）

矢印がマスクの裏に描画されていた。`TutorialArrowUI` の Canvas が `sortingOrder = 50`、
`TutorialMaskUI` が `900`。矢印はボタンの50px上に出るため穴の外にはみ出し、70%黒に沈んでいた。
実測で赤の平均値 128 → 214（sortingOrderを上げた場合）。

| ファイル | 変更 |
|---|---|
| `Common/UISortingOrders.cs` | `TutorialMask = 900` / `TutorialArrow = 910` を追加（マジックナンバーを定数化） |
| `UI/TutorialArrowUI.cs` | `sortingOrder = 50` → `UISortingOrders.TutorialArrow` |
| `UI/TutorialMaskGraphic.cs` | **新規**。`MaskableGraphic` を継承し、角丸＋ふちぼかしを1枚のメッシュで描く |
| `UI/TutorialMaskUI.cs` | 全面書き換え。4枚パネルの操作をやめ、上記グラフィックに穴の矩形を渡すだけに |

`TutorialMaskGraphic` の構成:
1. 穴の外側を埋める4枚の矩形
2. 外側矩形の角と円弧の間を埋めるくさび4つ
3. 穴のふちのリング（内側=アルファ0 / 外側=アルファ1）でぼかし

シェーダーも画像アセットも使っていない。頂点カラーの勾配だけでぼかしを出している。
穴の中はクリックを通す必要があるため `ICanvasRaycastFilter.IsRaycastLocationValid()` を実装。

**シーン変更（保存済み）**:
```
変更前: Tutorial/TutorialMaskUI / TopPanel, BottomPanel, LeftPanel, RightPanel
変更後: Tutorial/TutorialMaskUI / MaskGraphic
```
旧4パネルは `Undo.DestroyObjectImmediate` で削除（Ctrl+Zで戻せる）。
バックアップ: `%TEMP%\OpeningScene.20260728-205810.unity.bak`

**ハマりどころ**: `Graphic` の `[RequireComponent(typeof(CanvasRenderer))]` は、
派生クラスを実行時に `AddComponent` したときには効かない。明示的に付ける必要がある。

Inspector値: `padding: 10 / cornerRadius: 16 / edgeSoftness: 14`

---

## 4. 待ち牌が3つのとき枠からはみ出す

`WaitUI.RenderPage` が牌の幅を `tileWidth = 18f` で決め打ちしていたが、実際のプレハブは **45x40**。
枠の幅をこの18から計算していたため、3枚のとき内容119に対して枠は62しかなかった。

| ファイル | 変更 |
|---|---|
| `UI/WaitUI.cs` | 牌の実寸をプレハブから取得。`framePadding` を追加 |
| `UI/WaitUI.cs` | 収め方は「まず重なりを詰める → それでも無理なら縮小」。牌はドット絵なので等倍を優先 |
| `UI/WaitUI.cs` | コンテナ幅＝中身の幅にして枠の中央へ。`offsetX = -8 / -16` の決め打ちを廃止 |
| `UI/WaitUI.cs` | `ClearRenderedTiles()` を追加 |

**`Destroy` の遅延**: `Destroy` はフレーム末まで実行されないため、同じフレームで作り直すと
古い牌がレイアウトに残る。検証中に1枚表示のはずが4枚並んだ。破棄前に親から切り離すようにした。

検証結果（枠は120幅、`[-60..60]`）:
```
1枚: [-22.5 .. 22.5]
2枚: [-43.5 .. 43.5]
3枚: [-56.0 .. 56.0]   ← 牌は45pxのまま等倍を維持。spacing が -8 → -11.5 に詰まるだけ
```

---

## 5. 賭け金フェイズでスマホがセリフより先に拡大する

`SetPhase(RoundStatus.Betting)` の時点で `ZoomInRoutine` と `StartBettingPhase` が走っていた。

| ファイル | 変更 |
|---|---|
| `UI/GameUIPhaseController.cs` | `Betting` ケースの拡大とベット開始を `!IsTutorialMode` で囲む |
| `Managers/TutorialManager.cs` | `RunBettingPhase` を ①セリフ ②拡大 ③固定額UI＋confirm誘導 ④拡大を戻す の順に |

副次的に、通常対局用の**10秒ターンタイマー**がチュートリアルで走らなくなった
（`StartBettingPhase` の中で `StartTurnTimer(10f)` を呼んでいた）。

**拡大の戻し漏れも修正**: `ShowFixedBettingPhase` が確定コールバックを差し替えるため
`OnBetConfirmed`（拡大を戻す処理）が呼ばれず、次フェイズまで拡大したままだった。

賭け金は `ShowFixedBettingPhase` が増減・全賭けボタンを `interactable = false` にするので
1000から動かせない。全5局とも `betAmount = 1000`。

---

## 6. 第2局（流局の説明）を17打に

```
第2局開始 → 「しばらく黙って見ていなさい。勝手に打ち進めるわ。」
  → 自動で15手（自分・相手とも自動）
  → 「お互い17牌捨てたら流局して、次の局に移るわ。」
     「あと2回よ。好きな牌を捨ててみなさい。」
  → プレイヤーが手動で2打
  → 計17牌ずつで流局 → 第3局へ
```

| ファイル | 変更 |
|---|---|
| `Managers/Tutorial/TutorialScenario.cs` | `autoDiscardTurns` / `beforeManualDiscardLines` を追加 |
| `Managers/Tutorial/TutorialScenario.cs` | 第2局を17手・自動15に。捨て牌は筒子1〜9＋索子1〜8（待ち 7m/8m/9m を含まない） |
| `Managers/TutorialManager.cs` | `RunBattle` に自動手番の分岐。`autoDiscardInterval`（0.14秒）で回す |
| `Managers/TutorialManager.cs` | `AutoDiscardForPlayer()` を追加 |

チュートリアルの打牌は**手牌ではなく山牌から**捨てる仕組みなので、
`GameUIManager.DiscardSelectedTile` のチュートリアル分岐と同じ経路（山→河）を使っている。

**制約**: 山牌は34枚から手牌13枚を引いて21枚。17打すると4枚余る。
**18手以上に増やすと山が足りなくなる**ので、増やす場合は配牌側の調整が必要。

他の局は `autoDiscardTurns = 0` のままなので影響なし。

---

## 7. ロンボタンを押す前にロンが実行される

**原因**: `BoardStateManager.LastIsLocalWin` は宣言時の初期値が `true` で、
更新されるのはサーバー通信のハンドラ（`DiscardMessageHandler` / `RoundLifecycleMessageHandler`）だけ。
チュートリアルはサーバーに繋がないので常に `true` のまま。

`RunPlayerRon` の先頭で `SetPhase(RoundStatus.Agari)` を呼ぶと、
`HandlePhaseVisibility` の `Agari` ケースがこれを見て `ExecuteRonAction()` を実行していた。
ロンボタン（`AgariSelectionUI`）を出して待つのはその後。

| ファイル | 変更 |
|---|---|
| `UI/GameUIPhaseController.cs` | `Agari / Ron / Result` ケースに `!IsTutorialMode` ガードを追加 |

**副次的に直ったもの**: 第3局（敵のロン）も同じ経路を通っており、
`LastIsLocalWin` が `true` のままなので「敵のロンなのに自分の勝ちとしてロン演出が走る」状態だった。
第5局のロンも同様。

---

## 8. ダメージ・獲得ポイントが自分の分も相手側に出る

`PlayerInfoUI` と `EnemyInfoUI` は **どちらも 800x600 の全画面ルートCanvas**。
ポップアップはその `transform` を親に `anchoredPosition (0, 50)` で生成していたため、
自分も相手も画面中央（400, 350）に出ていた。

コード自体は左右対称で `isLocalPlayer` も正しく渡っていた。
「基準にしていた transform がパネルではなく全画面Canvasだった」という一点だけの問題。

| | 実際のパネル位置 | 修正前 | 修正後 |
|---|---|---|---|
| 自分（HPPanel＝スマホ） | (722, 241) | (400, 350) | (720, 306) |
| 相手（EnemyPanel＝血袋） | (273, 395) | (400, 350) | (270, 456) |

| ファイル | 変更 |
|---|---|
| `UI/HpPopupPresenter.cs` | コンストラクタを `(host, canvasRoot, anchor, prefab, offset, isLocalPlayer)` に。`anchor` の中心をCanvasローカルへ変換して出す |
| `UI/HpPopupPresenter.cs` | 生成時にアンカーを中央固定してから座標を入れる（プレハブ側の設定に左右されないため） |
| `UI/PlayerInfoUI.cs` | `damagePopupAnchor` を追加。未設定なら `zoomTarget`（HPPanel） |
| `UI/EnemyInfoUI.cs` | `damagePopupAnchor` を追加。未設定なら `zoomTarget` → `enemyPanel` |

親はCanvasルートのままにした。パネルの子にすると賭け金フェイズのスマホ拡大（4.5倍）で
ポップアップまで拡大され、パネル非表示時には消えてしまうため。

---

## 検証していないこと

- **第1局を手でロンまで通していない**。修正箇所はピンポイントで検証済み
  （チュートリアル時 `ronExecuted=False` / 通常時 `ronExecuted=True`）だが、
  実際にロンボタンを押す流れは未確認。
- **通常対局側の挙動は実測していない**。コード上は同じ呼び出しをガードで囲んだだけ。
- 自動操作の検証中に **第1局で13枚選んで『自動』を押した後、山牌の見た目が空になる**
  （内部状態は21枚のまま、`GetWallSlots()` が 0）挙動を2回再現した。
  ただしテスト操作側でも状態破壊が起きていたため、実プレイで起きるかは未確認。

## シーンの状態

`OpeningScene.unity` は **3.の変更のみ保存済み**。他の変更はすべてコード側のみ。

---

# 今後やったほうがいいこと

優先度順。上ほど「今回の変更に直接ぶら下がっていて、放置すると危ない」もの。

## A. 未消化の検証（最優先）

1. **第1局を手でロンまで通す。**
   7.の修正はピンポイントで検証済みだが、ロンボタンを押す実際の流れは未確認。
2. **通常対局（非チュートリアル）の回帰確認。**
   `GameUIPhaseController` の3箇所（`Betting` / `Agari・Ron・Result` / `UpdateDoraDisplay`）に
   `IsTutorialMode` ガードを足した。コード上は同じ呼び出しを囲んだだけだが実測していない。
3. **第1局で13枚選んで『自動』を押した後、山牌の見た目が空になる件。**
   自動操作の検証中に2回再現（内部状態は21枚、`GetWallSlots()` が 0）。
   ただしテスト側でも状態破壊が起きていたため、実プレイで起きるかは不明。
   手で第1局を通せば一発で分かる。

## B. 同じ構造の残りを洗う

今回の不具合の 5.と 7.は同じ原因（フェイズ切り替え時に演出まで走る）だった。
まだ見ていないケースが残っている。

- `HandlePhaseVisibility` の `Dealing` / `TurnDecision` / `Draw` ケースを、
  チュートリアル視点で読み直す。特に `Draw` は `DialogueUI.ShowText("流局…次の対局へ")` を
  直接出しており、チュートリアルの台本と競合しうる。
- `BoardStateManager.LastIsLocalWin` は **サーバー通信でしか更新されない**。
  初期値 `true` のまま参照している箇所が他にないか grep する。
  同種の「サーバー前提の状態」が他にもあるはず（`CurrentDoraId` なども確認する価値あり）。

## C. 設計上の借金

- **`damagePopupPrefab` が Player / Enemy とも未設定。**
  `HpPopupPresenter` のフォールバック（実行時に `TextMeshProUGUI` を組み立てる簡易版）が
  出ている。見た目を作り込むならプレハブを用意してアサインする。
- **`TutorialScenario` はScriptableObject対応なのにアセットが無い。**
  シーンの `TutorialManager.scenario` は `{fileID: 0}` で、実際は `BuildDefault()` が使われている。
  台本をコードを触らず編集したいなら、アセット化して差し替える。
- **`WaitUI.Awake` の矛盾。**
  「自動レイアウトを使わず手動配置にするため」と書いて `HorizontalLayoutGroup` を削除しているが、
  `RenderPage` が毎回追加し直している。動作に影響はないがコメントが実装と食い違ったまま。
- **第2局の手数は17が上限。**
  山牌は34枚 − 手牌13枚 = 21枚。18手以上に増やすと足りなくなるので配牌側の調整が必要。
- **シーンに残る死んだ値。**
  `waitContainer.anchoredPosition.x`（敵側 -22 / 自分側 -3）は旧レイアウトの補正値。
  実行時に中央へ上書きするので実害はないが、値としては意味を失っている。
- **`rejectFirstConfirm` が手動選択の局で到達不能。**
  2.の変更で Auto を押すまで決定ボタンが出なくなったため。
  フラグ自体は残してあるので、使わないなら消す判断が要る。

## D. 検証環境

このセッションでは Unity MCP のツールがロードされず、HTTP で直接叩いて作業した。
再発したときのために `reference_unity_mcp_bridge` にメモ済み。要点だけ再掲する。

- MCP経由の `execute_code` に **日本語リテラルを含めると壊れる**。ASCIIで書く。
- PowerShell の `Get-Content -Raw` は **UTF8 を明示**しないとJSONが壊れる。
- スクショは `PrintWindow(hWnd, hdc, 2)`。`SetForegroundWindow` は背景プロセスから効かない。
  `manage_camera` の screenshot は Main Camera 経由なので Screen Space - Overlay のUIが写らない。

**チュートリアルの自動操作ドライバを Editor 拡張としてプロジェクトに置くと、
この手の検証が一気に楽になる。** 今回は毎回スクリプトを書き直していた。
その際、**`BoardStateManager` を直接触ると盤面が壊れる**ので、
必ず `GameUIManager` 側の入口（`MoveTileToHand` / `DiscardSelectedTile` など）を通すこと。
今回それを怠って `wallSlots=0` や `BoardStateManager.Instance == null` を引き起こしている。
