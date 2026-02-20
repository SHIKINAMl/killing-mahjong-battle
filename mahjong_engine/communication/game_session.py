import asyncio
import heapq
from typing import Any, Awaitable, Callable, Dict, List, Optional

from ..engine.game_engine import GameEngine


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

		engine.on_dealing_complete = lambda dora_id, hands: asyncio.create_task(
			self.on_dealing_complete(match.match_id, dora_id, hands)
		)
		engine.on_status_change = lambda status: asyncio.create_task(
			self.on_status_change(match.match_id, status)
		)
		engine.on_round_start = lambda round_state: asyncio.create_task(
			self.on_round_start(match.match_id, round_state)
		)
		engine.on_round_end = lambda: asyncio.create_task(
			self.on_round_end(match.match_id)
		)
		engine.on_game_end = lambda: asyncio.create_task(
			self.on_game_end(match.match_id)
		)

		self._game_engines[match.match_id] = engine
		engine.start_game()

		payload = {
			"type": "game_started",
			"match": {
				"match_id": match.match_id,
				"players": [{"client_id": cid} for cid in match.players],
				"status": match.status,
			},
			"game_state": engine.get_game_state(),
		}

		await asyncio.gather(
			*(self._send_to_client(cid, payload) for cid in match.players),
			return_exceptions=True,
		)

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
		action_data = data.get("data", {})
		try:
			pass
		except Exception as exc:
			await self._send_to_client(client_id, {"type": "error", "message": str(exc)})

	async def on_dealing_complete(self, match_id: str, dora_id: int, hands: List) -> None:
		"""
		配牌完了時の処理
		Args:
			match_id: 対象のマッチID
			dora_id: ドラのID
			hands: 各プレイヤーの配牌（牌のリスト）および聴牌形の例
		"""
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
				"type": "dealing_complete",
				"dora_id": dora_id,
				"hands": hand_payload,
			},
		)

	async def on_status_change(self, match_id: str, status: Any) -> None:
		"""
		ステータス変更時の処理
		Args:
			match_id: 対象のマッチID
			status: 新しいステータス
		"""
		engine = self._game_engines.get(match_id)
		if engine:
			await self._broadcast_match_members(
				match_id,
				{
					"type": "status_change",
					"status": status.value,
					"game_state": engine.get_game_state(),
				},
			)

	async def on_round_start(self, match_id: str, round_state: Any) -> None:
		"""
		ラウンド開始時の処理
		Args:
			match_id: 対象のマッチID
			round_state: ラウンドの状態
		"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "round_start",
				"round": round_state.round_number,
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