# プロジェクト固有のUI描画ルール (Unity UI Sorting)

## Nested Canvas における UI の最前面化について
特定の UI パネル（例: ツールチップ、スマホ型UIなど）を他のUIや3Dオブジェクトよりも手前に（最前面に）表示したい場合、以下のルールに従うこと。

1. **親Canvasの制限に注意する**
   特定のUIの `Canvas` コンポーネントだけ `overrideSorting = true` に設定しても、その大元となる親のCanvas（World Space や低い Sorting Order のCanvas）の設定に影響され、最前面に出られないことがある（Zバッファなどの影響を受けるため）。
   
2. **ルートとなる親Canvasの Order を引き上げる**
   UIを最前面に出すには、対象のUIだけでなく、**そのUIが属する大元のパネル全体のルートCanvas**の `overrideSorting` を有効にし、`sortingLayerName` や `sortingOrder` を適切な値（例: 20 など）に引き上げるのが効果的である。

3. **内部の子Canvasを一律で上書きしない（重要！）**
   親UIの Canvas の Order を引き上げる際、**絶対に `GetComponentsInChildren<Canvas>(true)` を使って全ての子要素の Canvas の Order を一律で上書きしてはならない**。
   一律で上書きしてしまうと、UI内部の前後関係（背景画像とボタンの重なりなど）が壊れ、「背景が手前に来てボタンが隠れてしまう（ボタンが消えたように見える）」といった致命的なバグが発生する。
   **必ずルートのCanvasのみを設定し、子要素の順序はそのまま保つこと**。
