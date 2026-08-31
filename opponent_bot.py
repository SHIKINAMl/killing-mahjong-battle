"""
Opponent Bot for Killing Mahjong Battle (じゃんぱいあ)
仕様書: OPPONENT_BOT_SPEC_20260806.md
"""

import asyncio
import collections
import datetime
import json
import re
import sys
from pathlib import Path
from typing import Any, Dict, List, Optional, Set
import websockets

# Windows コンソールの文字コード問題 (cp932) 対策
sys.stdout.reconfigure(encoding="utf-8", errors="replace")

WS_URL = "wss://jongpire.onrender.com/ws"
MAX_DISCARD_RETRIES = 5
# 絶対パスだと、プロジェクトを移動したときに壊れる（2026-08-30 のPC移行で実際に壊れた）。
# このファイルはリポジトリ直下にあるので、そこからの相対で引く。
SCENE_PATH = Path(__file__).parent / "Assets" / "Scenes" / "UIテストシーン.unity"
LOG_FILE_PATH = Path(__file__).parent / "bot_log.txt"


class BotLogger:
    """標準出力とファイルの両方に時刻付きでログを出力するロガー"""

    def __init__(self, log_file: Path, token_to_mask: Optional[str] = None):
        self.log_file = log_file
        self.token_to_mask = token_to_mask

    def log(self, message: str):
        now_str = datetime.datetime.now().strftime("%H:%M:%S")
        text = f"[{now_str}] {message}"
        if self.token_to_mask and self.token_to_mask in text:
            text = text.replace(self.token_to_mask, "***TOKEN***")

        print(text, flush=True)
        try:
            with open(self.log_file, "a", encoding="utf-8") as f:
                f.write(text + "\n")
        except Exception:
            pass


