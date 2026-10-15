"""
掛け金の見積もり式を作るための記録（ふつう CPU の掛け金の判断に使う）

規則で打つ「つよい」（ふつうの土台）どうしの対局を打ち、掛け金を決めるたびに判断材料（RuleBrain.bet_features）を記録し、
その局の結果（和了・放銃・流局）と、掛け金 1 あたりの得た額・失った額を付ける。
training/fit_bet_model.py の入力になる。

- 局の結果は掛け金によらない（掛け金は打牌の判断に使わない）ので、いまの掛け金の決め方で集めてよい
- 得た額は勝者の獲得（ボルテージ・流局の持ち越しを含む。強襲なら 0）÷ 自分の掛け金
- 失った額は敗者の損失（単騎の 2 倍・流局の持ち越しを含む。相手の強襲の追加ダメージは除く。
  これは相手の掛け金で決まり、自分の掛け金では変わらないため）÷ 自分の掛け金

使い方（リポジトリ直下で）:
    python -m training.bet_data --pool training/data/deals --games 3000 --out training/data/bet_raw.jsonl
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

from mahjong_engine.ai.cpu_config import load_cpu_config  # noqa: E402
from mahjong_engine.ai.rule_brain import RuleBrain  # noqa: E402
from training.deal_pool import DealPool  # noqa: E402
from training.simulator import PLAYER_IDS, MatchSimulator, RuleAgent  # noqa: E402
from training.skill_game import MAX_ROUNDS  # noqa: E402

_POOL: Optional[DealPool] = None
_CONFIG: Optional[dict] = None


class RecordingAgent(RuleAgent):
    """掛け金を決めるときの判断材料を覚えておく「ふつう」。"""

    def __init__(self, brain: RuleBrain):
        super().__init__(brain)
        self.pending: Optional[dict] = None

    def choose_bet(self, obs, plan):
        self.pending = self.brain.bet_features(obs, plan) if plan is not None else None
        return super().choose_bet(obs, plan)


def _init_worker(pool_dir: str) -> None:
    global _POOL, _CONFIG
    _POOL = DealPool(Path(pool_dir))
    _CONFIG = load_cpu_config()["hard"]  # 強い方の設定（ふつうはこれを弱めたもの）


def _game(args: tuple) -> list[dict]:
    deal_seed, seed = args
    deal_rng = random.Random(deal_seed)
    deals = iter([deal_rng.randrange(len(_POOL)) for _ in range(MAX_ROUNDS + 2)])
    random.seed(seed)
    agents = {pid: RecordingAgent(RuleBrain("normal", _CONFIG, random.Random(seed + i)))
              for i, pid in enumerate(PLAYER_IDS)}
    sim = MatchSimulator([agents[pid] for pid in PLAYER_IDS], deal_factory=lambda _n: _POOL.get(next(deals)),
                         rng=random.Random(seed))
    rows = []
    rounds = 0
    while not sim.ended and rounds < MAX_ROUNDS:
        for agent in agents.values():
            agent.pending = None
        record = sim.play_round()
        rounds += 1
        liq = record.liquidation
        for pid, agent in agents.items():
            if agent.pending is None:
                continue
            bet = record.bets[pid]
            row = {"features": agent.pending, "bet": bet, "result": "draw", "unit_gain": 0.0, "unit_loss": 0.0}
            if liq is not None and bet > 0:
                if liq["winner_id"] == pid:
                    row["result"] = "win"
                    row["unit_gain"] = liq["winner_gain"] / bet
                else:
                    row["result"] = "lose"
                    row["unit_loss"] = (liq["loser_loss"] - liq["assault_bonus_damage"]) / bet
            rows.append(row)
    return rows


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="掛け金の見積もり式を作るための記録")
    parser.add_argument("--pool", type=Path, required=True)
    parser.add_argument("--games", type=int, default=3000)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--workers", type=int, default=min(20, os.cpu_count() or 1))
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    rng = random.Random(args.seed)
    tasks = [(rng.randrange(2**31), rng.randrange(2**31)) for _ in range(args.games)]
    print(f"掛け金の記録: {args.games} 対局、{args.workers} プロセス", flush=True)
    started = time.perf_counter()
    count = 0
    args.out.parent.mkdir(parents=True, exist_ok=True)
    with args.out.open("w", encoding="utf-8") as f, \
            ProcessPoolExecutor(max_workers=args.workers, initializer=_init_worker,
                                initargs=(str(args.pool),)) as executor:
        for i, rows in enumerate(executor.map(_game, tasks, chunksize=2), start=1):
            for row in rows:
                f.write(json.dumps(row, ensure_ascii=False) + "\n")
            count += len(rows)
            if i % 200 == 0:
                print(f"  {i}/{args.games} 対局、{count} 局 {time.perf_counter() - started:.0f} 秒", flush=True)
    print(f"保存: {args.out}（{count} 局）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
