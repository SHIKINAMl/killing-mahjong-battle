"""
スキルの使い方の比べ方（ふつう CPU の設計の材料）

skill_value.py と同じく、同じ配牌・同じ乱数で「スキルなし」と「スキルあり」を 1 局ずつ打ち比べ、
両者の HP の差の動き（コストは足し戻す）をスキルの効果とする。ここでは:

- 使い方（手順）を何通りも、同じ配牌・乱数の組で比べる。手順は「>」でつなぐ（ForcedSkillAgent を参照）
    例: perspective, perspective>perspective, mulligan>boost_hand
- 「A>B」と「A」を両方測ると、A の後に B を足した分の効果（上乗せ）も出す
- スキルを使う前の手の様子（手の強さ・倍率・待ちの数など）ごとに効果を分けて出す。
  どんな局で使うと得かを決める材料にする
- --random-scenario で、局ごとに開始時の役強化の数（両者 0〜3）・P0 の初期透視（0 か 7 枚）を変える。
  相手の手の高さ・見えている牌の量が違う局も混ぜて、判断材料の重みを学ぶため
- --raw に、1 試行ごとの判断材料と効果を JSON Lines で書き出す（training/fit_skill_model.py の入力）

使い方（リポジトリ直下で）:
    python -m training.skill_strategy --pool training/data/deals --trials 2000 \\
        --strategies "perspective,perspective>perspective,mulligan,mulligan>boost_hand" \\
        --out training/data/skill_strategy.json
"""
import argparse
import json
import os
import random
import sys
import time
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from typing import Optional

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai.cpu_config import HUMAN_PERKS  # noqa: E402
from training import skill_value  # noqa: E402
from training.deal_pool import DealPool  # noqa: E402

# 効果を分けて見る、スキルを使う前の手の様子（skill_value.plan_features の項目）
# strength は上限（1.0）に張り付きやすいので、上限のない score で分ける
FEATURES = (
    "score", "expected_multiplier", "max_multiplier", "winning_waits", "available", "dead_waits",
    "duplicated_rest", "boost_gain_ratio", "my_boosts", "opp_unseen_best_suit", "opp_exposed_ratio", "opp_boosts",
)
BINS = 4


def random_perks(rng: random.Random) -> list[dict]:
    """局の設定を散らす: 開始時の役強化の数（両者 0〜3）、P0 の初期透視（3 回に 1 回 7 枚）。"""
    return [
        {**HUMAN_PERKS, "opening_boost_count": rng.randint(0, 3),
         "initial_perspective_count": 7 if rng.random() < 1 / 3 else 0},
        {**HUMAN_PERKS, "opening_boost_count": rng.randint(0, 3)},
    ]


def _trial(args: tuple) -> dict:
    """1 つの配牌・乱数で、スキルなしと、それぞれの使い方を打つ。"""
    deal_index, seed, bet, strategies, perks = args
    base = skill_value._play(deal_index, seed, bet, perks=perks)
    runs = {strategy: skill_value._play(deal_index, seed, bet, skill=strategy, perks=perks) for strategy in strategies}
    return {"base": base, "runs": runs}


# ========== 集計 ==========

def _mean_se(values: list[float]) -> tuple[float, float]:
    return skill_value._mean_se(values)


