"""
強化学習の進み具合を見る（学習を止めずに、記録ファイル ppo_log.jsonl を読むだけ）

使い方（リポジトリ直下で）:
    python -m training.rl_progress training/data/rl/run3
"""
import json
import sys
from pathlib import Path


def main(argv=None) -> int:
    args = argv if argv is not None else sys.argv[1:]
    run_dir = Path(args[0]) if args else Path("training/data/rl/run3")
    log = run_dir / "ppo_log.jsonl"
    if not log.exists():
        print(f"{log} がまだありません（模倣学習の途中か、まだ 1 回目の更新が終わっていません）")
        return 1
    rows = [json.loads(line) for line in log.read_text(encoding="utf-8").splitlines() if line.strip()]
    if not rows:
        print("まだ記録がありません")
        return 1
    per_iteration = sum(r["sec"] for r in rows) / len(rows)
    print(f"{run_dir}: {len(rows)} 回更新済み（1 回あたり約 {per_iteration / 60:.1f} 分）")
    evals = [r for r in rows if "eval" in r]
    if evals:
        print("規則のつよいとの勝率（ネットがいちばん確率の高い行動を取ったとき）:")
        best = max(evals, key=lambda r: r["eval"]["win_rate"])
        for r in evals:
            mark = "  ← いちばん良い（best.npz）" if r is best else ""
            print(f"  {r['iteration']:>4} 回目: {r['eval']['win_rate']:.1%} ±{r['eval']['se']:.1%}{mark}")
    recent = [r["vs_rule_sample"] for r in rows[-10:] if r.get("vs_rule_sample") is not None]
    if recent:
        print(f"直近 {len(recent)} 回の学習中の対局（確率どおりに選ぶ）での勝率: 平均 {sum(recent) / len(recent):.1%}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
