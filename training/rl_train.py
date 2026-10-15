"""
つよい の強化学習（手牌・スキル・掛け金の判断を学習する）

学習するのは mahjong_engine/ai/model_brain.py の 3 つのネット（hand / skill / bet）。打牌などは規則のまま。
1. bc   … 模倣学習。規則で打つつよい（cpu_config.json の hard）の判断を手本に、まねるところから始める
2. ppo  … セルフプレイの強化学習（PPO）。相手は規則のつよい、または自分（いまのネット）。
          報酬は対局の勝ち負け（勝ち +1・負け −1）に、局ごとの補助（HP の差と累計獲得点数の差の動き）を足す
3. eval … 学習したネット（確率がいちばん高い行動）と、規則のつよいを対局させて勝率を出す

対局はプロセスを分けて並べて打ち（ネットは numpy で計算）、学習は PyTorch（CPU）で行う。
本番のサーバーには PyTorch は要らない（書き出した .npz を numpy で読む）。

使い方（リポジトリ直下で）:
    python -m training.rl_train bc   --pool training/data/deals --games 400 --out training/data/rl/bc.pt
    python -m training.rl_train ppo  --pool training/data/deals --init training/data/rl/bc.pt --iterations 200 --out-dir training/data/rl
    python -m training.rl_train eval --pool training/data/deals --model training/data/rl/latest.npz --games 1000
"""
import argparse
import json
import logging
import os
import random
import sys
import time
from concurrent.futures import ProcessPoolExecutor
from concurrent.futures.process import BrokenProcessPool
from pathlib import Path
from typing import Optional

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai import model_brain as mb  # noqa: E402
from mahjong_engine.ai.cpu_config import load_cpu_config  # noqa: E402
from mahjong_engine.ai.rule_brain import RuleBrain  # noqa: E402
from training.deal_pool import DealPool  # noqa: E402
from training.simulator import PLAYER_IDS, MatchSimulator, RuleAgent  # noqa: E402
from training.skill_game import MAX_ROUNDS, POINT_VICTORY, game_winner  # noqa: E402

HIDDEN = {"hand": (32, 32), "skill": (64, 64), "bet": (64, 64), "value": (64, 64)}
INPUT = {"hand": mb.CANDIDATE_SIZE, "skill": mb.STATE_SIZE, "bet": mb.STATE_SIZE, "value": mb.STATE_SIZE}
OUTPUT = {"hand": 1, "skill": len(mb.SKILL_ACTIONS), "bet": len(mb.BET_LEVELS), "value": 1}
SHAPING_SCALE = 20000.0

# ========== 対局（子プロセス） ==========

_POOL: Optional[DealPool] = None
_CONFIG: Optional[dict] = None


def _init_worker(pool_dir: str) -> None:
    global _POOL, _CONFIG
    # 対局ごとに出る決着の警告（掛け金を払えない など）は学習の記録に要らない
    logging.getLogger("mahjong_engine").setLevel(logging.ERROR)
    _POOL = DealPool(Path(pool_dir))
    _CONFIG = load_cpu_config()["hard"]
    # 学習では探索を時間で打ち切らない（CPU の混み具合で手が変わらないように、候補の数で打ち切る）
    _CONFIG["hand_selection"]["search_time_limit"] = 5.0


def _load_nets(weights: Optional[dict]) -> Optional[dict]:
    if weights is None:
        return None
    return {head: mb.Mlp([(np.asarray(w), np.asarray(b)) for w, b in layers]) for head, layers in weights.items()}


