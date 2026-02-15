"""
手牌の聴牌判定と翻数計算
"""
from typing import List, Set, Tuple
from collections import Counter


class HandAnalyzer:
    """手牌の分析・判定を行うクラス"""
    
    @staticmethod
    def is_tenpai(hand: List[int]) -> bool:
        """
        聴牌判定（簡易版）
        
        Args:
            hand: 手牌のリスト
            
        Returns:
            聴牌かどうか
        """
        # 全ての牌を試して、1枚加えて和了形になるか確認
        for tile_id in range(34):
            test_hand = hand + [tile_id]
            if HandAnalyzer._is_winning_hand(test_hand):
                return True
        return False
    
    @staticmethod
    def _is_winning_hand(hand: List[int]) -> bool:
        """
        和了形判定（簡易版）
        
        Args:
            hand: 手牌のリスト（14枚想定）
            
        Returns:
            和了形かどうか
        """
        if len(hand) != 14:
            return False
        
        counter = Counter(hand)
        
        # 七対子チェック
        if HandAnalyzer._is_seven_pairs(counter):
            return True
        
        # 国士無双チェック
        if HandAnalyzer._is_kokushi(counter):
            return True
        
        # 通常の和了形チェック（雀頭1つ+面子4つ）
        for tile_id, count in counter.items():
            if count >= 2:
                # この牌を雀頭とする
                temp_counter = counter.copy()
                temp_counter[tile_id] -= 2
                if temp_counter[tile_id] == 0:
                    del temp_counter[tile_id]
                
                if HandAnalyzer._check_mentsu(temp_counter):
                    return True
        
        return False
    
    @staticmethod
    def _is_seven_pairs(counter: Counter) -> bool:
        """七対子判定"""
        return len(counter) == 7 and all(count == 2 for count in counter.values())
    
    @staticmethod
    def _is_kokushi(counter: Counter) -> bool:
        """国士無双判定"""
        yaochu = [0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33]  # 么九牌
        tiles = list(counter.keys())
        
        # 13種類の么九牌が全て含まれているか
        if not all(tile in tiles for tile in yaochu):
            return False
        
        # そのうち1種類が2枚、他が1枚ずつ
        counts = list(counter.values())
        return sorted(counts) == [1] * 12 + [2]
    
    @staticmethod
    def _check_mentsu(counter: Counter) -> bool:
        """
        残りの牌が面子（刻子または順子）で構成できるか判定
        
        Args:
            counter: 牌のカウンター（雀頭を除いた状態）
            
        Returns:
            面子で構成できるかどうか
        """
        if not counter:
            return True
        
        # 最小の牌IDを取得
        tile_id = min(counter.keys())
        count = counter[tile_id]
        
        # 刻子を試す
        if count >= 3:
            temp_counter = counter.copy()
            temp_counter[tile_id] -= 3
            if temp_counter[tile_id] == 0:
                del temp_counter[tile_id]
            if HandAnalyzer._check_mentsu(temp_counter):
                return True
        
        # 順子を試す（数牌のみ）
        if tile_id <= 24:  # 数牌（萬子、筒子、索子）
            suit_base = (tile_id // 9) * 9
            tile_num = tile_id % 9
            
            # 順子が作れるか（例: 1-2-3）
            if tile_num <= 6:  # 7以下の数字なら順子の起点になれる
                next1 = tile_id + 1
                next2 = tile_id + 2
                
                if next1 in counter and next2 in counter:
                    temp_counter = counter.copy()
                    temp_counter[tile_id] -= 1
                    temp_counter[next1] -= 1
                    temp_counter[next2] -= 1
                    
                    if temp_counter[tile_id] == 0:
                        del temp_counter[tile_id]
                    if temp_counter[next1] == 0:
                        del temp_counter[next1]
                    if temp_counter[next2] == 0:
                        del temp_counter[next2]
                    
                    if HandAnalyzer._check_mentsu(temp_counter):
                        return True
        
        return False
    
    @staticmethod
    def calculate_han(hand: List[int], winning_tile: int) -> int:
        """
        翻数を計算（簡易版）
        
        Args:
            hand: 手牌のリスト
            winning_tile: 和了牌
            
        Returns:
            翻数
        """
        counter = Counter(hand)
        han = 0
        
        # 七対子: 2翻
        if HandAnalyzer._is_seven_pairs(counter):
            han += 2
        
        # 国士無双: 役満（13翻相当）
        if HandAnalyzer._is_kokushi(counter):
            han += 13
        
        # 対々和: 2翻（全て刻子）
        if HandAnalyzer._is_toitoi(counter):
            han += 2
        
        # 混一色: 3翻
        if HandAnalyzer._is_honitsu(counter):
            han += 3
        
        # 清一色: 6翻
        if HandAnalyzer._is_chinitsu(counter):
            han += 6
        
        # 断么九: 1翻
        if HandAnalyzer._is_tanyao(counter):
            han += 1
        
        # 平和: 1翻（簡易判定）
        if han == 0 and HandAnalyzer._is_pinfu(counter):
            han += 1
        
        # 最低でも1翻
        return max(han, 1)
    
    @staticmethod
    def _is_toitoi(counter: Counter) -> bool:
        """対々和判定（全て刻子+雀頭）"""
        # 雀頭を除いた後、全て3枚ずつになっているか
        for tile_id, count in counter.items():
            if count == 2:
                temp_counter = counter.copy()
                temp_counter[tile_id] -= 2
                if temp_counter[tile_id] == 0:
                    del temp_counter[tile_id]
                
                # 残りが全て3の倍数か
                return all(c % 3 == 0 for c in temp_counter.values())
        return False
    
    @staticmethod
    def _is_honitsu(counter: Counter) -> bool:
        """混一色判定（1種類の数牌+字牌）"""
        man = any(0 <= tile <= 8 for tile in counter.keys())
        pin = any(9 <= tile <= 17 for tile in counter.keys())
        sou = any(18 <= tile <= 26 for tile in counter.keys())
        jihai = any(27 <= tile <= 33 for tile in counter.keys())
        
        suit_count = sum([man, pin, sou])
        return suit_count == 1 and jihai
    
    @staticmethod
    def _is_chinitsu(counter: Counter) -> bool:
        """清一色判定（1種類の数牌のみ）"""
        man = any(0 <= tile <= 8 for tile in counter.keys())
        pin = any(9 <= tile <= 17 for tile in counter.keys())
        sou = any(18 <= tile <= 26 for tile in counter.keys())
        jihai = any(27 <= tile <= 33 for tile in counter.keys())
        
        suit_count = sum([man, pin, sou])
        return suit_count == 1 and not jihai
    
    @staticmethod
    def _is_tanyao(counter: Counter) -> bool:
        """断么九判定（么九牌を含まない）"""
        yaochu = {0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33}
        return not any(tile in yaochu for tile in counter.keys())
    
    @staticmethod
    def _is_pinfu(counter: Counter) -> bool:
        """平和判定（簡易版）"""
        # 字牌がなく、全て順子で構成されているか
        if any(27 <= tile <= 33 for tile in counter.keys()):
            return False
        # より詳細な判定は省略
        return True
    
    @staticmethod
    def get_tenpai_tiles(hand: List[int]) -> List[int]:
        """
        聴牌時の待ち牌を取得
        
        Args:
            hand: 手牌のリスト
            
        Returns:
            待ち牌のリスト
        """
        waiting_tiles = []
        for tile_id in range(34):
            test_hand = hand + [tile_id]
            if HandAnalyzer._is_winning_hand(test_hand):
                waiting_tiles.append(tile_id)
        return waiting_tiles
    
    @staticmethod
    def has_four_han_tenpai(hand: List[int]) -> bool:
        """
        4翻以上の聴牌形があるか判定
        
        Args:
            hand: 手牌のリスト（13枚）
            
        Returns:
            4翻以上の聴牌があるか
        """
        if len(hand) != 13:
            return False
        
        waiting_tiles = HandAnalyzer.get_tenpai_tiles(hand)
        
        for waiting_tile in waiting_tiles:
            test_hand = hand + [waiting_tile]
            han = HandAnalyzer.calculate_han(test_hand, waiting_tile)
            if han >= 4:
                return True
        
        return False
