"""
対局の記録とランキングのテスト（データベースの代わりに MemoryStore を使う）

- ID の発行・名乗り・名前: サーバーが ID と秘密の文字列を発行し、それで名乗って名前を登録・変更する。
  形式のチェック・知らない ID・秘密の文字列が違う名乗りを弾く・ログに秘密の文字列を出さない
- ランキング: 大きい順・同じ値は同じ順位・自分の順位
- 通しの対局: 名乗った 2 人の対人戦を決着まで打つと、記録とランキングに入る。
  CPU 戦・途中で切断した対局は記録だけ残り、ランキングには入らない
- 書き込みに失敗しても、やり直して書く

実行: python -m pytest tests -q
"""
import asyncio
import json
import logging
import uuid

import pytest

from mahjong_engine.communication.websocket_server import WebSocketGameServer
from mahjong_engine.records import service as service_module
from mahjong_engine.records.match_recorder import MatchRecord, PlayerResult
from mahjong_engine.records.service import RecordService, normalize_name
from mahjong_engine.records.store import MemoryStore

# 人間役のボットと、CPU の考える時間を 0 にするフィクスチャ（config_file・zero_think_time）を借りる
from test_cpu_match import _BotSocket, _make_human_bot, _ServerSocket, config_file, zero_think_time  # noqa: F401

SECRET_B = "b" * 32


def _record(players, ranked_mode="public", rounds=3, finished=True):
    return MatchRecord(
        mode=ranked_mode, started_at=0.0, ended_at=1.0, rounds=rounds, end_reason="hp_zero", finished=finished,
        players=[PlayerResult(i, f"C{i}", pid, None, None, 0, points, damage, won)
                 for i, (pid, points, damage, won) in enumerate(players)],
    )


# ========== ID の発行・名乗り・名前 ==========

async def _player(records, name):
    """ID を発行して名前を登録したプレイヤーの ID。"""
    issued = await records.register()
    named, error = await records.set_name(issued.identity, name)
    assert error is None and named.name == name
    return issued.identity.player_id


def test_register_identify_and_set_name():
    async def run():
        records = RecordService(MemoryStore())
        issued = await records.register()
        pid, secret = issued.identity.player_id, issued.secret
        assert str(uuid.UUID(pid)) == pid and 16 <= len(secret) <= 128 and issued.identity.name is None
        assert pid not in records.store.players  # 発行しただけではデータベースに書かない
        assert (await records.register()).identity.player_id != pid  # 発行するたびに別の ID

        # 名乗り: 名前はまだない
        first = await records.identify(pid.upper(), secret)
        assert first.error is None and first.identity.player_id == pid and first.identity.name is None

        # 名前の登録・変更
        assert (await records.identify(pid, SECRET_B)).error == "secret_mismatch"  # 発行しただけの ID でも確かめる
        named, error = await records.set_name(first.identity, "  たろう\n ")
        assert error is None and named.name == "たろう"
        assert records.store.players[pid].name == "たろう"  # 名前を登録して初めて書く
        assert records.store.players[pid].secret_hash != secret  # サーバーにはハッシュだけを残す
        assert (await records.set_name(named, "")) == (None, "invalid_name")
        assert (await records.set_name(named, "x" * 11)) == (None, "invalid_name")  # クライアントと同じ 10 文字まで
        renamed, _ = await records.set_name(named, "　全角の空白　")
        assert renamed.name == "全角の空白"
        assert (await records.identify(pid, secret)).identity.name == "全角の空白"  # 次の接続でも同じ名前

        # 名乗りに名前を付けると、続けて登録する
        assert (await records.identify(pid, secret, "じろう")).identity.name == "じろう"

        # 失敗
        assert (await records.identify("not-a-uuid", secret)).error == "invalid_player_id"
        assert (await records.identify(pid, "short")).error == "invalid_secret"
        assert (await records.identify(str(uuid.uuid4()), secret)).error == "unknown_player"
        assert (await records.identify(pid, SECRET_B)).error == "secret_mismatch"
        assert records.store.players[pid].name == "じろう"

    asyncio.run(run())
    assert normalize_name("a\u0000b") == "ab"


def test_unnamed_players_are_not_ranked():
    """名前を付ける前の対局は、プレイヤーに結びつけない（データベースにプレイヤーがいないので数えない）。"""
    from mahjong_engine.records.match_recorder import MatchRecorder, Seat
    from mahjong_engine.records.service import PlayerIdentity

    recorder = MatchRecorder("public", [
        Seat("C0001", PlayerIdentity("11111111-1111-1111-1111-111111111111", "なまえ")),
        Seat("C0002", PlayerIdentity("22222222-2222-2222-2222-222222222222", None)),
    ])
    record = recorder.build(None, finished=True)
    assert [r.player_id for r in record.players] == ["11111111-1111-1111-1111-111111111111", None]

    async def run():
        records = RecordService(MemoryStore())
        unnamed = (await records.register()).identity.player_id
        assert (await records.my_records(unnamed))["matches"] == 0
        assert (await records.ranking("max_damage", 10, unnamed))["me"] is None

    asyncio.run(run())


