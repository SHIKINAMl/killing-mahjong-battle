"""
配牌の事前生成（つよい の学習用）

配牌の生成（満貫聴牌形の探索）は 1 局あたり平均 0.06 秒、遅いと 1 秒以上かかり、
学習の速さの壁になる。そこで、本番と同じ generate_round_deal で前もって大量に作っておき、
学習中は読み出して使い回す。

学習と実戦でずれを出さないための決まり:
- 必ず本番と同じ generate_round_deal で作る（配牌の確率分布が実戦と同じになる）
- 生成処理のコード（tile_wall.py / hand_analyzer.py / yaku.py）の指紋を保存し、
  コードが変わっていたら読み込みを止める（古い配牌で学習しないように）
- 学習に使っていない新しい配牌（fresh_deal）でも評価し、丸暗記していないか確かめる

保存形式（out_dir/）:
- manifest.json: 指紋・乱数の種・シャードの一覧
- shard_00000/: シャード（既定 5 万局）ごとのディレクトリ。項目ごとの .npy（uint8）
    walls.npy     (N, 2, 34)  各プレイヤーの山（牌ID）
    examples.npy  (N, 2, 13)  サーバーの聴牌例（牌ID）
    reserved.npy  (N, 47)     配牌後に牌山へ残った牌（マリガンの交換候補）
    dora.npy      (N,)        ドラの牌種
  読むときはメモリマップするので、学習を複数プロセスで動かしてもメモリを重複して使わない

使い方（リポジトリ直下で）:
    python -m training.deal_pool generate --count 2000000 --out training/data/deals
    python -m training.deal_pool verify --pool training/data/deals
    python -m training.deal_pool info --pool training/data/deals
"""
import argparse
import bisect
import hashlib
import json
import os
import random
import shutil
import sys
import time
from collections import Counter
from concurrent.futures import ProcessPoolExecutor, as_completed
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator, Optional

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.engine.hand_analyzer import HandAnalyzer  # noqa: E402
from mahjong_engine.engine.tile_wall import RoundDeal, TileDefinitions, generate_round_deal  # noqa: E402

NUM_PLAYERS = 2
WALL_SIZE = 34
HAND_SIZE = 13
RESERVED_SIZE = TileDefinitions.TOTAL_TILES - 1 - NUM_PLAYERS * WALL_SIZE  # ドラ表示牌を1枚抜くので 47
FORMAT_VERSION = 1
DEFAULT_SHARD_SIZE = 50_000

# 配牌の確率分布を決めるコード。どれかが変わったら、配牌を作り直す
GENERATOR_SOURCES = (
    "mahjong_engine/engine/tile_wall.py",
    "mahjong_engine/engine/hand_analyzer.py",
    "mahjong_engine/engine/yaku.py",
)
FIELDS = ("walls", "examples", "reserved", "dora")


class StalePoolError(RuntimeError):
    """配牌を作ったときと、いまの生成処理のコードが違う。"""


def generator_fingerprint() -> str:
    """生成処理のコードの指紋。改行コードの違い（CRLF / LF）は無視する。"""
    digest = hashlib.sha256()
    for rel in GENERATOR_SOURCES:
        text = (ROOT / rel).read_bytes().replace(b"\r\n", b"\n")
        digest.update(rel.encode("utf-8") + b"\0" + text + b"\0")
    return digest.hexdigest()


def shard_seed(base_seed: int, shard_index: int) -> int:
    """シャードごとの乱数の種。どのプロセスが作っても、同じシャードからは同じ配牌ができる。"""
    return (base_seed * 1_000_003 + shard_index) & 0xFFFFFFFF


# ========== 生成 ==========

def _generate_shard(out_dir: str, shard_index: int, count: int, seed: int) -> tuple[int, int, float]:
    """1 シャード分の配牌を作って保存する（子プロセスで動く）。"""
    # generate_round_deal は random モジュールを使う（TileWall のシャッフル・ドラ・聴牌形の探索順）
    random.seed(seed)
    walls = np.zeros((count, NUM_PLAYERS, WALL_SIZE), dtype=np.uint8)
    examples = np.zeros((count, NUM_PLAYERS, HAND_SIZE), dtype=np.uint8)
    reserved = np.zeros((count, RESERVED_SIZE), dtype=np.uint8)
    dora = np.zeros((count,), dtype=np.uint8)

    started = time.perf_counter()
    for i in range(count):
        deal = generate_round_deal(NUM_PLAYERS)
        for p, (wall, example) in enumerate(deal.hands):
            walls[i, p] = wall
            examples[i, p] = example
        reserved[i] = deal.reserved_tiles
        dora[i] = deal.dora_id

    # 書きかけのシャードを完成品と取り違えないよう、一時ディレクトリに書いてから名前を変える
    final_dir = Path(out_dir) / f"shard_{shard_index:05d}"
    temp_dir = Path(out_dir) / f".tmp_shard_{shard_index:05d}_{os.getpid()}"
    temp_dir.mkdir(parents=True, exist_ok=True)
    for name, array in zip(FIELDS, (walls, examples, reserved, dora)):
        np.save(temp_dir / f"{name}.npy", array)
    _replace_dir(temp_dir, final_dir)
    return shard_index, count, time.perf_counter() - started


