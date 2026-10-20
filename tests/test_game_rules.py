"""
ゲームルールのテスト（2026-10-08 のすり合わせで決めた内容）

- 満貫未満の手は和了れない。満貫に届かない待ち牌が捨てられても、ロン確認を出さずに次の手番へ進む
- 満貫の判定は打牌の時点で、精算と同じ数え方（役強化・状況役を含む）で行う
- 精算で HP が 0 以下になったら、その場でゲーム終了
- 次の局の掛け金（流局後は持ち越しの掛け金、それ以外は最低掛け金）を払えなければ、局の終わりでゲーム終了

実行: python -m pytest tests -q
"""

from mahjong_engine.engine.game_engine import GameEngine, promoted_multiplier, promoted_tier
from mahjong_engine.engine.game_state import RoundStatus

DORA_NONE = 28  # 西。手牌に西を入れなければドラは乗らない

# 清一色（九蓮宝燈）の聴牌。萬子なら何でも満貫以上で和了れる
CHINITSU_HAND = [0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 8, 8]
# 三色同順（1-2-3）＋東の単騎。満貫に届かない
LOW_HAND = [0, 1, 2, 9, 10, 11, 18, 19, 20, 3, 4, 5, 27]


def _discard_engine(winner_hand, loser_wall, loser_previous_discards=2):
    """W が聴牌、L が打牌する直前の状態を作る。"""
    engine = GameEngine()
    engine.initialize_players(["W", "L"])
    events = []
    engine.on_game_end = lambda: events.append("game_end")
    engine.on_agari_pending = lambda *args: events.append("agari_pending")
    engine.state.round_state.dora_id = DORA_NONE

    winner = engine.get_player_by_id("W")
    winner.wall = list(winner_hand) + [21, 22]
    assert engine.select_hand(list(range(len(winner_hand))), winner)

    loser = engine.get_player_by_id("L")
    loser.wall = list(loser_wall)
    # 一発がつかないよう、既に何枚か捨てたことにしておく
    loser.discards = [24] * loser_previous_discards

    for player in (winner, loser):
        player.bet = 200
    engine.state.round_state.status = RoundStatus.DISCARD
    engine.state.round_state.current_player_index = 1  # L の手番
    return engine, events


def _han_for(engine, player_id, tile):
    """満貫未満であることの前提確認用。"""
    winner = engine.get_player_by_id(player_id)
    hand = [winner.wall[i] for i in winner.hand] + [tile]
    _, han, _, _ = engine._calc_agari_han(winner, engine.get_player_by_id("L"), hand, tile)
    return han


# ========== 満貫未満は和了れない ==========

def test_below_mangan_wait_does_not_prompt_ron():
    engine, events = _discard_engine(LOW_HAND, loser_wall=[27, 20])
    assert 27 in engine.get_player_by_id("W").waits
    assert _han_for(engine, "W", 27) < 4

    engine.discard("L", 0)
    assert "agari_pending" not in events
    assert engine.get_pending_agari() is None
    assert engine.get_current_player().player_id == "W"  # 普通に次の手番へ


def test_mangan_wait_prompts_ron():
    engine, events = _discard_engine(CHINITSU_HAND, loser_wall=[4, 20])
    engine.discard("L", 0)
    assert events == ["agari_pending"]
    assert engine.get_pending_agari()["winner_id"] == "W"


def test_boost_does_not_reach_mangan():
    """役強化は倍率の段階を上げるだけで、満貫未満の手を和了れるようにはしない。"""
    engine, events = _discard_engine(LOW_HAND, loser_wall=[27, 20])
    winner = engine.get_player_by_id("W")
    winner.boost_hand_bonus = {"三色同順": 1}
    engine.discard("L", 0)
    assert "agari_pending" not in events


# ========== 役強化は倍率の段階を上げる ==========

