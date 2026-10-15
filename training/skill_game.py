"""
スキルの効果を、対局の勝ち負けで測る（局をまたぐ効果の評価）

skill_value.py / skill_strategy.py は 1 局の HP の差で測るが、次の効果は 1 局では測れない:
- 役強化は対局の最後まで続く
- 強襲は自分の獲得を相手へのダメージに変える（HP の差は同じ）。相手を倒しきる・相手の掛け金やスキルを
  縛る効果は、対局の勝ち負けにしか出ない
そこで、1 局目に P0 だけがスキルを使う対局と使わない対局を、最後まで同じ配牌・乱数で打ち比べ、
P0 の勝率の差を効果とする。

- 打ち手は両者とも「ふつう」の考え方。1 局目の P0 のスキル以外は、どちらもスキルを使わない
- 掛け金は「ふつう」の決め方（固定しない）
- 勝ち負け: 累計獲得点数で終わったら到達した方の勝ち。それ以外は HP の多い方の勝ち
- 「hp+N」は、スキルの代わりに P0 の HP を N 増やして始める。勝率の差を HP に換算する物差しにする

使い方（リポジトリ直下で）:
    python -m training.skill_game --pool training/data/deals --trials 2000 \\
        --strategies "hp+1000,boost_hand,assault" --hp 20000,20000 --out training/data/skill_game.json
"""
import argparse
import json
import os
import random
import statistics
import sys
import time
from collections import Counter
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from typing import Optional

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai.rule_brain import RuleBrain  # noqa: E402
from training import skill_value  # noqa: E402
from training.deal_pool import DealPool  # noqa: E402
from training.simulator import MatchSimulator, RuleAgent  # noqa: E402

# 1 対局の局数の上限（不具合で終わらないときの止め）
MAX_ROUNDS = 200
POINT_VICTORY = 30000


def game_winner(sim: MatchSimulator) -> Optional[str]:
    """勝った方。累計獲得点数で終わったら到達した方、それ以外は HP の多い方。同点なら None。"""
    players = {p.player_id: p for p in sim.engine.state.players}
    if sim.engine.game_end_reason == "cumulative_earned_points":
        reached = [pid for pid, p in players.items() if p.cumulative_earned_points >= POINT_VICTORY]
        if len(reached) == 1:
            return reached[0]
    p0, p1 = players["P0"].health, players["P1"].health
    if p0 == p1:
        return None
    return "P0" if p0 > p1 else "P1"


def _game(deal_seed: int, seed: int, hp: tuple[int, int], strategy: Optional[str]) -> dict:
    """1 対局を最後まで打つ。strategy は 1 局目に P0 が使うスキルの手順、または「hp+N」。"""
    pool = skill_value._POOL
    config = skill_value._CONFIG
    deal_rng = random.Random(deal_seed)
    deals = iter([deal_rng.randrange(len(pool)) for _ in range(MAX_ROUNDS + 2)])

    skill, bonus_hp = strategy, 0
    if strategy is not None and strategy.startswith("hp+"):
        skill, bonus_hp = None, int(strategy[3:])

    random.seed(seed)
    p0 = skill_value.ForcedSkillAgent(RuleBrain("normal", config, random.Random(seed)), skill, None)
    p1 = RuleAgent(RuleBrain("normal", config, random.Random(seed + 1)), use_skills=False)
    sim = MatchSimulator([p0, p1], deal_factory=lambda _n: pool.get(next(deals)),
                         opening_boosts=False, rng=random.Random(seed))
    sim.engine.update_player_state("P0", health=hp[0] + bonus_hp)
    sim.engine.update_player_state("P1", health=hp[1])

    first_round_skills: list[str] = []
    rounds = 0
    while not sim.ended and rounds < MAX_ROUNDS:
        round_seed = seed * 7919 + rounds * 104729

        def align_random(round_seed=round_seed):
            # スキルの有無で乱数の消費が変わるので、毎局の打牌フェーズの前にそろえ直す
            random.seed(round_seed + 2)
            p0.brain.rng.seed(round_seed + 3)
            p1.brain.rng.seed(round_seed + 4)

        random.seed(round_seed)
        p1.brain.rng.seed(round_seed + 1)
        record = sim.play_round(before_discard_phase=align_random)
        if rounds == 0:
            first_round_skills = list(record.skills["P0"])
        rounds += 1

    players = {p.player_id: p for p in sim.engine.state.players}
    return {
        "winner": game_winner(sim),
        "rounds": rounds,
        "end_reason": sim.engine.game_end_reason or "max_rounds_cap",
        "final_diff": players["P0"].health - players["P1"].health,
        "used": bool(first_round_skills) or bonus_hp != 0,
        "skills": first_round_skills,
    }


def _trial(args: tuple) -> dict:
    deal_seed, seed, hp, strategies = args
    base = _game(deal_seed, seed, hp, None)
    return {"base": base, "runs": {s: _game(deal_seed, seed, hp, s) for s in strategies}}


# ========== 集計 ==========

