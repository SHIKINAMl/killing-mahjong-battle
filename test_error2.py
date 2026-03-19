import asyncio
import traceback
from mahjong_engine.engine.game_engine import GameEngine
from mahjong_engine.communication.game_session import GameSession

async def main():
    engine = GameEngine()
    engine.initialize_players(["C0001", "C0002"])
    
    # Force mock dealing
    p1 = engine.state.players[0]
    p2 = engine.state.players[1]
    
    # Both players need a valid winning hand wait
    # 0~8: 1m~9m
    # 9~17: 1p~9p
    # 18~26: 1s~9s
    # Tenpai hand: [0,0,0, 1,2,3, 4,5,6, 7,8,9, 10] -> wait is 10
    good_hand1 = [0,0,0, 1,2,3, 4,5,6, 7,8,9, 10]
    good_hand2 = [0,0,0, 1,2,3, 4,5,6, 7,8,9, 10]
    
    p1.wall = good_hand1 + [10]
    p2.wall = good_hand2 + [10]
    
    engine.state.round_state.status = __import__("mahjong_engine.engine.game_state", fromlist=["RoundStatus"]).RoundStatus.HAND_SELECTION

    session = GameSession(
        lock=asyncio.Lock(),
        matches={"M0001": type("Match", (), {"players": ["C0001", "C0002"], "match_id": "M0001"})},
        active_match_by_client={"C0001": "M0001", "C0002": "M0001"},
        game_engines={"M0001": engine},
        available_match_numbers=[],
        send_to_client=mock_send,
        broadcast_match_members=mock_send_broadcast
    )
    
    try:
        await session._selected(engine, "C0001", {"hand": good_hand1})
        await session._selected(engine, "C0002", {"hand": good_hand2})
    except Exception as e:
        traceback.print_exc()

async def mock_send(cid, msg):
    print(f"To {cid}: {msg}")
async def mock_send_broadcast(mid, msg):
    print(f"Broadcast {mid}: {msg}")

asyncio.run(main())