def test_boost_promotes_one_tier_per_yaku():
    # 満貫（4 翻）の手。1・2 翻の役は跳満まで
    assert promoted_tier(4, ["平和", "断么九"], {}) == 0
    assert promoted_tier(4, ["平和", "断么九"], {"平和": 1}) == 1
    # 2 つ強化しても、上限は跳満
    assert promoted_tier(4, ["平和", "断么九"], {"平和": 1, "断么九": 1}) == 1
    # 3 翻の役を含めば上限は倍満。段階は足し合わせる
    assert promoted_tier(4, ["平和", "混一色"], {"平和": 1, "混一色": 1}) == 2
    # 元が上限以上なら昇格しない（下がらない）
    assert promoted_tier(8, ["平和", "三色同順"], {"平和": 1}) == 2
    # 清一色は役満まで、役満はダブル役満まで
    assert promoted_tier(11, ["清一色", "断么九"], {"清一色": 1}) == 4
    assert promoted_tier(13, ["四暗刻"], {"四暗刻": 1}) == 5
    # 手に含まれない役の強化は効かない。満貫未満は None
    assert promoted_tier(4, ["平和"], {"清一色": 1}) == 0
    assert promoted_tier(3, ["平和"], {"平和": 1}) is None
    assert promoted_multiplier(4, ["平和"], {"平和": 1}) == 1.5
    assert promoted_multiplier(3, ["平和"], {"平和": 1}) == 0.0


def test_boost_does_not_exceed_double_yakuman():
    """CHINITSU_HAND は純正九蓮宝燈（ダブル役満）。強化してもそれより上がらない。"""
    engine, _ = _discard_engine(CHINITSU_HAND, loser_wall=[4, 20])
    winner = engine.get_player_by_id("W")
    winner.boost_hand_bonus = {"純正九蓮宝燈": 1}
    engine.discard("L", 0)
    assert engine.liquidation("W", [winner.wall[i] for i in winner.hand] + [4], 4)
    result = engine._last_liquidation_result
    assert result["multiplier"] == 8.0 and result["promoted_tiers"] == 0
    assert result["multiplier_label"] == "ダブル役満"


def test_context_yaku_counts_toward_mangan():
    """一発（両者の一打目まで）は打牌の時点の満貫判定に含める。"""
    # 前提: 三色同順＋立直の3翻。一発がつけば満貫に届く
    engine, _ = _discard_engine(LOW_HAND, loser_wall=[27, 20])
    winner = engine.get_player_by_id("W")
    yaku, han, tier, _ = engine._calc_agari_han(
        winner, engine.get_player_by_id("L"), [winner.wall[i] for i in winner.hand] + [27], 27,
    )
    assert han == 3 and tier is None and "立直" in yaku

    engine, events = _discard_engine(LOW_HAND, loser_wall=[27, 20], loser_previous_discards=0)
    engine.discard("L", 0)  # L の一打目
    assert events == ["agari_pending"]


# ========== 精算のその場で決着 ==========

def test_hp_zero_at_settlement_ends_game_immediately():
    engine, events = _discard_engine(CHINITSU_HAND, loser_wall=[4, 20])
    engine.get_player_by_id("L").health = 100
    engine.discard("L", 0)
    assert engine.resolve_pending_agari("W", True) is True

    # 残りの HP を超えた損失はマイナスのまま記録する
    result = engine.get_last_liquidation_result()
    assert result["loser_loss"] > 100
    assert engine.get_player_by_id("L").health == 100 - result["loser_loss"] == result["loser_health"]
    assert events[-1] == "game_end"
    assert engine.game_end_reason == "hp_zero"
    assert engine.state.round_state.status != RoundStatus.ROUND_END_WAITING  # 次の局へ進まない


def test_draw_with_unpayable_carry_over_ends_game():
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    ended = []
    engine.on_game_end = lambda: ended.append(True)
    a, b = engine.state.players
    a.bet, b.bet = 1000, 1000
    a.health, b.health = 5000, 800  # B は持ち越しの 1000 を払えない
    engine.end_round(is_draw=True)
    assert ended and engine.game_end_reason == "bet_unpayable"


def test_draw_after_betting_all_hp_is_bet_unpayable():
    """全額を賭けて HP 0 のまま流局した場合は、精算の HP 0 ではなく掛け金を払えない扱い。"""
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a, b = engine.state.players
    a.bet, b.bet = 400, 400
    b.health = 0
    engine.end_round(is_draw=True)
    assert engine.game_end_reason == "bet_unpayable"


