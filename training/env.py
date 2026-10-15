"""
つよい の学習環境（段階1: 打牌）

学習する打ち手は P0。P0 の打牌だけを選び、それ以外は「ふつう」の考え方が打つ:
- P0 の手牌選択・スキル・掛け金・ロン
- 相手（P1）のすべて
1 エピソード = 1 局。報酬は局の終わりにだけ出る（既定は P0 から見た HP の差の動き ÷ 1000）。
報酬の関数は差し替えられる（掛け金の精算方法が会議で変わっても、ここを変えれば済む）。

観測: 視界（CpuObservation）を数値の列にしたもの（OBS_SIZE 次元、float32）
行動: 捨てる牌の種類（0〜28 の牌種）。手牌以外でまだ打っていない牌の種類だけが選べる（action_mask）
"""
import random
import sys
from pathlib import Path
from typing import Callable, Optional

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai.observation import CpuObservation  # noqa: E402
from mahjong_engine.ai.rule_brain import possibly_held_exposed_kinds, rest_indexes, tile_kind  # noqa: E402
from mahjong_engine.engine.game_engine import DISCARDS_PER_PLAYER  # noqa: E402
from mahjong_engine.engine.tile_wall import RoundDeal, TileDefinitions  # noqa: E402
from training.simulator import MatchSimulator, RoundRecord, make_rule_agent  # noqa: E402

KINDS = TileDefinitions.TOTAL_TILE_TYPES  # 29
HP_SCALE = 20000.0
BET_SCALE = 5000.0
AKA_BIT = 0b1000000

# 牌種ごとの特徴（各 29 次元）
KIND_FEATURES = (
    "my_wall",              # 自分の山 34 枚の枚数
    "my_hand",              # 手牌 13 枚の枚数
    "my_rest",              # 手牌以外でまだ打っていない牌の枚数（＝捨てられる牌）
    "my_discards",          # 自分の捨て牌の枚数
    "my_waits",             # 自分の待ち（1/0）
    "opp_discards",         # 相手の捨て牌の枚数
    "opp_genbutsu",         # 相手が捨てた種類（1/0。フリテンで必ず安全）
    "opp_exposed_held",     # 透視で公開され、まだ捨てられていない可能性がある相手の牌の枚数
    "dora",                 # ドラ（1/0）
)
SCALAR_FEATURES = (
    "my_discard_count",     # ÷DISCARDS_PER_PLAYER
    "opp_discard_count",    # ÷DISCARDS_PER_PLAYER
    "i_am_first",
    "my_health",            # ÷20000
    "opp_health",           # ÷20000
    "my_bet",               # ÷5000
    "carry_over",           # 流局の持ち越し回数 ÷4
)
OBS_SIZE = KINDS * len(KIND_FEATURES) + len(SCALAR_FEATURES)

# 学習中は手牌の候補探索を軽くする（1 局あたりの時間の大半がここ）
FAST_HAND_SELECTION = {"candidate_limit": 10, "search_time_limit": 0.3}


def _counts(tiles) -> list[float]:
    counts = [0.0] * KINDS
    for t in tiles:
        counts[tile_kind(t)] += 1
    return counts


def _one_hot(kinds) -> list[float]:
    v = [0.0] * KINDS
    for kind in kinds:
        if 0 <= kind < KINDS:
            v[kind] = 1.0
    return v


def encode_observation(obs: CpuObservation) -> np.ndarray:
    """視界を数値の列にする（Python のリストで組み立て、最後に 1 回だけ配列にする）。"""
    wall = obs.me.wall
    opp_discards = _counts(obs.opponent.discards)
    held = [0.0] * KINDS
    for kind, count in possibly_held_exposed_kinds(obs).items():
        held[kind] = float(count)

    features = (
        _counts(wall)
        + _counts(wall[i] for i in obs.me.hand)
        + _counts(wall[i] for i in rest_indexes(obs, obs.me.hand))
        + _counts(wall[i] for i in obs.me.discarded_wall_indexes)
        + _one_hot(obs.me.waits)
        + opp_discards
        + [1.0 if c > 0 else 0.0 for c in opp_discards]
        + held
        + _one_hot([] if obs.dora_id is None else [obs.dora_id])
        + [
            obs.my_discard_count / DISCARDS_PER_PLAYER,
            len(obs.opponent.discards) / DISCARDS_PER_PLAYER,
            1.0 if obs.i_am_first_player else 0.0,
            obs.me.health / HP_SCALE,
            obs.opponent.health / HP_SCALE,
            obs.me.bet / BET_SCALE,
            obs.carry_over_draw_count / 4.0,
        ]
    )
    return np.asarray(features, dtype=np.float32)


