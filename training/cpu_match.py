"""
CPU どうしの対局で、考え方の強さを比べる

2 つの考え方（variant）を、同じ配牌・乱数で席を入れ替えて 1 組 2 対局ずつ打たせ、勝率を出す。
スキルを何回使ったか・HP をいくら払ったかも数える。

variant:
- value   … いまの「ふつう」（スキルの効果を見積もって使う。cpu_config.json の value_model）
- hard    … 「つよい」の設定を規則で打つもの（ふつうの土台。学習モデルができるまでのつよい）
- model   … 本番のつよい（学習したモデル。cpu_config.json の hard.model_path）
- rule    … 「ふつう」から value_model を外したもの（決まった条件で使う、前の考え方）
- stepbet … 「ふつう」から掛け金の見積もり式（bet.model）を外したもの（手の強さの段階で決める、前の掛け金）
- noskill … 「ふつう」でスキルを使わない
- easy    … 「よわい」
- exploit … 人間の読みをまねる打ち手（一九字牌待ち＋字牌の透視。training/exploiters.py）。CPU の弱点の測定用。
            土台の打ち方は legacy（前のふつう）に固定する（CPU を直しても、測る相手が変わらないように）。
            つよいと比べるときは --a-override '{"hand_selection": {"candidate_limit": 160}}' で探索をそろえる
- legacy  … 前のふつう（読まれにくくする仕組みを入れる前: 危険度は決まった値、揺らぎ・戦術なし）
- oldmodel … 前のつよい（学習したモデルを、legacy と同じく読まれにくくする仕組みなしで打つ）

使い方（リポジトリ直下で）:
    python -m training.cpu_match --pool training/data/deals --games 1000 --a value --b rule \\
        --out training/data/cpu_match_value_vs_rule.json
"""
import argparse
import copy
import logging
import json
import os
import random
import sys
import time
from collections import Counter
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from typing import Optional

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai.cpu_config import load_cpu_config  # noqa: E402
from mahjong_engine.ai.rule_brain import RuleBrain, create_brain  # noqa: E402
from mahjong_engine.engine.game_state import SkillType, get_skill_cost  # noqa: E402
from training.deal_pool import DealPool  # noqa: E402
from training.exploiters import ExploitBrain  # noqa: E402
from training.simulator import PLAYER_IDS, MatchSimulator, RuleAgent  # noqa: E402
from training.skill_game import MAX_ROUNDS, game_winner  # noqa: E402

VARIANTS = ("value", "hard", "model", "rule", "stepbet", "noskill", "easy", "exploit", "legacy", "oldmodel")

# 読まれにくくする仕組み（危険度の数え方・揺らぎ・戦術）を入れる前の打ち方にする上書き
LEGACY_OVERRIDE = {
    "discard": {"danger_model": "table", "weight_seen_by_opponent": 0.0},
    "variation": {"discard_temperature": 0.0, "hand_temperature": 0.0, "bet_noise": 0.0, "bluff_rate": 0.0,
                  "slowplay_rate": 0.0, "skill_noise": 0.0, "model_temperature": 0.0},
    "tactics": {"yaochu": {"counter_prior": 1.0, "counter_weight": 0.0, "adopt_weight": 0.0}},
}

_POOL: Optional[DealPool] = None
_CONFIGS: dict[str, dict] = {}


def _deep_update(base: dict, override: dict) -> None:
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            _deep_update(base[key], value)
        else:
            base[key] = value


def variant_config(name: str, override: Optional[dict] = None) -> tuple[str, dict, bool]:
    """(考え方の水準, 設定, スキルを使うか)。override は設定の上書き（調整の比較用）"""
    configs = load_cpu_config()
    level = "easy" if name == "easy" else "normal"
    config = copy.deepcopy(configs["hard" if name in ("hard", "model", "oldmodel") else level])
    if name == "rule":
        config["skill"].pop("value_model", None)
    if name == "stepbet":
        config["bet"].pop("model", None)
    if name in ("exploit", "legacy", "oldmodel"):
        _deep_update(config, copy.deepcopy(LEGACY_OVERRIDE))
    _deep_update(config, override or {})
    return level, config, name != "noskill"


def _init_worker(pool_dir: str, overrides: dict) -> None:
    global _POOL
    logging.getLogger("mahjong_engine").setLevel(logging.ERROR)  # 決着の警告は集計に要らない
    _POOL = DealPool(Path(pool_dir))
    for key, (name, override) in overrides.items():
        _CONFIGS[key] = variant_config(name, override)


def _agent(name: str, seed: int) -> RuleAgent:
    level, config, use_skills = _CONFIGS[name]
    if name.split(":")[-1] in ("model", "oldmodel"):
        return RuleAgent(create_brain(config, random.Random(seed)), use_skills=use_skills)
    if name.split(":")[-1] == "exploit":
        return RuleAgent(ExploitBrain(config, random.Random(seed)), use_skills=use_skills)
    return RuleAgent(RuleBrain(level, config, random.Random(seed)), use_skills=use_skills)


