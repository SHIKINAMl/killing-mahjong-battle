"""
よわい／ふつう の考え方

規則ごとに点数を付け、点数の高い行動を選ぶ。重み・しきい値はすべて cpu_config.json から受け取る。
入力は CpuObservation（視界）だけで、エンジンの状態は読まない。
出力は人間のクライアントと同じ action JSON。
"""
import itertools
import random
import time
from collections import Counter
from dataclasses import dataclass
from typing import Any, Optional

from ..engine.game_engine import GameEngine
from ..engine.hand_analyzer import HandAnalyzer
from ..engine.yaku import Yaku
from .observation import CpuObservation

HONOR_START = 27  # 牌種 27 以降は字牌（東・西）
HAND_SIZE = 13
OPPONENT_HAND_SIZE = 13


def tile_kind(tile: int) -> int:
    """牌ID から牌種（下位5ビット）を取り出す。"""
    return tile & 0b11111


def _clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


@dataclass(frozen=True)
class WaitInfo:
    kind: int
    han: int
    multiplier: float  # 満貫未満なら 0（ロンしても精算されない）
    yaku: tuple[str, ...]
    available: float  # 和了れそうな枚数の見積もり


@dataclass(frozen=True)
class HandPlan:
    hand_indexes: tuple[int, ...]
    waits: tuple[WaitInfo, ...]
    score: float
    strength: float  # 0〜1。掛け金やスキルの判断に使う

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


def tiles_to_wall_indexes(wall: list[int], tiles: list[int]) -> Optional[tuple[int, ...]]:
    """牌ID の並びを wall index へ変換する。ドラ・赤ドラを取り違えないよう、同一の牌を優先する。"""
    used = [False] * len(wall)
    indexes: list[int] = []
    for tile in tiles:
        found = next((i for i, t in enumerate(wall) if not used[i] and t == tile), None)
        if found is None:
            found = next((i for i, t in enumerate(wall) if not used[i] and tile_kind(t) == tile_kind(tile)), None)
        if found is None:
            return None
        used[found] = True
        indexes.append(found)
    return tuple(sorted(indexes))


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


