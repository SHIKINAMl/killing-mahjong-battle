# EditorWindow partial 分割メモ

対象は `ReactionEditorWindow`、`ReactionRuleEditorWindow`、`TutorialScenarioEditorWindow` のみです。
すべて EditorWindow であり、クラス名・namespace・フィールド名・シリアライズ対象・振る舞いは変更しません。

## 責務分割

| クラス | 元ファイルに残す責務 | partial へ移す責務 |
| --- | --- | --- |
| `ReactionEditorWindow` | 定数・状態フィールド・メニュー入口・Unity ライフサイクル・全体レイアウト | CSV 読み書き、CSV タブ、クリックタブ、トリガータブ、共通 GUI |
| `ReactionRuleEditorWindow` | 定数・状態フィールド・メニュー入口・Unity ライフサイクル・全体レイアウト | アセット操作、一覧、ルール詳細、条件、セリフ、共通 GUI |
| `TutorialScenarioEditorWindow` | 定数・状態フィールド・メニュー入口・Unity ライフサイクル・全体レイアウト | 台本の到達条件、局一覧/詳細、能力実演、セリフ行、共通 GUI |

## 作業分担

- ユーザー指定の担当範囲以外は編集しない。
- 本作業では、各 partial を同一クラス内の責務単位として分けるだけである。
- 死んだコードを削除する判断は行わず、既存メンバーをすべて保持する。

