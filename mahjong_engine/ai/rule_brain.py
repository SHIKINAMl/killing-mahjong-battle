"""
よわい／ふつう の考え方

規則ごとに点数を付け、点数の高い行動を選ぶ。重み・しきい値はすべて cpu_config.json から受け取る。
入力は CpuObservation（視界）だけで、エンジンの状態は読まない。
出力は人間のクライアントと同じ action の data。
"""
import itertools
import random
import time
from collections import Counter
from dataclasses import dataclass, field
from typing import Any, Optional

from ..engine.game_engine import GameEngine, tiles_to_wall_indexes
from ..engine.hand_analyzer import HandAnalyzer
from ..engine.game_state import SkillType
from .observation import CpuObservation

HONOR_START = 27  # 牌種 27 以降は字牌（東・西）
HAND_SIZE = 13
OPPONENT_HAND_SIZE = 13
# 相手が手牌に持つ牌と、待ちの牌種との近さ（同じ牌種 / 1 つ隣 / 2 つ隣）
CLOSENESS_BY_DISTANCE = (1.0, 0.6, 0.3)


def tile_kind(tile: int) -> int:
    """牌ID から牌種（下位5ビット）を取り出す。"""
    return tile & 0b11111


def _clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


@dataclass(frozen=True)
class WaitInfo:
    kind: int
    han: int
    multiplier: float  # 満貫未満なら 0（和了れない）
    yaku: tuple[str, ...]
    available: float  # 和了れそうな枚数の見積もり


@dataclass(frozen=True)
class HandPlan:
    hand_indexes: tuple[int, ...]
    waits: tuple[WaitInfo, ...]
    score: float
    strength: float  # 0〜1。掛け金やスキルの判断に使う
    # この案を選んだときに比べた候補。山が変わらない限り、選び直しは探し直さずに採点し直すだけで済む
    candidates: tuple[tuple[int, ...], ...] = field(default=(), compare=False)

    @property
    def best_han(self) -> int:
        return max((w.han for w in self.waits), default=0)

    @property
    def wait_kinds(self) -> set[int]:
        return {w.kind for w in self.waits}

    @property
    def expected_multiplier(self) -> float:
        """和了れそうな枚数で重み付けした精算倍率。"""
        winning = [w for w in self.waits if w.multiplier > 0]
        total = sum(w.available for w in winning)
        if total <= 0:
            return max((w.multiplier for w in winning), default=0.0)
        return sum(w.multiplier * w.available for w in winning) / total


def possibly_held_exposed_kinds(obs: CpuObservation) -> Counter:
    """
    透視で公開された相手の牌のうち、まだ捨てられていない可能性がある牌（牌種ごとの枚数）。

    どの位置の牌が捨てられたかは通知されないため、同じ牌ID の捨て牌を1枚ずつ差し引いて見積もる。
    """
    remaining = Counter(obs.opponent.exposed.values())
    remaining.subtract(Counter(obs.opponent.discards))
    held: Counter = Counter()
    for tile, count in remaining.items():
        if count > 0:
            held[tile_kind(tile)] += count
    return held


def rest_indexes(obs: CpuObservation, hand_indexes: tuple[int, ...]) -> list[int]:
    """手牌以外で、まだ打っていない wall index。"""
    hand = set(hand_indexes)
    return [
        i for i in range(len(obs.me.wall))
        if i not in hand and i not in obs.me.discarded_wall_indexes
    ]


def mulligan_targets(obs: CpuObservation, plan: "HandPlan") -> list[int]:
    """マリガンで交換する候補: 手牌以外にある自分の待ち牌（山から出ない上に、捨てればフリテンになる）。"""
    return [i for i in rest_indexes(obs, plan.hand_indexes) if tile_kind(obs.me.wall[i]) in plan.wait_kinds]


def boostable_yaku(plan: "HandPlan") -> list[str]:
    """役強化で強化できる、この手牌で和了れる待ちの役。"""
    return sorted({
        name
        for wait in plan.waits if wait.multiplier > 0
        for name in wait.yaku
        if GameEngine.normalize_boost_target_yaku_name(name) is not None
    })


