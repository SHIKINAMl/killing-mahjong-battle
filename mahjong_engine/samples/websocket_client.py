"""
手動テスト用 WebSocket クライアント

使い方:
    python -m mahjong_engine.samples.websocket_client [ws://localhost:8765]
"""
import asyncio
import json
import sys
from typing import Any

import websockets

# ─── ローカル状態キャッシュ ────────────────────────────────────────────────
_state: dict[str, Any] = {
    "client_id": None,
    "wall": [],      # 最新 dealing_completed で受け取った wall
    "hand": [],      # 確定済み手牌（牌 ID）
    "waits": [],     # 待ち牌リスト
    "phase": None,   # 現在フェーズ
    "health": 20000,
}


# ─── ユーティリティ ────────────────────────────────────────────────────────

def parse_int_list(text: str) -> list[int]:
    """カンマ区切りの整数リストをパース"""
    return [int(t.strip()) for t in text.split(",") if t.strip()]


def fmt_tiles(tiles: list[int]) -> str:
    return "[" + ", ".join(str(t) for t in tiles) + "]"


def fmt_bool(b: bool) -> str:
    return "はい" if b else "いいえ"


# ─── コマンドビルダー ──────────────────────────────────────────────────────

def build_action_message(command: str) -> dict[str, Any] | None:
    """
    コマンド文字列を WebSocket 送信ペイロードに変換する。

    サポートコマンド:
        join
        ping
        tenpai 0,1,2,...            # wall index を指定して聴牌チェック
        select 0,1,2,...            # wall index を指定して手牌を確定
        bet <amount>                # 掛け金を設定
        discard <wall_index>        # 指定 wall index の牌を打牌
        skill mulligan <hand_idx>   # 手牌の hand 内インデックスを交換
        skill boost_hand <役名>     # 役の翻数を +1
        skill perspective           # 相手の手牌 3 枚を公開
        skill special_victory       # 特殊勝利スキル
        help
        exit
    """
    parts = command.strip().split(maxsplit=2)
    if not parts:
        return None

    name = parts[0].lower()
    arg1 = parts[1] if len(parts) > 1 else ""
    arg2 = parts[2] if len(parts) > 2 else ""

    if name == "join":
        return {"type": "join"}

    if name == "ping":
        return {"type": "ping"}

    if name == "tenpai":
        try:
            wall_indexes = parse_int_list(arg1)
        except ValueError:
            print("[入力エラー] tenpai は 'tenpai 0,1,2,...' の形式で指定してください")
            return None
        return {
            "type": "action",
            "action": "is_tenpai",
            "data": {"wall_indexes": wall_indexes},
        }

    if name == "select":
        try:
            hand_indexes = parse_int_list(arg1)
        except ValueError:
            print("[入力エラー] select は 'select 0,1,2,...' の形式で指定してください")
            return None
        return {
            "type": "action",
            "action": "select",
            "data": {"hand_indexes": hand_indexes},
        }

    if name == "bet":
        try:
            amount = int(arg1.strip())
        except ValueError:
            print("[入力エラー] bet は 'bet <整数>' の形式で指定してください")
            return None
        return {
            "type": "action",
            "action": "bet",
            "data": {"bet_amount": amount},
        }

    if name == "discard":
        try:
            wall_index = int(arg1.strip())
        except ValueError:
            print("[入力エラー] discard は 'discard <wall_index>' の形式で指定してください")
            return None
        return {
            "type": "action",
            "action": "discard",
            "data": {"wall_index": wall_index},
        }

    if name == "skill":
        skill_type = arg1.lower()
        if not skill_type:
            print("[入力エラー] skill <mulligan|boost_hand|perspective|special_victory> の形式で指定してください")
            return None

        data: dict[str, Any] = {"skill_type": skill_type}

        if skill_type == "mulligan":
            try:
                data["target_hand_index"] = int(arg2.strip())
            except ValueError:
                print("[入力エラー] mulligan は 'skill mulligan <hand内index>' の形式で指定してください")
                return None

        elif skill_type == "boost_hand":
            if not arg2.strip():
                print("[入力エラー] boost_hand は 'skill boost_hand <役名>' の形式で指定してください")
                return None
            data["yaku_name"] = arg2.strip()

        elif skill_type not in ("perspective", "special_victory"):
            print(f"[入力エラー] 不明なスキル種別: '{skill_type}'")
            print("  有効なスキル: mulligan, boost_hand, perspective, special_victory")
            return None

        return {
            "type": "action",
            "action": "skill",
            "data": data,
        }

    return None


# ─── ヘルプ表示 ────────────────────────────────────────────────────────────

