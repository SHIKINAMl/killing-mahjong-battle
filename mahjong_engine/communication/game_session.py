import asyncio
import heapq
from typing import Any, Awaitable, Callable, Dict, List, Optional

from ..engine.game_engine import GameEngine
from ..engine.game_state import RoundStatus, SkillType, SKILL_HP_COSTS


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

	async def _send_error(self, client_id: str, message: str) -> None:
		"""クライアントへのエラー通知"""
		try:
			await self._send_to_client(client_id, {"type": "error", "message": message})
		except Exception:
			pass

	async def _respond_to_client(self, client_id: str, payload: Dict[str, Any]) -> bool:
		"""クライアント応答。送信失敗時はエラー通知を試行する。"""
		try:
			await self._send_to_client(client_id, payload)
			return True
		except Exception:
			await self._send_error(client_id, "Failed to deliver response")
			return False

	def _extract_hand_from_wall_indexes(self, wall: List[int], wall_indexes: List[Any]) -> Optional[List[int]]:
		"""壁牌リストの index 配列から手牌リストを組み立てる。"""
		if not isinstance(wall_indexes, list) or not wall_indexes:
			return None

		if not all(isinstance(index, int) for index in wall_indexes):
			return None

		if len(set(wall_indexes)) != len(wall_indexes):
			return None

		for index in wall_indexes:
			if index < 0 or index >= len(wall):
				return None

		return [wall[index] for index in wall_indexes]

	def _convert_tiles_to_wall_indexes(self, wall: List[int], tiles: List[int]) -> Optional[List[int]]:
		"""牌IDの並びを、対応する wall index の並びへ変換する（重複牌対応）。"""
		if not isinstance(tiles, list):
			return None

		used = [False] * len(wall)
		result: List[int] = []

		for tile in tiles:
			found_index = None
			for idx, wall_tile in enumerate(wall):
				if used[idx]:
					continue
				if wall_tile == tile:
					found_index = idx
					break

			if found_index is None:
				return None

			used[found_index] = True
			result.append(found_index)

		return result

	async def start_match(self, match: Any) -> None:
		engine = GameEngine(max_rounds=4)
		engine.initialize_players(match.players)
		original_deal_tiles = engine._deal_tiles
		round_start_done = asyncio.Event()
		dealing_phase_done = asyncio.Event()

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
		engine.on_skill_casted = lambda player_id, skill_type, cost: asyncio.create_task(
			self.on_skill_casted(match.match_id, player_id, skill_type, cost)
		)

		def _on_round_start() -> None:
			round_start_done.clear()
			dealing_phase_done.clear()

			async def _notify_round_start() -> None:
				try:
					await self.on_round_start(match.match_id)
				finally:
					round_start_done.set()

			async def _wait_and_start_deal() -> None:
				await round_start_done.wait()
				await dealing_phase_done.wait()
				original_deal_tiles()

			asyncio.create_task(_notify_round_start())
			asyncio.create_task(_wait_and_start_deal())

		engine.on_round_start = _on_round_start
		engine.on_round_end = lambda: asyncio.create_task(
			self.on_round_end(match.match_id)
		)
		engine.on_game_end = lambda: asyncio.create_task(
			self.on_game_end(match.match_id)
		)

		def _on_phase_change(new_status: RoundStatus) -> None:
			async def _notify_phase_change() -> None:
				try:
					await self.on_phase_change(match.match_id, new_status)
				finally:
					if new_status == RoundStatus.DEALING:
						dealing_phase_done.set()

			asyncio.create_task(_notify_phase_change())

		engine.on_phase_change = _on_phase_change
		engine._deal_tiles = lambda: None

		payload = {
			"type": "game_started",
			"data": {
				"match_id": match.match_id,
				"players": [{"client_id": cid} for cid in match.players],
			},
		}

		await asyncio.gather(
			*(self._respond_to_client(cid, payload) for cid in match.players),
			return_exceptions=True,
		)

		self._game_engines[match.match_id] = engine
		engine.start_game(4)

	async def handle_game_action(self, client_id: str, data: Dict[str, Any]) -> None:
		"""
		クライアントからのゲームアクションを処理
		Args:
			client_id: アクションを送信したクライアントのID
			data: アクションのデータ
		"""
		match_id = self._active_match_by_client.get(client_id)
		if not match_id or match_id not in self._game_engines:
			await self._send_error(client_id, "Not in game")
			return

		engine = self._game_engines[match_id]
		action_type = data.get("action")
		action_data = data.get("data")

		if not action_type:
			await self._send_error(client_id, "No action type specified")
			return
		if not isinstance(action_data, dict):
			await self._send_error(client_id, "Invalid action data")
			return

		action_handlers = {
			"is_tenpai": self._is_tenpai,
			"skill": self._skill,
			"select": self._select,
			"bet": self._bet,
			"discard": self._discard,
			"declare_win": self._declare_win,
		}
		handler = action_handlers.get(action_type)
		if handler is None:
			await self._send_error(client_id, "Unsupported action type")
			return

		try:
			await handler(engine, client_id, action_data)
		except Exception:
			await self._send_error(client_id, "Failed to process action")

	async def _is_tenpai(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""手牌の聴牌判定の処理"""

		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_error(client_id, "Not in hand selection phase")
			return

		wall_indexes = action_data.get("wall_indexes")

		player = engine.get_player_by_id(client_id)

		hand = self._extract_hand_from_wall_indexes(player.wall, wall_indexes)
		if hand is None:
			await self._send_error(client_id, "Invalid wall_indexes")
			return

		waits = engine.get_waits(hand, player)
		if waits is not None:
			await self._respond_to_client(client_id, {
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
			await self._respond_to_client(client_id, {
				"type": "not_tenpai",
				"message": "Hand is not in tenpai",
			})

	async def _skill(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""スキルアクションの処理"""
		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_error(client_id, "Not in hand selection phase")
			return

		raw_skill_type = action_data.get("skillType")
		if not isinstance(raw_skill_type, str):
			await self._send_error(client_id, "No valid skillType provided")
			return

		try:
			skill_type = SkillType(raw_skill_type)
		except ValueError:
			await self._send_error(client_id, "Unsupported skillType")
			return

		player = engine.get_player_by_id(client_id)
		if player is None:
			await self._send_error(client_id, "Player not found")
			return

		cost = SKILL_HP_COSTS.get(skill_type)
		if cost is None:
			await self._send_error(client_id, "Skill cost is not defined")
			return

		if player.health < cost:
			await self._send_error(client_id, "Not enough health")
			return

		if not engine.use_skill(player, skill_type):
			await self._send_error(client_id, "Skill cast failed")
			return

		await self._respond_to_client(client_id, {
			"type": "skill_accepted",
			"data": {
				"skillType": skill_type.value,
				"cost": cost,
				"health": player.health,
			},
		})

	async def _select(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""手牌選択アクションの処理"""
		if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
			await self._send_error(client_id, "Not in hand selection phase")
			return

		player = engine.get_player_by_id(client_id)
		if player is None:
			await self._send_error(client_id, "Player not found")
			return

		wall_indexes = action_data.get("hand")
		hand = self._extract_hand_from_wall_indexes(player.wall, wall_indexes)

		if hand is None:
			await self._send_error(client_id, "Invalid wall_indexes")
			return

		if engine.select_hand(hand, player):

			await self._respond_to_client(client_id, {
				"type": "hand_selection_accepted",
				"data": {
					"hand": player.hand,
					"waits": player.waits,
					"wall": player.wall,
				},
			})

			if all(p.waits for p in engine.state.players):
				engine.selected_hand()

		else:
			await self._send_error(client_id, "Selected hand is not in tenpai or does not have mangan potential")

		"""掛け金設定アクションの処理"""
		# 掛け金の処理は未実装
		if engine.state.round_state.status != RoundStatus.BETTING:
			await self._send_error(client_id, "Not in betting phase")
			return

		bet = action_data.get("bet")

		if bet is None or type(bet) is not int or bet < 0:
			await self._send_error(client_id, "Invalid bet amount")
			return

		player = engine.get_player_by_id(client_id)

		if player: # 本来は賭け可能な金額のチェックをする（未実装）
			player.bet = bet
			await self._respond_to_client(client_id, {
				"type": "bet_accepted",
				"data": {
					"bet": bet,
				},
			})

			if all(p.bet > 0 for p in engine.state.players):
				engine.bet()

	async def _discard(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""打牌アクションの処理"""
		if engine.state.round_state.status != RoundStatus.DISCARD:
			await self._send_error(client_id, "Not in discard phase")
			return

		player = engine.get_current_player()
		if player.player_id != client_id:
			await self._send_error(client_id, "Not your turn")
			return

		if "wall_index" not in action_data:
			await self._send_error(client_id, "No wall_index specified")
			return

		wall_index = action_data["wall_index"]
		if not isinstance(wall_index, int):
			await self._send_error(client_id, "wall_index must be integer")
			return

		if wall_index < 0 or wall_index >= len(player.wall):
			await self._send_error(client_id, "wall_index out of range")
			return

		tile = player.wall[wall_index]

		await self._respond_to_client(client_id, {
			"type": "discard_accepted",
			"data": {
				"wall_index": wall_index,
				"tile": tile,
			},
		})

		engine.discard(client_id, wall_index)

	async def _declare_win(self, engine: GameEngine, client_id: str, action_data: Dict[str, Any]) -> None:
		"""上がり宣言アクションの処理"""
		if engine.state.round_state.status != RoundStatus.DISCARD:
			await self._send_error(client_id, "Not in discard phase")
			return

		player = engine.get_current_player()
		if player.player_id != client_id:
			await self._send_error(client_id, "Not your turn")
			return

		tile = action_data.get("tile")
		if not tile:
			await self._send_error(client_id, "No tile specified")
			return

		if tile not in 	player.waits:
			await self._send_error(client_id, "Specified wait is not valid")
			return

		if engine.liquidation(client_id, player.hand + [tile]):
			await self._respond_to_client(client_id, {
				"type": "declare_win_accepted",
				"data": {
					"tile": tile,
				},
			})
		else:
			await self._send_error(client_id, "Declare win failed")

	async def on_dealt(self, match_id: str) -> None:
		"""配牌完了時の処理"""
		wall = [(p.wall, p.hand) for p in self._game_engines[match_id].state.players]
		dora = self._game_engines[match_id].state.round_state.dora_id

		hand_payload = []
		for i, h in enumerate(wall):
			wall_tiles = h[0]
			tenpai_example_tiles = h[1]
			tenpai_example_indexes = self._convert_tiles_to_wall_indexes(
				wall_tiles,
				tenpai_example_tiles,
			)

			if tenpai_example_indexes is None:
				tenpai_example_indexes = []

			hand_payload.append(
				{
					"client_id": await self.resolve_client_id(match_id, i),
					"wall": wall_tiles,
					"tenpai_examples": tenpai_example_indexes,
				}
			)

		await self._broadcast_match_members(
			match_id,
			{
				"type": "dealing_completed",
				"dora_id": dora,
				"hands": hand_payload,
			},
		)

	async def on_selected(self, match_id: str) -> None:
		"""手牌選択完了の処理"""
		payload = [
			{
				"client_id": await self.resolve_client_id(match_id, i),
				"hand": player.hand,
				"waits": player.waits,
				"wall": player.wall,
				"skills": player.skills,
			}
			for i, player in enumerate(self._game_engines[match_id].state.players)
		]

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
		bet_payload = [
			{
				"client_id": await self.resolve_client_id(match_id, i),
				"bet": b,
			}
			for i, b in enumerate(bet)
		]

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

	async def on_skill_casted(self, match_id: str, player_id: str, skill_type: SkillType, cost: int) -> None:
		"""スキル使用時の処理"""
		player = self._game_engines[match_id].get_player_by_id(player_id)

		await self._broadcast_match_members(
			match_id,
			{
				"type": "skill_casted",
				"data": {
					"player_id": player_id,
					"skillType": skill_type.value,
					"cost": cost,
					"health": player.health if player else None,
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