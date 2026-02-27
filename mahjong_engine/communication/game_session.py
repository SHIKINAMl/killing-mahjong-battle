import time
import asyncio
import heapq
from typing import Any, Awaitable, Callable, Dict, List, Optional

from ..engine.game_engine import GameEngine
from ..engine.game_state import GameStatus, RoundStatus


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

		engine.on_dealt = lambda: asyncio.create_task(
			self.on_dealt(match.match_id)
		)
		engine.on_selected = lambda: asyncio.create_task(
			self.on_selected(match.match_id)
		)
		engine.on_bet = lambda: asyncio.create_task(
			self.on_bet(match.match_id)
		)

		engine.on_discard_started = lambda: asyncio.create_task(
			self.on_discard_started(match.match_id)
		)
		engine.on_discarded = lambda player_id, tile_id: asyncio.create_task(
			self.on_discarded(match.match_id, player_id, tile_id)
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

		engine.on_phase_change = lambda new_status: asyncio.create_task(
			self.on_phase_change(match.match_id, new_status)
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
			elif action_type == "select": # 手牌選択の確定
				await self._select(engine, client_id, action_data)
			elif action_type == "bet": # 掛け金設定アクション
				await self._bet(engine, client_id, action_data)
			elif action_type == "discard": # 打牌アクション
				await self._discard(engine, client_id, action_data)
			elif action_type == "declare_win": # 上がり宣言アクション
				await self._declare_win(engine, client_id, action_data)
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

		hand = action_data.get("hand")

		if not hand:
			await self._send_to_client(client_id, {"type": "error", "message": "No hand provided"})
			return

		player = engine.get_player_by_id(client_id)

		waits = engine.get_waits(hand, player)
		if waits is not None:
			await self._send_to_client(client_id, {
				"type": "is_tenpai",
				"data": {
					"waits": [
						{
							"tile" : w[0],
							"mangan_or_more" : w[1],
							"yaku" : w[2],
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

	async def _select(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""手牌選択アクションの処理"""
		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in hand selection phase"})
			return

		hand = action_data.get("hand")

		if not hand:
			await self._send_to_client(client_id, {"type": "error", "message": "No hand provided"})
			return

		player = engine.get_player_by_id(client_id)

		if engine.select_hand(hand, player):
			await self._send_to_client(client_id, {
				"type": "hand_select_accepted",
				"data": {
					"hand": player.hand,
					"wait": player.wait,
					"wall": player.wall,
				},
			})

			if all(p.wait for p in engine.state.players):
				await engine.selected()

		else:
			await self._send_to_client(client_id, {
				"type": "error",
				"message": "Selected hand is not in tenpai or does not have mangan potential",
			})

	async def _bet(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""掛け金設定アクションの処理"""
		# 掛け金の処理は未実装
		if engine.state.round_state.status != RoundStatus.BETTING:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in betting phase"})
			return

		bet = action_data.get("bet")

		if bet is None or type(bet) is not int or bet < 0:
			await self._send_to_client(client_id, {"type": "error", "message": "Invalid bet amount"})
			return

		player = engine.get_player_by_id(client_id)

		if player: # 本来は賭け可能な金額のチェックをする（未実装）
			player.bet = bet
			await self._send_to_client(client_id, {
				"type": "bet_accepted",
				"data": {
					"bet": bet,
				},
			})

			if all(p.bet > 0 for p in engine.state.players):
				await engine.bet()

	async def _discard(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""打牌アクションの処理"""
		if engine.state.round_state.status != RoundStatus.DISCARD:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in discard phase"})
			return

		player = engine.get_current_player()
		if player.player_id != client_id:
			await self._send_to_client(client_id, {"type": "error", "message": "Not your turn"})
			return

		if "tile" not in action_data:
			await self._send_to_client(client_id, {"type": "error", "message": "No tile specified"})
			return

		tile = action_data["tile"]

		if tile not in player.wall:
			await self._send_to_client(client_id, {"type": "error", "message": "Tile not in your wall"})
			return

		await self._send_to_client(client_id, {
			"type": "discard_accepted",
			"data": {
				"tile": tile,
			},
		})

		engine.discard(client_id, tile)

	async def _declare_win(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""上がり宣言アクションの処理"""
		if engine.state.round_state.status != RoundStatus.DISCARD:
			await self._send_to_client(client_id, {"type": "error", "message": "Not in discard phase"})
			return

		player = engine.get_current_player()
		if player.player_id != client_id:
			await self._send_to_client(client_id, {"type": "error", "message": "Not your turn"})
			return

		wait = action_data.get("wait")
		if not wait:
			await self._send_to_client(client_id, {"type": "error", "message": "No wait specified"})
			return

		if wait not in player.wait:
			await self._send_to_client(client_id, {"type": "error", "message": "Specified wait is not valid"})
			return

		engine.liquidation()

		pass

	async def on_dealt(self, match_id: str) -> None:
		"""配牌完了時の処理"""
		wall = [(p.wall, p.hand) for p in self._game_engines[match_id].state.players]
		dora = self._game_engines[match_id].state.round_state.dora_id

		hand_payload = []
		for i, h in enumerate(wall):
			hand_payload.append(
				{
					"client_id": await self.resolve_client_id(match_id, i),
					"wall": h[0],
					"tenpai_examples": h[1],
				}
			)

		await self._broadcast_match_members(
			match_id,
			{
				"type": "wall_deal_completed",
				"dora_id": dora,
				"hands": hand_payload,
			},
		)

	async def on_selected(self, match_id: str) -> None:
		"""手牌選択完了の処理"""
		new_status = [(
			p.hand,
			p.wait,
			p.wall,
			p.skills,
		) for p in self._game_engines[match_id].state.players]

		payload = []

		for i, h in enumerate(new_status):
			payload.append(
				{
					"client_id": await self.resolve_client_id(match_id, i),
					"hand": h[0],
					"wait": h[1],
					"wall": h[2],
					"skills": h[3],
				}
			)

		await self._broadcast_match_members(
			match_id,
			{
				"type": "hand_selection_completed",
				"data": {
					"hands": payload,
				},
			},
		)

	async def on_bet(self, match_id: str) -> None:
		"""掛け金設定完了時の処理"""
		bet = [(p.bet) for p in self._game_engines[match_id].state.players]
		bet_payload = []

		for i, b in enumerate(bet):
			bet_payload.append(
				{
					"client_id": await self.resolve_client_id(match_id, i),
					"bet": b,
				}
			)

		await self._broadcast_match_members(
			match_id,
			{
				"type": "bet_completed",
				"data": {
					"bets": bet_payload,
				},
			},
		)

	async def on_discard_started(self, match_id: str) -> None:
		"""打牌フェーズ開始時の処理"""
		engine = self._game_engines[match_id]
		current_player_idx = engine.state.round_state.current_player_index

		await self._broadcast_match_members(
			match_id,
			{
				"type": "discard_phase_started",
				"data": {
					"first_player":  engine.get_current_player().player_id,
				},
			},
		)

	async def on_discarded(self, match_id: str, player_id: str, tile_id: int) -> None:
		"""打牌実行時の処理"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "discard_completed",
				"data": {
					"player_id": player_id,
					"tile": tile_id,
				},
			},
		)

	async def on_round_start(self, match_id: str) -> None:
		"""ラウンド開始時の処理"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "round_start",
				"round": self._game_engines[match_id].state.round_state.round_number,
			},
		)

	async def on_round_end(self, match_id: str) -> None:
		"""ラウンド終了時の処理"""
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

	async def on_phase_change(self, match_id: str, new_status: RoundStatus) -> None:
		"""ラウンドステータス変更時の処理"""
		await self._broadcast_match_members(
			match_id,
			{
				"type": "phase_change",
				"new_status": new_status.value,
			},
		)

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