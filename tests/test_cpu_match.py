"""
CPU 対戦のテスト

- 設定ファイル: 読み込み・extends・不正値の差し戻し・壊れたときの引き継ぎ
- エンジン: とくしゅの特典（コスト割引・役強化2つ同時・初期透視・初期役強化）
- 考え方: 打牌・掛け金がルールの範囲に収まること
- 通しの対局: opponent_bot.py を人間役にして、WebSocket の JSON だけで CPU と対局する

実行: python -m pytest tests -q
"""
import asyncio
import json
import logging
import random
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from mahjong_engine.ai import cpu_config  # noqa: E402
from mahjong_engine.ai.cpu_config import create_cpu_profile, load_cpu_config  # noqa: E402
from mahjong_engine.ai.observation import PublicInfo, build_observation  # noqa: E402
from mahjong_engine.ai.rule_brain import RuleBrain, tile_kind  # noqa: E402
from mahjong_engine.communication.game_session import GameSession  # noqa: E402
from mahjong_engine.communication.websocket_server import WebSocketGameServer  # noqa: E402
from mahjong_engine.engine.game_engine import GameEngine  # noqa: E402
from mahjong_engine.engine.game_state import RoundStatus, SkillType  # noqa: E402

ZERO_THINK_TIME = {key: [0, 0] for key in ("hand_selection", "skill", "bet", "discard", "agari", "next_round")}


@pytest.fixture
def config_file(tmp_path, monkeypatch):
    """CPU_CONFIG_PATH を一時ファイルに向け、中身を書き込む関数を返す。"""
    path = tmp_path / "cpu_config.json"
    monkeypatch.setenv(cpu_config.CONFIG_PATH_ENV, str(path))
    monkeypatch.setattr(cpu_config, "_last_good", None)

    def write(content):
        path.write_text(content if isinstance(content, str) else json.dumps(content, ensure_ascii=False), encoding="utf-8")

    return write


@pytest.fixture
def zero_think_time(config_file):
    config_file({d: {"think_time": ZERO_THINK_TIME} for d in cpu_config.DIFFICULTIES})


# ========== 設定ファイル ==========

def test_packaged_config_is_valid(monkeypatch, caplog):
    monkeypatch.delenv(cpu_config.CONFIG_PATH_ENV, raising=False)
    with caplog.at_level(logging.WARNING):
        config = load_cpu_config()
    assert not [r for r in caplog.records if "CPU 設定" in r.getMessage()]
    assert config["special"]["brain"] == "normal"
    assert config["special"]["perks"] == {
        "initial_perspective_count": 7,
        "opening_boost_count": 3,
        "boost_hand_targets": 2,
        "skill_cost_rate": 0.6,
    }
    # つよいは学習済みモデルの用意ができるまで、ふつうの考え方で打つ
    assert config["hard"]["brain"] == "model"
    assert config["hard"]["discard"] == config["normal"]["discard"]


def test_packaged_config_matches_builtin_defaults():
    """同梱の cpu_config.json と、読めなかったときに使う既定値（cpu_config.py）が揃っていること。"""
    raw = json.loads(cpu_config.DEFAULT_CONFIG_PATH.read_text(encoding="utf-8"))
    resolved = cpu_config._with_model_path(cpu_config._resolve(raw, cpu_config._BUILTIN_RESOLVED))
    assert resolved == cpu_config._BUILTIN_RESOLVED


def test_extends_and_invalid_values(config_file, caplog):
    config_file({
        "normal": {"bet": {"health_lead_factor": 0.5}, "hand_selection": {"candidate_limit": "many"}},
        "special": {"extends": "normal", "perks": {"skill_cost_rate": -1}},
    })
    with caplog.at_level(logging.WARNING):
        config = load_cpu_config()
    assert config["special"]["bet"]["health_lead_factor"] == 0.5  # normal から引き継ぐ
    assert config["normal"]["hand_selection"]["candidate_limit"] == 40  # 型違いは既定値へ
    assert config["special"]["perks"]["skill_cost_rate"] == 0.6  # 範囲外は、その難易度の既定値へ
    messages = " ".join(r.getMessage() for r in caplog.records)
    assert "candidate_limit" in messages and "skill_cost_rate" in messages