def _play_game(args: tuple) -> dict:
    """
    1 対局。seats は席ごとの打ち手:
      "teacher" … 規則どおりに打ち、判断を手本として記録（模倣学習）
      "learner" … ネットで確率どおりに選び、判断を記録（強化学習）
      "greedy"  … ネットでいちばん確率の高い行動（評価）
      "rule"    … 規則で打つつよい（記録しない）
    """
    seed, seats, weights = args
    nets = _load_nets(weights)
    deal_rng = random.Random(seed)
    deals = iter([deal_rng.randrange(len(_POOL)) for _ in range(MAX_ROUNDS + 2)])
    random.seed(seed)
    records = {pid: [] for pid in PLAYER_IDS}
    agents = []
    for i, (pid, kind) in enumerate(zip(PLAYER_IDS, seats)):
        rng = random.Random(seed * 31 + i)
        if kind == "rule":
            brain = RuleBrain("normal", _CONFIG, rng)
        else:
            mode = {"teacher": "teacher", "learner": "sample", "greedy": "greedy"}[kind]
            recorder = records[pid] if kind in ("teacher", "learner") else None
            brain = mb.ModelBrain(_CONFIG, rng, nets=nets, mode=mode, recorder=recorder)
        agents.append(RuleAgent(brain))
    sim = MatchSimulator(agents, deal_factory=lambda _n: _POOL.get(next(deals)), rng=random.Random(seed))

    # 判断ごとの局の番号と、局ごとの補助報酬（自分から見た HP の差 + 累計獲得点数の差の動き）
    round_of = {pid: [] for pid in PLAYER_IDS}
    shaping = {pid: [] for pid in PLAYER_IDS}
    rounds = 0
    while not sim.ended and rounds < MAX_ROUNDS:
        before = {p.player_id: (p.health, p.cumulative_earned_points) for p in sim.engine.state.players}
        sim.play_round()
        after = {p.player_id: (p.health, p.cumulative_earned_points) for p in sim.engine.state.players}
        for pid in PLAYER_IDS:
            other = PLAYER_IDS[1] if pid == PLAYER_IDS[0] else PLAYER_IDS[0]
            diff = lambda s: (s[pid][0] - s[other][0]) + (s[pid][1] - s[other][1])  # noqa: E731
            shaping[pid].append((diff(after) - diff(before)) / SHAPING_SCALE)
            round_of[pid] += [rounds] * (len(records[pid]) - len(round_of[pid]))
        rounds += 1

    winner = game_winner(sim)
    result = {"winner": winner, "rounds": rounds, "seats": list(seats), "trajectories": {}}
    for pid in PLAYER_IDS:
        if records[pid]:
            outcome = 1.0 if winner == pid else -1.0 if winner is not None else 0.0
            result["trajectories"][pid] = {"decisions": records[pid], "round_of": round_of[pid],
                                           "shaping": shaping[pid], "outcome": outcome}
    return result


class Workers:
    """
    対局を打つ子プロセスの集まり。子プロセスが落ちたら（原因の分からない終了があった）作り直してやり直す。
    長く動かしても重くならないよう、RECYCLE_EVERY 回の run ごとに子プロセスをまとめて作り直す。
    （ProcessPoolExecutor の max_tasks_per_child は Windows で入れ替えの途中に止まることがあったので使わない）
    """

    RECYCLE_EVERY = 10
    RETRIES = 3
    # 1 回の run（対局のまとまり）がこの秒数を超えたら止まったとみなし、作り直してやり直す
    TIMEOUT_SEC = 1800

    def __init__(self, workers: int, pool_dir: Path):
        self.workers = workers
        self.pool_dir = str(pool_dir)
        self.executor: Optional[ProcessPoolExecutor] = None
        self.runs = 0

    def _start(self) -> None:
        self.executor = ProcessPoolExecutor(self.workers, initializer=_init_worker, initargs=(self.pool_dir,))

    def __enter__(self) -> "Workers":
        self._start()
        return self

    def __exit__(self, *exc) -> None:
        if self.executor is not None:
            self.executor.shutdown(cancel_futures=True)

    def run(self, tasks: list[tuple]) -> list[dict]:
        self.runs += 1
        if self.runs % self.RECYCLE_EVERY == 0:
            self.executor.shutdown(wait=True)
            self._start()
        for attempt in range(self.RETRIES + 1):
            try:
                return list(self.executor.map(_play_game, tasks, chunksize=1, timeout=self.TIMEOUT_SEC))
            except (BrokenProcessPool, TimeoutError) as exc:
                if attempt == self.RETRIES:
                    raise
                reason = "時間内に終わらない" if isinstance(exc, TimeoutError) else "子プロセスが落ちた"
                print(f"  対局が{reason}ので、子プロセスを作り直してやり直します（{attempt + 1} 回目）", flush=True)
                self.executor.shutdown(wait=False, cancel_futures=True)
                self._start()
        raise AssertionError("unreachable")


