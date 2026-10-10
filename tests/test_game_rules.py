"""
ゲームルールのテスト（2026-10-08 のすり合わせで決めた内容）

- 満貫未満の手は和了れない。満貫に届かない待ち牌が捨てられても、ロン確認を出さずに次の手番へ進む
- 満貫の判定は打牌の時点で、精算と同じ数え方（役強化・状況役を含む）で行う
- 精算で HP が 0 以下になったら、その場でゲーム終了
- 次の局の掛け金（流局後は持ち越しの掛け金、それ以外は最低掛け金）を払えなければ、局の終わりでゲーム終了

実行: python -m pytest tests -q
"""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from mahjong_engine.engine.game_engine import GameEngine  # noqa: E402
from mahjong_engine.engine.game_state import RoundStatus  # noqa: E402

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
    _, base, bonus, _ = engine._calc_agari_han(winner, engine.get_player_by_id("L"), hand, tile)
    return base + bonus


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


def test_boost_counts_toward_mangan():
    engine, events = _discard_engine(LOW_HAND, loser_wall=[27, 20])
    winner = engine.get_player_by_id("W")
    base = _han_for(engine, "W", 27)
    winner.boost_hand_bonus = {"三色同順": 4 - base}
    engine.discard("L", 0)
    assert events == ["agari_pending"]


def test_context_yaku_counts_toward_mangan():
    """一発（両者の一打目まで）は打牌の時点の満貫判定に含める。"""
    # 前提: 三色同順＋立直の3翻。一発がつけば満貫に届く
    engine, _ = _discard_engine(LOW_HAND, loser_wall=[27, 20])
    winner = engine.get_player_by_id("W")
    yaku, base, bonus, _ = engine._calc_agari_han(
        winner, engine.get_player_by_id("L"), [winner.wall[i] for i in winner.hand] + [27], 27,
    )
    assert base + bonus == 3 and "立直" in yaku

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
    b.health = 200
    engine.end_round(is_draw=True)
    assert engine.game_end_reason is None
    assert engine.state.round_state.status == RoundStatus.ROUND_END_WAITING


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
