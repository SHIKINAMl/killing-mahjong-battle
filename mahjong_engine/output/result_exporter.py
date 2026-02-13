"""
ゲーム結果エクスポーター
"""
import json
import csv
from typing import Dict, List


class ResultExporter:
    """ゲーム結果をエクスポート"""

    def __init__(self):
        """エクスポーターを初期化"""
        self.results: Dict = {}

    def set_game_result(self, game_id: str, result: Dict):
        """
        ゲーム結果を設定

        Args:
            game_id: ゲームID
            result: ゲーム結果辞書
        """
        self.results[game_id] = result

    def export_json(self, filename: str):
        """
        結果をJSON形式でエクスポート

        Args:
            filename: 出力ファイル名
        """
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(self.results, f, ensure_ascii=False, indent=2)

    def export_csv(self, filename: str):
        """
        結果をCSV形式でエクスポート

        Args:
            filename: 出力ファイル名
        """
        if not self.results:
            return

        # 最初の結果からキーを取得
        first_result = next(iter(self.results.values()))
        fieldnames = ["game_id"] + list(first_result.keys())

        with open(filename, 'w', newline='', encoding='utf-8') as f:
            writer = csv.DictWriter(f, fieldnames=fieldnames)
            writer.writeheader()
            
            for game_id, result in self.results.items():
                row = {"game_id": game_id}
                row.update(result)
                writer.writerow(row)

    def get_results(self) -> Dict:
        """全結果を取得"""
        return self.results