def _replace_dir(source: Path, target: Path, attempts: int = 10) -> None:
    """
    source の名前を target に変える。

    OneDrive などの同期対象のフォルダでは、同期中のフォルダの名前変更が「アクセス拒否」で
    一時的に弾かれることがあるので、少し待ってやり直す。
    """
    for attempt in range(1, attempts + 1):
        try:
            if target.exists():
                shutil.rmtree(target)
            source.rename(target)
            return
        except PermissionError:
            if attempt == attempts:
                raise
            time.sleep(0.5 * attempt)


def _shard_count_on_disk(out_dir: Path, shard_index: int) -> Optional[int]:
    """保存済みのシャードの局数。ファイルが欠けている・形が合わないなら None。"""
    shard_dir = out_dir / f"shard_{shard_index:05d}"
    try:
        arrays = {name: np.load(shard_dir / f"{name}.npy", mmap_mode="r") for name in FIELDS}
    except (OSError, ValueError):
        return None
    n = arrays["dora"].shape[0]
    expected = {
        "walls": (n, NUM_PLAYERS, WALL_SIZE),
        "examples": (n, NUM_PLAYERS, HAND_SIZE),
        "reserved": (n, RESERVED_SIZE),
        "dora": (n,),
    }
    if any(arrays[name].shape != shape for name, shape in expected.items()):
        return None
    return n


def _manifest_path(out_dir: Path) -> Path:
    return out_dir / "manifest.json"


def _load_manifest(out_dir: Path) -> Optional[dict]:
    path = _manifest_path(out_dir)
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def _write_manifest(out_dir: Path, manifest: dict, attempts: int = 10) -> None:
    """
    台帳を書き換える。_replace_dir と同じく、OneDrive が同期中の台帳をつかんでいると差し替えが
    「アクセス拒否」で一時的に弾かれるので、少し待ってやり直す（2026-10-20 に生成が 77 シャード目で止まった）。
    """
    temp = out_dir / "manifest.json.tmp"
    temp.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    for attempt in range(1, attempts + 1):
        try:
            temp.replace(_manifest_path(out_dir))
            return
        except PermissionError:
            if attempt == attempts:
                raise
            time.sleep(0.5 * attempt)


