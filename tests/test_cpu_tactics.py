"""
CPU が人間に読まれにくくするための仕組みのテスト

- 視界: 相手の透視で見られた自分の牌が分かる
- 危険度: 見えている枚数から数える（一九字牌を一律に安全と見ない）・見られた牌は危険と見る
- 揺らぎ: 打牌・手牌・掛け金が毎回同じにならない（温度 0 なら前と同じく最善を選ぶ）
- 相手のくせ: 和了った牌・捨てた順番を覚え、対策（危険度）・採用（待ちの価値）に効かせる

実行: python -m pytest tests -q
"""
import copy
import random
from collections import Counter

from mahjong_engine.ai.cpu_config import load_cpu_config
from mahjong_engine.ai.observation import PublicInfo, build_observation
from mahjong_engine.ai.rule_brain import RuleBrain, dora_indicator_kind, rest_indexes, tile_kind
from mahjong_engine.ai.tactics import (
    OpponentProfile, danger_multiplier, kind_category, softmax_pick, wait_value_multiplier,
)
from mahjong_engine.engine.game_engine import GameEngine
from mahjong_engine.engine.game_state import RoundStatus

CPU, HUMAN = "CPU0001", "C0001"


def _discard_phase_engine(seed: int = 0) -> GameEngine:
    random.seed(seed)
    engine = GameEngine()
    engine.initialize_players([HUMAN, CPU])
    engine.start_game(5)
    for player in engine.state.players:
        assert engine.select_hand(player.hand, player)
    engine.state.round_state.status = RoundStatus.DISCARD
    return engine


def _config(level: str = "normal", **variation) -> dict:
    config = copy.deepcopy(load_cpu_config()[level])
    config["variation"].update(variation)
    return config


# ========== 視界 ==========

def test_observation_shows_my_tiles_seen_by_opponent():
    engine = _discard_phase_engine()
    cpu = engine.get_player_by_id(CPU)
    cpu.exposed_hand_indexes.update({0, 5})
    obs = build_observation(engine, CPU, PublicInfo())
    assert obs.me.exposed_to_opponent == {0: cpu.wall[0], 5: cpu.wall[5]}
    assert obs.opponent_profile is not None


def test_dora_indicator_kind_inverts_wall_generation():
    # TileWall._initialize_wall: 数牌は同じ色の次の牌、字牌は 東 ⇔ 西
    assert dora_indicator_kind(1) == 0
    assert dora_indicator_kind(0) == 8
    assert dora_indicator_kind(9) == 17
    assert dora_indicator_kind(27) == 28 and dora_indicator_kind(28) == 27


# ========== 危険度 ==========

def test_honors_are_not_treated_as_almost_safe():
    """前の決まった値（字牌 0.3・中張牌 1.0）より、字牌と中張牌の差が小さい。"""
    honor, simple = [], []
    for seed in range(10):
        engine = _discard_phase_engine(seed)
        brain = RuleBrain("normal", _config(), random.Random(seed))
        obs = build_observation(engine, CPU, PublicInfo())
        dctx = brain._danger_context(obs)
        for kind in range(29):
            if kind in dctx.opponent_discard_kinds:
                continue
            (honor if kind_category(kind) == "honor" else simple if kind_category(kind) == "simple" else []).append(
                brain.danger(kind, obs, dctx))
    ratio = (sum(honor) / len(honor)) / (sum(simple) / len(simple))
    assert 0.5 < ratio < 1.0


def test_tiles_seen_by_opponent_are_more_dangerous():
    engine = _discard_phase_engine()
    cpu = engine.get_player_by_id(CPU)
    brain = RuleBrain("normal", _config(), random.Random(0))
    obs = build_observation(engine, CPU, PublicInfo())
    index = rest_indexes(obs, obs.me.hand)[0]
    kind = tile_kind(cpu.wall[index])
    before = brain.danger(kind, obs, brain._danger_context(obs))

    cpu.exposed_hand_indexes.add(index)
    obs = build_observation(engine, CPU, PublicInfo())
    after = brain.danger(kind, obs, brain._danger_context(obs))
    assert after > before * 1.5


def test_genbutsu_is_still_safe():
    engine = _discard_phase_engine()
    cpu = engine.get_player_by_id(CPU)
    human = engine.get_player_by_id(HUMAN)
    brain = RuleBrain("normal", _config(), random.Random(0))
    obs = build_observation(engine, CPU, PublicInfo())
    kind = tile_kind(cpu.wall[rest_indexes(obs, obs.me.hand)[0]])
    human.discards.append(kind)
    obs = build_observation(engine, CPU, PublicInfo())
    assert brain.danger(kind, obs, brain._danger_context(obs)) == 0.0