def test_win_round_with_unpayable_min_bet_ends_game():
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a, b = engine.state.players
    b.health = 150  # 最低掛け金 200 を払えない（0 ではない）
    engine.end_round(is_draw=False)
    assert engine.game_end_reason == "bet_unpayable"


def test_round_continues_when_everyone_can_pay():
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a, b = engine.state.players
    a.bet, b.bet = 200, 200
    b.health = 400  # 流局後は 200 × 2 で精算されるので、400 あれば続けられる
    engine.end_round(is_draw=True)
    assert engine.game_end_reason is None
    assert engine.state.round_state.status == RoundStatus.ROUND_END_WAITING


def test_draw_ends_game_when_accumulated_stake_is_unpayable():
    """流局しても HP は減らないが、積み上がった掛け金（掛け金 × (流局の回数 + 1)）を払えなければ負け。"""
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a, b = engine.state.players
    a.bet, b.bet = 1000, 1000
    b.health = 1500  # 1 回目の流局: 次の局は 1000 × 2 = 2000 で精算される
    engine.end_round(is_draw=True)
    assert engine.game_end_reason == "bet_unpayable"
    assert b.health == 1500  # HP は減らない

    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a, b = engine.state.players
    a.bet, b.bet = 1000, 1000
    b.health = 2500
    engine.end_round(is_draw=True)  # 2000 は払える
    assert engine.game_end_reason is None
    engine.end_round(is_draw=True)  # 2 回目の流局: 1000 × 3 = 3000 は払えない
    assert engine.game_end_reason == "bet_unpayable"
    assert engine.bet_unpayable_players == ["B"]  # HP の多い少ないではなく、払えなかった方が負け


# ========== 掛け金の精算（案 B: 先払いなし。流局は同額のまま積み上げ） ==========

def test_bet_does_not_reduce_health():
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a = engine.get_player_by_id("A")
    assert engine.place_bet(a, 1000)
    assert a.health == 20000 and a.bet == 1000
    assert not engine.place_bet(a, 5000 + 200)  # 上限を超える額は賭けられない
    a.health = 800
    assert not engine.place_bet(a, 1000)  # HP を超える額は賭けられない


def test_draw_moves_no_health():
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    for player in engine.state.players:
        assert engine.place_bet(player, 1000)
    engine.end_round(is_draw=True)
    assert [p.health for p in engine.state.players] == [20000, 20000]
    assert engine.state.round_state.status == RoundStatus.ROUND_END_WAITING


def test_settlement_after_draws_accumulates_same_bet():
    """流局 2 回のあとに和了ると、同じ掛け金が 3 局分積み上がって精算される。"""
    engine, _ = _discard_engine(CHINITSU_HAND, loser_wall=[4, 20])
    engine._carry_over_draw_count = 2
    winner, loser = engine.get_player_by_id("W"), engine.get_player_by_id("L")
    engine.discard("L", 0)
    assert engine.resolve_pending_agari("W", True) is True

    result = engine.get_last_liquidation_result()
    multiplier = result["multiplier"]
    assert result["winner_bet"] == result["loser_bet"] == 200 * 3
    assert winner.health == 20000 + int(200 * 3 * multiplier)
    expected_loss = int(200 * 3 * multiplier * result["loser_loss_multiplier"])
    assert loser.health == 20000 - expected_loss


# ========== 流局（両者が 17 枚ずつ捨てたら） ==========

def test_draw_after_each_player_discards_17():
    from mahjong_engine.engine.game_engine import DISCARDS_PER_PLAYER
    assert DISCARDS_PER_PLAYER == 17

    engine, events = _discard_engine(LOW_HAND, loser_wall=list(range(18)), loser_previous_discards=0)
    winner, loser = engine.get_player_by_id("W"), engine.get_player_by_id("L")
    winner.discards = [24] * 17  # W は捨て終えている
    for index in range(16):
        engine.state.round_state.current_player_index = 1
        engine.discard("L", index)
        assert engine.state.round_state.status == RoundStatus.DISCARD  # 16 枚目ではまだ終わらない
    engine.state.round_state.current_player_index = 1
    engine.discard("L", 16)
    assert len(loser.discards) == 17
    assert engine.state.round_state.status == RoundStatus.ROUND_END_WAITING  # 17 枚目で流局


