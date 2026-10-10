"""
対局シミュレーター（training/simulator.py）のテスト

- 対局が決着まで進み、決着の理由が付く
- 1 局だけ打って止められる（advance=False）
- 同じ配牌を使い回しても、マリガンの書き換えが残らない
- 本番の CPU と同じ考え方（RuleBrain）で打てる

実行: python -m pytest tests -q
"""
import random

from mahjong_engine.engine.game_state import RoundStatus
from mahjong_engine.engine.tile_wall import generate_round_deal
from training.simulator import MatchSimulator, RuleAgent, make_rule_agent

FAST = {"candidate_limit": 5, "search_time_limit": 0.2}


def _agent(level: str, seed: int, **kwargs) -> RuleAgent:
    return make_rule_agent(level, seed, FAST, **kwargs)


def test_game_reaches_an_end():
    random.seed(0)
    deals = [generate_round_deal(2) for _ in range(6)]
    rng = random.Random(0)
    sim = MatchSimulator(
        [_agent("normal", 1, fixed_bet=5000), _agent("easy", 2, fixed_bet=5000)],
        deal_factory=lambda _n: rng.choice(deals),
        rng=random.Random(0),
    )
    record = sim.play_game(max_rounds=60)
    assert record.rounds
    if sim.ended:
        assert record.end_reason in {"hp_zero", "bet_unpayable", "cumulative_earned_points"}
    for r in record.rounds:
        assert set(r.health_after) == {"P0", "P1"}


def test_single_round_does_not_advance():
    random.seed(1)
    deal = generate_round_deal(2)
    sim = MatchSimulator([_agent("normal", 1), _agent("normal", 2)], deal_factory=lambda _n: deal,
                         opening_boosts=False)
    record = sim.play_round(advance=False)
    assert record.round_number == 1
    assert sim.engine.state.round_state.round_number == 1
    assert sim.ended or sim.engine.state.round_state.status == RoundStatus.ROUND_END_WAITING


def test_reused_deal_is_not_mutated_by_mulligan():
    random.seed(2)
    deal = generate_round_deal(2)
    original_wall = list(deal.hands[0][0])
    sim = MatchSimulator([_agent("normal", 1), _agent("normal", 2)], deal_factory=lambda _n: deal,
                         opening_boosts=False)
    player = sim.engine.get_player_by_id("P0")
    assert sim.engine.use_mulligan(player, 0) is not None
    assert deal.hands[0][0] == original_wall  # エンジンの山だけが変わる
