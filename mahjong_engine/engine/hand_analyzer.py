"""
手牌の聴牌判定と役計算
"""
from collections import Counter
from typing import List, Tuple, Generator
from itertools import combinations

from .yaku import Yaku


class HandAnalyzer:
    """手牌の分析・判定を行うクラス"""

    # ========== 聴牌判定 ==========

    @staticmethod
    def search_tenpai(wall: List[int]) -> List[list[int]]:
        """
        34枚の山牌から聴牌形を検索する

        聴牌形 = 3面子（9枚） + 残り4枚
        - 残り4枚：山牌の残り25枚から選ぶ

        Args:
            wall: 山牌のリスト（34枚を想定）

        Returns:
            聴牌形のリスト
        """
        base_tiles = [t & 0b11111 for t in wall]
        wall_counter = Counter(base_tiles)
        results: List[list[int]] = []

        # 34枚から順番に面子候補を抽出
        mentsu = list(HandAnalyzer._extract_mentsu(wall_counter, 0, 0))

        removed_wall_counter = wall_counter.copy()
        for pattarn in mentsu:
            for tile_id in pattarn:
                removed_wall_counter[tile_id] -= 1
            for rests in set(combinations(removed_wall_counter.elements(), 4)):

                comp_mentsu = pattarn + list(rests)

                if HandAnalyzer.is_tenpai(comp_mentsu, wall):
                    results.append(comp_mentsu)

        return results

    @staticmethod
    def _extract_mentsu(wall_counter: Counter, start_tile: int = 0, depth: int = 0) -> Generator[list[int], None, None]:
        """
        山牌から面子候補を抽出する

        Args:
            wall_counter: 山牌のカウンター
            start_tile: 探索を開始する牌ID
            depth: 再帰の深さ

        Returns:
            面子候補のリスト
        """
        if depth == 3:  # 面子が3つできたら終了
            yield []
            return

        for tile_id in sorted(wall_counter.keys()):
            if start_tile >= tile_id:
                continue

            temp_counter = wall_counter.copy()

            # 刻子
            if temp_counter[tile_id] >= 3:
                temp_counter[tile_id] -= 3
                for melds in HandAnalyzer._extract_mentsu(temp_counter, tile_id, depth + 1):
                    yield [tile_id] * 3 + melds

            # 順子
            elif HandAnalyzer._can_form_run(temp_counter, tile_id):
                temp_counter[tile_id] -= 1
                temp_counter[tile_id + 1] -= 1
                temp_counter[tile_id + 2] -= 1
                for melds in HandAnalyzer._extract_mentsu(temp_counter, tile_id, depth + 1):
                    yield [tile_id, tile_id + 1, tile_id + 2] + melds

    @staticmethod
    def is_tenpai(hand: List[int], wall: List[int]) -> bool:
        """
        聴牌判定

        Args:
            hand: 手牌のリスト（13枚を想定）
            wall: 山牌のリスト

        Returns:
            聴牌かどうか
        """
        skip_tiles = HandAnalyzer.skip_tenpai_tiles(wall)
        hand_counter = Counter(t & 0b11111 for t in hand)

        for tile_id in range(29):
            if tile_id in skip_tiles:
                continue
            sup_counter = hand_counter.copy()
            sup_counter[tile_id] += 1
            if HandAnalyzer._is_win(sup_counter):
                return True

        return False

    @staticmethod
    def get_tenpai_waiting_tiles(hand: List[int], wall: List[int]) -> List[int]:
        """
        待ち牌の検索

        Args:
            hand: 手牌のリスト（13枚を想定）
            wall: 山牌のリスト

        Returns:
            待ち牌のリスト
        """
        skip_tiles = HandAnalyzer.skip_tenpai_tiles(wall)
        hand_counter = Counter(t & 0b11111 for t in hand)
        waiting_tiles = []

        for tile_id in range(29):
            if tile_id in skip_tiles:
                continue

            sup_counter = hand_counter.copy()
            sup_counter[tile_id] += 1

            if HandAnalyzer._is_win(sup_counter):
                waiting_tiles.append(tile_id)
        return waiting_tiles

    @staticmethod
    def without_hand(hand: List[int], wall: List[int]) -> List[int]:
        """
        手牌を除外した山牌のリストを返す

        Args:
            hand: 手牌のリスト
            wall: 山牌のリスト

        Returns:
            手牌を除外した山牌のリスト
        """
        hand_counter = Counter(hand)
        wall_counter = Counter(wall)

        for tile_id, count in hand_counter.items():
            wall_counter[tile_id] -= count

        return list(wall_counter.elements())

    @staticmethod
    def skip_tenpai_tiles(wall: List[int]) -> List[int]:
        """
        聴牌判定で待ち牌から除外する牌IDのリストを返す

        Args:
            wall: 山牌のリスト

        Returns:
            待ち牌から除外する牌IDのリスト
        """
        wall_counter = Counter(t & 0b11111 for t in wall)
        return [tile_id for tile_id, count in wall_counter.items() if count >= 4]

    @staticmethod
    def is_win(hand: List[int]) -> bool:
        """
        和了形かどうかの判定

        Args:
            hand: 手牌のリスト（14枚を想定）

        Returns:
            和了形かどうか
        """
        counter = Counter(t & 0b11111 for t in hand)
        return HandAnalyzer._is_win(counter)

    @staticmethod
    def _is_win(counter: Counter) -> bool:
        """和了形かどうかの判定"""
        if HandAnalyzer._is_titoitsu(counter):
            return True

        for tile_id in HandAnalyzer._head_candidates(counter):
            temp_counter = counter.copy()
            temp_counter[tile_id] -= 2
            if any(len(melds) == 4 for melds in HandAnalyzer._generate_melds(temp_counter)):
                return True

        return False

    # ========== 役計算 ==========

    @staticmethod
    def filter_mangan_hands(hands: list[list[int]], wall: List[int], dora: int) -> List[list[int]]:
        """
        聴牌形の手牌リストのうち
        満貫以上となる上がりを持つものを返す

        Args:
            hands: 聴牌形の手牌リスト（13枚を想定）
            wall: 山牌のリスト
            dora: ドラの牌ID
        Returns:
            満貫以上の聴牌形の手牌リスト
        """

        aka_list = [t & 0b11111 for t in wall if (t >> 6) & 0b1 == 1]

        mangan_hands = []
        for hand in hands:

            waiting_tiles = HandAnalyzer.get_tenpai_waiting_tiles(hand, wall)

            hands = [hand + [tile_id] for tile_id in waiting_tiles]

            for h in hands:

                temp_aka_list = aka_list.copy()

                for t in h:
                    if t in temp_aka_list:
                        h[h.index(t)] = t | (1 << 6)
                        temp_aka_list.remove(t)

                    if t == dora:
                        h[h.index(t)] = t | (1 << 5)

                han = HandAnalyzer.calc_yaku(h)
                if han >= 4:
                    for t in hand:
                        if t in temp_aka_list:
                            hand[hand.index(t)] = t | (1 << 6)
                            temp_aka_list.remove(t)
                        if t == dora:
                            hand[hand.index(t)] = t | (1 << 5)

                    mangan_hands.append(hand)

        return mangan_hands

    @staticmethod
    def check_mangan(hand: List[int]) -> bool:
        """
        満貫以上かどうかの判定

        Args:
            hand: 手牌（14枚を想定）

        Returns:
            満貫（４翻）以上かどうか
        """
        return HandAnalyzer.calc_yaku(hand) >= 4

    @staticmethod
    def calc_yaku(hand: List[int]) -> int:
        """
        役の計算

        Args:
            hand: 手牌（14枚を想定）

        Returns:
            役の合計翻数
        """
        yaku = HandAnalyzer.enum_yaku(hand)
        return sum(Yaku.get_han_by_name(name) for name in yaku)


    @staticmethod
    def enum_yaku(hand: List[int]) -> List[str]:
        """
        役の列挙

        Args:
            hand: 手牌（14枚を想定）

        Returns:
            役名のリスト
        """
        base_tiles = [t & 0b11111 for t in hand]
        counter = Counter(base_tiles)

        # ドラと赤ドラの枚数
        dora_count = sum(1 for t in hand if (t >> 5) & 0b1 == 1)
        aka_count = sum(1 for t in hand if (t >> 6) & 0b1 == 1)
        dora_yaku = ["ドラ"] * dora_count + ["赤ドラ"] * aka_count
        dora_han = dora_count + aka_count

        best_yaku: List[str] = []
        best_han = 0

        # 七対子判定
        if HandAnalyzer._is_titoitsu(counter):
            yaku = ["七対子"]
            han = 2
            if HandAnalyzer._is_tanyao(counter):
                yaku.append("断么九")
                han += 1
            if HandAnalyzer._is_chinitsu(counter):
                yaku.append("清一色")
                han += 6
            elif HandAnalyzer._is_honitsu(counter):
                yaku.append("混一色")
                han += 3
            if HandAnalyzer._is_honroutou(counter):
                yaku.append("混老頭")
                han += 2

            yaku.append("立直")
            han += 1

            best_yaku = yaku + dora_yaku
            best_han = han + dora_han

        # 通常手（4面子1雀頭）の全分解を探索
        for head_tile in HandAnalyzer._head_candidates(counter):
            temp_counter = counter.copy()
            temp_counter[head_tile] -= 2

            for melds in HandAnalyzer._generate_melds(temp_counter):
                if len(melds) != 4:
                    continue

                yaku, han = HandAnalyzer._evaluate_melds(counter, melds, head_tile)
                yaku += dora_yaku
                han += dora_han

                if han > best_han:
                    best_han = han
                    best_yaku = yaku

        return best_yaku

    @staticmethod
    def _evaluate_melds(counter: Counter, melds: List[Tuple[str, int]], head: int) -> Tuple[List[str], int]:
        """面子と雀頭から役を判定する"""
        yaku: List[str] = []
        han = 0

        # 役満
        if HandAnalyzer._is_churen_poutou(counter):
            return ["九蓮宝燈"], 13
        elif HandAnalyzer._is_ryuuisou(counter):
            return ["緑一色"], 13
        elif HandAnalyzer._is_chinroutou(counter):
            return ["清老頭"], 13
        elif HandAnalyzer._is_suuankou(melds):
            return ["四暗刻"], 13

        if HandAnalyzer._is_tanyao(counter):
            yaku.append("断么九")
            han += 1
        if HandAnalyzer._is_pinfu(melds, head):
            yaku.append("平和")
            han += 1
        if HandAnalyzer._is_chinitsu(counter):
            yaku.append("清一色")
            han += 6
        elif HandAnalyzer._is_honitsu(counter):
            yaku.append("混一色")
            han += 3
        if HandAnalyzer._is_honroutou(counter):
            yaku.append("混老頭")
            han += 2

        if HandAnalyzer._is_junchan(melds, head):
            yaku.append("純全帯么九")
            han += 3
        elif HandAnalyzer._is_chanta(melds, head):
            yaku.append("混全帯么九")
            han += 2

        if HandAnalyzer._is_ikkitsuukan(melds):
            yaku.append("一気通貫")
            han += 2
        if HandAnalyzer._is_sanshoku_doujun(melds):
            yaku.append("三色同順")
            han += 2
        if HandAnalyzer._is_sanshoku_doukou(melds):
            yaku.append("三色同刻")
            han += 2
        if HandAnalyzer._is_sanankou(melds):
            yaku.append("三暗刻")
            han += 2

        if HandAnalyzer._is_ryanpeikou(melds):
            yaku.append("二盃口")
            han += 3
        elif HandAnalyzer._is_ipeikou(melds):
            yaku.append("一盃口")
            han += 1

        if HandAnalyzer._is_ton(melds):
            yaku.append("東")
            han += 1
        if HandAnalyzer._is_sha(melds):
            yaku.append("西")
            han += 1

        yaku.append("立直")
        han += 1

        return yaku, han

    # ========== 面子分解 ==========

    @staticmethod
    def _head_candidates(counter: Counter) -> List[int]:
        """対子候補の牌IDを返す"""
        return [tile_id for tile_id, count in counter.items() if count >= 2]

    @staticmethod
    def _generate_melds(counter: Counter) -> List[List[Tuple[str, int]]]:
        """面子分解を全探索して返す"""
        tile_id = None
        for tid in range(29):
            if counter[tid] > 0:
                tile_id = tid
                break

        if tile_id is None:
            return [[]]

        results: List[List[Tuple[str, int]]] = []

        # 刻子
        if counter[tile_id] >= 3:
            counter[tile_id] -= 3
            for rest in HandAnalyzer._generate_melds(counter):
                results.append([("triplet", tile_id)] + rest)
            counter[tile_id] += 3

        # 順子
        if HandAnalyzer._can_form_run(counter, tile_id):
            counter[tile_id] -= 1
            counter[tile_id + 1] -= 1
            counter[tile_id + 2] -= 1
            for rest in HandAnalyzer._generate_melds(counter):
                results.append([("run", tile_id)] + rest)
            counter[tile_id] += 1
            counter[tile_id + 1] += 1
            counter[tile_id + 2] += 1

        return results

    @staticmethod
    def _can_form_run(counter: Counter, tile_id: int) -> bool:
        """順子を作れるかどうかの判定"""
        if not HandAnalyzer._is_suited(tile_id):
            return False
        if tile_id % 9 >= 7:
            return False
        return counter[tile_id + 1] > 0 and counter[tile_id + 2] > 0

    # ========== 基本判定（牌単位） ==========

    @staticmethod
    def _is_suited(tile_id: int) -> bool:
        """数牌かどうかの判定"""
        return tile_id < 27

    @staticmethod
    def _is_honor(tile_id: int) -> bool:
        """字牌かどうかの判定"""
        return tile_id >= 27

    @staticmethod
    def _is_terminal(tile_id: int) -> bool:
        """么九牌かどうかの判定"""
        return tile_id % 9 == 0 or tile_id % 9 == 8

    @staticmethod
    def _tile_suit(tile_id: int) -> int:
        """牌の種類を返す（萬子=0、筒子=1、索子=2、字牌=3）"""
        return min(tile_id // 9, 3)

    # ========== 役判定（手全体） ==========

    @staticmethod
    def _is_titoitsu(counter: Counter) -> bool:
        """七対子の判定"""
        return all(count == 2 for count in counter.values())

    @staticmethod
    def _is_tanyao(counter: Counter) -> bool:
        """断么九かどうかの判定"""
        return all(not HandAnalyzer._is_terminal(tile_id) for tile_id in counter.keys())

    @staticmethod
    def _is_pinfu(melds: List[Tuple[str, int]], head: int) -> bool:
        """平和かどうかの判定"""
        if HandAnalyzer._is_honor(head):
            return False
        return all(kind == "run" for kind, _ in melds)

    @staticmethod
    def _is_honitsu(counter: Counter) -> bool:
        """混一色かどうかの判定"""
        suits = {HandAnalyzer._tile_suit(tile_id) for tile_id in counter.keys() if not HandAnalyzer._is_honor(tile_id)}
        return len(suits) == 1 and any(HandAnalyzer._is_honor(tile_id) for tile_id in counter.keys())

    @staticmethod
    def _is_chinitsu(counter: Counter) -> bool:
        """清一色かどうかの判定"""
        suits = {HandAnalyzer._tile_suit(tile_id) for tile_id in counter.keys() if not HandAnalyzer._is_honor(tile_id)}
        return len(suits) == 1 and all(not HandAnalyzer._is_honor(tile_id) for tile_id in counter.keys())

    @staticmethod
    def _is_honroutou(counter: Counter) -> bool:
        """混老頭かどうかの判定"""
        return all(HandAnalyzer._is_terminal(tile_id) or HandAnalyzer._is_honor(tile_id) for tile_id in counter.keys())

    @staticmethod
    def _is_chinroutou(counter: Counter) -> bool:
        """清老頭かどうかの判定"""
        all_tile = all(HandAnalyzer._is_terminal(tile_id) for tile_id in counter.keys())
        all_honor = all(not HandAnalyzer._is_honor(tile_id) for tile_id in counter.keys())
        return all_tile and all_honor


    @staticmethod
    def _is_chanta(melds: List[Tuple[str, int]], head: int) -> bool:
        """混全帯么九かどうかの判定"""
        if not HandAnalyzer._is_honor(head) and not HandAnalyzer._is_terminal(head):
            return False
        for kind, tile_id in melds:
            if kind == "run" and (tile_id % 9 != 0 and tile_id % 9 != 7):
                return False
            elif kind == "triplet" and not (HandAnalyzer._is_honor(tile_id) or HandAnalyzer._is_terminal(tile_id)):
                return False
        return True

    @staticmethod
    def _is_junchan(melds: List[Tuple[str, int]], head: int) -> bool:
        """純全帯么九かどうかの判定"""
        if not HandAnalyzer._is_terminal(head):
            return False
        for kind, tile_id in melds:
            if kind == "run" and (tile_id % 9 != 0 and tile_id % 9 != 7):
                return False
            elif kind == "triplet" and not HandAnalyzer._is_terminal(tile_id):
                return False
        return True

    @staticmethod
    def _is_ryuuisou(counter: Counter) -> bool:
        """緑一色かどうかの判定"""
        green_tiles = {19, 20, 21, 23, 25}
        return all(tile_id in green_tiles for tile_id in counter.keys())

    @staticmethod
    def _is_churen_poutou(counter: Counter) -> bool:
        """九蓮宝燈かどうかの判定"""
        for suit_base in (0, 9, 18):
            if all(counter[suit_base + i] >= (3 if i in (0, 8) else 1) for i in range(9)):
                return True
        return False

    # ========== 役判定（面子単位） ==========

    @staticmethod
    def _is_suuankou(melds: List[Tuple[str, int]]) -> bool:
        """四暗刻かどうかの判定"""
        return all(kind == "triplet" for kind, _ in melds)

    @staticmethod
    def _is_sanankou(melds: List[Tuple[str, int]]) -> bool:
        """三暗刻かどうかの判定"""
        return sum(1 for kind, _ in melds if kind == "triplet") == 3

    @staticmethod
    def _is_ikkitsuukan(melds: List[Tuple[str, int]]) -> bool:
        """一気通貫かどうかの判定"""
        run_starts = {start for kind, start in melds if kind == "run"}
        for suit_base in (0, 9, 18):
            if {suit_base, suit_base + 3, suit_base + 6}.issubset(run_starts):
                return True
        return False

    @staticmethod
    def _is_sanshoku_doujun(melds: List[Tuple[str, int]]) -> bool:
        """三色同順かどうかの判定"""
        run_starts = {start for kind, start in melds if kind == "run"}
        for base in range(7):
            if {base, base + 9, base + 18}.issubset(run_starts):
                return True
        return False

    @staticmethod
    def _is_sanshoku_doukou(melds: List[Tuple[str, int]]) -> bool:
        """三色同刻かどうかの判定"""
        triplet_tiles = {tile_id for kind, tile_id in melds if kind == "triplet"}
        for base in range(9):
            if {base, base + 9, base + 18}.issubset(triplet_tiles):
                return True
        return False

    @staticmethod
    def _is_ipeikou(melds: List[Tuple[str, int]]) -> bool:
        """一盃口かどうかの判定"""
        run_counts = Counter(start for kind, start in melds if kind == "run")
        return any(count >= 2 for count in run_counts.values())

    @staticmethod
    def _is_ryanpeikou(melds: List[Tuple[str, int]]) -> bool:
        """二盃口かどうかの判定"""
        run_counts = Counter(start for kind, start in melds if kind == "run")
        return sum(count >= 2 for count in run_counts.values()) >= 2

    @staticmethod
    def _is_ton(melds: List[Tuple[str, int]]) -> bool:
        """東の刻子かどうかの判定"""
        return any(kind == "triplet" and tile_id == 27 for kind, tile_id in melds)

    @staticmethod
    def _is_sha(melds: List[Tuple[str, int]]) -> bool:
        """西の刻子かどうかの判定"""
        return any(kind == "triplet" and tile_id == 28 for kind, tile_id in melds)