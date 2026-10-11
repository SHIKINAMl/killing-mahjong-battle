"""
スキルの効果の測定（スキルのコスト見直しの材料）

同じ配牌・同じ乱数で「P0 がスキルを使う局」と「使わない局」を打ち比べ、
両者の HP の差（P0 の増減 − P1 の増減）がどれだけ動いたかを、そのスキルの効果とする。
スキルのコストは足し戻して、効果だけを取り出す。

- 打ち手は両者とも「ふつう」の考え方。スキルは測る対象だけを P0 が使い、ほかは使わない
- 掛け金は両者とも同じ額に固定する（効果は掛け金にほぼ比例するので、損益分岐の掛け金を出せる）
- 開始時の役強化はなし（効果を見分けやすくするため）
- 役強化は効果が対局の最後まで続くので、役ごとに「1 局あたりの効果」を測る

使い方（リポジトリ直下で）:
    python -m training.skill_value --pool training/data/deals --trials 2000 --out training/data/skill_value.json
"""
import argparse
import json
import math
import os
import random
import sys
import time
from collections import defaultdict
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path
from typing import Optional

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai.cpu_config import load_cpu_config  # noqa: E402
from mahjong_engine.ai.rule_brain import RuleBrain  # noqa: E402
from mahjong_engine.engine.game_engine import GameEngine  # noqa: E402
from mahjong_engine.engine.game_state import SkillType, get_skill_cost  # noqa: E402
from training.deal_pool import DealPool  # noqa: E402
from training.simulator import MatchSimulator, RuleAgent  # noqa: E402

SKILLS = ("perspective", "mulligan", "assault", "boost_hand")
# 手牌の候補探索の制限時間（秒）。本番は 1 秒だが、CPU の混み具合で候補が変わると、対にした 2 局が
# 違う候補から選ぶことになり、その差がスキルの効果に混ざる。ほぼ必ず探し終わる長さにする
SEARCH_TIME_LIMIT = 10.0


def _skill_cost(skill: str) -> int:
    if skill.startswith("perspective"):
        skill = "perspective"
    return get_skill_cost(SkillType(skill), 0)


def plan_features(brain: RuleBrain, obs, plan) -> dict:
    """スキルを使う前の手の様子（本番の CPU と同じ RuleBrain.skill_features）。"""
    if plan is None:
        return {"has_plan": 0}
    return {"has_plan": 1, **brain.skill_features(obs, plan)}


class ForcedSkillAgent(RuleAgent):
    """
    測る対象のスキルを、決めた順に使う（使えない手順は飛ばす）。対象の選び方は「ふつう」と同じ。

    skill は「>」でつないだ手順。各手順は「スキル名」か「スキル名_xN」（同じ指定のまま N 回）。
    例: "perspective_x2"（同じ色に 2 回）、"perspective>perspective"（2 回目は色を選び直す）、
        "mulligan>boost_hand"（マリガンの後の手で役強化の役を選ぶ）
    """

    def __init__(self, brain: RuleBrain, skill: Optional[str], fixed_bet: int):
        super().__init__(brain, use_skills=False, fixed_bet=fixed_bet)
        self.skill = skill
        # (スキル名, 指定を選び直すか)。_xN の 2 回目以降は前の指定を使い回す
        self.steps: list[tuple[str, bool]] = []
        for step in (skill.split(">") if skill else []):
            name, _, times = step.partition("_x")
            self.steps.extend((name, i == 0) for i in range(int(times) if times else 1))
        self.position = 0
        self.data = None
        self.features: Optional[dict] = None
        self.plan_seed = brain.rng.randrange(2**31)

    def plan_hand(self, obs):
        # 手牌の候補の探索順を毎回そろえる。マリガンの後に探し直したとき、探索順の違いが
        # スキルの効果に混ざらないようにするため
        self.brain.rng.seed(self.plan_seed)
        return super().plan_hand(obs)

    def choose_skill(self, obs, plan):
        if self.features is None:
            self.features = plan_features(self.brain, obs, plan)
        while plan is not None and self.position < len(self.steps):
            name, fresh = self.steps[self.position]
            self.position += 1
            if fresh:
                self.data = self._skill_data(name, obs, plan)
            if self.data is not None:
                return self.data
        return None

    def _skill_data(self, name: str, obs, plan):
        """対象の選び方は「ふつう」と同じ（rule_brain の関数を使う）。使えない局は None。"""
        if name == "perspective":
            return {"skill_type": "perspective", "suit": self.brain.choose_perspective_suit(obs, plan)}
        if name == "perspective_waitsuit":
            # 比較用: 自分の待ち牌が残っていそうな色
            return {"skill_type": "perspective", "suit": self.brain.choose_perspective_suit(obs, plan, prefer_waits=True)}
        if name == "perspective_unseen":
            # 比較用: 相手の山に残っていそうな牌がいちばん多い色（よわいの選び方）
            level, self.brain.level = self.brain.level, "easy"
            suit = self.brain.choose_perspective_suit(obs, plan)
            self.brain.level = level
            return {"skill_type": "perspective", "suit": suit}
        if name == "perspective_any":
            # 比較用: 色を指定しない従来の透視（山全体からランダムに 3 枚）
            return {"skill_type": "perspective"}
        if name == "assault":
            return {"skill_type": "assault"}
        if name == "mulligan":
            return self.brain.mulligan_skill_data(obs, plan) if obs.me.can_mulligan else None
        if name == "boost_hand":
            combo, _score = self.brain.best_boost_combo(obs, plan)
            return {"skill_type": "boost_hand", "yaku_names": list(combo)} if combo else None
        raise ValueError(name)