def _game(args: tuple) -> dict:
    """1 対局。席 P0 / P1 にそれぞれの考え方を座らせる。"""
    deal_seed, seed, p0_name, p1_name = args
    deal_rng = random.Random(deal_seed)
    deals = iter([deal_rng.randrange(len(_POOL)) for _ in range(MAX_ROUNDS + 2)])
    random.seed(seed)
    agents = {"P0": _agent(p0_name, seed), "P1": _agent(p1_name, seed + 1)}
    sim = MatchSimulator([agents["P0"], agents["P1"]], deal_factory=lambda _n: _POOL.get(next(deals)),
                         rng=random.Random(seed))
    skills = {pid: Counter() for pid in PLAYER_IDS}
    # 局ごとの記録（席ごと）: 使ったスキル・払った HP・掛け金・結果・得た額・失った額
    round_log: dict[str, list[dict]] = {pid: [] for pid in PLAYER_IDS}
    rounds = 0
    while not sim.ended and rounds < MAX_ROUNDS:
        sim.start_round()
        # 手牌を決めた後の待ちが 1 種類か（単騎待ちボーナスの対象の手を選んだか）
        single_wait = {p.player_id: len({w & 0b11111 for w in p.waits}) == 1 for p in sim.engine.state.players}
        sim.advance_discards()
        record = sim.finish_round()
        liq = record.liquidation
        for pid in PLAYER_IDS:
            skills[pid].update(record.skills[pid])
            result = "draw" if liq is None else ("win" if liq["winner_id"] == pid else "lose")
            round_log[pid].append({
                "skills": list(record.skills[pid]),
                "cost": sum(get_skill_cost(SkillType(k), 0) for k in record.skills[pid]),
                "bet": record.bets[pid],
                "result": result,
                "gain": liq["winner_gain"] if result == "win" else 0,
                "loss": liq["loser_loss"] if result == "lose" else 0,
                "tanki_hand": single_wait.get(pid, False),
                "tanki_win": result == "win" and bool(liq.get("is_tanki_wait")),
                "damage": (liq["loser_loss"] - liq.get("assault_bonus_damage", 0)) if result == "win" else 0,
            })
        rounds += 1
    return {
        "names": {"P0": p0_name, "P1": p1_name},
        "winner": game_winner(sim),
        "rounds": rounds,
        "end_reason": sim.engine.game_end_reason or "max_rounds_cap",
        "skills": {pid: dict(c) for pid, c in skills.items()},
        "round_log": round_log,
    }


def summarize(results: list[dict], a: str, b: str) -> dict:
    score = []
    usage = {a: Counter(), b: Counter()}
    seats = Counter()
    for r in results:
        for pid in PLAYER_IDS:
            name = r["names"][pid]
            usage[name].update(r["skills"][pid])
            seats[name] += 1
        a_seat = "P0" if r["names"]["P0"] == a else "P1"
        score.append(1.0 if r["winner"] == a_seat else 0.0 if r["winner"] is not None else 0.5)
    n = len(score)
    mean = sum(score) / max(1, n)
    se = (sum((s - mean) ** 2 for s in score) / max(1, n - 1) / max(1, n)) ** 0.5

    def round_stats(rows: list[dict], games: int) -> dict:
        """局の集まりの成績（1 対局あたりの局数、局の勝率・放銃率、1 局あたりの得た額・失った額・払った HP）。"""
        n = len(rows)
        if n == 0:
            return {"rounds_per_game": 0.0}
        return {
            "rounds_per_game": n / games,
            "win_rate": sum(r["result"] == "win" for r in rows) / n,
            "lose_rate": sum(r["result"] == "lose" for r in rows) / n,
            "bet": sum(r["bet"] for r in rows) / n,
            "gain": sum(r["gain"] for r in rows) / n,
            "loss": sum(r["loss"] for r in rows) / n,
            "cost": sum(r["cost"] for r in rows) / n,
            "net": sum(r["gain"] - r["loss"] - r["cost"] for r in rows) / n,
            "damage": sum(r.get("damage", 0) for r in rows) / n,  # 和了で相手に与えた損失（強襲の追加分を除く）
        }

    def per_game(name: str) -> dict:
        games = max(1, seats[name])
        uses = {skill: count / games for skill, count in sorted(usage[name].items())}
        cost = sum(count * get_skill_cost(SkillType(skill), 0) for skill, count in usage[name].items()) / games
        rows = [row for r in results for pid in PLAYER_IDS if r["names"][pid] == name for row in r.get("round_log", {}).get(pid, [])]
        by_skill = {
            skill: round_stats([row for row in rows if skill in row["skills"]], games)
            for skill in sorted({k for row in rows for k in row["skills"]})
        }
        return {
            "uses_per_game": uses,
            "cost_per_game": cost,
            "gain_per_game": sum(row["gain"] for row in rows) / games,
            "loss_per_game": sum(row["loss"] for row in rows) / games,
            "all_rounds": round_stats(rows, games),
            "no_skill_rounds": round_stats([row for row in rows if not row["skills"]], games),
            "tanki_rounds": round_stats([row for row in rows if row.get("tanki_hand")], games),
            "non_tanki_rounds": round_stats([row for row in rows if not row.get("tanki_hand")], games),
            "rounds_with_skill": by_skill,
        }

    return {
        "a": a, "b": b, "games": n,
        "a_win_rate": mean, "a_win_rate_se": se,
        "rounds_mean": sum(r["rounds"] for r in results) / max(1, n),
        "end_reasons": dict(Counter(r["end_reason"] for r in results)),
        "skills": {a: per_game(a), b: per_game(b)},
    }


