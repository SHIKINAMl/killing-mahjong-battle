"""
麻雀ゲームエンジン
"""
from typing import Callable, Optional
from .game_state import GameState, GameStatus, RoundStatus, PlayerStatus, PlayerState, RoundState


class GameEngine:
    """麻雀ゲームエンジン"""

    def __init__(self, num_players: int = 2):
        """
        ゲームエンジンを初期化

        Args:
            num_players: プレイヤー数（デフォルト2人）
        """

        self.state = GameState()
        self.num_players = num_players
        self.target_status = GameStatus.GAME_END  # 目標ステータス

        # 各種コールバック
        self.on_status_change: Optional[Callable[[GameStatus], None]] = None
        self.on_round_start: Optional[Callable[[RoundState], None]] = None
        self.on_round_end: Optional[Callable[[], None]] = None
        self.on_game_end: Optional[Callable[[], None]] = None

    def initialize_players(self):
        """プレイヤーを初期化"""

        self.state.players = [
            PlayerState(
                player_id=i,
                health=0,
                status=PlayerStatus.WAITING
            )
            for i in range(self.num_players)
        ]

        self.state.add_log("players_initialized", {
            "players": [p.player_id for p in self.state.players]
        })

    def start_game(self):
        """ゲームを開始"""

        if self.state.status != GameStatus.WAITING:
            raise RuntimeError("ゲームはすでに開始されています")

        self._set_status(GameStatus.ROUND_START)
        self.state.add_log("game_started", {})

    def run_game_loop(self, max_rounds: int = 4):
        """
        ゲームループを実行

        Args:
            max_rounds: 最大局数
        """

        if self.state.status == GameStatus.WAITING:
            self.start_game()

        round_count = 0
        while round_count < max_rounds:
            if self.state.status == GameStatus.ROUND_START:
                self._start_round()
            elif self.state.status == GameStatus.PLAYING:
                self._play_round()
            elif self.state.status == GameStatus.ROUND_END:
                self._end_round()
                round_count += 1
            else:
                break

        self._set_status(GameStatus.GAME_END)
        self._on_game_end()

    def _start_round(self):
        """局を開始"""

        self.state.round_state.status = RoundStatus.DEALING
        self._set_status(GameStatus.PLAYING)

        self.state.add_log("round_started", {
            "round": self.state.round_state.round_number,
        })

        if self.on_round_start:
            self.on_round_start(self.state.round_state)

    def _play_round(self):
        """
        局をプレイ
        （中身は未実装、AI/APIが駆動するまでプレイ中のままループ）
        """

        # ここで実際の麻雀ロジックが実装
        # 現在は骨組みなので、ここで局を進める必要あり
        # AIプレイヤーかWebSocket APIでの入力を待ち

        pass

    def _end_round(self) -> bool:
        """
        局を終了
        （中身は未実装、現状は全て流局として終了処理）
        """

        self.state.round_state.status = RoundStatus.DISCARD  # 一旦全て流局とする

        # 次の局に遷移
        self.state.round_state.round_number += 1

        self.state.add_log("round_ended", {
            "round": self.state.round_state.round_number - 1,
        })

        if self.on_round_end:
            self.on_round_end()

        # 次の局が必要なら ROUND_START に戻す
        # ゲームループで判定される
        self._set_status(GameStatus.ROUND_START)

    def _on_game_end(self):
        """ゲーム終了時の処理"""

        self.state.add_log("game_ended", {
            "final_scores": {p.player_id: p.health for p in self.state.players}
        })

        if self.on_game_end:
            self.on_game_end()

    def _set_status(self, new_status: GameStatus):
        """ゲームステータスを変更"""

        if self.state.status != new_status:
            old_status = self.state.status
            self.state.status = new_status

            self.state.add_log("status_changed", {
                "from": old_status.value,
                "to": new_status.value
            })

            if self.on_status_change:
                self.on_status_change(new_status)

    def get_current_player(self) -> PlayerState:
        """現在のプレイヤーを取得"""

        return self.state.players[self.state.round_state.current_player]

    def advance_player(self):
        """プレイヤーを次に進める"""

        self.state.round_state.current_player = (
            self.state.round_state.current_player + 1
        ) % self.num_players

    def get_game_state(self) -> dict:
        """ゲーム状態を辞書で取得（API用）"""

        return {
            "status": self.state.status.value,
            "round": self.state.round_state.round_number,
            "current_player": self.get_current_player().player_id,
            "players": [
                {
                    "id": p.player_id,
                    "health": p.health,
                    "hand_size": len(p.hand),
                    "discard_size": len(p.discards)
                }
                for p in self.state.players
            ]
        }
