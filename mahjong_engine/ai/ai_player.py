"""
AI プレイヤー（骨組み）
"""
from ..engine.game_state import PlayerState, RoundStatus


class AIPlayer:
    """AI プレイヤー"""

    def __init__(self, player_state: PlayerState):
        """
        AI プレイヤーを初期化

        Args:
            player_state: プレイヤーの状態
        """
        self.player = player_state

    def get_action(self, game_state) -> dict:
        """
        ゲーム状態に基づいてアクションを決定

        Args:
            game_state: GameState インスタンス

        Returns:
            アクション辞書（例: {"action": "discard", "tile": 0}）
        """
        # 中身は未実装
        # 実装時は、評価関数に基づいて最善のアクションを選択
        return {"action": "pass"}

    def on_round_start(self):
        """局開始時のコールバック"""
        pass

    def on_discard_request(self):
        """打牌のリクエスト"""
        # 捨てる牌を決定（未実装）
        pass

    def on_round_end(self):
        """局終了時のコールバック"""
        pass