def test_skill_cannot_break_carry_over_stake():
    """流局の続きの局では、積み上がった掛け金を払えなくなるほど HP を使うスキルは使えない。"""
    from mahjong_engine.engine.game_state import SkillType

    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    engine.start_game(5)
    a = engine.get_player_by_id("A")
    engine._carry_over_bets, engine._carry_over_draw_count = True, 1
    a.bet = 1000
    a.health = 2400  # 積み上がった掛け金は 1000 × 2 = 2000
    assert engine.skill_health_floor(a) == 2000
    assert engine.execute_skill(a, SkillType.PERSPECTIVE, {"suit": "manzu"}) is None  # 2400 − 500 < 2000
    assert a.health == 2400
    a.health = 2500
    assert engine.execute_skill(a, SkillType.PERSPECTIVE, {"suit": "manzu"}) == 500  # ちょうど 2000 残る


def test_voltage_rises_with_new_kinds_and_resumes_after_break():
    engine = GameEngine()
    engine.initialize_players(["A", "B"])
    a = engine.get_player_by_id("A")
    for kind in range(6):  # 初めての種類を 6 枚 → 6 点（1.3 倍）
        engine._apply_voltage_discard(a, kind)
    assert (a.voltage_points, engine._get_voltage_multiplier(a.voltage_points)) == (6, 1.3)
    engine._apply_voltage_discard(a, 0)  # 出た種類で切れる。再開は 2 点から
    assert (a.voltage_points, a.voltage_broken, a.voltage_resume_points) == (0, True, 2)
    engine._apply_voltage_discard(a, 10)
    assert (a.voltage_points, a.voltage_broken) == (3, False)


def test_tanki_bonus_applies_to_any_single_kind_wait():
    """待ちが 1 種類なら、雀頭の待ちでなくても（嵌張でも）単騎待ちボーナスの対象。"""
    tanki = GameEngine._is_tanki_wait_agari
    assert tanki([], 5, [5])  # 1 種類の待ちをその牌で和了
    assert tanki([], 5 | 0b100000, [5])  # ドラの印が付いた牌 ID でも種類で比べる
    assert not tanki([], 5, [5, 8])  # 2 種類の待ち
    assert not tanki([], 8, [5])


def test_ryanpeikou_including_four_identical_runs():
    """二盃口は同じ順子の組が 2 組。123萬×4 も 2 組なので二盃口（前は三暗刻になり、満貫を割っていた）。"""
    from collections import Counter

    from mahjong_engine.engine.hand_analyzer import HandAnalyzer

    def check(hand14, winning_tile, expected):
        yaku = HandAnalyzer.enum_yaku(hand14, winning_tile=winning_tile)
        counter_tuple = tuple(Counter(hand14).get(i, 0) for i in range(29))
        assert ("二盃口" in yaku) is expected["ryanpeikou"], yaku
        assert ("一盃口" in yaku) is expected["ipeikou"], yaku
        # 配牌時の満貫チェック（速い判定）も同じ翻数になる
        assert HandAnalyzer._max_han_from_counter_tuple(counter_tuple, winning_tile) == HandAnalyzer.calc_yaku(hand14, winning_tile)

    four_runs = [0, 1, 2] * 4 + [13, 13]  # 123萬×4 + 5筒の雀頭
    check(four_runs, 13, {"ryanpeikou": True, "ipeikou": False})
    assert HandAnalyzer.calc_yaku(four_runs, 13) >= 4  # 二盃口 3 + 立直 1 で満貫
    check([0, 1, 2, 0, 1, 2, 12, 13, 14, 12, 13, 14, 24, 24], 24, {"ryanpeikou": True, "ipeikou": False})
    check([0, 1, 2, 0, 1, 2, 1, 2, 3, 1, 2, 3, 13, 13], 13, {"ryanpeikou": True, "ipeikou": False})
    check([0, 1, 2, 0, 1, 2, 12, 13, 14, 24, 25, 26, 27, 27], 27, {"ryanpeikou": False, "ipeikou": True})
    check([0, 1, 2] * 3 + [12, 13, 14, 27, 27], 14, {"ryanpeikou": False, "ipeikou": False})  # 三暗刻の方が高い
