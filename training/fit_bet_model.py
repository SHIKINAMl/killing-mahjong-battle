"""
掛け金の見積もり式を、記録から作る（ふつう CPU の掛け金の判断に使う）

training/bet_data.py の記録（掛け金を決めたときの判断材料と、その局の結果）から次の 4 つを作る。
判断材料は RuleBrain.bet_features と同じ名前。

- win  … 和了る確率（ロジスティック回帰）
- lose … 放銃する確率（ロジスティック回帰）
- gain … 和了ったときの、掛け金 1 あたりの得る額（線形回帰。和了った局だけ）
- loss … 放銃したときの、掛け金 1 あたりの失う額（線形回帰。放銃した局だけ）

どれも、交差検証の誤差がいちばん下がる判断材料を 1 つずつ足し、下がらなくなったら止める（前進選択）。

使い方（リポジトリ直下で）:
    python -m training.fit_bet_model --raw training/data/bet_raw.jsonl --out training/data/bet_model.json --write-config
"""
import argparse
import json
import random
import sys
from pathlib import Path
from typing import Callable

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from training.fit_skill_model import MIN_IMPROVEMENT, ridge  # noqa: E402

FOLDS = 5


def load(raw_path: Path) -> list[dict]:
    with raw_path.open(encoding="utf-8") as f:
        return [json.loads(line) for line in f]


def logistic(x: np.ndarray, y: np.ndarray, alpha: float) -> tuple[float, np.ndarray]:
    """標準化した上での L2 付きロジスティック回帰（ニュートン法）。元の単位の (切片, 重み) を返す。"""
    mean, std = x.mean(axis=0), x.std(axis=0)
    std[std == 0] = 1.0
    z = np.column_stack([np.ones(len(y)), (x - mean) / std])
    w = np.zeros(z.shape[1])
    penalty = alpha * np.eye(z.shape[1])
    penalty[0, 0] = 0.0
    for _ in range(50):
        p = 1 / (1 + np.exp(-(z @ w)))
        grad = z.T @ (p - y) + penalty @ w
        hess = (z * (p * (1 - p))[:, None]).T @ z + penalty
        step = np.linalg.solve(hess, grad)
        w -= step
        if np.abs(step).max() < 1e-8:
            break
    weights = w[1:] / std
    return float(w[0] - mean @ weights), weights


def _logloss(y: np.ndarray, p: np.ndarray) -> float:
    p = np.clip(p, 1e-6, 1 - 1e-6)
    return float(-(y * np.log(p) + (1 - y) * np.log(1 - p)).sum())


def _cv_error(x: np.ndarray, y: np.ndarray, alpha: float, seed: int, kind: str) -> float:
    """交差検証の誤差（確率は対数損失、額は二乗誤差）。x が 0 列なら平均値だけで見積もる。"""
    order = list(range(len(y)))
    random.Random(seed).shuffle(order)
    folds = [order[i::FOLDS] for i in range(FOLDS)]
    total = 0.0
    for test in folds:
        test_set = set(test)
        train = [i for i in order if i not in test_set]
        if x.shape[1] == 0:
            pred = np.full(len(test), y[train].mean())
        elif kind == "prob":
            b, w = logistic(x[train], y[train], alpha)
            pred = 1 / (1 + np.exp(-(b + x[test] @ w)))
        else:
            b, w = ridge(x[train], y[train], alpha)
            pred = b + x[test] @ w
        total += _logloss(y[test], pred) if kind == "prob" else float(((y[test] - pred) ** 2).sum())
    return total / len(y)


def fit_one(columns: dict[str, np.ndarray], y: np.ndarray, alpha: float, seed: int, kind: str) -> dict:
    chosen: list[str] = []
    best = base = _cv_error(np.zeros((len(y), 0)), y, alpha, seed, kind)
    while len(chosen) < len(columns):
        trials = []
        for name in columns:
            if name in chosen:
                continue
            x = np.column_stack([columns[c] for c in chosen + [name]])
            trials.append((_cv_error(x, y, alpha, seed, kind), name))
        err, name = min(trials)
        if err >= best * (1 - MIN_IMPROVEMENT):
            break
        chosen.append(name)
        best = err
    fitter: Callable = logistic if kind == "prob" else ridge
    if chosen:
        intercept, weights = fitter(np.column_stack([columns[c] for c in chosen]), y, alpha)
    else:
        mean = float(y.mean())
        intercept, weights = (float(np.log(mean / (1 - mean))) if kind == "prob" else mean), np.zeros(0)
    fitted = dict(zip(chosen, weights))
    return {
        "model": {"intercept": round(float(intercept), 4),
                  "weights": {name: round(float(fitted.get(name, 0.0)), 5) for name in columns}},
        "report": {"n": len(y), "mean": float(y.mean()), "features": chosen,
                   "cv_error": best, "cv_error_constant": base},
    }


def fit(rows: list[dict], alpha: float, seed: int) -> dict:
    names = sorted(rows[0]["features"])
    targets = {
        "win": ([r for r in rows], lambda r: float(r["result"] == "win"), "prob"),
        "lose": ([r for r in rows], lambda r: float(r["result"] == "lose"), "prob"),
        "gain": ([r for r in rows if r["result"] == "win"], lambda r: r["unit_gain"], "amount"),
        "loss": ([r for r in rows if r["result"] == "lose"], lambda r: r["unit_loss"], "amount"),
    }
    model, report = {}, {}
    for key, (subset, target, kind) in targets.items():
        columns = {n: np.array([r["features"][n] for r in subset], dtype=float) for n in names}
        y = np.array([target(r) for r in subset], dtype=float)
        result = fit_one(columns, y, alpha, seed, kind)
        model[key], report[key] = result["model"], result["report"]
    return {"model": model, "report": report}


def write_config(model: dict) -> None:
    """cpu_config.json の hard.bet.model だけを書き換える（ふつうは hard を土台にする。ほかの行の書き方は変えない）。"""
    from mahjong_engine.ai import cpu_config

    path = Path(cpu_config.__file__).with_name("cpu_config.json")
    text = path.read_text(encoding="utf-8")
    bet = text.index('"bet"', text.index('"hard"'))
    start = text.index('"model"', bet)
    brace = text.index("{", start)
    depth, end = 0, brace
    for end in range(brace, len(text)):
        depth += {"{": 1, "}": -1}.get(text[end], 0)
        if depth == 0:
            break
    lines = ",\n".join(f'        "{key}": {json.dumps(entry, ensure_ascii=False)}' for key, entry in model.items())
    text = text[:brace] + "{\n" + lines + "\n      }" + text[end + 1:]
    json.loads(text)
    path.write_text(text, encoding="utf-8")
    print(f"書き込み: {path}")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="掛け金の見積もり式を記録から作る")
    parser.add_argument("--raw", type=Path, required=True)
    parser.add_argument("--alpha", type=float, default=1.0)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--write-config", action="store_true", help="cpu_config.json の hard.bet.model に書き込む")
    args = parser.parse_args(argv)

    result = fit(load(args.raw), args.alpha, args.seed)
    args.out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    for key, rep in result["report"].items():
        entry = result["model"][key]
        print(f"[{key}] n={rep['n']} 平均 {rep['mean']:.3f}  交差検証の誤差 {rep['cv_error']:.4f}"
              f"（平均値だけ {rep['cv_error_constant']:.4f}）  切片 {entry['intercept']}")
        for name in rep["features"]:
            print(f"  {name:<24} {entry['weights'][name]:>10.4f}")
    if args.write_config:
        write_config(result["model"])
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
