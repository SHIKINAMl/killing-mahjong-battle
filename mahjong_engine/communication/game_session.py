import time
import asyncio
import heapq
from typing import Any, Awaitable, Callable, Dict, List, Optional

from ..engine.game_engine import GameEngine
from ..engine.hand_analyzer import HandAnalyzer
from ..engine.game_state import GameStatus, RoundStatus
from ..utils import TileConverter


class GameSession:
	"""ゲーム進行に関する処理を担当"""

	def __init__(
		self,
		lock: asyncio.Lock,
		matches: Dict[str, Any],
		active_match_by_client: Dict[str, str],
		game_engines: Dict[str, GameEngine],
		available_match_numbers: List[int],
		send_to_client: Callable[[str, Dict[str, Any]], Awaitable[None]],
		broadcast_match_members: Callable[[str, Dict[str, Any]], Awaitable[None]],
	):
		self._lock = lock
		self._matches = matches
		self._active_match_by_client = active_match_by_client
		self._game_engines = game_engines
		self._available_match_numbers = available_match_numbers
		self._send_to_client = send_to_client
		self._broadcast_match_members = broadcast_match_members

	async def start_match(self, match: Any) -> None:
		engine = GameEngine(num_players=len(match.players))
		engine.initialize_players(match.players)

		engine.on_wall_dealt = lambda: asyncio.create_task(
			self.on_dealt(match.match_id)
		)
		engine.on_hand_selected = lambda: asyncio.create_task(
			self.on_hand_selected(match.match_id)
		)
		engine.on_bet = lambda: asyncio.create_task(
			self.on_bet(match.match_id)
		)
		engine.on_turn_decided = lambda: asyncio.create_task(
			self.on_turn_decision_complete(match.match_id)
		)

		engine.on_round_start = lambda: asyncio.create_task(
			self.on_round_start(match.match_id)
		)
		engine.on_round_end = lambda: asyncio.create_task(
			self.on_round_end(match.match_id)
		)
		engine.on_game_end = lambda: asyncio.create_task(
			self.on_game_end(match.match_id)
		)

		payload = {
			"type": "game_started",
			#"data": {
			#	"match_id": match.match_id,
			#	"players": [{"client_id": cid} for cid in match.players],
			#	"status": match.status,
			#},
		}

		await asyncio.gather(
			*(self._send_to_client(cid, payload) for cid in match.players),
			return_exceptions=True,
		)

		self._game_engines[match.match_id] = engine
		engine.start_game(4)  # 最大4局でゲーム開始

	async def handle_game_action(self, client_id: str, data: Dict[str, Any]) -> None:
		"""
		クライアントからのゲームアクションを処理
		Args:
			client_id: アクションを送信したクライアントのID
			data: アクションのデータ
		"""
		match_id = self._active_match_by_client.get(client_id)
		if not match_id or match_id not in self._game_engines:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in game"})
			return

		engine = self._game_engines[match_id]
		action_type = data.get("action")
		action_data = data.get("data")

		if not action_type:
			await self._send_to_client(client_id, {"type": "error", "message": "No action type specified"})
			return
		if not action_data or type(action_data) is not dict:
			await self._send_to_client(client_id, {"type": "error", "message": "Invalid action data"})
			return

		try:
			if action_type == "is_tenpai": # 手牌の聴牌判定
				await self._is_tenpai(engine, client_id, action_data)
			elif action_type == "skill": # スキルアクション
				await self._skill(engine, client_id, action_data)
			elif action_type == "selected": # 手牌選択の確定
				await self._selected(engine, client_id, action_data)
			elif action_type == "betting": # 掛け金設定アクション
				await self._betting(engine, client_id, action_data)
			elif action_type == "discard": # 打牌アクション
				pass

			elif True:  # 他のアクションタイプもここで処理
				pass
		except Exception as exc:
			return

	async def _is_tenpai(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""手牌の聴牌判定の処理"""

		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in hand selection phase"})
			return

		if type(action_data) is not dict:
			await self._send_to_client(client_id, {"type": "error", "message": "Invalid action data"})
			return

		hand = action_data.get("hand")

		if not hand:
			await self._send_to_client(client_id, {"type": "error", "message": "No hand provided"})
			return

		player = next((p for p in engine.state.players if p.player_id == client_id), None)

		if HandAnalyzer.is_tenpai(hand, player.wall):
			waits = HandAnalyzer.get_tenpai_waiting_tiles(hand, player.wall)

			waits = [w+32 if w == engine.state.round_state.dora_id else w for w in waits]

			await self._send_to_client(client_id, {
				"type": "is_tenpai",
				"data": {
					"waits": [
						{
							"tile" : w,
							"mangan_or_more" : HandAnalyzer.check_mangan(hand + [w]),
							"yaku" : HandAnalyzer.enum_yaku(hand + [w]),
						} for w in waits
					]
				},
			})

		else:
			await self._send_to_client(client_id, {
				"type": "not_tenpai",
				"message": "Hand is not in tenpai",
			})

	async def _skill(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""スキルアクションの処理"""
		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in hand selection phase"})
			return

		# スキルの処理は未実装
		# ブロードキャストで通知

	async def _selected(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""手牌選択完了の処理"""
		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in hand selection phase"})
			return

		if type(action_data) is not dict:
			await self._send_to_client(client_id, {"type": "error", "message": "Invalid action data"})
			return

		hand = action_data.get("hand")

		if not hand:
			await self._send_to_client(client_id, {"type": "error", "message": "No hand provided"})
			return

		player = next((p for p in engine.state.players if p.player_id == client_id), None)

		if HandAnalyzer.is_tenpai(hand, player.wall):
			for p in engine.state.players:
				if p.player_id == client_id:
					p.hand = hand
					p.wait = HandAnalyzer.get_tenpai_waiting_tiles(hand, p.wall)
					p.wall = HandAnalyzer.without_hand(hand, p.wall)

					await self._send_to_client(client_id, {
						"type": "hand_selected",
						"data": {
							"hand": p.hand,
							"wait": p.wait,
							"wall": p.wall,
						},
					})

		else:
			await self._send_to_client(client_id, {
				"type": "error",
				"message": "Selected hand is not in tenpai or does not have mangan potential",
			})

		if all(p.wait for p in engine.state.players):
			await engine.selected()

	async def on_dealt(self, match_id: str) -> None:
		"""
		配牌完了時の処理
		Args:
			match_id: 対象のマッチID
			dora_id: ドラのID
			hands: 各プレイヤーの配牌（牌のリスト）および聴牌形の例
		"""
		hands = [(p.wall, p.hand) for p in self._game_engines[match_id].state.players]
		dora = self._game_engines[match_id].state.round_state.dora_id

		hand_payload = []
		for i, h in enumerate(hands):
			hand_payload.append(
				{
					"client_id": await self.resolve_client_id(match_id, i),
					"hand": h[0],  # 配られた牌
					"tenpai_examples": h[1],  # 聴牌形の例
				}
			)

		await self._broadcast_match_members(
			match_id,
			{
				"type": "wall_dealt",
				"dora_id": dora,
				"hands": hand_payload,
			},
		)

	async def on_hand_selected(self, match_id: str) -> None:
		"""
		手牌選択完了時の処理
		Args:
			match_id: 対象のマッチID
		"""
		hand = [(p.hand, p.wait, p.wall) for p in self._game_engines[match_id].state.players]
		hand_payload = []
		for i, h in enumerate(hand):
			hand_payload.append(
				{
					"client_id": await self.resolve_client_id(match_id, i),
					"hand": h[0],  # 選択された手牌
					"wait": h[1],  # 待ち牌
					"wall": h[2],  # 配られた牌から選択された手牌を除いたもの
				}
			)
		await self._broadcast_match_members(
			match_id,
			{
				"type": "hand_selected",
				"hands": hand_payload,
			},
		)

	async def on_bet(self, match_id: str) -> None:
		"""
		掛け金設定完了時の処理
		Args:
			match_id: 対象のマッチID
		"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "bet",
			},
		)

	async def on_turn_decided(self, match_id: str) -> None:
		"""
		手番決定完了時の処理
		Args:
			match_id: 対象のマッチID
		"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "turn_decided",
				"current_player": self._game_engines[match_id].state.round_state.current_player_index,
			},
		)

	async def on_round_start(self, match_id: str) -> None:
		"""
		ラウンド開始時の処理
		Args:
			match_id: 対象のマッチID
		"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "round_start",
				"round": self._game_engines[match_id].state.round_state.round_number,
			},
		)

	async def on_round_end(self, match_id: str) -> None:
		"""
		ラウンド終了時の処理
		Args:
			match_id: 対象のマッチID
		"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "round_end",
			},
		)

	async def on_game_end(self, match_id: str) -> None:
		engine = self._game_engines.get(match_id)
		if engine:
			final_scores = {}
			for p in engine.state.players:
				resolved_client_id = await self.resolve_client_id(match_id, p.player_id)
				score_key = resolved_client_id if resolved_client_id is not None else str(p.player_id)
				final_scores[score_key] = p.health

			await self._broadcast_match_members(
				match_id,
				{
					"type": "game_end",
					"final_scores": final_scores,
				},
			)

			async with self._lock:
				self._game_engines.pop(match_id, None)
				match = self._matches.pop(match_id, None)
				if match_id.startswith("M") and match_id[1:].isdigit():
					heapq.heappush(self._available_match_numbers, int(match_id[1:]))
				for player_id in (match.players if match else []):
					self._active_match_by_client.pop(player_id, None)

	async def resolve_client_id(self, match_id: str, engine_player_id: Any) -> Optional[str]:
		try:
			index = int(engine_player_id)
		except (TypeError, ValueError):
			return None

		async with self._lock:
			match = self._matches.get(match_id)
			if match is None:
				return None
			if index < 0 or index >= len(match.players):
				return None
			return match.players[index]