def test_broken_file_keeps_last_good(config_file):
    config_file({"normal": {"bet": {"health_lead_factor": 0.7}}})
    assert load_cpu_config()["normal"]["bet"]["health_lead_factor"] == 0.7
    config_file("{ broken json")
    assert load_cpu_config()["normal"]["bet"]["health_lead_factor"] == 0.7


def test_unknown_difficulty():
    with pytest.raises(ValueError):
        create_cpu_profile("ultra")


# ========== エンジン（とくしゅの特典） ==========

def _engine_with_perks(**perks) -> GameEngine:
    engine = GameEngine()
    engine.initialize_players(["C0001", "CPU0001"])
    cpu = engine.get_player_by_id("CPU0001")
    for key, value in perks.items():
        setattr(cpu, key, value)
    return engine


def test_skill_cost_rate():
    engine = _engine_with_perks(skill_cost_rate=0.6)
    cpu = engine.get_player_by_id("CPU0001")
    human = engine.get_player_by_id("C0001")
    assert engine.get_skill_cost(cpu, SkillType.MULLIGAN) == 720
    assert engine.get_skill_cost(cpu, SkillType.PERSPECTIVE) == 900
    assert engine.get_skill_cost(cpu, SkillType.BOOST_HAND) == 6000
    assert engine.get_skill_cost(cpu, SkillType.ASSAULT) == 1800
    assert engine.get_skill_cost(human, SkillType.MULLIGAN) == 1200


def test_boost_hand_two_targets():
    engine = _engine_with_perks(boost_hand_targets=2, skill_cost_rate=0.6)
    cpu = engine.get_player_by_id("CPU0001")
    cpu.health = 30000
    assert engine.use_skill(cpu, SkillType.BOOST_HAND, yaku_names=["平和", "平和"]) == 6000
    assert cpu.boost_hand_bonus == {"平和": 2}
    assert cpu.health == 24000  # 2つ強化してもコストは1回分
    assert engine.use_skill(cpu, SkillType.BOOST_HAND, yaku_names=["断么九", "一盃口"]) == 6000
    assert cpu.boost_hand_bonus == {"平和": 2, "断么九": 1, "一盃口": 1}
    # 3つ・不正な役名・立直は失敗し、HP は減らない
    assert engine.use_skill(cpu, SkillType.BOOST_HAND, yaku_names=["平和", "平和", "平和"]) is None
    assert engine.use_skill(cpu, SkillType.BOOST_HAND, yaku_names=["平和", "存在しない役"]) is None
    assert engine.use_skill(cpu, SkillType.BOOST_HAND, yaku_names=["立直"]) is None
    assert cpu.health == 18000


def test_human_cannot_boost_two():
    engine = _engine_with_perks()
    human = engine.get_player_by_id("C0001")
    assert engine.use_skill(human, SkillType.BOOST_HAND, yaku_names=["平和", "平和"]) is None
    assert engine.use_skill(human, SkillType.BOOST_HAND, yaku_name="平和") == 10000
    assert human.boost_hand_bonus == {"平和": 1}


def test_initial_perspective_on_every_deal():
    engine = _engine_with_perks(initial_perspective_count=7)
    engine.start_game(5)  # コールバックなしなので、配牌まで同期で進む
    human = engine.get_player_by_id("C0001")
    cpu = engine.get_player_by_id("CPU0001")
    assert len(human.exposed_hand_indexes) == 7
    assert not cpu.exposed_hand_indexes  # 人間には初期透視なし
    reveals = engine.get_initial_perspective_reveals()
    assert reveals == [{"player_id": "CPU0001", "target_id": "C0001", "exposed_indexes": sorted(human.exposed_hand_indexes)}]
    assert cpu.health == 20000  # 無料

    # 次の局でも新しく7枚公開され、特典は引き継がれる
    engine.end_round(is_draw=True)
    engine.confirm_next_round("C0001")
    engine.confirm_next_round("CPU0001")
    assert engine.get_player_by_id("CPU0001").initial_perspective_count == 7
    assert len(engine.get_player_by_id("C0001").exposed_hand_indexes) == 7


def test_opening_boosts_are_distinct():
    engine = _engine_with_perks(opening_boost_count=3)
    session = GameSession(asyncio.Lock(), {}, {}, {}, [], None, None)
    for _ in range(20):
        for player in engine.state.players:
            player.boost_hand_bonus = {}
        assigned = session._apply_opening_boosts(engine)
        cpu_boosts = [b["yaku_name"] for b in assigned if b["client_id"] == "CPU0001"]
        human_boosts = [b for b in assigned if b["client_id"] == "C0001"]
        assert len(cpu_boosts) == 3 and len(set(cpu_boosts)) == 3
        assert all(b["bonus_han"] == 1 for b in assigned)
        assert len(human_boosts) == 1


