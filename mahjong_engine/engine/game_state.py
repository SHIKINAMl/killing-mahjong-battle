"""
麻雀ゲームの状態管理
"""
from enum import Enum
from dataclasses import dataclass, field


class RoundStatus(Enum):
    """局内のステータス"""
    DEALING = "dealing"  # 配牌フェーズ
    HAND_SELECTION = "hand_selection"  # 手牌選択フェーズ
    BETTING = "betting"  # 掛け金設定フェーズ
    DISCARD = "discard"  # 打牌フェーズ
    LIQUIDATION = "liquidation"  # 清算フェーズ


class SkillType(Enum):
    """スキルの種類"""
    MULLIGAN = "mulligan"  # 配牌や手牌の一部をやり直す
    BOOST_HAND = "boost_hand"  # 指定役の翻数を上げる
    PERSPECTIVE = "perspective"  # 他プレイヤーの手牌の一部を覗き見る
    SPESIAL_VICTORY = "special_victory"  # 特定の条件で勝利を得る

@dataclass
class PlayerState:
    """プレイヤーの状態"""
    player_id: str  # プレイヤーID
    hand: list[int] = field(default_factory=list)  # 手牌
    wall: list[int] = field(default_factory=list)  # 牌山
    waits: list[int] = field(default_factory=list)  # 待ち牌
    discards: list[int] = field(default_factory=list)  # 捨て牌
    skills: dict[SkillType, any] = field(default_factory=dict)  # スキル
    health: int = 20000  # 体力
    bet: int = 0  # 掛け金


@dataclass
class RoundState:
    """局の状態"""
    round_number: int  # 第何局か
    honba: int  # 本場数
    current_player_index: int # 現在のプレイヤーのインデックス
    status: RoundStatus = None  # 現在の局のステータス
    dora_id: int = None  # ドラのID


@dataclass
class GameState:
    """ゲーム全体の状態"""
    players: list[PlayerState] = field(default_factory=list)
    round_state: RoundState = field(default_factory=lambda: RoundState(
        round_number=1,
        honba=0,
        current_player_index=0
    ))