def test_issued_ids_are_forgotten_after_a_while(monkeypatch):
    monkeypatch.setattr(service_module, "ISSUED_MAX", 3)

    async def run():
        records = RecordService(MemoryStore())
        first = await records.register()
        for _ in range(3):
            await records.register()
        assert (await records.identify(first.identity.player_id, first.secret)).error == "unknown_player"
        assert len(records._issued) <= 3

    asyncio.run(run())


# ========== ランキング ==========

def test_ranking_order_ties_and_me():
    async def run():
        records = RecordService(MemoryStore())
        ids = [await _player(records, f"P{i}") for i in range(4)]
        records.start()
        records.submit(_record([(ids[0], 5000, 9000, True), (ids[1], 2000, 9000, False)]))
        records.submit(_record([(ids[2], 31000, 1200, True), (None, 0, 0, False)], rounds=7))  # 名乗っていない人
        records.submit(_record([(ids[3], 99999, 99999, True), (ids[0], 0, 0, False)], ranked_mode="cpu"))  # CPU 戦
        records.submit(_record([(ids[3], 88888, 88888, True), (ids[1], 0, 0, False)], finished=False))  # 途中で切断
        await asyncio.wait_for(records._queue.join(), 5)

        damage = await records.ranking("max_damage", 10, ids[1])
        # 同じ値は同じ順位（同じ順位どうしの並びは player_id 順なので、ここでは順番を問わない）
        assert sorted((e["rank"], e["name"], e["value"]) for e in damage["entries"]) == [
            (1, "P0", 9000), (1, "P1", 9000), (3, "P2", 1200),
        ]
        assert damage["me"] == {"rank": 1, "value": 9000}
        points = await records.ranking("match_points", 2, ids[2])
        assert [e["name"] for e in points["entries"]] == ["P2", "P0"] and points["me"]["rank"] == 1
        mine = await records.my_records(ids[0])
        assert mine["matches"] == 1 and mine["wins"] == 1
        assert mine["max_damage"]["value"] == 9000 and mine["match_points"] == {"value": 5000, "rank": 2}
        assert (await records.my_records(ids[3]))["rounds"] == {"value": 0, "rank": None}
        rounds = await records.ranking("rounds", 10, ids[3])
        assert rounds["entries"][0] == {"rank": 1, "name": "P2", "value": 7}
        assert rounds["me"] is None  # CPU 戦・途中で切断した対局しかない人はランキングにいない
        with pytest.raises(ValueError):
            await records.ranking("unknown")
        await records.stop()
        assert len(records.store.matches) == 4  # ランキングに入らない対局も記録は残る

    asyncio.run(run())


def test_failed_writes_are_retried(monkeypatch):
    class FlakyStore(MemoryStore):
        failures = 2

        async def save_match(self, record):
            if self.failures:
                self.failures -= 1
                raise ConnectionError("database is waking up")
            await super().save_match(record)

    monkeypatch.setattr(service_module, "RETRY_DELAYS", (0.01, 0.01, 0.01))

    async def run():
        records = RecordService(FlakyStore())
        records.start()
        records.submit(_record([(None, 0, 0, False), (None, 0, 0, False)]))
        await asyncio.wait_for(records._queue.join(), 5)
        await records.stop()
        return records.store

    store = asyncio.run(run())
    assert store.failures == 0 and len(store.matches) == 1


# ========== 通しの対局 ==========

def _named_bot(join_payload, name):
    """起動時に ID を発行してもらい（register）、名前を登録して（set_name）から join する人間役。"""
    bot = _make_human_bot(join_payload)
    send_raw = bot.send_raw

    async def send_with_identify(payload):
        if payload.get("type") == "join":
            await send_raw({"type": "register"})
            await send_raw({"type": "set_name", "data": {"name": name}})
        await send_raw(payload)

    bot.send_raw = send_with_identify
    return bot


async def _connect(server, bot):
    socket = _ServerSocket(bot.handle_message)
    bot.ws = _BotSocket(server, socket)
    client_id = await server._register_client(socket)
    await server._send_json(socket, {"type": "connected", "data": {"client_id": client_id}})
    return socket


async def _wait(predicate, timeout=120.0):
    async def loop():
        while not predicate():
            await asyncio.sleep(0.05)
    await asyncio.wait_for(loop(), timeout)


def _registered(socket):
    return next(m for m in socket.received if m.get("type") == "registered")["data"]