def print_help() -> None:
    print()
    print("=" * 54)
    print(" コマンド一覧")
    print("=" * 54)
    print("  join                        マッチに参加")
    print("  ping                        サーバー疎通確認")
    print("  tenpai 0,1,2,...            聴牌チェック (wall index)")
    print("  select 0,1,2,...            手牌確定 (wall index)")
    print("  bet <amount>                掛け金設定")
    print("  discard <wall_index>        打牌")
    print("  skill mulligan <idx>        手牌交換 (hand 内 index)")
    print("  skill boost_hand <役名>     役翻数 +1")
    print("  skill perspective           相手手牌 3 枚を公開")
    print("  skill special_victory       特殊勝利スキル")
    print("  help                        このヘルプを表示")
    print("  exit                        切断して終了")
    print("=" * 54)
    print()


# ─── メッセージハンドラ ────────────────────────────────────────────────────

def handle_message(data: dict[str, Any]) -> None:
    """受信メッセージを種別ごとに表示・ローカル状態を更新"""
    msg_type = data.get("type")

    # ── 接続 ──────────────────────────────────────────────
    if msg_type == "connected":
        cid = data.get("data", {}).get("client_id")
        _state["client_id"] = cid
        print(f"[接続完了] client_id={cid}")

    # ── Ping/Pong ─────────────────────────────────────────
    elif msg_type == "pong":
        print("[pong]")

    # ── ゲーム開始 ────────────────────────────────────────
    elif msg_type == "game_started":
        d = data.get("data", {})
        players = [p.get("client_id") for p in d.get("players", [])]
        print(f"\n{'='*54}")
        print(f"[ゲーム開始] match_id={d.get('match_id')}")
        print(f"             players={players}")

    # ── 局開始 ────────────────────────────────────────────
    elif msg_type == "round_start":
        print(f"\n{'─'*54}")
        print(f"[局開始] round={data.get('round')}")

    # ── フェーズ変更 ──────────────────────────────────────
    elif msg_type == "phase_change":
        phase = data.get("new_status")
        _state["phase"] = phase
        print(f"[フェーズ変更] → {phase}")

    # ── 配牌完了 ──────────────────────────────────────────
    elif msg_type == "dealing_completed":
        dora = data.get("dora_id")
        print(f"[配牌完了] dora_id={dora}")
        for h in data.get("hands", []):
            cid = h.get("client_id")
            wall = h.get("wall", [])
            examples = h.get("tenpai_examples", [])
            if cid == _state["client_id"]:
                _state["wall"] = wall
                print(f"  [自分] wall({len(wall)}枚) = {fmt_tiles(wall)}")
                if examples:
                    print(f"         テンパイ例 index = {examples}")
            else:
                print(f"  [相手 {cid}] wall {len(wall)}枚")

    # ── 聴牌チェック結果 ──────────────────────────────────
    elif msg_type == "is_tenpai":
        waits = data.get("data", {}).get("waits", [])
        print(f"[聴牌] 待ち牌 {len(waits)} 種")
        for w in waits:
            mangan = "満貫以上 ○" if w.get("mangan_or_more") else "満貫未満"
            print(f"  tile={w.get('tile'):3d}  {mangan}  役={w.get('yaku')}")

    elif msg_type == "not_tenpai":
        print("[聴牌なし] その手牌では聴牌になりません")

    # ── 手牌確定（自分への応答） ──────────────────────────
    elif msg_type == "hand_selection_accepted":
        d = data.get("data", {})
        hand = d.get("hand", [])
        waits = d.get("waits", [])
        _state["hand"] = hand
        _state["waits"] = waits
        print(f"[手牌確定] hand = {fmt_tiles(hand)}")
        print(f"           waits = {fmt_tiles(waits)}")

    # ── 手牌確定（全員へのブロードキャスト） ─────────────
    elif msg_type == "hand_selection_completed":
        print("[全員 手牌確定]")
        for h in data.get("data", {}).get("hands", []):
            cid = h.get("client_id")
            me = " ← 自分" if cid == _state["client_id"] else ""
            print(f"  {cid}: hand={fmt_tiles(h.get('hand', []))}  waits={fmt_tiles(h.get('waits', []))}{me}")

    # ── スキル（自分への応答） ────────────────────────────
    elif msg_type == "skill_accepted":
        d = data.get("data", {})
        _state["health"] = d.get("currentHealth", _state["health"])
        print(f"[スキル成功] type={d.get('skillType')}  cost={d.get('cost')}  残HP={d.get('currentHealth')}")

    # ── スキル（ブロードキャスト） ────────────────────────
    elif msg_type == "skill_casted":
        d = data.get("data", {})
        cid = d.get("player_id")
        me = " (自分)" if cid == _state["client_id"] else ""
        print(f"[スキル発動{me}] player={cid}  type={d.get('skillType')}  cost={d.get('cost')}  残HP={d.get('health')}")
        exposed = d.get("exposedHandIndexes", [])
        if exposed:
            print(f"  公開インデックス = {exposed}")

    # ── 掛け金確定（自分への応答） ────────────────────────
    elif msg_type == "bet_accepted":
        d = data.get("data", {})
        print(f"[掛け金設定] bet={d.get('bet_amount')}  (上限={d.get('max_bet')}  単位={d.get('bet_unit')})")

    # ── 掛け金確定（ブロードキャスト） ───────────────────
    elif msg_type == "bet_completed":
        print("[全員 掛け金確定]")
        for b in data.get("data", {}).get("bets", []):
            cid = b.get("client_id")
            me = " ← 自分" if cid == _state["client_id"] else ""
            print(f"  {cid}: bet={b.get('bet')}{me}")

    # ── 打牌フェーズ開始 ──────────────────────────────────
    elif msg_type == "discard_phase_started":
        first = data.get("data", {}).get("first_player")
        me = "（自分の番）" if first == _state["client_id"] else f"（{first} の番）"
        print(f"[打牌フェーズ開始] 先攻={first} {me}")

    # ── 打牌（ブロードキャスト） ──────────────────────────
    elif msg_type == "discard_completed":
        d = data.get("data", {})
        cid = d.get("player_id")
        me = " (自分)" if cid == _state["client_id"] else ""
        print(f"[打牌{me}] player={cid}  tile={d.get('tile')}")

    # ── 打牌受理（自分への応答） ──────────────────────────
    elif msg_type == "discard_accepted":
        d = data.get("data", {})
        is_win = d.get("is_win", False)
        print(f"[打牌受理] wall_index={d.get('wall_index')}  tile={d.get('tile')}  上がり={fmt_bool(is_win)}")
        if is_win:
            liq = d.get("liquidation") or {}
            print(f"  ▼ 上がり確定!")
            print(f"    winner={liq.get('winner_id')}  han={liq.get('han')}翻  x{liq.get('multiplier')}")
            print(f"    winner +{liq.get('winner_gain')} → 残HP={liq.get('winner_health')}")
            print(f"    loser  -{liq.get('loser_loss')} → 残HP={liq.get('loser_health')}")

    # ── 局終了 ────────────────────────────────────────────
    elif msg_type == "round_end":
        d = data.get("data", {})
        is_draw = d.get("is_draw", False)
        if is_draw:
            print("[局終了] 流局（次局に掛け金を持ち越し）")
        else:
            liq = d.get("liquidation") or {}
            print(f"[局終了] winner={liq.get('winner_id')}  loser={liq.get('loser_id')}")

    # ── 特殊勝利 ──────────────────────────────────────────
    elif msg_type == "special_victory_won":
        pid = data.get("data", {}).get("player_id")
        me = " (自分)" if pid == _state["client_id"] else ""
        print(f"[特殊勝利{me}] player={pid}")

    # ── ゲーム終了 ────────────────────────────────────────
    elif msg_type == "game_end":
        scores = data.get("final_scores", {})
        print("\n" + "=" * 54)
        print("[ゲーム終了] 最終スコア")
        for cid, hp in scores.items():
            me = " ← 自分" if cid == _state["client_id"] else ""
            print(f"  {cid}: HP={hp}{me}")
        print("=" * 54)

    # ── エラー ────────────────────────────────────────────
    elif msg_type == "error":
        print(f"[サーバーエラー] {data.get('message')}")

    # ── その他（未知のメッセージ） ────────────────────────
    else:
        print(f"[{msg_type or '?'}] {json.dumps(data, ensure_ascii=False)}")


