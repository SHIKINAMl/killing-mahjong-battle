"""
WebSocket サーバーの使用例（2人対戦）
"""
import asyncio
from ..engine.game_engine import GameEngine
from ..communication.websocket_server import WebSocketGameServer


async def example_1v1_battle():
    """2人対戦の基本的な流れをシミュレート"""

    # 1. エンジンを2人対戦用に初期化
    engine = GameEngine(num_players=2)
    engine.initialize_players()

    # 2. WebSocket サーバーを初期化
    ws_server = WebSocketGameServer(engine)

    print("=" * 60)
    print("2人対戦 WebSocket API サーバー シミュレーション")
    print("=" * 60)

    # 3. クライアント接続をシミュレート
    print("\n[接続フェーズ]")

    # ダミーのWebSocketオブジェクト（実装時は実のWebSocketになる）
    class DummyWebSocket:
        def __init__(self, player_id):
            self.player_id = player_id

    # プレイヤー1が接続
    await ws_server.register_client(
        WebSocketGameServer.PLAYER_1,
        DummyWebSocket(0)
    )
    print("✓ プレイヤー1が接続しました")

    # プレイヤー2が接続
    await ws_server.register_client(
        WebSocketGameServer.PLAYER_2,
        DummyWebSocket(1)
    )
    print("✓ プレイヤー2が接続しました")

    # 4. ゲーム開始
    print("\n[ゲーム開始]")
    engine.start_game()

    # 5. ゲーム状態をブロードキャスト
    await ws_server.broadcast_state()

    # 6. プレイヤーアクションのシミュレーション
    print("\n[プレイヤーアクション]")

    # プレイヤー1がアクション
    print("\nプレイヤー1: 牌を打つ")
    await ws_server.handle_player_action(0, {"action": "discard", "tile": 3})

    # プレイヤー2がアクション
    print("\nプレイヤー2: 牌を打つ")
    await ws_server.handle_player_action(1, {"action": "discard", "tile": 5})

    # 7. 状態更新をブロードキャスト
    print("\n[状態更新]")
    await ws_server.broadcast_state()

    # 8. アクション要求
    print("\n[アクション要求]")
    await ws_server.request_player_action(0, ["discard", "fold"])

    print("\n" + "=" * 60)
    print("シミュレーション完了")
    print("=" * 60)


if __name__ == "__main__":
    asyncio.run(example_1v1_battle())