# ========== 考え方 ==========

def _discard_phase_engine() -> GameEngine:
    engine = _engine_with_perks()
    engine.start_game(5)
    for player in engine.state.players:
        assert engine.select_hand(player.hand, player)
    engine.state.round_state.status = RoundStatus.DISCARD
    return engine


@pytest.mark.parametrize("level", ["easy", "normal"])
def test_discard_never_uses_hand_and_prefers_safe(level):
    config = load_cpu_config()["easy" if level == "easy" else "normal"]
    for seed in range(10):
        random.seed(seed)
        engine = _discard_phase_engine()
        cpu = engine.get_player_by_id("CPU0001")
        human = engine.get_player_by_id("C0001")
        brain = RuleBrain(level, config, random.Random(seed))

        # 相手が捨てた種類と同じ牌が自分の残りにあれば、それを選ぶ（自分の待ちでない限り）
        rest = [i for i in range(len(cpu.wall)) if i not in cpu.hand]
        target = next(
            (i for i in rest if tile_kind(cpu.wall[i]) not in {tile_kind(w) for w in cpu.waits}),
            None,
        )
        if target is not None:
            human.discards.append(cpu.wall[target])

        obs = build_observation(engine, "CPU0001", PublicInfo())
        index = brain.choose_discard(obs)
        assert index not in cpu.hand
        if target is not None:
            assert tile_kind(cpu.wall[index]) in {tile_kind(t) for t in human.discards}


def test_bet_follows_rules():
    engine = _discard_phase_engine()
    engine.state.round_state.status = RoundStatus.BETTING
    cpu = engine.get_player_by_id("CPU0001")
    for level, name in (("easy", "easy"), ("normal", "normal")):
        brain = RuleBrain(level, load_cpu_config()[name], random.Random(0))
        for health in (20000, 3000, 250):
            cpu.health = health
            obs = build_observation(engine, "CPU0001", PublicInfo())
            for strength_hand in (None, brain.evaluate_hand(obs, tuple(cpu.hand))):
                bet = brain.choose_bet(obs, strength_hand)
                max_bet, unit = engine.get_bet_rule(cpu)
                assert bet % unit == 0 and unit <= bet <= max(unit, min(max_bet, health))


# ========== 通しの対局（WebSocket の JSON だけで） ==========

class _ServerSocket:
    """サーバーから見た人間の WebSocket。送られた JSON を人間役のボットへ渡す。"""

    def __init__(self, deliver):
        self._deliver = deliver
        self.received: list[dict] = []

    async def send(self, raw: str) -> None:
        payload = json.loads(raw)
        self.received.append(payload)
        asyncio.get_running_loop().create_task(self._deliver(payload))


class _BotSocket:
    """人間役のボットから見た WebSocket。送った JSON をサーバーへ渡す。"""

    def __init__(self, server, server_socket):
        self._server = server
        self._server_socket = server_socket

    async def send(self, raw: str) -> None:
        await self._server._handle_message(self._server_socket, raw)


def _make_human_bot(join_payload):
    sys.path.insert(0, str(ROOT))
    import opponent_bot

    class HumanBot(opponent_bot.OpponentBot):
        """opponent_bot を人間役にする。join の内容を差し替え、打牌の間を詰める。"""

        async def send_raw(self, payload):
            if payload.get("type") == "join":
                payload = join_payload
            await super().send_raw(payload)

        async def _delayed_discard(self, delay: float = 0.8):
            await super()._delayed_discard(delay=0.01)

    logger = opponent_bot.BotLogger(Path("NUL") if sys.platform == "win32" else Path("/dev/null"))
    logger.log = lambda message: None
    return HumanBot(select_delay=0.0, bet_delay=0.0, logger=logger)