def run_games(workers: "Workers", tasks: list[tuple]) -> list[dict]:
    return workers.run(tasks)


# ========== ネット（PyTorch） ==========

def _torch():
    import torch
    return torch


def build_nets():
    torch = _torch()
    nn = torch.nn

    class Net(nn.Module):
        def __init__(self, sizes):
            super().__init__()
            self.layers = nn.ModuleList(nn.Linear(a, b) for a, b in zip(sizes[:-1], sizes[1:]))

        def forward(self, x):
            for i, layer in enumerate(self.layers):
                x = layer(x)
                if i < len(self.layers) - 1:
                    x = torch.tanh(x)
            return x

    return nn.ModuleDict({
        head: Net([INPUT[head], *HIDDEN[head], OUTPUT[head]]) for head in ("hand", "skill", "bet", "value")
    })


def export_weights(nets) -> dict:
    """子プロセスに渡す numpy の重み（model_brain.Mlp の形: x @ W + b）。value は対局に使わない。"""
    return {
        head: [(layer.weight.detach().numpy().T.copy(), layer.bias.detach().numpy().copy()) for layer in nets[head].layers]
        for head in mb.HEADS
    }


def save_npz(nets, path: Path) -> None:
    arrays = {}
    for head, layers in export_weights(nets).items():
        for i, (w, b) in enumerate(layers):
            arrays[f"{head}.W{i}"] = w.astype(np.float32)
            arrays[f"{head}.b{i}"] = b.astype(np.float32)
    path.parent.mkdir(parents=True, exist_ok=True)
    np.savez(path, **arrays)


# ========== 学習データ ==========

def flatten(results: list[dict], gamma: float, shaping_weight: float) -> list[dict]:
    """判断ごとに、その局から先の報酬の合計（return）を付ける。"""
    items = []
    for r in results:
        for traj in r["trajectories"].values():
            shaping = np.asarray(traj["shaping"], dtype=np.float64) * shaping_weight
            # その局から先の補助報酬の合計（gamma で割り引く）+ 対局の勝ち負け
            future = np.zeros(len(shaping) + 1)
            for k in range(len(shaping) - 1, -1, -1):
                future[k] = shaping[k] + gamma * future[k + 1]
            n_rounds = len(shaping)
            for decision, k in zip(traj["decisions"], traj["round_of"]):
                ret = future[k] + traj["outcome"] * gamma ** (n_rounds - k)
                items.append({**decision, "return": float(ret)})
    return items


def _batch_tensors(items: list[dict], head: str):
    torch = _torch()
    if head == "hand":
        size = max(len(it["x"]) for it in items)
        x = np.zeros((len(items), size, mb.CANDIDATE_SIZE))
        mask = np.zeros((len(items), size), dtype=bool)
        for i, it in enumerate(items):
            x[i, : len(it["x"])] = it["x"]
            mask[i, : len(it["x"])] = True
    else:
        x = np.stack([it["x"] for it in items])
        mask = np.stack([it["mask"] if it["mask"] is not None else np.ones(OUTPUT[head], dtype=bool) for it in items])
    return (torch.tensor(x, dtype=torch.float32), torch.tensor(mask),
            torch.tensor([it["action"] for it in items]),
            torch.tensor(np.stack([it["state"] for it in items]), dtype=torch.float32))


