# Claude Code向け修正指示書：UI配置スライドアニメーションの表示バグ修正（再）

## プロジェクト状況とバグの概要
Unity製の麻雀ゲーム（`killing-mahjong-battle`）において、手動で手牌を組むフェーズ（HandSelection）で牌を移動させる際、「旧配置から新配置へと牌がスライド移動する」アニメーション（`PlayTransitionAnimationRoutine`）を導入しています。

しかし現在、**「牌を移動させると全ての牌が一瞬消え、約0.4秒後（アニメーション終了後）に移動が終わった状態で突然現れる」**というバグが依然として発生しています。
つまり、アニメーション中に表示されるはずの**ダミー牌の描画が完全に失敗して見えなくなっている（または画面外に飛んでいる）**状態です。

## 問題が起きている処理（現状のコードベース）
対象メソッド：`GameUIVisualController.cs` の `PlayTransitionAnimationRoutine`

このルーチンは以下の仕組みで動いています：
1. **古い位置の記録**: `RectTransformUtility.WorldToScreenPoint` を使い、スクリーン座標（対角の頂点）として記録する。
2. **ダミーの生成**: `TransitionAnimContainer` という最前面用コンテナ（`Nested Canvas`付き、`overrideSorting=true`）を作り、ダミー牌を配置する。
3. **ローカル座標変換**: `RectTransformUtility.ScreenPointToLocalPointInRectangle` でスクリーン座標からダミーコンテナ内のローカル座標とサイズ（`sizeDelta`, `anchoredPosition`）を計算する。
4. **状態更新**: 本物のUIを再生成し、一時的に透明（`CanvasGroup.alpha = 0`）にする。
5. **アニメーション**: ダミー牌を `Lerp` でスライド移動させる。
6. **アニメーション終了**: ダミー牌を破棄し、本物の牌を再表示する。

## 考えられる原因
現在のコードでは、ダミー牌の `position` などをワールド座標で直接設定する代わりに、**スクリーン座標をローカル座標に変換してサイズと位置を設定するアプローチ**が取られていますが、ここで以下の計算ズレ・描画失敗が起きている可能性が高いです。

1. **`animCanvasRt` のレイアウト未確定による座標計算の狂い**
   - 新しく作った `TransitionAnimContainer` に `anchorMin/Max = 0/1` 等を設定した直後に `ScreenPointToLocalPointInRectangle` を呼んでいますが、Canvas 内の Layout がまだリビルドされていないため、コンテナの実際の `rect` サイズが 0 等になっており、変換された `localBL/TR` が完全に画面外に飛んでいるか、`sizeDelta` が `(0, 0)` になっている可能性があります。
2. **`uiCam` （カメラ）の指定ミス**
   - Canvas の `RenderMode` が `ScreenSpaceOverlay` の場合、`WorldToScreenPoint` や `ScreenPointToLocalPointInRectangle` に渡すカメラ（`uiCam`）は `null` である必要があります。現在のコードでは `rootCanvas.worldCamera` を渡していますが、オーバーレイの場合はこれが null ではなくても計算が狂う原因になります。
3. **ダミー牌自体のコンポーネントによる非表示**
   - ダミー牌に対して `CanvasGroup.alpha = 1` を設定していますが、親の Nested Canvas やプレハブ側の設定の影響で実際には描画されていない可能性もあります。

## Claude Codeへの依頼事項
1. `GameUIVisualController.cs` の `PlayTransitionAnimationRoutine` を調査し、ダミー牌がアニメーション中に**正しいサイズと位置で確実に画面に表示される**ように修正してください。
2. もしスクリーン座標変換が複雑で不安定な場合は、`rootCanvas` の直下に単なる `RectTransform`（Canvas付きではない、あるいはCanvasScalerに依存しない）を配置し、ダミーの `worldPos`（`transform.position`）や `localScale` を直接代入して移動させるシンプルで確実な方式に戻すことも検討してください。
3. スライドアニメーションの動作を安定させ、移動時に牌が消えずにスムーズに動く完成形に仕上げてください。
