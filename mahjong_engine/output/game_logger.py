"""
ゲームログ管理
"""
import json
from datetime import datetime
from typing import List, Dict


class GameLogger:
    """ゲームのログを管理"""

    def __init__(self, log_file: str = None):
        """
        ログマネージャーを初期化

        Args:
            log_file: ログファイルパス
        """
        self.log_file = log_file
        self.logs: List[Dict] = []

    def log(self, event: str, data: Dict = None):
        """
        ログを記録

        Args:
            event: イベント名
            data: イベントデータ
        """
        log_entry = {
            "timestamp": datetime.now().isoformat(),
            "event": event,
            "data": data or {}
        }
        self.logs.append(log_entry)

    def save_to_file(self, filename: str = None):
        """
        ログをファイルに保存

        Args:
            filename: ファイル名（未指定時は初期化時のファイル名）
        """
        filepath = filename or self.log_file
        if filepath:
            with open(filepath, 'w', encoding='utf-8') as f:
                json.dump(self.logs, f, ensure_ascii=False, indent=2)

    def get_logs(self) -> List[Dict]:
        """全ログを取得"""
        return self.logs

    def clear(self):
        """ログをクリア"""
        self.logs.clear()
