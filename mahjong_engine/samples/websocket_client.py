import asyncio
import json
from typing import Any

import websockets


def parse_int_list(text: str) -> list[int]:
    if not text.strip():
        return []
    return [int(token.strip()) for token in text.split(",") if token.strip()]


def build_action_message(command: str) -> dict[str, Any] | None:
    parts = command.strip().split(maxsplit=1)
    if not parts:
        return None

    name = parts[0].lower()
    args = parts[1] if len(parts) > 1 else ""

    if name == "join":
        return {"type": "join"}

    if name == "ping":
        return {"type": "ping"}

    if name == "tenpai":
        hand = parse_int_list(args)
        return {
            "type": "action",
            "action": "is_tenpai",
            "data": {"hand": hand},
        }

    if name == "select":
        hand = parse_int_list(args)
        return {
            "type": "action",
            "action": "select",
            "data": {"hand": hand},
        }

    if name == "bet":
        return {
            "type": "action",
            "action": "bet",
            "data": {"bet": int(args.strip())},
        }

    if name == "discard":
        return {
            "type": "action",
            "action": "discard",
            "data": {"tile": int(args.strip())},
        }

    if name == "win":
        return {
            "type": "action",
            "action": "declare_win",
            "data": {"tile": int(args.strip())},
        }

    return None


def print_help() -> None:
    print("\n=== Commands ===")
    print("join")
    print("ping")
    print("tenpai 1,2,3,...")
    print("select 1,2,3,...")
    print("bet 100")
    print("discard 12")
    print("win 12")
    print("help")
    print("exit")


async def sender(ws) -> None:
    print_help()
    while True:
        raw = await asyncio.to_thread(input, "> ")
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
                print("Unknown command. type 'help'")
                continue

            await ws.send(json.dumps(payload, ensure_ascii=False))
        except ValueError:
            print("Invalid number format")
        except Exception as exc:
            print(f"Send error: {exc}")


async def receiver(ws) -> None:
    async for message in ws:
        try:
            data = json.loads(message)
        except json.JSONDecodeError:
            print(f"[RAW] {message}")
            continue

        msg_type = data.get("type")
        if msg_type == "connected":
            client_id = data.get("data", {}).get("client_id")
            print(f"[connected] client_id={client_id}")
        elif msg_type == "phase_change":
            phase = data.get("new_status")
            print(f"[phase_change] {phase}")
        elif msg_type == "error":
            print(f"[error] {data.get('message')}")
        else:
            print(json.dumps(data, ensure_ascii=False))


async def main() -> None:
    uri = "ws://localhost:8765"
    print(f"Connecting to {uri} ...")
    async with websockets.connect(uri) as ws:
        await asyncio.gather(sender(ws), receiver(ws))


if __name__ == "__main__":
    asyncio.run(main())