def _log_probs(nets, head: str, x, mask):
    torch = _torch()
    logits = nets[head](x).squeeze(-1) if head == "hand" else nets[head](x)
    logits = logits.masked_fill(~mask, -1e9)
    return torch.log_softmax(logits, dim=-1)


def train_bc(nets, items: list[dict], epochs: int, lr: float, log=print) -> None:
    """手本の行動をまねる（交差エントロピー）。value は対局の結果（return）を当てる。"""
    torch = _torch()
    opt = torch.optim.Adam(nets.parameters(), lr=lr)
    by_head = {h: [it for it in items if it["head"] == h] for h in mb.HEADS}
    for epoch in range(epochs):
        total, correct, count = 0.0, 0, 0
        for head, subset in by_head.items():
            random.shuffle(subset)
            for start in range(0, len(subset), 256):
                chunk = subset[start:start + 256]
                x, mask, action, state = _batch_tensors(chunk, head)
                logp = _log_probs(nets, head, x, mask)
                loss = -logp.gather(1, action[:, None]).mean()
                ret = torch.tensor([it["return"] for it in chunk], dtype=torch.float32)
                loss = loss + 0.5 * ((nets["value"](state).squeeze(-1) - ret) ** 2).mean()
                opt.zero_grad()
                loss.backward()
                opt.step()
                total += loss.item() * len(chunk)
                correct += int((logp.argmax(-1) == action).sum())
                count += len(chunk)
        log(f"  模倣 {epoch + 1}/{epochs}: 損失 {total / max(1, count):.4f}  手本と同じ行動 {correct / max(1, count):.1%}")


def train_ppo(nets, opt, items: list[dict], epochs: int, clip: float, entropy_coef: float, log=print) -> dict:
    torch = _torch()
    # 利得 = return − その時点の value（標準化する）
    with torch.no_grad():
        states = torch.tensor(np.stack([it["state"] for it in items]), dtype=torch.float32)
        values = nets["value"](states).squeeze(-1).numpy()
    returns = np.array([it["return"] for it in items])
    adv = returns - values
    adv = (adv - adv.mean()) / (adv.std() + 1e-8)
    for it, a in zip(items, adv):
        it["adv"] = float(a)
    by_head = {h: [it for it in items if it["head"] == h] for h in mb.HEADS}
    stats = {"policy": 0.0, "value": 0.0, "entropy": 0.0, "n": 0}
    for _ in range(epochs):
        for head, subset in by_head.items():
            random.shuffle(subset)
            for start in range(0, len(subset), 512):
                chunk = subset[start:start + 512]
                x, mask, action, state = _batch_tensors(chunk, head)
                logp_all = _log_probs(nets, head, x, mask)
                logp = logp_all.gather(1, action[:, None]).squeeze(1)
                old = torch.tensor([it["logp"] for it in chunk], dtype=torch.float32)
                a = torch.tensor([it["adv"] for it in chunk], dtype=torch.float32)
                ratio = torch.exp(logp - old)
                policy_loss = -torch.min(ratio * a, torch.clamp(ratio, 1 - clip, 1 + clip) * a).mean()
                probs = logp_all.exp()
                entropy = -(probs * logp_all.masked_fill(~mask, 0.0)).sum(-1).mean()
                ret = torch.tensor([it["return"] for it in chunk], dtype=torch.float32)
                value_loss = ((nets["value"](state).squeeze(-1) - ret) ** 2).mean()
                loss = policy_loss + 0.5 * value_loss - entropy_coef * entropy
                opt.zero_grad()
                loss.backward()
                torch.nn.utils.clip_grad_norm_(nets.parameters(), 1.0)
                opt.step()
                stats["policy"] += policy_loss.item() * len(chunk)
                stats["value"] += value_loss.item() * len(chunk)
                stats["entropy"] += entropy.item() * len(chunk)
                stats["n"] += len(chunk)
    n = max(1, stats.pop("n"))
    return {k: v / n for k, v in stats.items()}


