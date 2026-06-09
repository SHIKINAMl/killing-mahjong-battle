"""TileWall.deal の簡易テスト。

- 満貫以上の聴牌形探索を含む配牌処理の所要時間を計測
- 各プレイヤーの配牌 34 枚と聴牌例 13 枚を表示
"""

from __future__ import annotations

import argparse
import os
import sys
import time
from typing import Optional

CURRENT_DIR = os.path.dirname(os.path.abspath(__file__))
ENGINE_ROOT = os.path.join(CURRENT_DIR, "mahjong_engine")
if ENGINE_ROOT not in sys.path:
    sys.path.insert(0, ENGINE_ROOT)

from engine.tile_wall import TileWall


def _fmt_tiles(tiles: list[int]) -> str:
    return "[" + ", ".join(str(t) for t in tiles) + "]"


def run_once(players: int = 2) -> None:
    tile_wall = TileWall()
    tile_wall.reset()
    tile_wall.shuffle()

    print(f"[計算開始] {time.strftime('%Y-%m-%d %H:%M:%S')}")
    deal_start = time.perf_counter()
    dealt_results: list[tuple[list[int], list[int]]] = [tile_wall.deal(34) for _ in range(players)]
    deal_end = time.perf_counter()
    elapsed_ms = (deal_end - deal_start) * 1000.0

    print("=== 配牌時 聴牌形検索テスト ===")
    print(f"players: {players}")
    print(f"dora_id: {tile_wall.dora_id}")
    print(f"elapsed_ms(deal start -> deal completed): {elapsed_ms:.3f} ms")
    print()

    for i, (wall, tenpai_example) in enumerate(dealt_results, start=1):
        print(f"--- player {i} ---")
        print(f"wall_count: {len(wall)}")
        print(f"wall: {_fmt_tiles(wall)}")
        print(f"tenpai_example_count: {len(tenpai_example)}")
        print(f"tenpai_example: {_fmt_tiles(tenpai_example)}")
        print()


def main() -> None:
    parser = argparse.ArgumentParser(
        description="配牌時の聴牌形検索にかかる時間と配牌結果を出力します。"
    )
    parser.add_argument(
        "--players",
        type=int,
        default=2,
        help="配牌する人数（デフォルト: 2）",
    )
    args = parser.parse_args()

    if args.players <= 0:
        raise ValueError("--players は 1 以上の整数を指定してください。")

    run_once(players=args.players)


if __name__ == "__main__":
    main()