# ─── 送受信ループ ──────────────────────────────────────────────────────────

async def sender(ws) -> None:
    print_help()
    while True:
        try:
            raw = await asyncio.to_thread(input, "> ")
        except EOFError:
            break

        command = raw.strip()
        if not command:
            continue

        if command.lower() == "help":
            print_help()
            continue

        if command.lower() == "exit":
            await ws.close()
            return

        try:
            payload = build_action_message(command)
            if payload is None:
                print(f"不明なコマンド: '{command}'  ('help' でコマンド一覧を確認)")
                continue
            await ws.send(json.dumps(payload, ensure_ascii=False))
        except ValueError as exc:
            print(f"[入力エラー] {exc}")
        except Exception as exc:
            print(f"[送信エラー] {exc}")


async def receiver(ws) -> None:
    async for message in ws:
        try:
            data = json.loads(message)
        except json.JSONDecodeError:
            print(f"[RAW] {message}")
            continue

        try:
            handle_message(data)
        except Exception as exc:
            print(f"[表示エラー] {exc}  raw={json.dumps(data, ensure_ascii=False)}")


# ─── エントリーポイント ────────────────────────────────────────────────────

async def main() -> None:
    uri = sys.argv[1] if len(sys.argv) > 1 else "ws://localhost:8765"
    print(f"接続中: {uri} ...")
    try:
        async with websockets.connect(uri) as ws:
            await asyncio.gather(
                sender(ws),
                receiver(ws),
                return_exceptions=True,
            )
    except (ConnectionRefusedError, OSError) as exc:
        print(f"[接続失敗] {exc}")


if __name__ == "__main__":
    asyncio.run(main())
