"""
スキルの効果の見積もり式を、測定結果から作る（ふつう CPU のスキル判断に使う）

training/skill_strategy.py --raw の出力（1 試行ごとの判断材料と効果）から、スキルごとに
「掛け金 1000 あたりの効果 ≒ 切片 + Σ 重み × 判断材料」の重みを最小二乗（リッジ）で求める。
判断材料は RuleBrain.skill_features と同じ名前。

- 1 回目の効果は「スキル単独」の使い方（例: mulligan）から求める
- 2 回目の効果は「同じスキル 2 回」と「1 回」の差の平均を、1 回目の平均で割った比（repeat_factor）にする
- 1 試行ごとの効果はばらつきがとても大きいので、判断材料を全部使うと当たらない（交差検証で確かめた）。
  交差検証の誤差がいちばん下がる判断材料を 1 つずつ足し、下がらなくなったら止める（前進選択）。
  選ばれなかった判断材料の重みは 0
- 交差検証で、判断材料を使うと「いつも平均値と見積もる」よりどれだけ当たるかも出す

使い方（リポジトリ直下で）:
    python -m training.fit_skill_model --raw training/data/skill_raw.jsonl --out training/data/skill_model.json
    # cpu_config.json の hard.skill.value_model に書き込むなら --write-config
"""
import argparse
import json
import random
import sys
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

SKILLS = ("perspective", "mulligan", "boost_hand")
FOLDS = 5
# 前進選択で判断材料を足す条件: 交差検証の誤差がこの割合より下がること
MIN_IMPROVEMENT = 0.0005


def load(raw_path: Path) -> list[dict]:
    rows = []
    with raw_path.open(encoding="utf-8") as f:
        for line in f:
            row = json.loads(line)
            if row["features"].get("has_plan"):
                rows.append(row)
    return rows


def ridge(x: np.ndarray, y: np.ndarray, alpha: float) -> tuple[float, np.ndarray]:
    """標準化した上でのリッジ回帰。元の単位の (切片, 重み) を返す。"""
    mean, std = x.mean(axis=0), x.std(axis=0)
    std[std == 0] = 1.0
    z = (x - mean) / std
    y_mean = y.mean()
    w = np.linalg.solve(z.T @ z + alpha * np.eye(z.shape[1]), z.T @ (y - y_mean))
    weights = w / std
    return float(y_mean - mean @ weights), weights


def cross_validate(x: np.ndarray, y: np.ndarray, alpha: float, seed: int) -> dict:
    """交差検証の二乗誤差（判断材料あり／いつも平均値）。"""
    order = list(range(len(y)))
    random.Random(seed).shuffle(order)
    folds = [order[i::FOLDS] for i in range(FOLDS)]
    err_model = err_const = 0.0
    for test in folds:
        test_set = set(test)
        train = [i for i in order if i not in test_set]
        intercept, weights = ridge(x[train], y[train], alpha)
        pred = intercept + x[test] @ weights
        err_model += float(((y[test] - pred) ** 2).sum())
        err_const += float(((y[test] - y[train].mean()) ** 2).sum())
    return {"mse_model": err_model / len(y), "mse_constant": err_const / len(y),
            "r2": 1 - err_model / err_const if err_const else 0.0}


def forward_select(columns: dict[str, np.ndarray], y: np.ndarray, alpha: float, seed: int) -> list[str]:
    """交差検証の誤差がいちばん下がる判断材料を 1 つずつ足す。下がらなくなったら止める。"""
    chosen: list[str] = []
    best = float(((y - y.mean()) ** 2).mean())
    while len(chosen) < len(columns):
        trials = []
        for name in columns:
            if name in chosen:
                continue
            x = np.column_stack([columns[c] for c in chosen + [name]])
            trials.append((cross_validate(x, y, alpha, seed)["mse_model"], name))
        mse, name = min(trials)
        if mse >= best * (1 - MIN_IMPROVEMENT):
            break
        chosen.append(name)
        best = mse
    return chosen


