"""
手牌の聴牌判定と役計算
"""
from collections import Counter
from typing import List, Tuple


class HandAnalyzer:
    """手牌の分析・判定を行うクラス"""

    # ========== 聴牌判定 ==========

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
        wall_counter = Counter(wall)
        skip_tiles = {tile_id for tile_id, count in wall_counter.items() if count >= 4}
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
    def _is_win(counter: Counter) -> bool:
        """和了形かどうかの判定"""
        if HandAnalyzer._is_seven_pairs(counter):
            return True

        for tile_id in HandAnalyzer._head_candidates(counter):
            temp_counter = counter.copy()
            temp_counter[tile_id] -= 2
            if any(len(melds) == 4 for melds in HandAnalyzer._generate_melds(temp_counter)):
                return True

        return False

    # ========== 役計算 ==========

    @staticmethod
    def calc_yaku(hand: List[int]) -> List[str]:
        """
        役計算

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
        dora_han = dora_count + aka_count

        best_yaku: List[str] = []
        best_han = 0

        # 七対子判定
        if HandAnalyzer._is_seven_pairs(counter):
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

            best_yaku = yaku
            best_han = han

        # 通常手（4面子1雀頭）の全分解を探索
        for head_tile in HandAnalyzer._head_candidates(counter):
            temp_counter = counter.copy()
            temp_counter[head_tile] -= 2

            for melds in HandAnalyzer._generate_melds(temp_counter):
                if len(melds) != 4:
                    continue

                yaku, han = HandAnalyzer._evaluate_melds(counter, melds, head_tile)

                if han > best_han:
                    best_han = han
                    best_yaku = yaku

        return best_yaku

    @staticmethod
    def _evaluate_melds(
        counter: Counter, melds: List[Tuple[str, int]], head: int
    ) -> Tuple[List[str], int]:
        """面子と雀頭から役を判定する"""
        yaku: List[str] = []
        han = 0

        # 役満
        if HandAnalyzer._is_churen_poutou(counter):
            return ["九蓮宝燈"]
        elif HandAnalyzer._is_ryuuisou(counter):
            return ["緑一色"]
        elif HandAnalyzer._is_chinroutou(counter):
            return ["清老頭"]
        elif HandAnalyzer._is_suuankou(melds):
            return ["四暗刻"]

        # 基本役：色系
        if HandAnalyzer._is_chinitsu(counter):
            yaku.append("清一色")
            han += 6
        elif HandAnalyzer._is_honitsu(counter):
            yaku.append("混一色")
            han += 3

        # 基本役：数系
        if HandAnalyzer._is_tanyao(counter):
            yaku.append("断么九")
            han += 1
        if HandAnalyzer._is_honroutou(counter):
            yaku.append("混老頭")
            han += 2

        # 面子系
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

        # 帯幺九系
        if HandAnalyzer._is_junchan(melds, head):
            yaku.append("純全帯么九")
            han += 3
        elif HandAnalyzer._is_chanta(melds, head):
            yaku.append("混全帯么九")
            han += 2

        # ペアシステム
        if HandAnalyzer._is_ryanpeikou(melds):
            yaku.append("二盃口")
            han += 3
        elif HandAnalyzer._is_ipeikou(melds):
            yaku.append("一盃口")
            han += 1

        # 翻牌（役牌）
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
    def _is_seven_pairs(counter: Counter) -> bool:
        """七対子の判定"""
        return all(count == 2 for count in counter.values())

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
        if tile_id < 9:
            return 0
        elif tile_id < 18:
            return 1
        elif tile_id < 27:
            return 2
        else:
            return 3

    # ========== 役判定（手全体） ==========

    @staticmethod
    def _is_tanyao(counter: Counter) -> bool:
        """断么九かどうかの判定"""
        return all(not HandAnalyzer._is_terminal(tile_id) for tile_id in counter.keys())

    @staticmethod
    def _is_chinitsu(counter: Counter) -> bool:
        """清一色かどうかの判定"""
        suits = {HandAnalyzer._tile_suit(tile_id) for tile_id in counter.keys() if not HandAnalyzer._is_honor(tile_id)}
        return len(suits) == 1 and all(not HandAnalyzer._is_honor(tile_id) for tile_id in counter.keys())

    @staticmethod
    def _is_honitsu(counter: Counter) -> bool:
        """混一色かどうかの判定"""
        suits = {HandAnalyzer._tile_suit(tile_id) for tile_id in counter.keys() if not HandAnalyzer._is_honor(tile_id)}
        return len(suits) == 1 and any(HandAnalyzer._is_honor(tile_id) for tile_id in counter.keys())

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
        for base in range(0, 7):
            if {base, base + 9, base + 18}.issubset(run_starts):
                return True
        return False

    @staticmethod
    def _is_sanshoku_doukou(melds: List[Tuple[str, int]]) -> bool:
        """三色同刻かどうかの判定"""
        triplet_tiles = {tile_id for kind, tile_id in melds if kind == "triplet"}
        for base in range(0, 9):
            if {base, base + 9, base + 18}.issubset(triplet_tiles):
                return True
        return False

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
