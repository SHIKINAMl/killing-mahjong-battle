"""
WebSocket API サーバー（2人対戦用）
"""

import asyncio
import json
from typing import Callable, Optional
from ..engine.game_engine import GameEngine


class WebSocketGameServer:
    """WebSocket ベースのゲーム API サーバー（2人対戦用）"""

    # 2人対戦用の定義
    PLAYER_1 = 0
    PLAYER_2 = 1
    NUM_PLAYERS = 2

    def __init__(self, engine: GameEngine):
        """
        サーバーを初期化（2人対戦）

        Args:
            engine: GameEngine インスタンス
        """
        self.engine = engine
        # 2つのプレイヤーごとにWebSocketコネクションを管理
        self.clients = {
            self.PLAYER_1: None,  # クライアント1
            self.PLAYER_2: None,  # クライアント2
        }
        self.ready_players = set()  # 準備完了したプレイヤー

    async def register_client(self, player_id: int, websocket):
        """
        クライアントを登録（player_id: 0 or 1）

        Args:
            player_id: プレイヤーID (0 or 1)
            websocket: WebSocket コネクション
        """
        if player_id not in [self.PLAYER_1, self.PLAYER_2]:
            raise ValueError(f"無効なプレイヤーID: {player_id}")

        self.clients[player_id] = websocket
        await self._send_to_player(player_id, {
            "type": "registered",
            "player_id": player_id,
            "message": f"プレイヤー{player_id + 1}として登録されました"
        })

        # 両方のプレイヤーが接続したら通知
        if self._all_players_connected():
            await self._notify_all_players({
                "type": "ready_to_start",
                "message": "両プレイヤーが接続しました。ゲーム開始待機中..."
            })

    async def unregister_client(self, player_id: int):
        """クライアントを登録解除"""
        if player_id in self.clients:
            self.clients[player_id] = None
            self.ready_players.discard(player_id)

    def _all_players_connected(self) -> bool:
        """両プレイヤーが接続しているか確認"""
        return all(ws is not None for ws in self.clients.values())

    def _get_opponent_id(self, player_id: int) -> int:
        """対戦相手のプレイヤーIDを取得"""
        return self.PLAYER_2 if player_id == self.PLAYER_1 else self.PLAYER_1

    async def _send_to_player(self, player_id: int, message: dict):
        """特定のプレイヤーにメッセージを送信"""
        if self.clients[player_id] is not None:
            try:
                # 実装時: await self.clients[player_id].send(json.dumps(message))
                print(f"[Player {player_id}] <- {json.dumps(message, ensure_ascii=False)}")
            except Exception as e:
                print(f"[Error] プレイヤー{player_id}への送信失敗: {e}")

    async def _notify_all_players(self, message: dict):
        """全プレイヤーにメッセージをブロードキャスト"""
        for player_id in [self.PLAYER_1, self.PLAYER_2]:
            await self._send_to_player(player_id, message)

    async def broadcast_state(self):
        """全クライアントにゲーム状態をブロードキャスト"""
        if not self._all_players_connected():
            return

        state = self.engine.get_game_state()
        
        # 各プレイヤーに自分の情報を含めたメッセージを送信
        for player_id in [self.PLAYER_1, self.PLAYER_2]:
            opponent_id = self._get_opponent_id(player_id)
            message = {
                "type": "state_update",
                "game_status": state["status"],
                "round": state["round"],
                "current_player": state["current_player"],
                "you": {
                    "player_id": player_id,
                    "health": state["players"][player_id].get("health", 0),
                    "hand": state["players"][player_id].get("hand", []),
                    "discards": state["players"][player_id].get("discards", [])
                },
                "opponent": {
                    "player_id": opponent_id,
                    "health": state["players"][opponent_id].get("health", 0),
                    "discard_count": len(state["players"][opponent_id].get("discards", [])),
                    "hand_count": len(state["players"][opponent_id].get("hand", []))
                }
            }
            await self._send_to_player(player_id, message)

    async def handle_player_action(self, player_id: int, action: dict):
        """
        プレイヤーのアクションを処理

        Args:
            player_id: プレイヤーID (0 or 1)
            action: アクション辞書 (例: {"action": "discard", "tile": 0})
        """
        if player_id not in [self.PLAYER_1, self.PLAYER_2]:
            await self._send_to_player(player_id, {
                "type": "error",
                "message": "無効なプレイヤーID"
            })
            return

        # アクションの検証（未実装）
        opponent_id = self._get_opponent_id(player_id)
        
        # プレイヤーの操作を対戦相手にも通知
        await self._send_to_player(opponent_id, {
            "type": "opponent_action",
            "player_id": player_id,
            "action": action
        })

        # ゲームエンジンにアクションを通知（中身は未実装）
        # await self.engine.process_action(player_id, action)

    async def request_player_action(self, player_id: int, valid_actions: list):
        """
        プレイヤーにアクションを要求

        Args:
            player_id: 行動するプレイヤーID
            valid_actions: 有効なアクションのリスト
        """
        await self._send_to_player(player_id, {
            "type": "action_required",
            "valid_actions": valid_actions
        })

    async def start_server(self, host: str = "localhost", port: int = 8000):
        """
        WebSocket サーバーを開始（2人対戦用）

        Args:
            host: バインドするホスト
            port: バインドするポート
        """
        print(f"WebSocket server for 1v1 battle starting on {host}:{port}")
        print("対人戦用サーバー：2つのクライアント接続を待機中...")
        # 実装時は websockets ライブラリ等を使用して実装