def action_mask(obs: CpuObservation) -> np.ndarray:
    """選べる行動（捨てられる牌の種類）。"""
    mask = np.zeros(KINDS, dtype=bool)
    for i in rest_indexes(obs, obs.me.hand):
        mask[tile_kind(obs.me.wall[i])] = True
    return mask


def kind_to_wall_index(obs: CpuObservation, kind: int) -> int:
    """牌種から、実際に捨てる wall index を選ぶ。赤ドラは相手の和了の翻を増やすので後回しにする。"""
    candidates = [i for i in rest_indexes(obs, obs.me.hand) if tile_kind(obs.me.wall[i]) == kind]
    if not candidates:
        raise ValueError(f"捨てられない牌種: {kind}")
    return min(candidates, key=lambda i: (obs.me.wall[i] & AKA_BIT) != 0)


def hp_swing_reward(record: RoundRecord) -> float:
    """既定の報酬: P0 から見た HP の差の動き（P0 の増減 − P1 の増減）÷ 1000。"""
    d0 = record.health_after["P0"] - record.health_before["P0"]
    d1 = record.health_after["P1"] - record.health_before["P1"]
    return (d0 - d1) / 1000.0


class DiscardEnv:
    """段階1 の学習環境。reset → step を局の終わりまで繰り返す。"""

    def __init__(
        self,
        deal_source: Callable[[], RoundDeal],
        reward_fn: Callable[[RoundRecord], float] = hp_swing_reward,
        opponent_level: str = "normal",
        seed: Optional[int] = None,
        hand_selection: Optional[dict] = None,
    ):
        self.deal_source = deal_source
        self.reward_fn = reward_fn
        self.rng = random.Random(seed)
        self.opponent_level = opponent_level
        self.hand_selection = hand_selection if hand_selection is not None else FAST_HAND_SELECTION
        self.sim: Optional[MatchSimulator] = None
        # いまの P0 の手番の視界（reset / step で作り、step と rule_action で使い回す）
        self._current: Optional[CpuObservation] = None

    def _observe(self) -> tuple[np.ndarray, np.ndarray]:
        self._current = self.sim._obs("P0")
        return encode_observation(self._current), action_mask(self._current)

    def reset(self) -> tuple[np.ndarray, np.ndarray]:
        """新しい局を始め、P0 の最初の手番の観測を返す。P0 が打つ前に局が終わったら次の局を始め直す。"""
        while True:
            seed = self.rng.randrange(2**31)
            random.seed(seed)  # エンジンの乱数（先手の抽選・透視・マリガン）
            me = make_rule_agent("normal", seed + 1, self.hand_selection)
            opponent = make_rule_agent(self.opponent_level, seed + 2, self.hand_selection)
            self.sim = MatchSimulator([me, opponent], deal_factory=lambda _n: self.deal_source(),
                                      rng=random.Random(seed + 3))
            self.sim.start_round()
            if self.sim.advance_discards(stop_for="P0"):
                return self._observe()
            # P0 の手番が来る前に局が終わった（先手の一打目でロンなど）。学習には使えないので打ち直す

    def step(self, kind: int) -> tuple[np.ndarray, np.ndarray, float, bool, dict]:
        """牌種 kind を捨て、次の P0 の手番まで進める。"""
        self.sim.discard("P0", kind_to_wall_index(self._current, kind))
        if self.sim.advance_discards(stop_for="P0"):
            obs_vector, mask = self._observe()
            return obs_vector, mask, 0.0, False, {}
        self._current = None
        record = self.sim.finish_round(advance=False)
        return np.zeros(OBS_SIZE, dtype=np.float32), np.zeros(KINDS, dtype=bool), self.reward_fn(record), True, {"record": record}

    def rule_action(self) -> int:
        """「ふつう」が選ぶ牌種（模倣学習の手本）。"""
        obs = self._current
        return tile_kind(obs.me.wall[self.sim.agents["P0"].choose_discard(obs)])
