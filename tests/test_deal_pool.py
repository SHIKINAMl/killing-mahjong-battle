"""
配牌の事前生成（training/deal_pool.py）のテスト

- 同じ種からは同じ配牌ができる（何プロセスで分担しても）
- 途中で止めても、足りないシャードだけを作り直して再開できる
- 生成処理のコードが変わっていたら読み込みを止める
- 取り出した配牌は本番の決まりを満たし、そのまま GameEngine に配れる

実行: python -m pytest tests -q
"""
import random
from pathlib import Path

import numpy as np
import pytest

from mahjong_engine.engine.game_engine import GameEngine
from mahjong_engine.engine.tile_wall import RoundDeal
from training import deal_pool
from training.deal_pool import DealPool, StalePoolError, check_deal, generate, verify


def _quiet(*_args):
    pass


@pytest.fixture(scope="module")
def small_pool(tmp_path_factory):
    out = tmp_path_factory.mktemp("deals")
    generate(out, count=25, seed=7, workers=2, shard_size=10, log=_quiet)
    return out


def _arrays(pool_dir: Path) -> dict:
    return {
        f"{shard.name}/{f.name}": np.load(f)
        for shard in sorted(pool_dir.glob("shard_*"))
        for f in sorted(shard.glob("*.npy"))
    }


def test_generate_and_load(small_pool):
    pool = DealPool(small_pool)
    assert len(pool) == 25  # 10 + 10 + 5
    deal = pool.get(24)
    assert isinstance(deal, RoundDeal)
    assert len(deal.hands) == 2 and len(deal.reserved_tiles) == 47
    assert verify(pool, sample=0, log=_quiet) == 0


def test_same_seed_same_deals(small_pool, tmp_path):
    """プロセス数を変えても、同じ種からは同じ配牌ができる。"""
    generate(tmp_path, count=25, seed=7, workers=1, shard_size=10, log=_quiet)
    first, second = _arrays(small_pool), _arrays(tmp_path)
    assert first.keys() == second.keys()
    assert all(np.array_equal(first[k], second[k]) for k in first)


def test_resume_only_builds_missing_shards(tmp_path):
    generate(tmp_path, count=20, seed=3, workers=1, shard_size=10, log=_quiet)
    before = _arrays(tmp_path)

    # 2つ目のシャードを作っている途中で止まったことにする（書きかけの一時ディレクトリだけが残った）
    manifest = deal_pool._load_manifest(tmp_path)
    del manifest["shards"]["1"]
    deal_pool._write_manifest(tmp_path, manifest)
    (tmp_path / "shard_00001").rename(tmp_path / ".tmp_shard_00001_999")

    logs = []
    generate(tmp_path, count=20, seed=3, workers=1, shard_size=10, log=logs.append)
    assert "うち 1 シャードを作る" in logs[0]
    assert not list(tmp_path.glob(".tmp_shard_*"))
    after = _arrays(tmp_path)
    assert all(np.array_equal(before[k], after[k]) for k in before)


def test_resume_adopts_finished_shards_missing_from_manifest(tmp_path):
    """シャードはできたが台帳に記録する前に止まった場合は、作り直さずに取り込む。"""
    generate(tmp_path, count=20, seed=3, workers=1, shard_size=10, log=_quiet)
    manifest = deal_pool._load_manifest(tmp_path)
    del manifest["shards"]["1"]
    deal_pool._write_manifest(tmp_path, manifest)

    logs = []
    generate(tmp_path, count=20, seed=3, workers=1, shard_size=10, log=logs.append)
    assert any("1 個取り込みました" in line for line in logs)
    assert any("うち 0 シャードを作る" in line for line in logs)
    assert len(DealPool(tmp_path)) == 20


def test_resume_rejects_different_seed(small_pool):
    with pytest.raises(ValueError):
        generate(small_pool, count=25, seed=8, workers=1, shard_size=10, log=_quiet)


def test_stale_pool_is_rejected(small_pool, monkeypatch):
    monkeypatch.setattr(deal_pool, "generator_fingerprint", lambda: "changed")
    with pytest.raises(StalePoolError):
        DealPool(small_pool)
    DealPool(small_pool, check_fingerprint=False)  # 承知のうえで読むことはできる


def test_check_deal_detects_broken_deal(small_pool):
    deal = DealPool(small_pool).get(0)
    assert check_deal(deal) == []
    wall, example = deal.hands[0]
    other_kind = ((wall[-1] & 0b11111) + 1) % 27  # 山の最後の牌を、別の種類の牌に置き換える
    broken = RoundDeal(hands=[(wall[:-1] + [other_kind], example), deal.hands[1]],
                       reserved_tiles=deal.reserved_tiles, dora_id=deal.dora_id)
    assert check_deal(broken)


def test_pool_deal_can_be_dealt_by_engine(small_pool):
    """取り出した配牌を、本番の GameEngine がそのまま配れる。"""
    deal = DealPool(small_pool).sample(random.Random(0))
    engine = GameEngine(deal_factory=lambda _n: deal)
    engine.initialize_players(["A", "B"])
    engine.start_game(5)

    for player, (wall, _) in zip(engine.state.players, deal.hands):
        assert player.wall == wall
        assert len(player.hand) == 13
        assert engine.select_hand(player.hand, player)  # 聴牌例はそのまま確定できる
    assert engine.state.round_state.dora_id == deal.dora_id
    assert engine.state.round_state.reserved_tiles == deal.reserved_tiles
