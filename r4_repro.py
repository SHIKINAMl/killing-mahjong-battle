# -*- coding: utf-8 -*-
"""R-4 検証: HP が 0 になっても敗北にならないことを、サーバーのエンジン単体で再現する。
mahjong_engine は読むだけ。一切変更しない。"""
import sys, logging
sys.path.insert(0, r"D:\Unity\killing-mahjong-battle")
logging.disable(logging.CRITICAL)

from mahjong_engine.engine.game_engine import GameEngine

def new_engine():
    e = GameEngine()
    e.initialize_players(["ME", "YOU"])
    e.max_rounds = 25
    e.state.round_state.round_number = 3   # 最大ラウンド未満
    return e

fired = []
def hook(e):
    e.on_game_end = lambda: fired.append(True)

print("=== 1) place_bet: HP と同額を賭けると HP はちょうど 0 で止まるか ===")
e = new_engine()
me = e.state.players[0]
me.health = 5000
ok = e.place_bet(me, 5000)
print("  place_bet(5000) ->", ok, " health =", me.health, " (負数にならない)")

print()
print("=== 2) 精算で HP が 0 になった直後の end_round で game_end が飛ぶか ===")
e = new_engine(); fired.clear(); hook(e)
e.state.players[0].health = 0
e.state.players[1].health = 40000
e.end_round(is_draw=False)
print("  health=0 -> on_game_end fired:", bool(fired), " phase =", e.state.round_state.status)

print()
print("=== 3) 参考: HP が負なら飛ぶ（＝判定自体は生きている） ===")
e = new_engine(); fired.clear(); hook(e)
e.state.players[0].health = -1
e.state.players[1].health = 40000
e.end_round(is_draw=False)
print("  health=-1 -> on_game_end fired:", bool(fired))

print()
print("=== 4) bet() の判定でも同じか ===")
e = new_engine(); fired.clear(); hook(e)
e.state.players[0].health = 0
e.bet()
print("  health=0 -> on_game_end fired:", bool(fired), " phase =", e.state.round_state.status)

print()
print("=== 5) 実際に敗北が確定するのはどこか（次局の手牌決定） ===")
e = new_engine(); fired.clear(); hook(e)
me = e.state.players[0]
me.health = 0
print("  minimum_bet =", e.get_minimum_bet(me), " / health =", me.health)
e._carry_over_bets = False
e.selected_hand()
print("  selected_hand() -> on_game_end fired:", bool(fired), " (＝1局遅れて敗北)")

print()
print("=== 6) game_end に載る victory_method / health_zero_players の判定条件 ===")
e = new_engine()
e.state.players[0].health = 0
e.state.players[1].health = 40000
hp_zero = any(p.health < 0 for p in e.state.players)
zero_players = [p.player_id for p in e.state.players if p.health < 0]
print("  any(health < 0) =", hp_zero, "-> victory_method は 'hp_zero' にならない")
print("  health_zero_players =", zero_players, "-> 空のまま")