# ========== 評価 ==========

def evaluate(executor, weights: dict, games: int, seed: int) -> dict:
    """ネット（いちばん確率の高い行動）と規則のつよいを、席を入れ替えて同じ配牌で打たせる。"""
    rng = random.Random(seed)
    tasks = []
    for _ in range(max(1, games // 2)):
        s = rng.randrange(2**31)
        tasks.append((s, ("greedy", "rule"), weights))
        tasks.append((s, ("rule", "greedy"), weights))
    results = run_games(executor, tasks)
    score = [1.0 if r["winner"] == ("P0" if r["seats"][0] == "greedy" else "P1") else 0.0 if r["winner"] else 0.5
             for r in results]
    mean = float(np.mean(score))
    se = float(np.std(score, ddof=1) / np.sqrt(len(score))) if len(score) > 1 else 0.0
    return {"win_rate": mean, "se": se, "games": len(score)}


# ========== コマンド ==========

def cmd_bc(args) -> None:
    torch = _torch()
    torch.manual_seed(args.seed)
    nets = build_nets()
    rng = random.Random(args.seed)
    with Workers(args.workers, args.pool) as ex:
        started = time.perf_counter()
        tasks = [(rng.randrange(2**31), ("teacher", "teacher"), None) for _ in range(args.games)]
        results = run_games(ex, tasks)
        items = flatten(results, args.gamma, args.shaping)
        counts = {h: sum(it["head"] == h for it in items) for h in mb.HEADS}
        print(f"手本の記録: {args.games} 対局、判断 {counts}  {time.perf_counter() - started:.0f} 秒", flush=True)
        train_bc(nets, items, args.epochs, args.lr)
        ev = evaluate(ex, export_weights(nets), args.eval_games, args.seed + 1)
        print(f"模倣したネット 対 規則のつよい: 勝率 {ev['win_rate']:.1%} ±{ev['se']:.1%}", flush=True)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    torch.save(nets.state_dict(), args.out)
    save_npz(nets, args.out.with_suffix(".npz"))
    print(f"保存: {args.out}")


def cmd_ppo(args) -> None:
    torch = _torch()
    torch.manual_seed(args.seed)
    nets = build_nets()
    if args.init:
        nets.load_state_dict(torch.load(args.init))
    opt = torch.optim.Adam(nets.parameters(), lr=args.lr)
    rng = random.Random(args.seed)
    args.out_dir.mkdir(parents=True, exist_ok=True)
    log_path = args.out_dir / "ppo_log.jsonl"
    best = -1.0
    with Workers(args.workers, args.pool) as ex:
        for it in range(1, args.iterations + 1):
            started = time.perf_counter()
            weights = export_weights(nets)
            tasks = []
            for _ in range(args.games):
                # 半分は規則のつよいと、半分は自分どうし（両方の席を学習に使う）。席は入れ替える
                if rng.random() < args.rule_share:
                    seats = ("learner", "rule") if rng.random() < 0.5 else ("rule", "learner")
                else:
                    seats = ("learner", "learner")
                tasks.append((rng.randrange(2**31), seats, weights))
            results = run_games(ex, tasks)
            items = flatten(results, args.gamma, args.shaping)
            vs_rule = [r for r in results if "rule" in r["seats"]]
            wins = [1.0 if r["winner"] == ("P0" if r["seats"][0] == "learner" else "P1") else 0.0 if r["winner"] else 0.5
                    for r in vs_rule]
            stats = train_ppo(nets, opt, items, args.epochs, args.clip, args.entropy)
            entry = {"iteration": it, "decisions": len(items), "vs_rule_sample": float(np.mean(wins)) if wins else None,
                     **stats, "sec": round(time.perf_counter() - started, 1)}
            if it % args.eval_every == 0:
                ev = evaluate(ex, export_weights(nets), args.eval_games, args.seed + it)
                entry["eval"] = ev
                if ev["win_rate"] > best:
                    best = ev["win_rate"]
                    torch.save(nets.state_dict(), args.out_dir / "best.pt")
                    save_npz(nets, args.out_dir / "best.npz")
            torch.save(nets.state_dict(), args.out_dir / "latest.pt")
            save_npz(nets, args.out_dir / "latest.npz")
            with log_path.open("a", encoding="utf-8") as f:
                f.write(json.dumps(entry, ensure_ascii=False) + "\n")
            ev_text = f"  評価 {entry['eval']['win_rate']:.1%} ±{entry['eval']['se']:.1%}" if "eval" in entry else ""
            sample_text = f"{entry['vs_rule_sample']:.1%}" if entry["vs_rule_sample"] is not None else "-"
            print(f"[{it}] 判断 {len(items)}  規則との対局（確率どおり）{sample_text}  "
                  f"方策 {stats['policy']:+.4f}  価値 {stats['value']:.4f}  エントロピー {stats['entropy']:.3f}"
                  f"{ev_text}  {entry['sec']:.0f} 秒", flush=True)


def cmd_eval(args) -> None:
    with np.load(args.model) as data:
        weights = {head: [] for head in mb.HEADS}
        for head in mb.HEADS:
            i = 0
            while f"{head}.W{i}" in data:
                weights[head].append((data[f"{head}.W{i}"], data[f"{head}.b{i}"]))
                i += 1
    with Workers(args.workers, args.pool) as ex:
        ev = evaluate(ex, weights, args.games, args.seed)
    print(f"{args.model} 対 規則のつよい: 勝率 {ev['win_rate']:.1%} ±{ev['se']:.1%}（{ev['games']} 対局）")


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="つよい の強化学習")
    sub = parser.add_subparsers(dest="command", required=True)

    def common(p):
        p.add_argument("--pool", type=Path, required=True)
        p.add_argument("--seed", type=int, default=1)
        p.add_argument("--workers", type=int, default=min(20, os.cpu_count() or 1))

    p = sub.add_parser("bc", help="模倣学習（規則のつよいをまねる）")
    common(p)
    p.add_argument("--games", type=int, default=400)
    p.add_argument("--epochs", type=int, default=20)
    p.add_argument("--lr", type=float, default=1e-3)
    p.add_argument("--gamma", type=float, default=1.0)
    p.add_argument("--shaping", type=float, default=0.5, help="局ごとの補助報酬の重み")
    p.add_argument("--eval-games", type=int, default=400)
    p.add_argument("--out", type=Path, required=True)

    p = sub.add_parser("ppo", help="セルフプレイの強化学習")
    common(p)
    p.add_argument("--init", type=Path, default=None, help="始めの重み（bc の .pt）")
    p.add_argument("--iterations", type=int, default=200)
    p.add_argument("--games", type=int, default=200, help="1 回の更新に使う対局数")
    p.add_argument("--rule-share", type=float, default=0.5, help="規則のつよいと打つ対局の割合")
    p.add_argument("--epochs", type=int, default=4)
    p.add_argument("--lr", type=float, default=3e-4)
    p.add_argument("--clip", type=float, default=0.2)
    p.add_argument("--entropy", type=float, default=0.01)
    p.add_argument("--gamma", type=float, default=1.0)
    p.add_argument("--shaping", type=float, default=0.5)
    p.add_argument("--eval-every", type=int, default=5)
    p.add_argument("--eval-games", type=int, default=400)
    p.add_argument("--out-dir", type=Path, required=True)

    p = sub.add_parser("eval", help="規則のつよいとの勝率")
    common(p)
    p.add_argument("--model", type=Path, required=True)
    p.add_argument("--games", type=int, default=1000)

    args = parser.parse_args(argv)
    {"bc": cmd_bc, "ppo": cmd_ppo, "eval": cmd_eval}[args.command](args)
    return 0


if __name__ == "__main__":
    sys.exit(main())
