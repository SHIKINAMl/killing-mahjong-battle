# WebSocket JSON 通信仕様（現行実装ベース）

---

## 1. 概要

本ドキュメントは、**現段階の実装に基づく** WebSocket 通信における、
クライアントの JSON メッセージ仕様を整理したものです。

- すべてのメッセージは JSON オブジェクト
- 文字コードは UTF-8（サーバー送信時は `ensure_ascii=False`）
- 受信 JSON が不正な場合は `error` を返す

---

## 2. 共通ルール

### 2.1 共通フィールド

 `type` (string): メッセージ種別（必須）
```json
{
  "type": "...",
}
```

### 2.2 エラー形式

```json
{
  "type": "error",
  "message": "..."
}
```

主な `message` 例:
- `Invalid JSON`
- `type is required`
- `Unknown type: ...`
- `Not in game`
- `Not in hand selection phase`
- `No hand provided`
- `Selected hand is not in tenpai or does not have mangan potential`

---

## 3. クライアント → サーバー

## 3.1 接続維持

### `ping`

```json
{ "type": "ping" }
```

サーバー応答:

```json
{
  "type": "ping" ,
  "data": {
    "ts": 1700000000.123
  }
}
```

---

## 3.2 マッチング参加

### `join`

```json
{ "type": "join" }
```

サーバー応答:
- 待機中は
```json
{ "type": "matching_waiting" }
```

- 人数が揃うと
```json
{ "type": "game_started" }

```

---

## 3.3 ゲームアクション

### `action`

- 共通フィールド
```json
{
  "type": "action",
  "action": "...",
  "data": {
  }
}
```

#### `is_tenpai`
揃えた手牌が聴牌形か
```json
{
  "type": "action",
  "action": "is_tenpai",
  "data": {
    "hand": list[int]
  }
}
```

サーバー応答:
- 聴牌の時
```json
{
	"type": "is_tenpai",
	"data": {
		"waits": [
			{
				"tile" : int,               // 待ち
				"mangan_or_more" : boolean, // 満貫以上か
				"yaku" : list[string],      // 役
			},
      {
        ...
      }
		]
	},
}
```

- 聴牌でない時
```json
{
	"type": "not_tenpai",
	"message": "Hand is not in tenpai",
}
```

#### `skill`（未実装）
スキルの使用
```json
{
  "type": "action",
  "action": "skill",
  "data": {
  }
}
```

#### `selected`
手牌の選択完了
```json
{
  "type": "action",
  "action": "selected",
  "data": {
    "hand": list[int]
  }
}
```

サーバー応答:
```json
{
	"type": "hand_selected",
	"data": {
		"hand": list[int], // 手牌
		"wait": list[int], // 待ち
		"wall": list[int], // 山牌
	}
}