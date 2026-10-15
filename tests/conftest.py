"""
テストの共通設定: リポジトリ直下を import できるようにする（mahjong_engine / training / opponent_bot）。
"""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