class OpponentBot:
    """使い捨て対戦相手ボット"""

    def __init__(self, select_delay: float = 0.0, bet_delay: float = 0.5, logger: Optional[BotLogger] = None):
        self.select_delay = select_delay
        self.bet_delay = bet_delay
        self.logger = logger or BotLogger(LOG_FILE_PATH)

        self.ws: Optional[websockets.ClientConnection] = None
        self.client_id: Optional[str] = None
        self.match_id: Optional[str] = None

        # 局ごとの状態
        self.wall: List[int] = []
        self.hand_indexes: List[int] = []
        self.discard_candidates: List[int] = []
        self.pending_discard_index: Optional[int] = None
        self.is_discard_phase: bool = False
        self.is_my_turn: bool = False
        self.discard_retry_count: int = 0
        self.round_ended: bool = False
        self.active_tasks: Set[asyncio.Task] = set()

        # 受信した type の集計。終了時に出すと「何が来ていないか」が一目で分かる
        self.type_counter: "collections.Counter[str]" = collections.Counter()

    def _track_task(self, coro) -> asyncio.Task:
        task = asyncio.create_task(coro)
        self.active_tasks.add(task)
        task.add_done_callback(self.active_tasks.discard)
        return task

    async def send_raw(self, payload: Dict[str, Any]):
        if not self.ws:
            return
        msg = json.dumps(payload, ensure_ascii=False)
        await self.ws.send(msg)

    async def send_action(self, action: str, data: Optional[Dict[str, Any]] = None):
        """
        §2.1: 正しいアクション封筒
        {"type": "action", "data": {"action": action, "data": data or {}}}
        """
        payload = {
            "type": "action",
            "data": {
                "action": action,
                "data": data or {}
            }
        }
        self.logger.log(f"-> {action} {json.dumps(data or {}, ensure_ascii=False)}")
        await self.send_raw(payload)

    def reset_round_state(self):
        self.wall = []
        self.hand_indexes = []
        self.discard_candidates = []
        self.pending_discard_index = None
        self.is_discard_phase = False
        self.is_my_turn = False
        self.discard_retry_count = 0
        self.round_ended = False

    async def run(self, token: str):
        url = f"{WS_URL}?token={token}"

        self.logger.log("Connecting to WebSocket server...")
        try:
            # §2.7: keepalive を必ず切る。配牌中サーバーは ping に応答しないため、
            # 既定の ping_interval=20 / ping_timeout=20 だと配牌の途中で勝手に切断される。
            async with websockets.connect(url, ping_interval=None) as ws:
                self.ws = ws
                self.logger.log("WebSocket connection established.")

                async for message in ws:
                    try:
                        data = json.loads(message)
                        await self.handle_message(data)
                    except json.JSONDecodeError:
                        self.logger.log(f"Invalid JSON received: {message[:100]}")
                    except Exception as e:
                        self.logger.log(f"Error handling message: {e}")
        except websockets.exceptions.ConnectionClosed as e:
            self.logger.log(f"WebSocket closed: {e}")
        except Exception as e:
            self.logger.log(f"WebSocket connection error: {e}")
        finally:
            self.ws = None
            for t in list(self.active_tasks):
                t.cancel()

    async def handle_message(self, msg: Dict[str, Any]):
        msg_type = msg.get("type")
        payload_data = msg.get("data", {})
        if not isinstance(payload_data, dict):
            payload_data = {}
        self.type_counter[str(msg_type)] += 1

        if msg_type == "connected":
            self.client_id = payload_data.get("client_id")
            self.logger.log(f"Connected as client_id: {self.client_id}")
            # マッチング参加
            self.logger.log("Sending join...")
            await self.send_raw({"type": "join"})

        elif msg_type == "matching_waiting":
            self.logger.log("waiting for an opponent...")

        elif msg_type == "game_started":
            self.match_id = payload_data.get("match_id")
            players = payload_data.get("players", [])
            self.logger.log(f"Game started! match_id: {self.match_id}, players: {players}")

        elif msg_type == "match_cancelled":
            reason = payload_data.get("reason", "unknown")
            self.logger.log(f"Match cancelled ({reason}). Re-joining...")
            await self.send_raw({"type": "join"})

        elif msg_type == "round_start":
            self.reset_round_state()
            # サーバーは局番号を `round`（トップレベル）で送る。`round_index` は存在しない
            round_number = msg.get("round", payload_data.get("round", "?"))
            self.logger.log(f"Round start. round: {round_number}")

        elif msg_type == "dealing_completed":
            self.handle_dealing_completed(msg)

        elif msg_type == "hand_selection_confirmation_required":
            # §2.2 満貫未満の確認要求に応答する（そのまま確定させる）
            reason = payload_data.get("reason")
            hand_indexes = payload_data.get("hand_indexes", self.hand_indexes)
            self.logger.log(f"Confirmation required ({reason}). Sending select_confirm...")
            await self.send_action("select_confirm", {"hand_indexes": hand_indexes})

        elif msg_type == "not_tenpai":
            # 聴牌でなくても再度 select で押し通すかログ
            self.logger.log("Received not_tenpai. Re-sending select with current hand...")
            await self.send_action("select", {"hand_indexes": self.hand_indexes})

        elif msg_type == "phase_change":
            # **サーバーは `new_status` をトップレベルに置く**（game_session.on_phase_change）。
            # `data.phase` を読むと常に None になり、賭け金を一度も送らないまま対局が止まる。
            phase = msg.get("new_status") or payload_data.get("phase") or payload_data.get("status")
            self.logger.log(f"Phase change: {phase}")
            if phase == "betting":
                self._track_task(self._delayed_bet())
            elif phase == "discard":
                self.is_discard_phase = True
            elif phase == "round_end_waiting":
                self.round_ended = True

        elif msg_type == "bet_completed":
            self.logger.log(f"Bet completed: {payload_data}")

        elif msg_type == "discard_phase_started":
            self.is_discard_phase = True
            first_player = payload_data.get("first_player")
            self.logger.log(f"Discard phase started. First player: {first_player}")
            if first_player == self.client_id:
                self.is_my_turn = True
                self._track_task(self._delayed_discard(delay=0.8))

        elif msg_type == "discard_completed":
            # サーバーは `player_id` / `tile` で送る（`client_id` / `tile_id` ではない）。
            # 取り違えると自分の打牌にも反応して、手番でないのに打とうとする。
            player = payload_data.get("player_id")
            discarded_tile = payload_data.get("tile")
            self.logger.log(f"Discard completed by {player}: tile={discarded_tile}")
            if player is not None and player != self.client_id and self.is_discard_phase:
                # 相手が切ったので次は自分の番
                self.is_my_turn = True
                self._track_task(self._delayed_discard(delay=0.8))

        elif msg_type == "discard_accepted":
            # §2.5: サーバーが打牌を受理した時のみ確定として候補から削除。
            # 受理は `wall_index` で返るので、保留中の index と突き合わせる。
            accepted_index = payload_data.get("wall_index", self.pending_discard_index)
            tile = payload_data.get("tile")
            if accepted_index in self.discard_candidates:
                self.discard_candidates.remove(accepted_index)
            self.logger.log(
                f"Discard accepted for index {accepted_index} (tile={tile}, is_win={payload_data.get('is_win')}). "
                f"Remaining candidates: {len(self.discard_candidates)}"
            )
            self.pending_discard_index = None
            self.discard_retry_count = 0

        elif msg_type == "agari_pending":
            # §2.3: ロンは絶対に辞退しない (accept: True)
            self.logger.log("Agari pending! Responding with accept: True...")
            await self.send_action("agari", {"accept": True})

        elif msg_type == "agari_accepted":
            # **これは `agari` を送った側にしか届かない**（game_session._agari は _respond_to_client）。
            # つまり受け取っているのは打牌した側ではないので、ここで打牌を再送してはいけない。
            # 再送すると「現在の手番ではありません」を貰い続ける無限ループになる（実際に踏んだ）。
            self.logger.log(f"Agari accepted: is_win={payload_data.get('is_win')}")

        elif msg_type == "round_end":
            # §3.1: 次局へ進む合図は next_round_waiting が駆動する。ここでは印を立てるだけ
            self.round_ended = True
            self.logger.log(f"Round end: {payload_data}")

        elif msg_type == "next_round_waiting":
            # §2.3: 同じ通知が複数回届くので、自分が既に ready なら送り返さない
            ready_players = payload_data.get("ready_players", []) or []
            if not self.round_ended:
                # 次の局が既に始まったあとに古い next_round_waiting が届くことがある。
                # そのまま返すと「ROUND_END_WAITING フェーズでのみ実行可能」を貰うだけなので無視する
                self.logger.log("next_round_waiting: ignored (round already restarted)")
            elif self.client_id in ready_players:
                self.logger.log(
                    f"next_round_waiting: already ready "
                    f"({payload_data.get('ready_count')}/{payload_data.get('required_count')})"
                )
            else:
                self.logger.log("next_round_waiting: sending next_round...")
                await self.send_action("next_round", {})

        elif msg_type == "game_end":
            self.logger.log(f"Game end! Result: {payload_data}")

        elif msg_type == "error":
            message = str(payload_data.get("message", msg.get("message", msg)))
            self.logger.log(f"Server Error: {message}")
            if self.pending_discard_index is not None:
                # §2.5: 弾かれた打牌は `error` で返る。保留のまま待つと二度と打たなくなる。
                # ただし**再送してよいのは「和了入力待ち」で弾かれたときだけ**。
                # 「現在の手番ではありません」で再送すると同じエラーを貰い続けて無限ループになる
                # （実際に踏んだ）。この場合は手番が回ってくるのを discard_completed で待つ。
                self.pending_discard_index = None
                if "和了入力待ち中" in message:
                    self._track_task(self._retry_discard(f"error: {message}"))
                else:
                    self.is_my_turn = False

        else:
            self.logger.log(f"Unhandled message type '{msg_type}': {json.dumps(payload_data, ensure_ascii=False)[:200]}")

    def handle_dealing_completed(self, msg: Dict[str, Any]):
        hands = msg.get("hands", msg.get("data", {}).get("hands", []) if isinstance(msg.get("data"), dict) else [])
        my_hand_entry = next((h for h in hands if h.get("client_id") == self.client_id), None)
        if not my_hand_entry and hands:
            # 自分が特定できない場合は最初のエントリを参照。
            # これは相手の山を掴んでいる可能性があるので、必ず気づけるようログに出す
            self.logger.log(f"WARNING: own client_id ({self.client_id}) not found in hands. Falling back to hands[0].")
            my_hand_entry = hands[0]

        if my_hand_entry:
            self.wall = my_hand_entry.get("wall", [])
            tenpai_examples = my_hand_entry.get("tenpai_examples", [])
            if tenpai_examples and len(tenpai_examples) >= 13:
                self.hand_indexes = list(tenpai_examples[:13])
            else:
                # フォールバック: 最初の13枚
                self.hand_indexes = list(range(min(13, len(self.wall))))

            # 手牌以外の山牌インデックスを打牌候補にする
            self.discard_candidates = [i for i in range(len(self.wall)) if i not in self.hand_indexes]
            self.logger.log(f"Dealing completed. Hand indexes: {self.hand_indexes}, Discard candidates: {len(self.discard_candidates)}")

            # 遅延後に手牌選択アクションを送信
            self._track_task(self._delayed_select())

    async def _delayed_select(self):
        if self.select_delay > 0:
            self.logger.log(f"Waiting select_delay ({self.select_delay}s) before selecting hand...")
            await asyncio.sleep(self.select_delay)

        self.logger.log(f"Sending hand selection: {self.hand_indexes}")
        await self.send_action("select", {"hand_indexes": self.hand_indexes})

    async def _delayed_bet(self):
        if self.bet_delay > 0:
            await asyncio.sleep(self.bet_delay)

        # 最小額 200 をベット
        bet_amount = 200
        self.logger.log(f"Sending bet: {bet_amount}")
        await self.send_action("bet", {"bet_amount": bet_amount})

    async def _delayed_discard(self, delay: float = 0.8):
        await asyncio.sleep(delay)
        if not self.is_discard_phase or not self.is_my_turn:
            return
        if self.pending_discard_index is not None:
            # まだ discard_accepted を受けていない打牌がある。二重に打たない（§2.5）
            return

        if not self.discard_candidates:
            # 候補が尽きた場合は手牌以外から選ぶ
            self.discard_candidates = [i for i in range(len(self.wall)) if i not in self.hand_indexes]

        if self.discard_candidates:
            # ランダムまたは先頭の候補を打牌
            idx = self.discard_candidates[0]
            self.pending_discard_index = idx
            self.is_my_turn = False
            self.logger.log(f"Sending discard for wall_index={idx}...")
            await self.send_action("discard", {"wall_index": idx})
        else:
            self.logger.log("Warning: No discard candidates available!")

    async def _retry_discard(self, reason: str, delay: float = 1.0):
        """
        弾かれた打牌を再送する（§2.5）。

        保留を解除してから打ち直す。解除しないと `_delayed_discard` が
        「まだ受理待ち」と誤解したままになり、そこで対局が止まる。

        連続再送には上限を置く。想定外のエラーで延々と打ち続けるより、
        止まってログに残ったほうが原因を追える。
        """
        if not self.is_discard_phase:
            return
        if self.discard_retry_count >= MAX_DISCARD_RETRIES:
            self.logger.log(f"Giving up re-sending discard ({MAX_DISCARD_RETRIES} retries). Last reason: {reason}")
            return
        self.discard_retry_count += 1
        self.logger.log(f"Re-sending discard after {reason} (retry {self.discard_retry_count})")
        self.pending_discard_index = None
        self.is_my_turn = True
        await self._delayed_discard(delay=delay)