async def _play_cpu_match(difficulty: str, rounds: int, timeout: float = 120.0):
    server = WebSocketGameServer()
    bot = _make_human_bot({"type": "join", "data": {"mode": "cpu", "difficulty": difficulty}})
    server_socket = _ServerSocket(bot.handle_message)
    bot.ws = _BotSocket(server, server_socket)

    client_id = await server._register_client(server_socket)
    await server._send_json(server_socket, {"type": "connected", "data": {"client_id": client_id}})

    async def wait_for_rounds():
        while True:
            round_ends = [m for m in server_socket.received if m.get("type") == "round_end"]
            if len(round_ends) >= rounds or any(m.get("type") == "game_end" for m in server_socket.received):
                return
            await asyncio.sleep(0.05)

    await asyncio.wait_for(wait_for_rounds(), timeout=timeout)
    stats_before = server.get_stats()
    await server._unregister_client(server_socket)
    await asyncio.sleep(0)  # キャンセルしたタスクを片付けさせる
    return server_socket.received, stats_before, server.get_stats()


@pytest.mark.parametrize("difficulty", ["easy", "normal", "hard", "special"])
def test_cpu_match_over_websocket(difficulty, zero_think_time, caplog):
    with caplog.at_level(logging.INFO, logger="mahjong_engine.ai.cpu_controller"):
        received, stats_before, stats = asyncio.run(_play_cpu_match(difficulty, rounds=3))

    types = [m.get("type") for m in received]
    assert "matching_waiting" not in types  # 待たずに始まる

    started = next(m for m in received if m.get("type") == "game_started")["data"]
    assert started["mode"] == "cpu"
    human, cpu = started["players"]
    assert human["is_cpu"] is False and "cpu" not in human
    assert cpu["is_cpu"] is True and cpu["client_id"].startswith("CPU")
    assert cpu["cpu"]["difficulty"] == difficulty
    assert ("perks" in cpu["cpu"]) == (difficulty == "special")

    # CPU が実際に打っている
    cpu_discards = [m for m in received if m.get("type") == "discard_completed" and m["data"]["player_id"] == cpu["client_id"]]
    assert cpu_discards
    assert types.count("round_end") >= 3 or "game_end" in types

    boosts = next(m for m in received if m.get("type") == "opening_boost_assigned")["data"]["boosts"]
    cpu_boosts = [b["yaku_name"] for b in boosts if b["client_id"] == cpu["client_id"]]
    perk_reveals = [
        m["data"] for m in received
        if m.get("type") == "skill_casted" and m["data"].get("source") == "cpu_perk"
    ]
    if difficulty == "special":
        assert len(cpu_boosts) == 3 and len(set(cpu_boosts)) == 3
        assert len(perk_reveals) >= 3  # 毎局
        for reveal in perk_reveals:
            assert reveal["cost"] == 0 and reveal["skillType"] == "perspective"
            assert len(reveal["exposedHandIndexesByPlayer"][human["client_id"]]) == 7
    else:
        assert len(cpu_boosts) == 1
        assert not perk_reveals

    # 通常のスキル使用には source: "skill" が付く
    for m in received:
        if m.get("type") == "skill_casted" and m["data"]["player_id"] == cpu["client_id"] and m["data"]["source"] == "skill":
            assert m["data"]["skillType"] != "special_victory"

    assert not [r for r in caplog.records if "諦めます" in r.getMessage()]

    # 人間が切断したら CPU も含めて片付く
    # 3局より前に決着した（game_end）場合は、その時点でマッチが片付いている
    assert stats_before["cpu_controllers"] == (0 if "game_end" in types else 1)
    assert stats["matches"] == 0 and stats["cpu_controllers"] == 0 and stats["active_match_clients"] == 0
    assert stats["waiting_queue"] == 0  # CPU は待ち行列へ戻らない


def test_cpu_join_errors(zero_think_time):
    async def run():
        server = WebSocketGameServer()
        received = []

        class Socket:
            async def send(self, raw):
                received.append(json.loads(raw))

        socket = Socket()
        await server._register_client(socket)
        await server._handle_message(socket, json.dumps({"type": "join", "data": {"mode": "cpu", "difficulty": "ultra"}}))
        await server._handle_message(socket, json.dumps({"type": "join", "data": {"mode": "cpu"}}))
        await server._handle_message(socket, json.dumps({"type": "join"}))  # 公開待ち行列へ
        await server._handle_message(socket, json.dumps({"type": "join", "data": {"mode": "cpu", "difficulty": "easy"}}))
        await server._unregister_client(socket)
        return received

    received = asyncio.run(run())
    errors = [m["message"] for m in received if m.get("type") == "error"]
    assert errors == [
        "Unsupported difficulty: ultra",
        "Unsupported difficulty: None",
        "Already in matchmaking queue",
    ]