def replan_needs_search(skill: str) -> bool:
    """スキルの後、手牌の候補を探し直す必要があるか（山が変わるのはマリガンだけ）。"""
    return skill == SkillType.MULLIGAN.value


# ========== 安全な手（考え方が失敗したときに必ず通る行動） ==========

def fallback_hand_indexes(obs: CpuObservation) -> list[int]:
    if len(obs.me.hand) == HAND_SIZE:
        return list(obs.me.hand)
    return list(range(min(HAND_SIZE, len(obs.me.wall))))


def fallback_bet(obs: CpuObservation) -> int:
    return obs.me.bet_unit


def fallback_discard(obs: CpuObservation) -> int:
    candidates = rest_indexes(obs, obs.me.hand)
    if not candidates:
        candidates = [i for i in range(len(obs.me.wall)) if i not in obs.me.discarded_wall_indexes]
    return candidates[0]


@dataclass(frozen=True)
class _HandContext:
    """手牌の候補によらない、視界から決まる値（候補ごとに数え直さないため）。"""
    wall: list[int]
    own_counts: Counter
    opponent_discards: Counter
    held_exposed: Counter


class RuleBrain:
    """よわい（level="easy"）／ふつう（level="normal"）の考え方。"""

    def __init__(self, level: str, config: dict, rng: Optional[random.Random] = None):
        if level not in ("easy", "normal"):
            raise ValueError(f"Unsupported level: {level}")
        self.level = level
        self.config = config
        self.rng = rng or random.Random()

    # ========== 手牌選択 ==========

    @staticmethod
    def _context(obs: CpuObservation) -> _HandContext:
        wall = list(obs.me.wall)
        return _HandContext(
            wall=wall,
            own_counts=Counter(tile_kind(t) for t in wall),
            opponent_discards=Counter(tile_kind(t) for t in obs.opponent.discards),
            held_exposed=possibly_held_exposed_kinds(obs),
        )

    def plan_hand(self, obs: CpuObservation) -> Optional[HandPlan]:
        """
        聴牌の候補を挙げて点数を付け、一番高いものを返す。満貫の候補がなければ None。
        聴牌形の探索は重いので、イベントループの外（別スレッド）から呼ぶ。
        """
        cfg = self.config["hand_selection"]
        wall = list(obs.me.wall)

        candidates: list[tuple[int, ...]] = []
        if len(obs.me.hand) == HAND_SIZE:
            candidates.append(tuple(sorted(obs.me.hand)))
        # 候補が少ない山では全列挙になって終わらないことがあるので、制限時間で打ち切る
        deadline = time.monotonic() + cfg["search_time_limit"]
        found = HandAnalyzer.search_tenpai(
            wall, wall, obs.dora_id, limit=cfg["candidate_limit"], rng=self.rng, deadline=deadline,
        )
        for tiles in found:
            indexes = tiles_to_wall_indexes(wall, tiles)
            if indexes is not None and tuple(sorted(indexes)) not in candidates:
                candidates.append(tuple(sorted(indexes)))
        return self._best_of(obs, tuple(candidates))

    def replan(self, obs: CpuObservation, plan: Optional[HandPlan], skill: str) -> Optional[HandPlan]:
        """
        スキルを使った後に手牌を選び直す。

        山が変わるマリガンの後だけ探し直す。透視（見える牌）・役強化（翻数）の後は、
        前の候補を採点し直すだけで足りる。強襲は手牌の評価に関係しない。
        """
        if plan is None or replan_needs_search(skill):
            return self.plan_hand(obs)
        if skill == SkillType.ASSAULT.value:
            return plan
        return self._best_of(obs, plan.candidates)

    def _best_of(self, obs: CpuObservation, candidates: tuple[tuple[int, ...], ...]) -> Optional[HandPlan]:
        ctx = self._context(obs)
        best: Optional[HandPlan] = None
        for indexes in candidates:
            plan = self.evaluate_hand(obs, indexes, ctx=ctx)
            if plan is not None and (best is None or plan.score > best.score):
                best = plan
        if best is None:
            return None
        return HandPlan(best.hand_indexes, best.waits, best.score, best.strength, candidates)

    def evaluate_hand(
        self,
        obs: CpuObservation,
        hand_indexes: tuple[int, ...],
        ctx: Optional[_HandContext] = None,
    ) -> Optional[HandPlan]:
        """手牌に点数を付ける。聴牌でない・満貫の待ちがない場合は None。"""
        cfg = self.config["hand_selection"]
        ctx = ctx or self._context(obs)
        hand_tiles = [ctx.wall[i] for i in hand_indexes]
        scored = GameEngine.evaluate_waits(hand_tiles, ctx.wall, obs.dora_id, obs.me.boost_hand_bonus)
        if scored is None:
            return None

        rest_counts = Counter(tile_kind(ctx.wall[i]) for i in rest_indexes(obs, hand_indexes))
        waits: list[WaitInfo] = []
        for winning_tile, yaku, han in scored:
            kind = tile_kind(winning_tile)
            live = max(0, 4 - ctx.own_counts[kind] - ctx.opponent_discards[kind])
            available = max(
                0.0,
                live
                + cfg["bonus_wait_seen_in_opponent_wall"] * ctx.held_exposed[kind]
                - cfg["penalty_wait_in_own_wall"] * rest_counts[kind],
            )
            multiplier = obs.multiplier_for_han(han) if han >= 4 else 0.0
            waits.append(WaitInfo(kind, han, multiplier, tuple(yaku), available))

        score = self._score_waits(waits)
        if score is None:
            return None
        strength = _clamp(score / cfg["strong_hand_score"], 0.0, 1.0)
        return HandPlan(tuple(sorted(hand_indexes)), tuple(waits), score, strength)

    def _score_waits(self, waits) -> Optional[float]:
        """待ちの点数。和了れる（満貫以上の）待ちがなければ None。"""
        cfg = self.config["hand_selection"]
        winning = [w for w in waits if w.multiplier > 0]
        if not winning:
            return None
        return sum(
            (w.multiplier ** cfg["weight_han_multiplier"]) * (w.available ** cfg["weight_wait_count"])
            for w in winning
        )

    def best_boost_combo(
        self, obs: CpuObservation, plan: HandPlan
    ) -> tuple[Optional[tuple[str, ...]], float]:
        """
        役強化で強化する役の組合せのうち、点数がいちばん上がるもの（と、そのときの点数）。
        上がる組合せがなければ (None, plan.score)。手牌の評価はやり直さず、待ちの翻数に足し直して採点する。
        """
        best_combo: Optional[tuple[str, ...]] = None
        best_score = plan.score
        for combo in itertools.combinations_with_replacement(boostable_yaku(plan), obs.me.boost_hand_targets):
            extra = Counter(combo)
            boosted = []
            for w in plan.waits:
                han = w.han + sum(extra[name] for name in w.yaku)
                boosted.append(WaitInfo(w.kind, han, obs.multiplier_for_han(han) if han >= 4 else 0.0, w.yaku, w.available))
            score = self._score_waits(boosted) or 0.0
            if score > best_score:
                best_combo, best_score = combo, score
        return best_combo, best_score

    # ========== スキル ==========

    def next_skill(self, obs: CpuObservation, plan: Optional[HandPlan]) -> Optional[dict[str, Any]]:
        """次に使うスキルの action の data を返す。使わないなら None。手牌選択フェーズで繰り返し呼ぶ。"""
        if plan is None:
            return None
        for decide in (self._decide_perspective, self._decide_mulligan, self._decide_boost_hand, self._decide_assault):
            data = decide(obs, plan)
            if data is not None:
                return data
        return None

    def _can_afford(self, obs: CpuObservation, skill: str) -> bool:
        reserve = self.config["skill"]["min_health_after_use"]
        return obs.me.health - obs.me.skill_costs[skill] >= reserve

    def _uses(self, obs: CpuObservation, skill: str) -> int:
        return obs.my_skills_this_round.get(skill, 0)

    def _decide_perspective(self, obs: CpuObservation, plan: HandPlan) -> Optional[dict[str, Any]]:
        cfg = self.config["skill"]["perspective"]
        skill = "perspective"
        if not cfg["enabled"] or self._uses(obs, skill) >= cfg["max_uses_per_round"]:
            return None
        if not self._can_afford(obs, skill) or obs.me.health < cfg["min_health"]:
            return None
        unrevealed = obs.opponent.wall_size - len(obs.opponent.exposed)
        if unrevealed <= 0:
            return None
        if self.level == "normal":
            # 公開済みが多いほど、追加で見える価値は下がる
            value = cfg["value"] * unrevealed / max(1, obs.opponent.wall_size)
            if value < obs.me.skill_costs[skill]:
                return None
        return {"skill_type": skill}

    def _decide_mulligan(self, obs: CpuObservation, plan: HandPlan) -> Optional[dict[str, Any]]:
        cfg = self.config["skill"]["mulligan"]
        skill = "mulligan"
        if not cfg["enabled"] or self._uses(obs, skill) >= cfg["max_uses_per_round"]:
            return None
        if not obs.me.can_mulligan or not self._can_afford(obs, skill):
            return None
        if plan.strength >= cfg["max_strength"]:
            return None
        targets = mulligan_targets(obs, plan)
        if not targets:
            return None
        if self.level == "normal" and cfg["value_per_wait_tile"] * len(targets) < obs.me.skill_costs[skill]:
            return None
        return {"skill_type": skill, "target_hand_index": self.rng.choice(targets)}

    def _decide_boost_hand(self, obs: CpuObservation, plan: HandPlan) -> Optional[dict[str, Any]]:
        cfg = self.config["skill"]["boost_hand"]
        skill = "boost_hand"
        if not cfg["enabled"] or self._uses(obs, skill) >= 1:
            return None
        if not self._can_afford(obs, skill) or obs.me.health < cfg["min_health"]:
            return None

        combo, score = self.best_boost_combo(obs, plan)
        if combo is None:
            return None
        gain_ratio = (score - plan.score) / max(plan.score, 1e-9)
        if self.level == "normal" and gain_ratio < cfg["min_gain_ratio"]:
            return None
        return {"skill_type": skill, "yaku_names": list(combo)}

    def _decide_assault(self, obs: CpuObservation, plan: HandPlan) -> Optional[dict[str, Any]]:
        cfg = self.config["skill"]["assault"]
        skill = "assault"
        if not cfg["enabled"] or obs.me.assault_used_this_round or self._uses(obs, skill) >= 1:
            return None
        if not self._can_afford(obs, skill):
            return None

        use = plan.best_han >= cfg["min_han"]
        if not use and self.level == "normal" and cfg["kill_ratio"] > 0:
            # 強襲は自分の獲得を相手への追加ダメージに変える。HP の差は変わらないので、
            # 「普通に和了っても倒せないが、強襲なら倒せる」ときだけ使う（相手の掛け金は自分と同じと見積もる）
            gain = self.choose_bet(obs, plan) * plan.expected_multiplier
            use = gain > 0 and gain < obs.opponent.health <= 2 * gain * cfg["kill_ratio"]
        if not use:
            return None
        return {"skill_type": skill}

    # ========== 掛け金 ==========

    def choose_bet(self, obs: CpuObservation, plan: Optional[HandPlan]) -> int:
        cfg = self.config["bet"]
        unit = obs.me.bet_unit
        max_bet = min(obs.me.bet_max, obs.me.health // unit * unit)
        if max_bet <= unit:
            return unit

        # 和了ると「掛け金 × 倍率」を得て、放銃すると「掛け金 × 倍率」を失う（先払いはない）。
        # 和了れる見込みの倍率が設定値に届くときだけ、手の強さに応じて上げる
        if plan is None or plan.expected_multiplier < cfg["min_multiplier_to_raise"]:
            return unit

        ratio = 0.0
        for step in sorted(cfg["steps"], key=lambda s: s["min_strength"]):
            if plan.strength >= step["min_strength"]:
                ratio = step["bet_ratio"]

        lead = _clamp((obs.me.health - obs.opponent.health) / 20000, -1.0, 1.0)
        ratio = _clamp(ratio + cfg["health_lead_factor"] * lead, 0.0, 1.0)
        return unit + int((max_bet - unit) * ratio) // unit * unit

    # ========== 打牌 ==========

    def choose_discard(self, obs: CpuObservation) -> int:
        cfg = self.config["discard"]
        candidates = rest_indexes(obs, obs.me.hand)
        if not candidates:
            return fallback_discard(obs)

        opponent_discard_kinds = {tile_kind(t) for t in obs.opponent.discards}
        my_waits = set(obs.me.waits)
        held_exposed = possibly_held_exposed_kinds(obs)
        # 公開牌が相手の手牌に入っている見込み。相手が捨て進めるほど、残った公開牌は手牌である可能性が高い
        undiscarded = max(OPPONENT_HAND_SIZE, obs.opponent.wall_size - len(obs.opponent.discards))
        in_hand_probability = OPPONENT_HAND_SIZE / undiscarded

        # 点数は牌種で決まるので、牌種ごとに 1 回だけ数える（同点の選び方だけは牌ごとに散らす）
        kind_scores: dict[int, float] = {}
        for kind in {tile_kind(obs.me.wall[i]) for i in candidates}:
            score = 0.0
            if kind in opponent_discard_kinds:
                score += cfg["weight_genbutsu"]  # 相手が捨てた種類はフリテンで必ず安全
            if kind in my_waits:
                score += cfg["weight_own_wait"]  # 自分の待ちを捨てると自分がフリテンになる
            score += cfg["weight_danger"] * self._danger(kind, opponent_discard_kinds, held_exposed, in_hand_probability)
            kind_scores[kind] = score
        return max(candidates, key=lambda i: kind_scores[tile_kind(obs.me.wall[i])] + self.rng.random() * 1e-3)

    def _danger(
        self,
        kind: int,
        opponent_discard_kinds: set[int],
        held_exposed: Counter,
        in_hand_probability: float,
    ) -> float:
        """その牌種が相手の待ちである見込み（0 なら安全）。"""
        cfg = self.config["discard"]
        if kind in opponent_discard_kinds:
            return 0.0

        if kind >= HONOR_START:
            danger = cfg["danger_honor"]
        else:
            number = kind % 9
            if number in (0, 8):
                danger = cfg["danger_terminal"]
            elif number in (1, 7):
                danger = cfg["danger_2_8"]
            else:
                danger = cfg["danger_middle"]

            # 筋: 相手が3つ離れた同じ色の牌を捨てていれば、両面待ちの可能性が下がる
            suit_start = kind - number
            suji = [suit_start + n for n in (number - 3, number + 3) if 0 <= n <= 8]
            if any(s in opponent_discard_kinds for s in suji):
                danger *= cfg["suji_factor"]

        if cfg["weight_exposed_neighbor"] > 0 and held_exposed:
            nearby = sum(count * self._closeness(kind, exposed) for exposed, count in held_exposed.items())
            danger *= 1 + cfg["weight_exposed_neighbor"] * in_hand_probability * nearby
        return danger

    @staticmethod
    def _closeness(kind: int, other: int) -> float:
        """other を手牌に持つ相手が kind を待っている見込みの近さ。"""
        if kind == other:
            return CLOSENESS_BY_DISTANCE[0]
        if kind >= HONOR_START or other >= HONOR_START or kind // 9 != other // 9:
            return 0.0
        distance = abs(kind - other)
        return CLOSENESS_BY_DISTANCE[distance] if distance < len(CLOSENESS_BY_DISTANCE) else 0.0


def create_brain(config: dict, rng: Optional[random.Random] = None) -> RuleBrain:
    """設定の brain から考え方を作る。つよい（model）は学習済みモデルの用意ができるまで、ふつうで代わりに打つ。"""
    brain = config["brain"]
    level = "easy" if brain == "easy" else "normal"
    return RuleBrain(level, config, rng)