def extract_token_from_scene(scene_path: Path) -> Optional[str]:
    """Unityシーンファイルから authToken を正規表現で抽出する"""
    if not scene_path.exists():
        return None
    try:
        content = scene_path.read_text(encoding="utf-8", errors="ignore")
        m = re.search(r"authToken:\s*(\S+)", content)
        if m:
            return m.group(1)
    except Exception as e:
        print(f"Error reading scene file: {e}", file=sys.stderr)
    return None


async def run_pair(duration: float, select_delay: float, bet_delay: float, token: str):
    """
    Unity なしでプロトコルだけ確かめるモード（仕様書 §6）。
    ボット2体を1プロセスで走らせて互いにマッチさせる。ログは bot1/bot2 に分ける。
    """
    bots = []
    for i in (1, 2):
        logger = BotLogger(LOG_FILE_PATH.parent / f"bot{i}_log.txt", token_to_mask=token)
        logger.log("=" * 60)
        logger.log(f"Opponent Bot (pair #{i}) started")
        bots.append(OpponentBot(select_delay=select_delay, bet_delay=bet_delay, logger=logger))

    try:
        await asyncio.wait_for(
            asyncio.gather(*(b.run(token) for b in bots), return_exceptions=True),
            timeout=duration,
        )
    except asyncio.TimeoutError:
        pass

    for i, bot in enumerate(bots, start=1):
        summary = ", ".join(f"{t}={n}" for t, n in bot.type_counter.most_common()) or "(none)"
        bot.logger.log(f"Received types: {summary}")
        bot.logger.log(f"Opponent Bot (pair #{i}) finished.")


