"""
CPU の視界（CpuObservation）

CPU が判断に使ってよい情報だけを詰める。エンジンの状態を CPU に直接読ませないための壁。

見てよい情報（人間のクライアントが画面に出すものと同じ）:
- 自分の山・聴牌例・手牌・待ち、ドラ
- 相手の捨て牌（順番付き）、透視で公開された相手の牌
- 両者の HP、掛け金、公開された役強化、スキル使用の通知
- 局数、流局の持ち越し回数

見てはいけない情報: 相手の山の未公開部分、相手の手牌、相手の待ち
（サーバーは dealing_completed などで相手の山もクライアントへ送っているが、画面には出していない）
"""
from dataclasses import dataclass, field
from typing import Callable, Optional

from ..engine.game_engine import GameEngine
from ..engine.game_state import RoundStatus, SkillType

# CPU が使うスキル（特殊勝利は使わない）
CPU_SKILLS = (SkillType.MULLIGAN, SkillType.PERSPECTIVE, SkillType.BOOST_HAND, SkillType.ASSAULT)


@dataclass
class PublicInfo:
    """WebSocket の通知から CPU 自身が覚えておく公開情報。"""

    opponent_opening_boosts: dict[str, int] = field(default_factory=dict)
    opponent_boost_use_count: int = 0
    opponent_skills_this_round: list[str] = field(default_factory=list)
    my_skills_this_round: dict[str, int] = field(default_factory=dict)

    def reset_round(self) -> None:
        self.opponent_skills_this_round = []
        self.my_skills_this_round = {}


@dataclass(frozen=True)
class MyView:
    client_id: str
    wall: tuple[int, ...]
    hand: tuple[int, ...]  # 手牌確定前はサーバーの聴牌例（wall index）
    hand_confirmed: bool
    waits: tuple[int, ...]  # 牌種（下位5ビット）
    discarded_wall_indexes: frozenset[int]
    health: int
    bet: int
    boost_hand_bonus: dict[str, int]
    special_victory_count: int
    assault_used_this_round: bool
    skill_costs: dict[str, int]
    bet_max: int
    bet_unit: int
    boost_hand_targets: int
    can_mulligan: bool  # 交換用の予備牌が残っているか


@dataclass(frozen=True)
class OpponentView:
    client_id: str
    wall_size: int
    exposed: dict[int, int]  # 透視で公開された wall index → 牌ID
    discards: tuple[int, ...]  # 捨て牌（牌ID、捨てた順）
    health: int
    bet: int
    opening_boosts: dict[str, int]
    boost_use_count: int
    special_victory_count: int
    skills_this_round: tuple[str, ...]


@dataclass(frozen=True)
class CpuObservation:
    phase: Optional[str]
    round_number: int
    carry_over_draw_count: int
    dora_id: Optional[int]
    me: MyView
    opponent: OpponentView
    my_turn: bool
    pending_agari_for_me: bool
    pending_agari_tile: Optional[int]  # 自分がロンできる牌（相手の捨て牌）
    i_am_first_player: bool  # 打牌フェーズの先手か（discard_phase_started で公開される）
    my_discard_count: int
    my_skills_this_round: dict[str, int]
    # 翻数 → 精算倍率（ルールそのもので、状態を持たない）
    multiplier_for_han: Callable[[int], float]


def build_observation(engine: GameEngine, client_id: str, public: PublicInfo) -> CpuObservation:
    """エンジンの状態から、CPU が見てよい情報だけを取り出す。"""
    me = engine.get_player_by_id(client_id)
    opponent = next(p for p in engine.state.players if p.player_id != client_id)
    round_state = engine.state.round_state
    status = round_state.status

    max_bet, bet_unit = engine.get_bet_rule(me)
    pending = engine.get_pending_agari()
    my_turn = (
        status == RoundStatus.DISCARD
        and pending is None
        and engine.get_current_player().player_id == client_id
    )

    my_view = MyView(
        client_id=client_id,
        wall=tuple(me.wall),
        hand=tuple(me.hand),
        hand_confirmed=bool(me.waits) or status not in (RoundStatus.DEALING, RoundStatus.HAND_SELECTION),
        waits=tuple(w & 0b11111 for w in me.waits),
        discarded_wall_indexes=frozenset(me.discarded_wall_indexes),
        health=me.health,
        bet=me.bet,
        boost_hand_bonus=dict(engine._effective_boost_bonus_map(me)),
        special_victory_count=me.special_victory_count,
        assault_used_this_round=me.assault_used_this_round,
        skill_costs={skill.value: engine.get_skill_cost(me, skill) for skill in CPU_SKILLS},
        bet_max=max_bet,
        bet_unit=bet_unit,
        boost_hand_targets=me.boost_hand_targets,
        can_mulligan=bool(round_state.reserved_tiles),
    )

    opponent_view = OpponentView(
        client_id=opponent.player_id,
        wall_size=len(opponent.wall),
        exposed={idx: opponent.wall[idx] for idx in sorted(opponent.exposed_hand_indexes) if idx < len(opponent.wall)},
        discards=tuple(opponent.discards),
        health=opponent.health,
        bet=opponent.bet,
        opening_boosts=dict(public.opponent_opening_boosts),
        boost_use_count=public.opponent_boost_use_count,
        special_victory_count=opponent.special_victory_count,
        skills_this_round=tuple(public.opponent_skills_this_round),
    )

    return CpuObservation(
        phase=status.value if status else None,
        round_number=round_state.round_number,
        carry_over_draw_count=engine._carry_over_draw_count,
        dora_id=round_state.dora_id,
        me=my_view,
        opponent=opponent_view,
        my_turn=my_turn,
        pending_agari_for_me=bool(pending and pending.get("winner_id") == client_id),
        pending_agari_tile=pending.get("winning_tile") if pending and pending.get("winner_id") == client_id else None,
        i_am_first_player=engine.state.players[round_state.first_player_index].player_id == client_id,
        my_discard_count=len(me.discards),
        my_skills_this_round=dict(public.my_skills_this_round),
        multiplier_for_han=engine._get_liquidation_multiplier,
    )
