"""
通信メッセージハンドラー
"""
import json
from typing import Dict, Callable, Optional


class MessageHandler:
    """WebSocket メッセージハンドラー"""

    def __init__(self):
        """メッセージハンドラーを初期化"""
        self.handlers: Dict[str, Callable] = {}
        self._register_default_handlers()

    def _register_default_handlers(self):
        """デフォルトメッセージハンドラーを登録"""
        self.register("action", self._handle_action)
        self.register("status", self._handle_status)

    def register(self, message_type: str, handler: Callable):
        """
        メッセージハンドラーを登録

        Args:
            message_type: メッセージタイプ
            handler: ハンドラー関数
        """
        self.handlers[message_type] = handler

    async def handle_message(self, message: str) -> dict:
        """
        メッセージを処理

        Args:
            message: JSON文字列のメッセージ

        Returns:
            レスポンス辞書
        """
        try:
            data = json.loads(message)
            message_type = data.get("type", "unknown")

            if message_type in self.handlers:
                return await self.handlers[message_type](data)
            else:
                return {"error": f"Unknown message type: {message_type}"}
        except json.JSONDecodeError:
            return {"error": "Invalid JSON"}

    async def _handle_action(self, data: dict) -> dict:
        """アクションメッセージを処理"""
        # 実装は未定
        return {"status": "action received"}

    async def _handle_status(self, data: dict) -> dict:
        """ステータスメッセージを処理"""
        # 実装は未定
        return {"status": "status received"}
