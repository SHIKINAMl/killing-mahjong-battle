"""
麻雀ゲームエンジン
"""
from typing import Callable, Optional
import random

from .game_state import GameState, GameStatus, RoundStatus, SkillType, PlayerState, RoundState
from .tile_wall import TileWall
from .hand_analyzer import HandAnalyzer


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
        # 準備フェーズ
        self.on_dealt: Optional[Callable[[], None]] = None
        self.on_selected: Optional[Callable[[], None]] = None
        self.on_bet: Optional[Callable[[], None]] = None

        # 打牌フェーズ
        self.on_discard_started: Optional[Callable[[], None]] = None
        self.on_discarded: Optional[Callable[[str, int], None]] = None

        self.on_round_start: Optional[Callable[[], None]] = None
        self.on_round_end: Optional[Callable[[], None]] = None
        self.on_game_end: Optional[Callable[[], None]] = None
        self.on_phase_change: Optional[Callable[[RoundStatus], None]] = None

    def initialize_players(self, player_ids: list[str]):
        """プレイヤーを初期化"""
        self.state.players = [
            PlayerState(
                player_id=player_id,
                health=20000,  # 初期体力
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
            self.on_round_start()

        # 配牌を実行
        self._set_phase(RoundStatus.DEALING)
        self._deal_tiles()

    def _deal_tiles(self):
        """各プレイヤーに牌を配る"""
        hands = [self.tile_wall.deal() for _ in range(self.num_players)]

        for i, player in enumerate(self.state.players):
            player.wall = hands[i][0] # 配られた牌
            player.hand = hands[i][1] # 聴牌形の例

        self.state.round_state.dora_id = self.tile_wall.dora_id

        if self.on_dealt:
            self.on_dealt()

        self._set_phase(RoundStatus.HAND_SELECTION)

    def selected(self):
        """手牌の選択が完了したときの処理"""
        if self.on_selected:
            self.on_selected()

        self._set_phase(RoundStatus.BETTING)

    def bet(self):
        """掛け金の設定が完了したときの処理"""
        if self.on_bet:
            self.on_bet()

        self._set_phase(RoundStatus.DISCARD)
        self.state.round_state.current_player_index = random.randrange(0, self.num_players)

        if self.on_discard_started:
            self.on_discard_started()

    def select_hand(self, hand: list[int], player: PlayerState) -> bool:
        """
        プレイヤーが手牌を選択したときの処理

        Args:
            hand: 選択された手牌のリスト
            player: プレイヤー状態
        Returns:
            手牌が有効であれば True、そうでなければ False
        """
        if not HandAnalyzer.is_tenpai(hand, player.wall): # 満貫以上の手牌でない場合の拒否も必要かもしれない
            return False

        player.hand = hand
        player.waits = HandAnalyzer.get_tenpai_waiting_tiles(hand, player.wall)
        player.wall = HandAnalyzer.without_hand(hand, player.wall)

        return True

    def discard(self, player_id: str, tile_id: int) -> None:
        """
        プレイヤーが牌を捨てたときの処理
        Args:
            player_id: 捨てたプレイヤーのID
            tile_id: 捨てた牌のID
        """
        if self.on_discarded:
            self.on_discarded(player_id, tile_id)

        discarding_player = self.get_player_by_id(player_id)

        if len(discarding_player.discards) >= 13: # 13枚以上捨てたら流局（今は局終了）
            self.state.round_state.honba += 1
            self.end_round()
            return

        discarding_player.wall.remove(tile_id)
        discarding_player.discards.append(tile_id)
        self._advance_player()

    def liquidation(self, player_id: str, hand: list[int]) -> bool:
        """
        清算処理

        Args:
            player_id: 清算対象のプレイヤーのID
            hand: 清算対象の手牌
        Returns:
            上がりが成立していれば True、そうでなければ False
        """
        if not HandAnalyzer.is_win(hand) or not HandAnalyzer.check_mangan(hand):
            return False

        # 上がりが成立している場合の処理（点数計算や体力の減少など）
        # 単騎待ちの場合は点数が倍になるなどのルールもここで処理する <- どうする？
        # 今は局終了の処理だけ
        self.end_round()
        return True

    def end_round(self) -> None:
        """局を終了"""
        if self.on_round_end:
            self.on_round_end()

        self.state.round_state.round_number += 1

        if self.state.round_state.round_number > 4: # x局終了でゲーム終了(今は4局で固定)
            self.state.status = GameStatus.GAME_END
            self._on_game_end()
            return

        self.state.players = [
            PlayerState(
                player_id=p.player_id,
                health=p.health,
                hand=[],
                wall=[],
                wait=[],
                discards=[],
                skills={SkillType.SPESIAL_VICTORY: p.skills.get(SkillType.SPESIAL_VICTORY, None)}
            )
            for p in self.state.players
        ]

        self._start_round()

    def _on_game_end(self) -> None:
        """ゲーム終了時の処理"""

        if self.on_game_end:
            self.on_game_end()

    def _set_phase(self, new_status: RoundStatus) -> None:
        """ラウンドフェーズを変更"""

        if self.state.round_state.status != new_status:
            old_status = self.state.round_state.status
            self.state.round_state.status = new_status

            if self.on_phase_change:
                self.on_phase_change(new_status)

    def get_current_player(self) -> PlayerState:
        """現在のプレイヤーを取得"""
        return self.state.players[self.state.round_state.current_player_index]

    def get_player_by_id(self, player_id: str) -> PlayerState:
        """プレイヤーIDからプレイヤー状態を取得"""
        return next((p for p in self.state.players if p.player_id == player_id), None)

    def get_waits(self, hand: list[int], player: PlayerState) -> list[tuple[int, bool, list[str]]]:
        """
        手牌から待ち牌を取得

        Args:
            hand: 聴牌形の手牌のリスト
            player: プレイヤー

        Returns:
            待ち牌のリスト
                - 待ち牌のID
                - 満貫以上の待ちかどうか
                - 役のリスト
        """
        if not HandAnalyzer.is_tenpai(hand, player.wall):
            return None

        waits = HandAnalyzer.get_tenpai_waiting_tiles(hand, player.wall)
        waits = [w+32 if w == self.state.round_state.dora_id else w for w in waits]

        return [(
            w,
            HandAnalyzer.check_mangan(hand + [w]),
            HandAnalyzer.get_yaku(hand + [w])
        ) for w in waits]


    def _advance_player(self):
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