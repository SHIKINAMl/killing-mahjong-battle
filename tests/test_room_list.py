"""募集中のフレンドマッチの部屋の一覧（rooms）。メインメニューのルームマッチング画面の右の一覧に出す。"""
import asyncio
import json

from mahjong_engine.communication.websocket_server import WebSocketGameServer


class _Socket:
    def __init__(self):
        self.received: list[dict] = []

    async def send(self, raw):
        self.received.append(json.loads(raw))

    def last(self, kind):
        return [m for m in self.received if m.get("type") == kind][-1]


def test_rooms_lists_open_private_rooms_newest_first_without_own():
    async def run():
        server = WebSocketGameServer()
        host_a, host_b, guest = _Socket(), _Socket(), _Socket()
        for s in (host_a, host_b, guest):
            await server._register_client(s)

        await server._handle_message(host_a, json.dumps({"type": "join", "data": {"mode": "private_create"}}))
        await server._handle_message(host_b, json.dumps({"type": "join", "data": {"mode": "private_create"}}))
        code_a = server._private_password_by_client[server._client_id_by_socket[host_a]]
        code_b = server._private_password_by_client[server._client_id_by_socket[host_b]]

        await server._handle_message(guest, json.dumps({"type": "rooms"}))
        listed_for_guest = [r["code"] for r in guest.last("rooms")["data"]["rooms"]]

        # 自分の部屋は出さない
        await server._handle_message(host_a, json.dumps({"type": "rooms"}))
        listed_for_a = [r["code"] for r in host_a.last("rooms")["data"]["rooms"]]

        # 一覧のコードで入ると部屋は消え、一覧からも消える
        await server._handle_message(guest, json.dumps({"type": "join", "data": {"mode": "private_join", "password": code_b}}))
        other = _Socket()
        await server._register_client(other)
        await server._handle_message(other, json.dumps({"type": "rooms"}))
        listed_after = [r["code"] for r in other.last("rooms")["data"]["rooms"]]

        # 部屋を作った人が抜けたら一覧から消える
        await server._unregister_client(host_a)
        await server._handle_message(other, json.dumps({"type": "rooms"}))
        listed_after_leave = other.last("rooms")["data"]["rooms"]

        for s in (host_b, guest, other):
            await server._unregister_client(s)
        await asyncio.sleep(0)
        return code_a, code_b, listed_for_guest, listed_for_a, listed_after, listed_after_leave

    code_a, code_b, for_guest, for_a, after, after_leave = asyncio.run(run())
    assert for_guest == [code_b, code_a]
    assert for_a == [code_b]
    assert after == [code_a]
    assert after_leave == []
