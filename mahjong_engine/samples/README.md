# WebSocket API 仕様（現行実装ベース）

このドキュメントは、現在のサーバー実装（`websocket_server.py` / `game_session.py`）に合わせた JSON 通信仕様です。

## 1. 基本

- すべて JSON オブジェクト
- エンコーディングは UTF-8
- サーバー送信は `json.dumps(..., ensure_ascii=False)`

### 共通エラー形式

```json
{
  "type": "error",
  "message": "..."
}
```

主なエラーメッセージ例:

- `Invalid JSON`
- `type is required`
- `Unknown type: ...`
- `Already in a match`
- `Already in matchmaking queue`
- `Not in game`
- `Unsupported action type`

---

## 2. クライアント → サーバー

## 2.1 接続・待機系

### `join`

```json
{ "type": "join" }
```

### `ping`

```json
{ "type": "ping" }
```

### `action`

```json
{
  "type": "action",
  "action": "...",
  "data": {}
}
```

サポートされる `action` は以下:

- `is_tenpai`
- `skill`
- `select`
- `bet`
- `discard`

---

## 2.2 ゲームアクション詳細

### `is_tenpai`

手牌候補（wall index 配列）に対して聴牌判定。

```json
{
  "type": "action",
  "action": "is_tenpai",
  "data": {
    "wall_indexes": [0, 1, 2, 3, 4]
  }
}
```

### `skill`

```json
{
  "type": "action",
  "action": "skill",
  "data": {
    "skill_type": "mulligan|boost_hand|perspective|special_victory"
  }
}
```

追加フィールド:

- `boost_hand` の場合: `yaku_name` (string)
- `mulligan` の場合: `target_hand_index` (int)

### `select`

手牌確定（wall index 配列）。

```json
{
  "type": "action",
  "action": "select",
  "data": {
    "hand_indexes": [0, 3, 5, 7, 9]
  }
}
```

### `bet`

掛け金設定。

```json
{
  "type": "action",
  "action": "bet",
  "data": {
    "bet_amount": 5000
  }
}
```

### `discard`

打牌（wall index 指定）。

```json
{
  "type": "action",
  "action": "discard",
  "data": {
    "wall_index": 12
  }
}
```

---

## 3. サーバー → クライアント

## 3.1 接続・マッチング

### `connected`

接続直後に送信。

```json
{
  "type": "connected",
  "data": {
    "client_id": "C0001"
  }
}
```

### `matching_waiting`

`join` 後、待機中に送信。

```json
{
  "type": "matching_waiting"
}
```

### `game_started`

2人揃ってマッチ開始時に送信。

```json
{
  "type": "game_started",
  "data": {
    "match_id": "M0001",
    "players": [
      { "client_id": "C0001" },
      { "client_id": "C0002" }
    ]
  }
}
```

### `match_cancelled`

対戦相手切断時に送信。

```json
{
  "type": "match_cancelled",
  "data": {
    "match_id": "M0001",
    "reason": "player_disconnected"
  }
}
```

### `ping`

`ping` の応答。

```json
{
  "type": "ping",
  "data": {
    "ts": 1700000000.123
  }
}
```

---

## 3.2 ラウンド進行通知

### `round_start`

```json
{
  "type": "round_start",
  "round": 1
}
```

### `phase_change`

```json
{
  "type": "phase_change",
  "new_status": "dealing|hand_selection|betting|discard|liquidation"
}
```

### `dealing_completed`

```json
{
  "type": "dealing_completed",
  "dora_id": 10,
  "hands": [
    {
      "client_id": "C0001",
      "wall": [1, 2, 3],
      "tenpai_examples": [0, 1, 2]
    }
  ]
}
```

### `hand_selection_completed`

```json
{
  "type": "hand_selection_completed",
  "data": {
    "hands": [
      {
        "client_id": "C0001",
        "hand": [1, 2, 3],
        "waits": [9, 17],
        "wall": [1, 2, 3]
      }
    ]
  }
}
```

### `bet_completed`

```json
{
  "type": "bet_completed",
  "data": {
    "bets": [
      { "client_id": "C0001", "bet": 5000 },
      { "client_id": "C0002", "bet": 3000 }
    ]
  }
}
```

### `discard_phase_started`

```json
{
  "type": "discard_phase_started",
  "data": {
    "first_player": "C0001"
  }
}
```

### `discard_completed`

```json
{
  "type": "discard_completed",
  "data": {
    "player_id": "C0001",
    "tile": 42
  }
}
```

### `round_end`

```json
{
  "type": "round_end",
  "data": {
    "is_draw": false,
    "liquidation": {
      "winner_id": "C0001",
      "loser_id": "C0002",
      "han": 6,
      "multiplier": 1.5,
      "winner_bet": 5000,
      "loser_bet": 3000,
      "winner_gain": 7500,
      "loser_loss": 4500,
      "winner_health": 22500,
      "loser_health": 10500
    }
  }
}
```

流局時は以下:

```json
{
  "type": "round_end",
  "data": {
    "is_draw": true,
    "liquidation": null
  }
}
```

---

## 3.3 アクション応答

### `is_tenpai`（成功）

```json
{
  "type": "is_tenpai",
  "data": {
    "waits": [
      {
        "tile": 10,
        "mangan_or_more": true,
        "yaku": ["riichi", "tanyao"]
      }
    ]
  }
}
```

### `not_tenpai`

```json
{
  "type": "not_tenpai",
  "message": "Hand is not in tenpai"
}
```

### `hand_selection_accepted`

```json
{
  "type": "hand_selection_accepted",
  "data": {
    "hand": [1, 2, 3],
    "waits": [9, 17],
    "wall": [1, 2, 3, 4]
  }
}
```

### `skill_accepted`

```json
{
  "type": "skill_accepted",
  "data": {
    "skillType": "boost_hand",
    "cost": 10000,
    "currentHealth": 9000
  }
}
```

### `skill_casted`

```json
{
  "type": "skill_casted",
  "data": {
    "player_id": "C0001",
    "skillType": "perspective",
    "cost": 1500,
    "health": 18500,
    "exposedHandIndexes": [1, 4, 8]
  }
}
```

### `bet_accepted`

```json
{
  "type": "bet_accepted",
  "data": {
    "bet_amount": 5000,
    "max_bet": 10000,
    "bet_unit": 1000
  }
}
```

### `discard_accepted`

```json
{
  "type": "discard_accepted",
  "data": {
    "wall_index": 12,
    "tile": 42,
    "is_win": true,
    "liquidation": {
      "winner_id": "C0001",
      "loser_id": "C0002",
      "han": 6,
      "multiplier": 1.5,
      "winner_bet": 5000,
      "loser_bet": 3000,
      "winner_gain": 7500,
      "loser_loss": 4500,
      "winner_health": 22500,
      "loser_health": 10500
    }
  }
}
```

勝利でない場合は `is_win: false`、`liquidation: null`。

---

## 3.4 ゲーム終了

### `special_victory_won`

```json
{
  "type": "special_victory_won",
  "data": {
    "player_id": "C0001"
  }
}
```

### `game_end`

```json
{
  "type": "game_end",
  "final_scores": {
    "C0001": 22500,
    "C0002": 10500
  }
}
```

---

## 4. 実装上の注意

- `action.data` は必須で、オブジェクトである必要があります。
- `select` は `hand_indexes` を受け付けます（`hand` も後方互換で受理）。
- `bet` は `bet_amount` を受け付けます（`bet` も後方互換で受理）。
- `discard` は和了判定を内部で即時実行し、`declare_win` は不要です。
- フェーズ外アクションは `error` が返ります。