def generate(out_dir: Path, count: int, seed: int = 1, workers: Optional[int] = None,
             shard_size: int = DEFAULT_SHARD_SIZE, log=print) -> dict:
    """
    count 局分の配牌を作る。作り終えたシャードは飛ばすので、途中で止めても続きから再開できる。

    同じ out_dir で続きを作るときは、seed・shard_size・生成処理のコードが前回と同じでなければならない。
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    fingerprint = generator_fingerprint()
    manifest = _load_manifest(out_dir) or {
        "format_version": FORMAT_VERSION,
        "fingerprint": fingerprint,
        "generator_sources": list(GENERATOR_SOURCES),
        "seed": seed,
        "shard_size": shard_size,
        "num_players": NUM_PLAYERS,
        "shards": {},
    }
    if manifest["fingerprint"] != fingerprint:
        raise StalePoolError(
            f"{out_dir} は別の生成処理のコードで作られています。別のディレクトリに作り直してください"
        )
    if manifest["seed"] != seed or manifest["shard_size"] != shard_size:
        raise ValueError(
            f"{out_dir} は seed={manifest['seed']} shard_size={manifest['shard_size']} で作られています。"
            "続きを作るときは同じ値を指定してください"
        )

    shard_counts = [shard_size] * (count // shard_size)
    if count % shard_size:
        shard_counts.append(count % shard_size)

    # 書きかけの一時ディレクトリは捨てる。台帳に載っていないが中身が揃っているシャードは取り込む
    # （前回が途中で止まった場合。同じ種からは同じ配牌ができるので、作り直す必要がない）
    for temp_dir in out_dir.glob(".tmp_shard_*"):
        shutil.rmtree(temp_dir, ignore_errors=True)
    adopted = 0
    for index, n in enumerate(shard_counts):
        if manifest["shards"].get(str(index), {}).get("count") != n and _shard_count_on_disk(out_dir, index) == n:
            manifest["shards"][str(index)] = {"count": n, "seconds": None}
            adopted += 1
    if adopted:
        _write_manifest(out_dir, manifest)
        log(f"台帳に載っていなかった保存済みのシャードを {adopted} 個取り込みました")

    todo = [
        (index, n) for index, n in enumerate(shard_counts)
        if manifest["shards"].get(str(index), {}).get("count") != n
    ]
    log(f"配牌の生成: {count} 局（{len(shard_counts)} シャード、うち {len(todo)} シャードを作る）")
    if not todo:
        return manifest

    workers = workers or os.cpu_count() or 1
    started = time.perf_counter()
    done_deals = 0
    with ProcessPoolExecutor(max_workers=min(workers, len(todo))) as pool:
        futures = [
            pool.submit(_generate_shard, str(out_dir), index, n, shard_seed(seed, index))
            for index, n in todo
        ]
        try:
            for future in as_completed(futures):
                index, n, seconds = future.result()
                manifest["shards"][str(index)] = {"count": n, "seconds": round(seconds, 1)}
                _write_manifest(out_dir, manifest)  # シャードが1つできるたびに記録する（中断しても再開できる）
                done_deals += n
                elapsed = time.perf_counter() - started
                log(f"  shard_{index:05d}: {n} 局 {seconds:.0f} 秒 | 累計 {done_deals} 局 {elapsed:.0f} 秒")
        except BaseException:
            # まだ始まっていないシャードは取り消す（失敗した後も作り続けないように）
            pool.shutdown(wait=True, cancel_futures=True)
            raise

    log(f"完了: {done_deals} 局を {time.perf_counter() - started:.0f} 秒で作りました")
    return manifest


# ========== 読み込み ==========

@dataclass(frozen=True)
class _Shard:
    start: int
    arrays: dict


class DealPool:
    """事前に作った配牌。学習中はここから配牌を取り出す。"""

    def __init__(self, pool_dir: Path, check_fingerprint: bool = True):
        self.pool_dir = Path(pool_dir)
        manifest = _load_manifest(self.pool_dir)
        if manifest is None:
            raise FileNotFoundError(f"配牌がありません: {self.pool_dir}（先に generate を実行してください）")
        if manifest.get("format_version") != FORMAT_VERSION:
            raise StalePoolError(f"配牌の保存形式が違います: {manifest.get('format_version')}")
        if check_fingerprint and manifest["fingerprint"] != generator_fingerprint():
            raise StalePoolError(
                "配牌を作ったときと、いまの生成処理のコード（"
                + ", ".join(GENERATOR_SOURCES)
                + "）が違います。古い配牌で学習すると実戦とずれるので、作り直してください"
            )
        self.manifest = manifest

        self._shards: list[_Shard] = []
        start = 0
        for index in sorted(manifest["shards"], key=int):
            shard_dir = self.pool_dir / f"shard_{int(index):05d}"
            arrays = {name: np.load(shard_dir / f"{name}.npy", mmap_mode="r") for name in FIELDS}
            self._shards.append(_Shard(start, arrays))
            start += manifest["shards"][index]["count"]
        self._starts = [shard.start for shard in self._shards]
        self._size = start

    def __len__(self) -> int:
        return self._size

    def _locate(self, i: int) -> tuple[_Shard, int]:
        if not 0 <= i < self._size:
            raise IndexError(i)
        shard = self._shards[bisect.bisect_right(self._starts, i) - 1]
        return shard, i - shard.start

    def get(self, i: int) -> RoundDeal:
        """i 番目の配牌を、本番と同じ RoundDeal の形で返す。"""
        shard, j = self._locate(i)
        a = shard.arrays
        # tolist() で Python の int のリストにまとめて変換する（牌ごとに int() を呼ぶより速い）
        hands = list(zip(a["walls"][j].tolist(), a["examples"][j].tolist()))
        return RoundDeal(hands=hands, reserved_tiles=a["reserved"][j].tolist(), dora_id=int(a["dora"][j]))

    def sample(self, rng: random.Random) -> RoundDeal:
        return self.get(rng.randrange(self._size))

    def __iter__(self) -> Iterator[RoundDeal]:
        for i in range(self._size):
            yield self.get(i)


def fresh_deal() -> RoundDeal:
    """学習に使っていない新しい配牌（評価用）。本番と同じ生成処理でその場で作る。"""
    return generate_round_deal(NUM_PLAYERS)


# ========== 検査 ==========

def _dora_indicator_kind(dora: int) -> int:
    """ドラの牌種から、抜かれたドラ表示牌の牌種を逆算する（TileWall._initialize_wall の逆）。"""
    if dora >= 27:
        return 55 - dora  # 字牌は東 → 西 → 東 の順
    return (dora - 1) % 9 + (dora // 9) * 9


def check_deal(deal: RoundDeal) -> list[str]:
    """配牌が本番の生成処理の決まりを満たしているか。問題点の一覧を返す（空なら正常）。"""
    problems = []
    all_tiles = [t for wall, _ in deal.hands for t in wall] + list(deal.reserved_tiles)
    if len(all_tiles) != TileDefinitions.TOTAL_TILES - 1:
        problems.append(f"牌の総数が {len(all_tiles)} 枚")

    kinds = Counter(t & 0b11111 for t in all_tiles)
    indicator = _dora_indicator_kind(deal.dora_id)
    for kind in range(TileDefinitions.TOTAL_TILE_TYPES):
        expected = TileDefinitions.TILES_PER_TYPE - (1 if kind == indicator else 0)
        if kinds[kind] != expected:
            problems.append(f"牌種 {kind} が {kinds[kind]} 枚（{expected} 枚のはず）")
    if any(((t >> 5) & 1) != ((t & 0b11111) == deal.dora_id) for t in all_tiles):
        problems.append("ドラの印がドラの牌種と合わない")

    for p, (wall, example) in enumerate(deal.hands):
        if len(wall) != WALL_SIZE or len(example) != HAND_SIZE:
            problems.append(f"プレイヤー {p} の山 {len(wall)} 枚・聴牌例 {len(example)} 枚")
            continue
        if Counter(t & 0b11111 for t in example) - Counter(t & 0b11111 for t in wall):
            problems.append(f"プレイヤー {p} の聴牌例が山に無い牌を含む")
        elif not HandAnalyzer.is_tenpai(list(example), list(wall)):
            problems.append(f"プレイヤー {p} の聴牌例が聴牌していない")
    return problems


def verify(pool: DealPool, sample: int = 2000, seed: int = 0, log=print) -> int:
    """配牌を抜き出して検査する。問題のあった配牌の数を返す。"""
    rng = random.Random(seed)
    indexes = range(len(pool)) if sample <= 0 or sample >= len(pool) else rng.sample(range(len(pool)), sample)
    bad = 0
    for i in indexes:
        problems = check_deal(pool.get(i))
        if problems:
            bad += 1
            if bad <= 10:
                log(f"  {i} 番目: {'; '.join(problems)}")
    log(f"検査: {len(indexes)} 局中 {bad} 局に問題")
    return bad


# ========== コマンド ==========

def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="つよい の学習用に、配牌を前もって作る")
    sub = parser.add_subparsers(dest="command", required=True)

    gen = sub.add_parser("generate", help="配牌を作る（途中で止めても続きから再開できる）")
    gen.add_argument("--count", type=int, required=True, help="作る局数")
    gen.add_argument("--out", type=Path, required=True, help="保存先ディレクトリ")
    gen.add_argument("--seed", type=int, default=1)
    gen.add_argument("--workers", type=int, default=None, help="並列プロセス数（既定: CPU のコア数）")
    gen.add_argument("--shard-size", type=int, default=DEFAULT_SHARD_SIZE)

    ver = sub.add_parser("verify", help="配牌を抜き出して検査する")
    ver.add_argument("--pool", type=Path, required=True)
    ver.add_argument("--sample", type=int, default=2000, help="検査する局数（0 なら全部）")

    info = sub.add_parser("info", help="配牌の情報を出す")
    info.add_argument("--pool", type=Path, required=True)

    args = parser.parse_args(argv)
    if args.command == "generate":
        generate(args.out, args.count, seed=args.seed, workers=args.workers, shard_size=args.shard_size)
        return 0
    if args.command == "verify":
        return 1 if verify(DealPool(args.pool), sample=args.sample) else 0
    if args.command == "info":
        manifest = _load_manifest(args.pool)
        if manifest is None:
            print(f"配牌がありません: {args.pool}")
            return 1
        total = sum(s["count"] for s in manifest["shards"].values())
        fresh = manifest["fingerprint"] == generator_fingerprint()
        print(f"局数: {total}  シャード: {len(manifest['shards'])}  seed: {manifest['seed']}")
        print(f"生成処理のコード: {'いまと同じ' if fresh else '変わっている（作り直しが必要）'}")
        return 0
    return 1


if __name__ == "__main__":
    sys.exit(main())