def _bin_edges(values: list[float], bins: int) -> list[float]:
    """区切り（下から等分）。値の種類が少なければ値ごとに分ける。同じ区切りは 1 つにまとめる。"""
    distinct = sorted(set(values))
    if len(distinct) <= bins:
        return distinct[1:]
    ordered = sorted(values)
    edges = {ordered[min(len(ordered) - 1, len(ordered) * i // bins)] for i in range(1, bins)}
    return sorted(edge for edge in edges if edge > ordered[0])


def _bin_of(value: float, edges: list[float]) -> int:
    return sum(value >= edge for edge in edges)


def summarize(results: list[dict], strategies: tuple[str, ...], bet: int) -> dict:
    summary: dict = {"bet": bet, "trials": len(results), "strategies": {}, "increments": {}}

    def effect(r: dict, strategy: str) -> float:
        return r["runs"][strategy]["diff"] - r["base"]["diff"]

    # 手の様子の区切り（スキルを使う前なので、どの使い方でも同じ）
    with_plan = [r for r in results if r["base"]["features"].get("has_plan")]
    edges = {
        name: _bin_edges([r["base"]["features"][name] for r in with_plan], BINS)
        for name in FEATURES
    } if with_plan else {}

    for strategy in strategies:
        used = [r for r in results if r["runs"][strategy]["used"]]
        mean, se = _mean_se([effect(r, strategy) for r in used])
        all_mean, all_se = _mean_se([effect(r, strategy) for r in results])
        cost = _mean_se([
            sum(skill_value._skill_cost(s) for s in r["runs"][strategy]["skills"]) for r in used
        ])[0]
        patterns: dict[str, int] = {}
        for r in used:
            key = ">".join(r["runs"][strategy]["skills"])
            patterns[key] = patterns.get(key, 0) + 1

        by_feature = {}
        for name in FEATURES:
            rows = []
            for b in range(len(edges[name]) + 1):
                in_bin = [r for r in used
                          if r["base"]["features"].get("has_plan")
                          and _bin_of(r["base"]["features"][name], edges[name]) == b]
                m, e = _mean_se([effect(r, strategy) for r in in_bin])
                low = edges[name][b - 1] if b > 0 else None
                rows.append({"from": low, "n": len(in_bin), "effect": m, "se": e})
            by_feature[name] = rows

        summary["strategies"][strategy] = {
            "used": len(used),
            "applicable_rate": len(used) / max(1, len(results)),
            "effect_per_use": mean,
            "effect_se": se,
            "effect_per_round": all_mean,  # 使えない局も 0 として含めた平均
            "effect_per_round_se": all_se,
            "cost_per_use": cost,
            "win_rate_base": sum(r["base"]["p0_win"] for r in used) / max(1, len(used)),
            "win_rate_treated": sum(r["runs"][strategy]["p0_win"] for r in used) / max(1, len(used)),
            "loss_rate_base": sum(r["base"]["p1_win"] for r in used) / max(1, len(used)),
            "loss_rate_treated": sum(r["runs"][strategy]["p1_win"] for r in used) / max(1, len(used)),
            "skill_patterns": patterns,
            "by_feature": by_feature,
        }

    # 「A>B」と「A」の差（A の後に B を足した上乗せ）。同じ配牌・乱数の組で比べる
    for strategy in strategies:
        if ">" not in strategy:
            continue
        prefix = strategy.rsplit(">", 1)[0]
        if prefix not in strategies:
            continue
        diffs = [r["runs"][strategy]["diff"] - r["runs"][prefix]["diff"] for r in results]
        m, e = _mean_se(diffs)
        summary["increments"][strategy] = {"over": prefix, "effect_per_round": m, "se": e}
    return summary


def format_report(summary: dict) -> str:
    lines = [f"掛け金 {summary['bet']}、{summary['trials']} 試行（効果は HP の差の動き。コストは含めない）", ""]
    lines.append(f"{'使い方':<40} {'使えた局':>8} {'1回あたり':>14} {'1局あたり':>14} {'コスト':>7}")
    for name, s in summary["strategies"].items():
        lines.append(
            f"{name:<40} {s['applicable_rate']:>7.0%} "
            f"{s['effect_per_use']:>7.0f} ±{s['effect_se']:<5.0f} "
            f"{s['effect_per_round']:>7.0f} ±{s['effect_per_round_se']:<5.0f} {s['cost_per_use']:>7.0f}"
        )
    if summary["increments"]:
        lines += ["", "上乗せ（前の手順に足した分。1 局あたり）"]
        for name, inc in summary["increments"].items():
            lines.append(f"  {name:<40} vs {inc['over']:<30} {inc['effect_per_round']:>7.0f} ±{inc['se']:.0f}")
    lines += ["", "手の様子ごとの効果（スキルを使えた局。下から等分。「x〜」は x 以上）"]
    for name, s in summary["strategies"].items():
        lines.append(f"  [{name}]")
        for feature, rows in s["by_feature"].items():
            cells = []
            for row in rows:
                start = "    -" if row["from"] is None else f"{row['from']:>5.2f}"
                cells.append(f"{start}〜: {row['effect']:>6.0f}±{row['se']:<4.0f}(n={row['n']})")
            lines.append(f"    {feature:<20} " + " | ".join(cells))
    return "\n".join(lines)


def run(pool_dir: Path, trials: int, bet: int, seed: int, workers: int, strategies: tuple[str, ...],
        override: Optional[dict] = None, random_scenario: bool = False, raw_path: Optional[Path] = None,
        log=print) -> dict:
    pool = DealPool(pool_dir)
    rng = random.Random(seed)
    tasks = [
        (rng.randrange(len(pool)), rng.randrange(2**31), bet, strategies,
         random_perks(rng) if random_scenario else None)
        for _ in range(trials)
    ]

    log(f"スキルの使い方の比較: {trials} 試行 × {len(strategies) + 1} 局、{workers} プロセス")
    started = time.perf_counter()
    results = []
    with ProcessPoolExecutor(max_workers=workers, initializer=skill_value._init_worker,
                             initargs=(str(pool_dir), override)) as executor:
        for i, result in enumerate(executor.map(_trial, tasks, chunksize=4), start=1):
            results.append(result)
            if i % 500 == 0:
                log(f"  {i}/{trials} 試行 {time.perf_counter() - started:.0f} 秒")
    if raw_path is not None:
        raw_path.parent.mkdir(parents=True, exist_ok=True)
        with raw_path.open("w", encoding="utf-8") as f:
            for r in results:
                row = {
                    "features": r["base"]["features"],
                    "effects": {s: r["runs"][s]["diff"] - r["base"]["diff"] for s in strategies},
                    "used": {s: r["runs"][s]["used"] for s in strategies},
                    "skills": {s: r["runs"][s]["skills"] for s in strategies},
                }
                f.write(json.dumps(row, ensure_ascii=False) + "\n")
    summary = summarize(results, strategies, bet)
    summary["elapsed_sec"] = round(time.perf_counter() - started, 1)
    return summary


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="スキルの使い方を対局シミュレーションで比べる")
    parser.add_argument("--pool", type=Path, required=True)
    parser.add_argument("--trials", type=int, default=2000)
    parser.add_argument("--bet", type=int, default=1000)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--workers", type=int, default=os.cpu_count() or 1)
    parser.add_argument("--override", default="{}", help="「ふつう」の設定の上書き（JSON）")
    parser.add_argument("--strategies", required=True, help="使い方（カンマ区切り。手順は > でつなぐ）")
    parser.add_argument("--random-scenario", action="store_true", help="開始時の役強化の数・初期透視を局ごとに変える")
    parser.add_argument("--raw", type=Path, default=None, help="1 試行ごとの判断材料と効果の書き出し先（JSON Lines）")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    strategies = tuple(name for name in args.strategies.split(",") if name)
    summary = run(args.pool, args.trials, args.bet, args.seed, args.workers, strategies,
                  override=json.loads(args.override), random_scenario=args.random_scenario, raw_path=args.raw)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    report = format_report(summary)
    args.out.with_suffix(".txt").write_text(report, encoding="utf-8")
    print(report)
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
