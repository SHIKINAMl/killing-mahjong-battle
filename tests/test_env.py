"""
学習環境（training/env.py、段階1: 打牌）のテスト

- 観測の大きさと、選べる行動（手牌以外でまだ打っていない牌の種類）が正しい
- 選んだ牌種から、赤ドラを後回しにして wall index を選ぶ
- 局の終わりまで進み、報酬が出る

実行: python -m pytest tests -q
"""
import random

import numpy as np

from mahjong_engine.ai.rule_brain import rest_indexes, tile_kind
from mahjong_engine.engine.tile_wall import generate_round_deal
from training.env import AKA_BIT, KINDS, OBS_SIZE, DiscardEnv, action_mask, kind_to_wall_index


def _env(seed: int) -> DiscardEnv:
    random.seed(seed)
    deals = [generate_round_deal(2) for _ in range(3)]
    rng = random.Random(seed)
    return DiscardEnv(lambda: rng.choice(deals), seed=seed,
                      hand_selection={"candidate_limit": 3, "search_time_limit": 0.1})


def test_episode_runs_to_the_end_with_valid_actions():
    env = _env(0)
    obs, mask = env.reset()
    assert obs.shape == (OBS_SIZE,) and obs.dtype == np.float32
    assert mask.shape == (KINDS,) and mask.any()
    for _ in range(40):
        p0 = env.sim._obs("P0")
        expected = {tile_kind(p0.me.wall[i]) for i in rest_indexes(p0, p0.me.hand)}
        assert set(np.flatnonzero(mask)) == expected
        action = env.rule_action()
        assert mask[action]
        obs, mask, reward, done, info = env.step(action)
        if done:
            assert "record" in info and isinstance(reward, float)
            return
        assert reward == 0.0
    raise AssertionError("局が終わらない")


def test_kind_to_wall_index_avoids_aka_dora():
    env = _env(1)
    env.reset()
    obs = env.sim._obs("P0")
    for kind in np.flatnonzero(action_mask(obs)):
        index = kind_to_wall_index(obs, int(kind))
        same_kind = [i for i in rest_indexes(obs, obs.me.hand) if tile_kind(obs.me.wall[i]) == kind]
        assert index in same_kind
        if any(not obs.me.wall[i] & AKA_BIT for i in same_kind):
            assert not obs.me.wall[index] & AKA_BIT
