import unittest
from ..tile_converter import TileConverter

if __name__ == "__main__":
    # テストコード
    test_values = [0, 1, 2, 0b01000000, 0b10001001]
    tiles = TileConverter.array_to_tiles(test_values)
    print(tiles)  # ['1萬', '2萬', '3萬', '1萬(ドラ)', '1筒(ドラドラ)']

    binary_value = TileConverter.tile_to_binary("中", 1)
    print(f"0b{binary_value:08b}")