async def main_async(duration: float, select_delay: float, bet_delay: float, pair: bool = False,
                     log_path: Optional[Path] = None):
    token = extract_token_from_scene(SCENE_PATH)
    if not token:
        print(f"ERROR: Could not extract authToken from scene file: {SCENE_PATH}", file=sys.stderr)
        sys.exit(1)

    if pair:
        await run_pair(duration, select_delay, bet_delay, token)
        return

    logger = BotLogger(log_path or LOG_FILE_PATH, token_to_mask=token)
    logger.log("=" * 60)
    logger.log("Opponent Bot started")
    logger.log(f"Settings: duration={duration}s, select_delay={select_delay}s, bet_delay={bet_delay}s")
    logger.log("Token extracted successfully from scene file (masked for security).")
    logger.log("=" * 60)

    bot = OpponentBot(select_delay=select_delay, bet_delay=bet_delay, logger=logger)

    try:
        # duration で全体をラップし、制限時間を超えたら自動で安全に終了する
        await asyncio.wait_for(bot.run(token), timeout=duration)
    except asyncio.TimeoutError:
        logger.log(f"Duration limit reached ({duration}s). Bot shutting down cleanly.")
    except Exception as e:
        logger.log(f"Bot exited with exception: {e}")
    finally:
        if bot.type_counter:
            summary = ", ".join(f"{t}={n}" for t, n in bot.type_counter.most_common())
            logger.log(f"Received types: {summary}")
        logger.log("Opponent Bot finished.")


def main():
    # 引数パース: python opponent_bot.py [duration] [select_delay] [bet_delay] [--pair] [--log PATH]
    duration = 900.0
    select_delay = 0.0
    bet_delay = 0.5
    pair = False
    log_path: Optional[Path] = None

    args = sys.argv[1:]
    positional: List[str] = []
    i = 0
    while i < len(args):
        if args[i] == "--pair":
            pair = True
        elif args[i] == "--log" and i + 1 < len(args):
            i += 1
            log_path = Path(args[i])
        else:
            positional.append(args[i])
        i += 1

    defaults = [duration, select_delay, bet_delay]
    for n, value in enumerate(positional[:3]):
        try:
            defaults[n] = float(value)
        except ValueError:
            pass
    duration, select_delay, bet_delay = defaults

    asyncio.run(main_async(duration, select_delay, bet_delay, pair=pair, log_path=log_path))


if __name__ == "__main__":
    main()