# ========== 揺らぎ ==========

def test_softmax_pick():
    rng = random.Random(0)
    assert softmax_pick([1.0, 3.0, 2.0], 0.0, rng) == 1
    picks = Counter(softmax_pick([1.0, 1.1, 0.9], 1.0, rng) for _ in range(300))
    assert len(picks) == 3  # ほぼ同点なら、どれも選ばれる
    picks = Counter(softmax_pick([0.0, 50.0], 1.0, rng) for _ in range(300))
    assert picks == {1: 300}  # 大差なら必ず高い方


def test_discard_order_varies_but_never_breaks_rules():
    engine = _discard_phase_engine()
    cpu = engine.get_player_by_id(CPU)
    obs = build_observation(engine, CPU, PublicInfo())
    picks = Counter(RuleBrain("normal", _config(), random.Random(s)).choose_discard(obs) for s in range(60))
    assert len({tile_kind(cpu.wall[i]) for i in picks}) >= 2
    assert not set(picks) & set(cpu.hand)
    assert not {tile_kind(cpu.wall[i]) for i in picks} & {tile_kind(w) for w in cpu.waits}

    # 温度 0 なら毎回同じ（前の動き）
    fixed = {RuleBrain("normal", _config(discard_temperature=0.0), random.Random(s)).choose_discard(obs)
             for s in range(20)}
    assert len({tile_kind(cpu.wall[i]) for i in fixed}) == 1


def test_bluff_and_bet_bounds():
    engine = _discard_phase_engine()
    engine.state.round_state.status = RoundStatus.BETTING
    cpu = engine.get_player_by_id(CPU)
    config = _config(bluff_rate=1.0, bluff_ratio=0.5, bet_noise=0.0)
    brain = RuleBrain("normal", config, random.Random(0))
    obs = build_observation(engine, CPU, PublicInfo())
    plan = brain.evaluate_hand(obs, tuple(cpu.hand))
    weak = plan.__class__(plan.hand_indexes, plan.waits, plan.score, 0.1, plan.candidates)
    max_bet, unit = engine.get_bet_rule(cpu)
    bluff = brain.vary_bet(obs, weak, unit)
    assert bluff > unit and bluff % unit == 0 and bluff <= max_bet
    # HP が少ないときは、放銃の悪い場合を払える額までしか上げない
    cpu.health = 3000
    obs = build_observation(engine, CPU, PublicInfo())
    assert brain.vary_bet(obs, weak, unit) <= max(unit, (3000 - config["bet"]["ruin_reserve"]) / config["bet"]["worst_loss_multiplier"])


# ========== 相手のくせ ==========

def test_opponent_profile_counter_and_adopt():
    tactics = load_cpu_config()["normal"]["tactics"]
    profile = OpponentProfile()
    east, five_man = 27, 4
    neutral = danger_multiplier(east, profile, tactics)
    assert neutral == tactics["yaochu"]["counter_prior"]  # 和了りを見る前から、人間は一九字牌を狙うと見込む
    assert danger_multiplier(five_man, profile, tactics) == 1.0

    # 相手が字牌で何度も和了ったら、字牌をもっと危険と見る（対策）
    for _ in range(4):
        profile.record_round([five_man] * 10, opponent_won=True, winning_tile=east, is_tanki=True)
    assert danger_multiplier(east, profile, tactics) > neutral * 1.5
    assert profile.tanki_wins == 4

    # 相手が字牌を早く捨てるなら、字牌を待ちにする価値を上げる（採用）
    early = OpponentProfile()
    assert wait_value_multiplier(east, early, tactics) == 1.0  # まだ分からない
    for _ in range(3):
        early.record_round([east, 28, east, 28] + [five_man] * 13, opponent_won=False, winning_tile=None)
    assert wait_value_multiplier(east, early, tactics) > 1.2
    assert wait_value_multiplier(five_man, early, tactics) == 1.0  # 中張牌は一九字牌の戦術の対象外


def test_round_end_records_winning_tile_from_my_last_discard():
    public = PublicInfo()
    public.record_round_end(CPU, my_discards=[3, 27], opponent_discards=[1, 2], liquidation={"winner_id": HUMAN, "is_tanki_wait": True})
    profile = public.opponent_profile
    assert profile.wins == 1 and profile.win_categories["honor"] == 1 and profile.tanki_wins == 1
    public.reset_round()  # 局が変わっても、相手のくせは消さない
    assert public.opponent_profile.wins == 1
    public.record_round_end(CPU, my_discards=[3], opponent_discards=[1], liquidation={"winner_id": CPU})
    assert public.opponent_profile.wins == 1 and public.opponent_profile.rounds == 2
