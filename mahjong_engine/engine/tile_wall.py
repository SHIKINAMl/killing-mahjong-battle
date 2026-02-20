"""
麻雀牌の定義と牌山管理
"""
import random
from typing import List

from .hand_analyzer import HandAnalyzer


class TileDefinitions:
    """牌の定義"""

    # 牌の総数
    TOTAL_TILE_TYPES = 29  # 29種類の牌（萬子1-9、筒子1-9、索子1-9、字牌東西）
    TILES_PER_TYPE = 4
    TOTAL_TILES = TOTAL_TILE_TYPES * TILES_PER_TYPE  # 29種類 × 4枚 = 116枚


class TileWall:
    """牌山を管理するクラス"""

    def __init__(self):
        """牌山を初期化"""
        self.tiles: List[int] = []
        self.dora_id: int = None

        self._initialize_wall()
        self.shuffle()

    def _initialize_wall(self):
        """116枚の牌山を生成"""
        self.tiles = []

        # ドラをランダムに選択
        dora = random.randrange(0, TileDefinitions.TOTAL_TILE_TYPES)
        self.dora_id = dora

        # 各牌を4枚ずつ追加
        for tile_id in range(TileDefinitions.TOTAL_TILE_TYPES):
            for i in range(TileDefinitions.TILES_PER_TYPE):
                dora_flag = 1 if tile_id == dora else 0 # ドラの場合はフラグを立てる
                dora_flag += 1 << 1 if (tile_id == 4 or tile_id == 13 or tile_id == 22) and i == 0 else 0 # 赤ドラの場合はさらにフラグを立てる

                self.tiles.append(tile_id | (dora_flag << 5)) # ドラ情報を上位2ビットに格納

    def shuffle(self):
        """牌山をシャッフル"""
        random.shuffle(self.tiles)

    def deal(self, count: int = 34) -> tuple[List[int], List[int]]:
        """
        牌山から指定枚数を配る

        Args:
            count: 配る枚数 (デフォルト: 34)

        Returns:
            配った牌のリスト
            聴牌形の例
        """

        dealt_tiles = self.tiles[:count]
        rest_tiles = self.tiles[count:]

        hands = HandAnalyzer.serch_tenpai(dealt_tiles)
        sample = HandAnalyzer.filter_mangan_hands(hands, rest_tiles, self.dora_id)
        if not sample:
            return self.deal(count)

        self.tiles = rest_tiles

        return dealt_tiles, sample

    def reset(self):
        """牌山をリセット"""
        self._initialize_wall()