def test_pvp_match_is_recorded_and_ranked(zero_think_time, caplog):
    async def run():
        server = WebSocketGameServer()
        server.start_housekeeping()
        bots = [
            _named_bot({"type": "join", "data": {"mode": "public"}}, "あかね"),
            _named_bot({"type": "join", "data": {"mode": "public"}}, "そら"),
        ]
        sockets = [await _connect(server, bot) for bot in bots]
        await _wait(lambda: server.game_engines)
        # 2 局で決着させる（最大局数に達したら終わる）
        engine = next(iter(server.game_engines.values()))
        engine.max_rounds = 2
        await _wait(lambda: all(any(m.get("type") == "game_end" for m in s.received) for s in sockets))
        await asyncio.wait_for(server._records._queue.join(), 5)
        ranking = await server._records.ranking("rounds", 10, _registered(sockets[0])["player_id"])
        for socket in sockets:
            await server._unregister_client(socket)
        await server.stop_housekeeping()
        return sockets, server._records.store, ranking

    with caplog.at_level(logging.INFO, logger="mahjong_engine.communication.websocket_server"):
        sockets, store, ranking = asyncio.run(run())

    registered = [_registered(s) for s in sockets]
    ids = [d["player_id"] for d in registered]
    assert registered[0]["persistent"] is False  # データベースなし（メモリ）
    named = [next(m for m in s.received if m.get("type") == "name_set")["data"] for s in sockets]
    assert [d["name"] for d in named] == ["あかね", "そら"]
    started = next(m for m in sockets[0].received if m.get("type") == "game_started")["data"]
    assert sorted(p["name"] for p in started["players"]) == ["あかね", "そら"]

    assert len(store.matches) == 1
    record = store.matches[0]
    assert record.ranked and record.mode == "public" and record.rounds >= 2  # 最大局数は対局が始まってから下げるので、早ければ 3 局目まで進む
    assert {r.player_id for r in record.players} == set(ids)
    assert len(record.round_logs) == record.rounds and len(record.round_logs[0]["players"][0]["wall"]) == 34
    # 先手・両者の打牌の順番（ボルテージ付き）・局の終わりのボルテージ
    log = record.round_logs[0]
    discards = [e for e in log["events"] if e["type"] == "discard"]
    assert discards and log["first_player"] == discards[0]["player"]
    assert all(x["player"] != y["player"] for x, y in zip(discards, discards[1:]))  # 交互に捨てる
    for p in log["players"]:
        assert [e["tile"] for e in discards if e["player"] == p["client_id"]] == p["discards"]
        assert set(p["voltage"]) == {"points", "broken", "multiplier"}
    for r in record.players:
        stats = store.players[r.player_id].stats
        assert stats["matches"] == 1 and stats["max_rounds"] == record.rounds
        assert stats["max_damage"] == r.max_damage and stats["max_match_points"] == r.match_points
    assert {e["name"] for e in ranking["entries"]} == {"あかね", "そら"} and ranking["me"]["value"] == record.rounds

    # 発行した秘密の文字列はログに出さない（送ったメッセージのログでも伏せる）
    assert all(d["secret"] not in caplog.text for d in registered)


def test_cpu_and_cancelled_matches_are_not_ranked(zero_think_time):
    async def run():
        server = WebSocketGameServer()
        server.start_housekeeping()
        bot = _named_bot({"type": "join", "data": {"mode": "cpu", "difficulty": "easy"}}, "ひかり")
        socket = await _connect(server, bot)
        await _wait(lambda: any(m.get("type") == "round_end" for m in socket.received) or
                    any(m.get("type") == "game_end" for m in socket.received))
        await server._unregister_client(socket)  # 途中で切断
        await asyncio.wait_for(server._records._queue.join(), 5)
        await server.stop_housekeeping()
        return server._records.store, _registered(socket)["player_id"]

    store, pid = asyncio.run(run())
    assert len(store.matches) == 1
    record = store.matches[0]
    assert record.mode == "cpu" and not record.ranked
    human, cpu = record.players
    assert human.player_id == pid and human.name == "ひかり" and cpu.cpu_difficulty == "easy" and cpu.player_id is None
    assert record.round_logs  # 学習用の局の記録は残る
    assert store.players[pid].stats["matches"] == 0  # ランキングには入らない


def test_register_once_per_connection_and_set_name_needs_identity():
    async def run():
        server = WebSocketGameServer()
        socket = _ServerSocket(lambda _m: None)
        client_id = await server._register_client(socket)
        await server._handle_set_name(client_id, {"name": "はやい"})  # まだ名乗っていない
        await server._handle_register(client_id)
        await server._handle_register(client_id)  # 2 回目は発行しない
        await server._handle_set_name(client_id, {"name": "あおい"})
        await server._handle_set_name(client_id, {"name": "x" * 11})
        await server._unregister_client(socket)
        return socket.received, server._records.store

    received, store = asyncio.run(run())
    types = [m.get("type") for m in received]
    assert types[-5:] == ["set_name_failed", "registered", "register_failed", "name_set", "set_name_failed"]
    failures = [m["data"]["reason"] for m in received if m.get("type", "").endswith("_failed")]
    assert failures == ["not_identified", "already_registered", "invalid_name"]
    assert len(store.players) == 1 and next(iter(store.players.values())).name == "あおい"


def test_clean_dsn_drops_parameters_asyncpg_does_not_know():
    from mahjong_engine.records.store import clean_dsn

    dsn = "postgresql://u:p@ep-x-pooler.ap-southeast-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"
    assert clean_dsn(dsn) == "postgresql://u:p@ep-x-pooler.ap-southeast-1.aws.neon.tech/neondb?sslmode=require"
    assert clean_dsn("  postgresql://u:p@h/db  ") == "postgresql://u:p@h/db"