# ========== 1 試行（子プロセスで動く） ==========

_POOL: Optional[DealPool] = None
_CONFIG: Optional[dict] = None


def _deep_update(base: dict, override: dict) -> None:
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(base.get(key), dict):
            _deep_update(base[key], value)
        else:
            base[key] = value


def _init_worker(pool_dir: str, override: Optional[dict] = None) -> None:
    global _POOL, _CONFIG
    _POOL = DealPool(Path(pool_dir))
    _CONFIG = load_cpu_config()["hard"]  # 強い方の設定（ふつうはこれを弱めたもの）
    _deep_update(_CONFIG, override or {})
    _CONFIG["hand_selection"]["search_time_limit"] = SEARCH_TIME_LIMIT


def _play(deal_index: int, seed: int, bet: int, skill: Optional[str] = None,
          boost_yaku: Optional[str] = None, perks: Optional[list[dict]] = None) -> dict:
    """
    1 局打ち、P0 から見た HP の差の動きと、スキルの使用状況を返す。

    Args:
        perks: 両者の特典（開始時の役強化の数・初期透視の枚数など）。省略すると役強化なし・特典なし
    """
    random.seed(seed)
    p0 = ForcedSkillAgent(RuleBrain("normal", _CONFIG, random.Random(seed)), skill, bet)
    p1 = RuleAgent(RuleBrain("normal", _CONFIG, random.Random(seed + 1)), use_skills=False, fixed_bet=bet)
    # 配牌は毎回取り出し直す（同じオブジェクトを使い回すと、マリガンの書き換えが残る）
    sim = MatchSimulator([p0, p1], deal_factory=lambda _n: _POOL.get(deal_index), perks=perks,
                         opening_boosts=perks is not None, rng=random.Random(seed))
    if boost_yaku is not None:
        sim.engine.get_player_by_id("P0").boost_hand_bonus[boost_yaku] = 1

    def align_random():
        # スキルの有無で乱数の消費が変わるので、打牌フェーズの前にそろえ直す（先手の抽選・同点の選び方）
        random.seed(seed + 2)
        p0.brain.rng.seed(seed + 3)
        p1.brain.rng.seed(seed + 4)

    record = sim.play_round(before_discard_phase=align_random, advance=False)
    swing = {pid: record.health_after[pid] - record.health_before[pid] for pid in ("P0", "P1")}
    cost = sum(_skill_cost(s) for s in record.skills["P0"])
    liq = record.liquidation or {}
    return {
        "diff": swing["P0"] - swing["P1"] + cost,  # コストを足し戻した HP の差の動き
        # 役ごとの役強化の測定では、スキルを使わずに強化を直接付ける。その局も「使った」として数える
        "used": bool(record.skills["P0"]) or boost_yaku is not None,
        "p0_win": record.winner == "P0",
        "p1_win": record.winner == "P1",
        "assault_damage": liq.get("assault_bonus_damage", 0) if record.winner == "P0" else 0,
        "skills": list(record.skills["P0"]),
        "features": p0.features,
    }


def _paired_trial(args: tuple) -> dict:
    """同じ配牌・乱数で、スキルなし／ありを打ち比べる。"""
    deal_index, seed, bet, skill, boost_yaku = args
    base = _play(deal_index, seed, bet)
    treated = _play(deal_index, seed, bet, skill=skill, boost_yaku=boost_yaku)
    return {"skill": skill, "boost_yaku": boost_yaku, "base": base, "treated": treated}


# ========== 集計 ==========

