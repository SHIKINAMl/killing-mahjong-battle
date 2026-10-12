"""
規則で打つ CPU の設定値を、CPU どうしの対局の勝率で 1 つずつ調整する（座標探索）

調整するのは「つよい」の設定（cpu_config.json の hard。ふつうはこれを土台に弱めたもの）。
設定値を 1 つだけ変えたものを、いまの設定と同じ配牌・席入れ替えで打たせ（training/cpu_match.py）、
勝率が 50% より「標準誤差 × z」以上高ければその値を採用して次の設定値へ進む。
採用した値は、以降の比較の土台（相手側にも同じ値）になる。cpu_config.json は書き換えない（結果を見て人が決める）。

使い方（リポジトリ直下で）:
    python -m training.tune_cpu --pool training/data/deals --group hand --games 800 --out training/data/tune_hand.json
"""
import argparse
import copy
import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from training import cpu_match  # noqa: E402

# 調整する設定値（「ふつう」の設定のパス）と、試す値
GROUPS: dict[str, list[tuple[str, list]]] = {
    # 手牌の決め方: 点数 = Σ 倍率^weight_han_multiplier × 枚数^weight_wait_count
    "hand": [
        ("hand_selection.weight_han_multiplier", [0.5, 1.5, 2.0]),
        ("hand_selection.weight_wait_count", [0.5, 1.5]),
        ("hand_selection.penalty_wait_in_own_wall", [0.0, 1.0]),
        ("hand_selection.bonus_wait_seen_in_opponent_wall", [0.0, 0.6]),
        ("hand_selection.candidate_limit", [20, 80]),
    ],
    # ボルテージを考えた打牌（0 = 考えない）
    "voltage": [
        ("discard.weight_voltage", [10, 30, 100]),
    ],
    # ボルテージは、危険度がほぼ同じ牌どうしの選び分けにだけ効く小さな重み
    "voltage_small": [
        ("discard.weight_voltage", [1, 3]),
    ],
    # 単騎待ちを考えた手牌（0 = 考えない）
    "tanki": [
        ("hand_selection.tanki_bonus", [0.3, 0.6, 1.0]),
    ],
    # 打牌の決め方
    "discard": [
        ("discard.weight_danger", [-5, -20]),
        ("discard.weight_own_wait", [-20, -100]),
        ("discard.suji_factor", [0.25, 0.75]),
        ("discard.weight_exposed_neighbor", [0.5, 3.0]),
        ("discard.danger_honor", [0.1, 0.6]),
        ("discard.danger_terminal", [0.4, 0.8]),
    ],
}


def _nested(path: str, value) -> dict:
    keys = path.split(".")
    result: dict = {keys[-1]: value}
    for key in reversed(keys[:-1]):
        result = {key: result}
    return result


def _merge(base: dict, override: dict) -> dict:
    merged = copy.deepcopy(base)
    cpu_match._deep_update(merged, override)
    return merged


def _current(path: str, adopted: dict):
    level, config, _ = cpu_match.variant_config("hard", adopted)
    value = config
    for key in path.split("."):
        value = value[key]
    return value


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="「ふつう」の設定値を CPU どうしの対局で調整する")
    parser.add_argument("--pool", type=Path, required=True)
    parser.add_argument("--group", choices=sorted(GROUPS), required=True)
    parser.add_argument("--games", type=int, default=800, help="1 つの値の比較に使う対局数")
    parser.add_argument("--z", type=float, default=2.0, help="採用に必要な勝率の上積み（標準誤差の何倍か）")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--workers", type=int, default=min(20, os.cpu_count() or 1))
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    adopted: dict = {}
    history = []
    started = time.perf_counter()
    for path, values in GROUPS[args.group]:
        base_value = _current(path, adopted)
        best = None
        for value in values:
            if value == base_value:
                continue
            candidate = _merge(adopted, _nested(path, value))
            summary = cpu_match.run(args.pool, max(1, args.games // 2), "hard", "hard", args.seed, args.workers,
                                    candidate, adopted, log=lambda *_: None)
            rate, se = summary["a_win_rate"], summary["a_win_rate_se"]
            entry = {"path": path, "base": base_value, "value": value, "win_rate": rate, "se": se,
                     "uses": summary["skills"]["a:hard"]["uses_per_game"]}
            history.append(entry)
            print(f"{path} = {value}（いま {base_value}）: 勝率 {rate:.1%} ±{se:.1%}"
                  f"  {time.perf_counter() - started:.0f} 秒", flush=True)
            if rate - 0.5 >= args.z * se and (best is None or rate > best[1]):
                best = (value, rate)
        if best is not None:
            adopted = _merge(adopted, _nested(path, best[0]))
            print(f"  → 採用: {path} = {best[0]}", flush=True)
        args.out.write_text(json.dumps({"adopted": adopted, "history": history}, ensure_ascii=False, indent=2),
                            encoding="utf-8")
    print(f"採用した値: {json.dumps(adopted, ensure_ascii=False)}")
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
