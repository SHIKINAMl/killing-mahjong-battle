"""
ゲームエンジン全体の使用例
"""
from ..engine.game_engine import GameEngine
from ..engine.game_state import GameStatus


def main():
    """基本的な使用例"""

    # 1. エンジン初期化
    engine = GameEngine(num_players=2)

    # 2. コールバック設定
    def on_status_change(status: GameStatus):
        print(f"[Status Change] {status.value}")

    def on_round_start(round_state):
        print(f"[Round Start] 第{round_state.round_number}局")

    def on_round_end():
        print(f"[Round End] 局が終了しました")

    def on_game_end():
        print("[Game End] ゲーム終了")
        print(f"最終スコア: {engine.get_game_state()['players']}")

    engine.on_status_change = on_status_change
    engine.on_round_start = on_round_start
    engine.on_round_end = on_round_end
    engine.on_game_end = on_game_end

    # 3. プレイヤー初期化
    engine.initialize_players()

    # 4. ゲームループ実行
    engine.run_game_loop(max_rounds=1)

    # 5. ゲームログ確認
    print("\n--- Game Log ---")
    for log in engine.state.game_log:
        print(f"{log['event']}: {log['data']}")


if __name__ == "__main__":
    main()