def _mean_se(values: list[float]) -> tuple[float, float]:
    n = len(values)
    if n == 0:
        return 0.0, 0.0
    mean = sum(values) / n
    if n < 2:
        return mean, 0.0
    var = sum((v - mean) ** 2 for v in values) / (n - 1)
    return mean, math.sqrt(var / n)


def summarize(results: list[dict], bet: int) -> dict:
    by_key = defaultdict(list)
    for r in results:
        by_key[(r["skill"], r["boost_yaku"])].append(r)

    summary = {"bet": bet, "skills": {}, "boost_per_yaku": {}}
    for (skill, yaku), rows in sorted(by_key.items(), key=lambda kv: (kv[0][0] or "", kv[0][1] or "")):
        used = [r for r in rows if r["treated"]["used"]]
        effects = [r["treated"]["diff"] - r["base"]["diff"] for r in used]
        mean, se = _mean_se(effects)

        def rate(side: str, key: str) -> float:
            return sum(r[side][key] for r in used) / max(1, len(used))

        entry = {
            "trials": len(rows),
            "used": len(used),
            "applicable_rate": len(used) / len(rows) if rows else 0.0,
            "effect_per_use": mean,
            "effect_se": se,
            "win_rate_base": rate("base", "p0_win"),
            "win_rate_treated": rate("treated", "p0_win"),
            "loss_rate_base": rate("base", "p1_win"),
            "loss_rate_treated": rate("treated", "p1_win"),
        }
        if skill == "assault":
            # 強襲で相手に追加で与えたダメージ（＝自分の獲得が 0 になった分。累計獲得点数には入らない）
            entry["extra_damage_per_use"] = _mean_se([r["treated"]["assault_damage"] for r in used])[0]
        if yaku is not None:
            summary["boost_per_yaku"][yaku] = entry
            continue
        cost = _skill_cost(skill)
        entry["cost"] = cost
        entry["value_ratio"] = mean / cost if cost else None
        entry["break_even_bet"] = cost / (mean / bet) if mean > 0 else None
        summary["skills"][skill] = entry
    return summary


def run(pool_dir: Path, trials: int, bet: int, seed: int, workers: int, boost_trials: int,
        skills: tuple[str, ...] = SKILLS, override: Optional[dict] = None,
        log=print) -> dict:
    pool = DealPool(pool_dir)
    rng = random.Random(seed)
    tasks = []
    for skill in skills:
        for _ in range(trials):
            tasks.append((rng.randrange(len(pool)), rng.randrange(2**31), bet, skill, None))
    for yaku in GameEngine.get_opening_boost_candidates():
        for _ in range(boost_trials):
            tasks.append((rng.randrange(len(pool)), rng.randrange(2**31), bet, None, yaku))

    log(f"スキルの効果の測定: {len(tasks)} 試行（各 2 局）、{workers} プロセス")
    started = time.perf_counter()
    results = []
    with ProcessPoolExecutor(max_workers=workers, initializer=_init_worker, initargs=(str(pool_dir), override)) as executor:
        for i, result in enumerate(executor.map(_paired_trial, tasks, chunksize=8), start=1):
            results.append(result)
            if i % 2000 == 0:
                log(f"  {i}/{len(tasks)} 試行 {time.perf_counter() - started:.0f} 秒")
    summary = summarize(results, bet)
    summary["elapsed_sec"] = round(time.perf_counter() - started, 1)
    summary["trials_per_skill"] = trials
    summary["trials_per_boost_yaku"] = boost_trials
    return summary


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="スキルの効果を対局シミュレーションで測る")
    parser.add_argument("--pool", type=Path, required=True)
    parser.add_argument("--trials", type=int, default=2000, help="スキルごとの試行数")
    parser.add_argument("--boost-trials", type=int, default=600, help="役強化の役ごとの試行数")
    parser.add_argument("--bet", type=int, default=1000)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--workers", type=int, default=os.cpu_count() or 1)
    parser.add_argument("--override", default="{}", help='「ふつう」の設定の上書き（JSON。例: {"hand_selection": {"bonus_wait_seen_in_opponent_wall": 1.4}}）')
    parser.add_argument("--skills", default=",".join(SKILLS), help="測るスキル（カンマ区切り）。役ごとの役強化は --boost-trials 0 で省ける")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)

    skills = tuple(name for name in args.skills.split(",") if name)
    summary = run(args.pool, args.trials, args.bet, args.seed, args.workers, args.boost_trials, skills,
                  override=json.loads(args.override))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary["skills"], ensure_ascii=False, indent=2))
    print(f"保存: {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
