"""
麻雀ゲームエンジン
"""
from typing import Callable, Optional
import random

from .game_state import GameState, GameStatus, RoundStatus, PlayerStatus, PlayerState, RoundState
from .tile_wall import TileWall

from ..utils import TileConverter


class GameEngine:
    """麻雀ゲームエンジン"""

    def __init__(self, num_players: int = 2):
        """
        ゲームエンジンを初期化

        Args:
            num_players: プレイヤー数（デフォルト2人）
        """

        self.state = GameState()
        self.tile_wall = TileWall()
        self.num_players = num_players
        #self.target_status = GameStatus.GAME_END  # 目標ステータス

        # 各種コールバック
        self.on_hand_selecting: Optional[Callable[[], None]] = None
        self.on_betting: Optional[Callable[[], list[int]]] = None

        self.on_wall_dealt: Optional[Callable[[int, list[list[int]]], None]] = None
        self.on_hand_selected: Optional[Callable[[], None]] = None
        self.on_bet: Optional[Callable[[], list[int]]] = None
        self.on_turn_decided: Optional[Callable[[], int]] = None

        #self.on_status_change: Optional[Callable[[GameStatus], None]] = None
        self.on_round_start: Optional[Callable[[RoundState], None]] = None
        self.on_round_end: Optional[Callable[[], None]] = None
        self.on_game_end: Optional[Callable[[], None]] = None


    def initialize_players(self, player_ids: list[str]):
        """プレイヤーを初期化"""

        self.state.players = [
            PlayerState(
                player_id=player_id,
                health=20000,  # 初期体力
                status=PlayerStatus.WAITING
            )
            for player_id in player_ids
        ]

    def start_game(self, max_rounds: int = 4):
        """ゲームを開始"""

        if self.state.status != GameStatus.WAITING:
            raise RuntimeError("ゲームはすでに開始されています")

        self.state.status = GameStatus.PLAYING
        self._start_round()

    def _start_round(self):
        """局を開始"""

        if self.on_round_start:
            self.on_round_start(self.state.round_state)

        # 配牌を実行
        self.state.round_state.status = RoundStatus.DEALING
        self._deal_tiles()

    def _deal_tiles(self):
        """各プレイヤーに牌を配る"""

        hands = [self.tile_wall.deal() for _ in range(self.num_players)]

        for i, player in enumerate(self.state.players):
            player.wall = hands[i][0] # 配られた牌
            player.hand = hands[i][1] # 聴牌形の例

        self.state.round_state.dora_id = self.tile_wall.dora_id

        if self.on_wall_dealt:
            self.on_wall_dealt()

        self.state.round_state.status = RoundStatus.HAND_SELECTION
        self._select_hand()

    def _select_hand(self):
        """手牌の選択を行う"""

        # ここでプレイヤーからの手牌選択を待ち、選択された手牌を反映する
        if self.on_hand_selecting:
            self.on_hand_selecting()

        if self.on_hand_selected:
            self.on_hand_selected()

        self.state.round_state.status = RoundStatus.BETTING
        self._betting()

    def _betting(self):
        """掛け金の設定を行う"""

        # ここでプレイヤーからの掛け金設定を待ち、設定された掛け金を反映する
        if self.on_betting:
            self.on_betting()

        if self.on_bet:
            self.on_bet()

    def _decide_turn(self):
        """手番の決定を行う"""

        self.state.round_state.current_player_index = random.randrange(0, self.num_players)

        if self.on_turn_decided:
            self.on_turn_decided()

    def _play_round(self):
        """局のプレイ処理"""

        # ここでプレイヤーの打牌やスキル使用などのアクションを処理する
        while True:
            break

        self._discard()

    def _discard(self):
        """打牌処理"""
        #if self.on_discard:
        #    self.on_discard()

        if self.on_round_end:
            self.on_round_end()

    def _end_round(self) -> bool:
        """
        局を終了
        （中身は未実装、現状は全て流局として終了処理）
        """

        self.state.round_state.status = RoundStatus.DISCARD  # 一旦全て流局とする

        # 次の局に遷移
        self.state.round_state.round_number += 1

        if self.on_round_end:
            self.on_round_end()

        # 次の局が必要なら ROUND_START に戻す
        # ゲームループで判定される

    def _on_game_end(self):
        """ゲーム終了時の処理"""

        if self.on_game_end:
            self.on_game_end()

    #def _set_status(self, new_status: GameStatus):
    #    """ゲームステータスを変更"""
    #
    #    if self.state.status != new_status:
    #        old_status = self.state.status
    #        self.state.status = new_status
    #
    #        if self.on_status_change:
    #            self.on_status_change(new_status)

    def get_current_player(self) -> PlayerState:
        """現在のプレイヤーを取得"""
        return self.state.players[self.state.round_state.current_player_index]

    def advance_player(self):
        """プレイヤーを次に進める"""
        self.state.round_state.current_player_index = (self.state.round_state.current_player_index + 1) % self.num_players

    def get_game_state(self) -> dict:
        """ゲーム状態を辞書で取得（API用）"""

        return {
            "status": self.state.status.value,
            "round": self.state.round_state.round_number,
            "honba": self.state.round_state.honba,
            "dora_id": self.state.round_state.dora_id,
            "current_player": self.get_current_player().player_id,
            "players": [
                {
                    "id": p.player_id,
                    "health": p.health,
                    "hand": p.hand,
                    "wall": p.wall,
                    "wait": p.wait,
                    "discards": p.discards
                }
                for p in self.state.players
            ]
        }