def fit(rows: list[dict], bet: int, alpha: float, seed: int) -> dict:
    features = sorted(k for k in rows[0]["features"] if k != "has_plan")
    model: dict = {}
    report: dict = {}
    for skill in SKILLS:
        used = [r for r in rows if r["used"].get(skill)]
        if len(used) < 50:
            continue
        y = np.array([r["effects"][skill] for r in used], dtype=float) * 1000 / bet
        columns = {f: np.array([r["features"][f] for r in used], dtype=float) for f in features}
        chosen = forward_select(columns, y, alpha, seed)
        if chosen:
            x = np.column_stack([columns[c] for c in chosen])
            intercept, weights = ridge(x, y, alpha)
        else:
            x, intercept, weights = np.zeros((len(y), 1)), float(y.mean()), np.zeros(0)
        fitted = dict(zip(chosen, weights))
        # 選ばれなかった判断材料も 0 で書く（設定の検証で、書いていない重みは既定値に戻るため）
        entry = {"intercept": round(intercept, 2),
                 "weights": {f: round(float(fitted.get(f, 0.0)), 3) for f in features}}

        # 2 回目: 「skill>skill」と「skill」の差の平均 ÷ 1 回目の平均
        twice = f"{skill}>{skill}"
        both = [r for r in used if twice in r["effects"]]
        if both and y.mean() > 0:
            second = np.mean([r["effects"][twice] - r["effects"][skill] for r in both]) * 1000 / bet
            entry["repeat_factor"] = round(float(max(0.0, second / y.mean())), 3)
        model[skill] = entry
        report[skill] = {"n": len(used), "mean": float(y.mean()), "features": chosen,
                         **(cross_validate(x, y, alpha, seed) if chosen else {"r2": 0.0})}
    return {"model": model, "report": report, "features": features}


def write_config(model: dict) -> None:
    """cpu_config.json の hard.skill.value_model だけを書き換える（ふつうは hard を土台にする。ほかの行の書き方は変えない）。"""
    from mahjong_engine.ai import cpu_config

    path = Path(cpu_config.__file__).with_name("cpu_config.json")
    text = path.read_text(encoding="utf-8")
    start = text.index('"value_model"', text.index('"hard"'))
    brace = text.index("{", start)
    depth, end = 0, brace
    for end in range(brace, len(text)):
        depth += {"{": 1, "}": -1}.get(text[end], 0)
        if depth == 0:
            break
    lines = ",\n".join(
        f'        "{skill}": {json.dumps(entry, ensure_ascii=False)}' for skill, entry in model.items()
    )
    text = text[:brace] + "{\n" + lines + "\n      }" + text[end + 1:]
    json.loads(text)  # 壊していないか
    path.write_text(text, encoding="utf-8")
    print(f"書き込み: {path}")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="スキルの効果の見積もり式を測定結果から作る")
    parser.add_argument("--raw", type=Path, required=True)
    parser.add_argument("--bet", type=int, default=1000, help="測定したときの掛け金")
    parser.add_argument("--alpha", type=float, default=10.0, help="リッジの強さ（大きいほど重みを 0 に寄せる）")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--write-config", action="store_true", help="cpu_config.json の hard.skill.value_model に書き込む")
    args = parser.parse_args(argv)

    result = fit(load(args.raw), args.bet, args.alpha, args.seed)
    args.out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    for skill, rep in result["report"].items():
        entry = result["model"][skill]
        print(f"[{skill}] n={rep['n']} 平均 {rep['mean']:.0f}  交差検証 R² {rep['r2']:.3f}  "
              f"2 回目の比 {entry.get('repeat_factor', '-')}")
        print(f"  切片 {entry['intercept']}")
        for name in rep["features"]:
            print(f"  {name:<24} {entry['weights'][name]:>9.3f}")
    if args.write_config:
        write_config(result["model"])
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