def summarize(results: list[dict], strategies: tuple[str, ...], hp: tuple[int, int]) -> dict:
    def score(game: dict) -> float:
        return {"P0": 1.0, "P1": 0.0}.get(game["winner"], 0.5)

    base_rounds = [r["base"]["rounds"] for r in results]
    summary = {
        "hp": list(hp),
        "trials": len(results),
        "base": {
            "p0_win_rate": sum(score(r["base"]) for r in results) / max(1, len(results)),
            "rounds_mean": statistics.mean(base_rounds) if base_rounds else 0,
            "rounds_median": statistics.median(base_rounds) if base_rounds else 0,
            "end_reasons": dict(Counter(r["base"]["end_reason"] for r in results)),
        },
        "strategies": {},
    }
    for strategy in strategies:
        used = [r for r in results if r["runs"][strategy]["used"]]
        win_diff, win_se = skill_value._mean_se([score(r["runs"][strategy]) - score(r["base"]) for r in used])
        hp_diff, hp_se = skill_value._mean_se([r["runs"][strategy]["final_diff"] - r["base"]["final_diff"] for r in used])
        cost = skill_value._mean_se([
            sum(skill_value._skill_cost(s) for s in r["runs"][strategy]["skills"]) for r in used
        ])[0]
        summary["strategies"][strategy] = {
            "used": len(used),
            "applicable_rate": len(used) / max(1, len(results)),
            "cost_per_use": cost,
            "win_rate_diff": win_diff,  # 使えた対局での P0 の勝率の差（コスト込み）
            "win_rate_diff_se": win_se,
            "final_hp_diff": hp_diff,  # 対局の終わりの HP の差の差（コスト込み）
            "final_hp_diff_se": hp_se,
            "rounds_mean": statistics.mean(r["runs"][strategy]["rounds"] for r in used) if used else 0,
        }
    return summary


def format_report(summary: dict) -> str:
    base = summary["base"]
    lines = [
        f"開始 HP {summary['hp']}、{summary['trials']} 対局。スキルなしの P0 勝率 {base['p0_win_rate']:.1%}、"
        f"局数 平均 {base['rounds_mean']:.1f}・中央値 {base['rounds_median']}、終わり方 {base['end_reasons']}",
        "効果はコスト込み（HP を払った分も含めた、対局の勝ち負けへの影響）",
        "",
        f"{'使い方':<40} {'使えた':>6} {'勝率の差':>16} {'最終HP差の差':>18} {'コスト':>7} {'局数':>5}",
    ]
    for name, s in summary["strategies"].items():
        lines.append(
            f"{name:<40} {s['applicable_rate']:>6.0%} "
            f"{s['win_rate_diff']:>+8.2%} ±{s['win_rate_diff_se']:<6.2%} "
            f"{s['final_hp_diff']:>+9.0f} ±{s['final_hp_diff_se']:<6.0f} {s['cost_per_use']:>7.0f} {s['rounds_mean']:>5.1f}"
        )
    return "\n".join(lines)


def run(pool_dir: Path, trials: int, seed: int, workers: int, strategies: tuple[str, ...], hp: tuple[int, int],
        override: Optional[dict] = None, log=print) -> dict:
    rng = random.Random(seed)
    tasks = [(rng.randrange(2**31), rng.randrange(2**31), hp, strategies) for _ in range(trials)]
    log(f"対局での効果の測定: {trials} 試行 × {len(strategies) + 1} 対局、開始 HP {hp}、{workers} プロセス")
    started = time.perf_counter()
    results = []
    with ProcessPoolExecutor(max_workers=workers, initializer=skill_value._init_worker,
                             initargs=(str(pool_dir), override)) as executor:
        for i, result in enumerate(executor.map(_trial, tasks, chunksize=2), start=1):
            results.append(result)
            if i % 200 == 0:
                log(f"  {i}/{trials} 試行 {time.perf_counter() - started:.0f} 秒")
    summary = summarize(results, strategies, hp)
    summary["elapsed_sec"] = round(time.perf_counter() - started, 1)
    return summary


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="スキルの効果を対局の勝ち負けで測る")
    parser.add_argument("--pool", type=Path, required=True)
    parser.add_argument("--trials", type=int, default=1000)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--workers", type=int, default=os.cpu_count() or 1)
    parser.add_argument("--hp", default="20000,20000", help="開始時の HP（P0,P1）")
    parser.add_argument("--override", default="{}", help="「ふつう」の設定の上書き（JSON）")
    parser.add_argument("--strategies", required=True, help="1 局目に P0 が使うスキルの手順（カンマ区切り）。hp+N も可")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    strategies = tuple(name for name in args.strategies.split(",") if name)
    hp = tuple(int(v) for v in args.hp.split(","))
    summary = run(args.pool, args.trials, args.seed, args.workers, strategies, hp, override=json.loads(args.override))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    report = format_report(summary)
    args.out.with_suffix(".txt").write_text(report, encoding="utf-8")
    print(report)
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