class RuleBrain:
    """よわい（level="easy"）／ふつう（level="normal"）の考え方。"""

    def __init__(self, level: str, config: dict, rng: Optional[random.Random] = None):
        if level not in ("easy", "normal"):
            raise ValueError(f"Unsupported level: {level}")
        self.level = level
        self.config = config
        self.rng = rng or random.Random()

    # ========== 手牌選択 ==========

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
            if indexes is not None and indexes not in candidates:
                candidates.append(indexes)

        best: Optional[HandPlan] = None
        for indexes in candidates:
            plan = self.evaluate_hand(obs, indexes)
            if plan is not None and (best is None or plan.score > best.score):
                best = plan
        return best

    def evaluate_hand(
        self,
        obs: CpuObservation,
        hand_indexes: tuple[int, ...],
        extra_boosts: Optional[dict[str, int]] = None,
    ) -> Optional[HandPlan]:
        """手牌に点数を付ける。聴牌でない・満貫の待ちがない場合は None。"""
        cfg = self.config["hand_selection"]
        wall = list(obs.me.wall)
        hand_tiles = [wall[i] for i in hand_indexes]
        if not HandAnalyzer.is_tenpai(hand_tiles, wall):
            return None

        boosts = Counter(obs.me.boost_hand_bonus)
        if extra_boosts:
            boosts.update(extra_boosts)

        own_counts = Counter(tile_kind(t) for t in wall)
        opponent_discards = Counter(tile_kind(t) for t in obs.opponent.discards)
        held_exposed = possibly_held_exposed_kinds(obs)
        rest_counts = Counter(tile_kind(wall[i]) for i in rest_indexes(obs, hand_indexes))

        waits: list[WaitInfo] = []
        score = 0.0
        for kind in HandAnalyzer.get_tenpai_waiting_tiles(hand_tiles, wall):
            # GameEngine.get_waits と同じく、ドラの待ちはドラの牌IDで役を数える
            winning_tile = kind + 32 if kind == obs.dora_id else kind
            yaku = HandAnalyzer.enum_yaku(hand_tiles + [winning_tile], winning_tile=winning_tile)
            han = sum(Yaku.get_han_by_name(name) for name in yaku) + sum(boosts.get(name, 0) for name in yaku)
            multiplier = obs.multiplier_for_han(han) if han >= 4 else 0.0

            live = max(0, 4 - own_counts[kind] - opponent_discards[kind])
            available = max(
                0.0,
                live
                + cfg["bonus_wait_seen_in_opponent_wall"] * held_exposed[kind]
                - cfg["penalty_wait_in_own_wall"] * rest_counts[kind],
            )
            if multiplier > 0:
                score += (multiplier ** cfg["weight_han_multiplier"]) * (available ** cfg["weight_wait_count"])
            waits.append(WaitInfo(kind, han, multiplier, tuple(yaku), available))

        if not any(w.multiplier > 0 for w in waits):
            return None

        strength = _clamp(score / cfg["strong_hand_score"], 0.0, 1.0)
        return HandPlan(tuple(sorted(hand_indexes)), tuple(waits), score, strength)

    # ========== スキル ==========

    def next_skill(self, obs: CpuObservation, plan: Optional[HandPlan]) -> Optional[dict[str, Any]]:
        """次に使うスキルの action を返す。使わないなら None。手牌選択フェーズで繰り返し呼ぶ。"""
        if plan is None:
            return None
        for decide in (self._decide_perspective, self._decide_mulligan, self._decide_boost_hand, self._decide_assault):
            action = decide(obs, plan)
            if action is not None:
                return action
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
        return {"action": "skill", "data": {"skill_type": skill}}

    def _decide_mulligan(self, obs: CpuObservation, plan: HandPlan) -> Optional[dict[str, Any]]:
        cfg = self.config["skill"]["mulligan"]
        skill = "mulligan"
        if not cfg["enabled"] or self._uses(obs, skill) >= cfg["max_uses_per_round"]:
            return None
        if not obs.me.can_mulligan or not self._can_afford(obs, skill):
            return None
        if plan.strength >= cfg["max_strength"]:
            return None
        # 手牌以外にある自分の待ち牌は、山から出ない上に、捨てればフリテンになる。交換して減らす
        targets = [i for i in rest_indexes(obs, plan.hand_indexes) if tile_kind(obs.me.wall[i]) in plan.wait_kinds]
        if not targets:
            return None
        if self.level == "normal" and cfg["value_per_wait_tile"] * len(targets) < obs.me.skill_costs[skill]:
            return None
        return {"action": "skill", "data": {"skill_type": skill, "target_hand_index": self.rng.choice(targets)}}

    def _decide_boost_hand(self, obs: CpuObservation, plan: HandPlan) -> Optional[dict[str, Any]]:
        cfg = self.config["skill"]["boost_hand"]
        skill = "boost_hand"
        if not cfg["enabled"] or self._uses(obs, skill) >= 1:
            return None
        if not self._can_afford(obs, skill) or obs.me.health < cfg["min_health"]:
            return None

        boostable = sorted({
            name
            for wait in plan.waits if wait.multiplier > 0
            for name in wait.yaku
            if GameEngine.normalize_boost_target_yaku_name(name) is not None
        })
        if not boostable:
            return None

        best_combo: Optional[tuple[str, ...]] = None
        best_score = plan.score
        for combo in itertools.combinations_with_replacement(boostable, obs.me.boost_hand_targets):
            boosted = self.evaluate_hand(obs, plan.hand_indexes, extra_boosts=Counter(combo))
            if boosted is not None and boosted.score > best_score:
                best_combo, best_score = combo, boosted.score
        if best_combo is None:
            return None

        gain_ratio = (best_score - plan.score) / max(plan.score, 1e-9)
        if self.level == "normal" and gain_ratio < cfg["min_gain_ratio"]:
            return None
        data: dict[str, Any] = {"skill_type": skill}
        if len(best_combo) == 1:
            data["yaku_name"] = best_combo[0]
        else:
            data["yaku_names"] = list(best_combo)
        return {"action": "skill", "data": data}

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
        return {"action": "skill", "data": {"skill_type": skill}}

    # ========== ロン ==========

    def should_accept_agari(self, obs: CpuObservation) -> bool:
        """
        ロンを受けるか。満貫以上で精算されるなら必ず受ける。

        満貫未満の手は和了れないので、エンジンは満貫に届く牌でしかロン確認を出さない（GameEngine.can_agari）。
        ここでの判定は念のための二重確認。数え方は GameEngine.liquidation と同じ（状況役の一発・河底撈魚を含む）。
        """
        tile = obs.pending_agari_tile
        if tile is None or len(obs.me.hand) != HAND_SIZE:
            return True
        hand_tiles = [obs.me.wall[i] for i in obs.me.hand]
        yaku = HandAnalyzer.enum_yaku(hand_tiles + [tile], winning_tile=tile)
        han = sum(Yaku.get_han_by_name(name) for name in yaku)
        han += sum(obs.me.boost_hand_bonus.get(name, 0) for name in yaku)
        if Yaku.RICHI.japanese_name in yaku and obs.my_discard_count <= 1 and len(obs.opponent.discards) == 1:
            han += Yaku.IPPATSU.han + obs.me.boost_hand_bonus.get(Yaku.IPPATSU.japanese_name, 0)
        if len(obs.opponent.discards) == 16 and obs.i_am_first_player:
            han += Yaku.KAWA_ZO.han + obs.me.boost_hand_bonus.get(Yaku.KAWA_ZO.japanese_name, 0)
        return han >= 4

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

        best_index = candidates[0]
        best_score = float("-inf")
        for index in candidates:
            kind = tile_kind(obs.me.wall[index])
            score = 0.0
            if kind in opponent_discard_kinds:
                score += cfg["weight_genbutsu"]  # 相手が捨てた種類はフリテンで必ず安全
            if kind in my_waits:
                score += cfg["weight_own_wait"]  # 自分の待ちを捨てると自分がフリテンになる
            score += cfg["weight_danger"] * self._danger(kind, opponent_discard_kinds, held_exposed, in_hand_probability)
            score += self.rng.random() * 1e-3  # 同点のときの選び方を散らす
            if score > best_score:
                best_index, best_score = index, score
        return best_index

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
            return 1.0
        if kind >= HONOR_START or other >= HONOR_START or kind // 9 != other // 9:
            return 0.0
        distance = abs(kind - other)
        return {1: 0.6, 2: 0.3}.get(distance, 0.0)


def create_brain(config: dict, rng: Optional[random.Random] = None) -> RuleBrain:
    """設定の brain から考え方を作る。つよい（model）は学習済みモデルの用意ができるまで、ふつうで代わりに打つ。"""
    brain = config["brain"]
    level = "easy" if brain == "easy" else "normal"
    return RuleBrain(level, config, rng)
