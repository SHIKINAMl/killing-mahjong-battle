"""
麻雀ゲームの状態管理
"""
from enum import Enum
from dataclasses import dataclass, field


class GameStatus(Enum):
    """ゲームのステータス"""
    WAITING = "waiting"  # ゲーム開始待機中
    ROUND_START = "round_start"  # 局開始
    PLAYING = "playing"  # プレイ中
    ROUND_END = "round_end"  # 局終了
    GAME_END = "game_end"  # ゲーム終了


class RoundStatus(Enum):
    """局内のステータス"""
    DEALING = "dealing"  # 配牌フェーズ
    HAND_SELECTION = "hand_selection"  # 手牌選択フェーズ
    BETTING = "betting"  # 掛け金設定フェーズ
    TURN_DECISION = "turn_decision"  # 手番決定フェーズ
    DISCARD = "discard"  # 打牌フェーズ


class PlayerStatus(Enum):
    """プレイヤーのステータス"""
    WAITING = "waiting"  # 待機中
    ACTIVE = "active"  # アクティブ（自分の番）
    INACTIVE = "inactive"  # 非アクティブ


@dataclass
class PlayerState:
    """プレイヤーの状態"""
    player_id: str  # プレイヤーID
    hand: list[int] = field(default_factory=list)  # 手牌
    wall: list[int] = field(default_factory=list)  # 牌山
    discards: list[int] = field(default_factory=list)  # 捨て牌
    health: int = 20000  # 体力
    status: PlayerStatus = PlayerStatus.WAITING


@dataclass
class RoundState:
    """局の状態"""
    round_number: int  # 第何局か
    honba: int  # 本場数
    status: RoundStatus = RoundStatus.DEALING
    current_player_index: int # 現在のプレイヤーのインデックス
    dora_id: int = None  # ドラのID


@dataclass
class GameState:
    """ゲーム全体の状態"""
    status: GameStatus = GameStatus.WAITING
    players: list[PlayerState] = field(default_factory=list)
    round_state: RoundState = field(default_factory=lambda: RoundState(
        round_number=1,
        honba=0,
        current_player_index=0
    ))

    game_log: list[dict] = field(default_factory=list)  # ゲームログ

    def add_log(self, event: str, data: dict = None):
        """ゲームログにイベントを記録"""
        log_entry = {"event": event, "data": data or {}}
        self.game_log.append(log_entry)
