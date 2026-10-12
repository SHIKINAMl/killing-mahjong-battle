import random

import numpy as np

from mahjong_engine.ai import model_brain as mb
from mahjong_engine.ai.cpu_config import load_cpu_config
from mahjong_engine.ai.rule_brain import RuleBrain, create_brain
from mahjong_engine.engine.tile_wall import generate_round_deal
from training.simulator import MatchSimulator, RuleAgent


def _random_nets(seed: int = 0) -> dict:
    rng = np.random.default_rng(seed)
    sizes = {"hand": (mb.CANDIDATE_SIZE, 8, 1), "skill": (mb.STATE_SIZE, 8, len(mb.SKILL_ACTIONS)),
             "bet": (mb.STATE_SIZE, 8, len(mb.BET_LEVELS))}
    return {head: mb.Mlp([(rng.normal(size=(a, b)) * 0.3, np.zeros(b)) for a, b in zip(s[:-1], s[1:])])
            for head, s in sizes.items()}


def _config() -> dict:
    config = load_cpu_config()["hard"]
    config["hand_selection"]["candidate_limit"] = 20
    return config


def _play(brains, seed: int = 3, rounds: int = 2):
    random.seed(seed)
    deals = [generate_round_deal(2) for _ in range(rounds + 2)]
    it = iter(deals)
    sim = MatchSimulator([RuleAgent(b) for b in brains], deal_factory=lambda _n: next(it), opening_boosts=False)
    records = [sim.play_round() for _ in range(rounds) if not sim.ended]
    return sim, records


def test_model_brain_plays_rounds_and_records_decisions():
    recorder = []
    brain = mb.ModelBrain(_config(), random.Random(1), nets=_random_nets(), mode="sample", recorder=recorder)
    _play([brain, RuleBrain("normal", _config(), random.Random(2))])
    heads = {d["head"] for d in recorder}
    assert heads <= set(mb.HEADS) and recorder
    for d in recorder:
        assert d["state"].shape == (mb.STATE_SIZE,)
        if d["head"] == "hand":
            assert d["x"].shape[1] == mb.CANDIDATE_SIZE and 0 <= d["action"] < len(d["x"])
        else:
            assert d["mask"] is None or d["mask"][d["action"]]  # 選べない行動は選ばない


def test_teacher_mode_plays_like_the_rule_brain():
    """手本の記録中は規則と同じ手を打つ（記録した行動は規則の行動）。"""
    recorder = []
    teacher = mb.ModelBrain(_config(), random.Random(1), mode="teacher", recorder=recorder)
    _, with_teacher = _play([teacher, RuleBrain("normal", _config(), random.Random(2))])
    _, with_rule = _play([RuleBrain("normal", _config(), random.Random(1)), RuleBrain("normal", _config(), random.Random(2))])
    assert [r.health_after for r in with_teacher] == [r.health_after for r in with_rule]
    assert any(d["head"] == "hand" for d in recorder)


def test_create_brain_uses_model_file_when_present(tmp_path):
    nets = _random_nets()
    arrays = {}
    for head, net in nets.items():
        for i, (w, b) in enumerate(net.layers):
            arrays[f"{head}.W{i}"], arrays[f"{head}.b{i}"] = w, b
    path = tmp_path / "hard.npz"
    np.savez(path, **arrays)
    config = {**_config(), "model_path": str(path)}
    assert isinstance(create_brain(config, random.Random(0)), mb.ModelBrain)
    # モデルがなければ規則で打つ
    assert type(create_brain({**config, "model_path": str(tmp_path / "none.npz")}, random.Random(0))) is RuleBrain