def format_report(s: dict) -> str:
    lines = [
        f"{s['a']} 対 {s['b']}: {s['games']} 対局（席を入れ替えて同じ配牌）",
        f"  {s['a']} の勝率 {s['a_win_rate']:.1%} ±{s['a_win_rate_se']:.1%}、"
        f"局数 平均 {s['rounds_mean']:.1f}、終わり方 {s['end_reasons']}",
    ]
    for name, usage in s["skills"].items():
        uses = "、".join(f"{k} {v:.2f}" for k, v in usage["uses_per_game"].items()) or "なし"
        lines.append(f"  {name}: 1 対局あたり {uses}（払った HP {usage['cost_per_game']:.0f}、"
                     f"得た額 {usage['gain_per_game']:.0f}、失った額 {usage['loss_per_game']:.0f}）")
        groups = [("全部の局", usage["all_rounds"]), ("スキルなしの局", usage["no_skill_rounds"]),
                  ("単騎（1 種類待ち）の局", usage["tanki_rounds"]), ("それ以外の局", usage["non_tanki_rounds"])]
        groups += [(f"{k} を使った局", v) for k, v in usage["rounds_with_skill"].items()]
        for label, st in groups:
            if not st.get("rounds_per_game"):
                continue
            lines.append(
                f"    {label:<22} {st['rounds_per_game']:>5.2f} 局/対局  和了 {st['win_rate']:.1%}  放銃 {st['lose_rate']:.1%}  "
                f"掛け金 {st['bet']:>5.0f}  得た額 {st['gain']:>5.0f}  失った額 {st['loss']:>5.0f}  "
                f"払った HP {st['cost']:>5.0f}  与えた損失 {st['damage']:>5.0f}  差し引き {st['net']:>+6.0f}（1 局あたり）")
    return "\n".join(lines)


def run(pool_dir: Path, pairs: int, a: str, b: str, seed: int, workers: int,
        a_override: Optional[dict] = None, b_override: Optional[dict] = None, log=print) -> dict:
    # 同じ考え方を設定違いで比べられるよう、席の名前は「a:考え方」「b:考え方」にする
    overrides = {f"a:{a}": (a, a_override), f"b:{b}": (b, b_override)}
    a, b = f"a:{a}", f"b:{b}"
    rng = random.Random(seed)
    tasks = []
    for _ in range(pairs):
        deal_seed, game_seed = rng.randrange(2**31), rng.randrange(2**31)
        tasks.append((deal_seed, game_seed, a, b))
        tasks.append((deal_seed, game_seed, b, a))
    log(f"CPU どうしの対局: {a} 対 {b}、{len(tasks)} 対局、{workers} プロセス")
    started = time.perf_counter()
    results = []
    with ProcessPoolExecutor(max_workers=workers, initializer=_init_worker, initargs=(str(pool_dir), overrides)) as executor:
        for i, result in enumerate(executor.map(_game, tasks, chunksize=2), start=1):
            results.append(result)
            if i % 200 == 0:
                log(f"  {i}/{len(tasks)} 対局 {time.perf_counter() - started:.0f} 秒")
    summary = summarize(results, a, b)
    summary["elapsed_sec"] = round(time.perf_counter() - started, 1)
    return summary


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="CPU どうしの対局で考え方の強さを比べる")
    parser.add_argument("--pool", type=Path, required=True)
    parser.add_argument("--games", type=int, default=1000, help="対局数（席を入れ替えた 2 対局で 1 組）")
    parser.add_argument("--a", choices=VARIANTS, default="value")
    parser.add_argument("--b", choices=VARIANTS, default="rule")
    parser.add_argument("--a-override", default="{}", help="a の設定の上書き（JSON）")
    parser.add_argument("--b-override", default="{}", help="b の設定の上書き（JSON）")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--workers", type=int, default=os.cpu_count() or 1)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    summary = run(args.pool, max(1, args.games // 2), args.a, args.b, args.seed, args.workers,
                  json.loads(args.a_override), json.loads(args.b_override))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    report = format_report(summary)
    args.out.with_suffix(".txt").write_text(report, encoding="utf-8")
    print(report)
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
