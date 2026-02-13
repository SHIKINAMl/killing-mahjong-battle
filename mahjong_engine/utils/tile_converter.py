"""
麻雀牌の2進数表現と文字列変換クラス
牌の2進数表現（6ビット）：
- 萬子 1-9: 0-8 (0b000000 - 0b001000)
- 筒子 1-9: 9-17 (0b001001 - 0b010001)
- 索子 1-9: 18-26 (0b010010 - 0b011010)
- 字牌 東南西北白発中: 27-33 (0b011011 - 0b100001)

ドラ情報（2ビット）：
- 00: ドラでない
- 01: ドラ
- 10: ドラ + 赤ドラ

合計8ビットで表現
"""


class TileConverter:
    """麻雀牌の変換クラス"""

    # 牌のマッピング（0-33）
    TILE_NAMES = [
        # 萬子 (0-8)
        "1萬", "2萬", "3萬", "4萬", "5萬", "6萬", "7萬", "8萬", "9萬",
        # 筒子 (9-17)
        "1筒", "2筒", "3筒", "4筒", "5筒", "6筒", "7筒", "8筒", "9筒",
        # 索子 (18-26)
        "1索", "2索", "3索", "4索", "5索", "6索", "7索", "8索", "9索",
        # 字牌 (27-33)
        "東", "南", "西", "北", "白", "発", "中"
    ]

    DORA_NAMES = ["", "ドラ", "ドラドラ"]

    @staticmethod
    def binary_to_tile(value: int) -> str:
        """
        2進数値を麻雀牌の文字列に変換

        Args:
            value: 8ビットの整数値（下位6ビット=牌、上位2ビット=ドラ情報）

        Returns:
            麻雀牌の文字列表現

        Examples:
            >>> TileConverter.binary_to_tile(0b00000000)  # 1萬
            '1萬'
            >>> TileConverter.binary_to_tile(0b01000000)  # 1萬(ドラ)
            '1萬(ドラ)'
            >>> TileConverter.binary_to_tile(0b00001001)  # 1筒
            '1筒'
        """

        # 下位6ビットから牌の種類を取得
        tile_id = value & 0b00111111
        # 上位2ビットからドラ情報を取得
        dora_type = (value >> 6) & 0b00000011

        if tile_id >= len(TileConverter.TILE_NAMES):
            raise ValueError(f"無効な牌ID: {tile_id}")

        tile_name = TileConverter.TILE_NAMES[tile_id]
        dora_name = TileConverter.DORA_NAMES[dora_type]

        if dora_name:
            return f"{tile_name}({dora_name})"
        return tile_name

    @staticmethod
    def array_to_tiles(values: list[int]) -> list[str]:
        """
        数値配列を麻雀牌の文字列配列に変換

        Args:
            values: 8ビット整数のリスト

        Returns:
            麻雀牌の文字列リスト

        Examples:
            >>> TileConverter.array_to_tiles([0, 1, 2])
            ['1萬', '2萬', '3萬']
            >>> TileConverter.array_to_tiles([0b01000000, 0b10001001])
            ['1萬(ドラ)', '1筒(ドラドラ)']
        """

        return [TileConverter.binary_to_tile(v) for v in values]

    @staticmethod
    def tile_to_binary(tile_name: str, dora_type: int = 0) -> int:
        """
        麻雀牌の文字列を2進数値に変換

        Args:
            tile_name: 牌の名前（例: "1萬", "東"）
            dora_type: ドラ種別 (0-2)

        Returns:
            8ビットの整数値

        Examples:
            >>> TileConverter.tile_to_binary("1萬")
            0
            >>> TileConverter.tile_to_binary("1萬", 1)
            64
        """
        try:
            tile_id = TileConverter.TILE_NAMES.index(tile_name)
        except ValueError:
            raise ValueError(f"無効な牌名: {tile_name}")

        if not 0 <= dora_type <= 3:
            raise ValueError(f"無効なドラ種別: {dora_type}")

        return tile_id | (dora_type << 6)

    @staticmethod
    def format_hand(values: list[int]) -> str:
        """
        手牌を見やすい形式で表示

        未実装
        """
