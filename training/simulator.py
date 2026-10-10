"""
対局シミュレーター（WebSocket・セッションを通さず、エンジンを直接動かす）

つよい の学習環境の土台であり、スキルの効果の測定などの実験にも使う。
進行は GameSession と同じ順番で行う:
  手牌選択（スキル → 手牌確定）→ 掛け金 → 打牌（ロン確認）→ 局の終わり → 次局
スキルの実行・開始時の役強化・特典の反映は、GameSession と同じエンジンのメソッドを使う。

打ち手（Agent）は差し替えられる。いまは RuleAgent（よわい／ふつう の考え方）だけ。
学習したモデルも、同じメソッドを持たせれば差し込める。
"""
import random
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Optional, Protocol

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from mahjong_engine.ai.cpu_config import HUMAN_PERKS, load_cpu_config  # noqa: E402
from mahjong_engine.ai.observation import CpuObservation, PublicInfo, build_observation  # noqa: E402
from mahjong_engine.ai.rule_brain import HandPlan, RuleBrain, fallback_bet, fallback_hand_indexes  # noqa: E402
from mahjong_engine.engine.game_engine import GameEngine  # noqa: E402
from mahjong_engine.engine.game_state import RoundStatus, SkillType  # noqa: E402
from mahjong_engine.engine.tile_wall import RoundDeal  # noqa: E402

PLAYER_IDS = ("P0", "P1")
# 1 局の打牌の上限（両者の打牌 + 余裕）。超えたら不具合として止める
MAX_DISCARDS_PER_ROUND = 64
MAX_SKILLS_PER_ROUND = 6


class Agent(Protocol):
    """打ち手。入力は視界（CpuObservation）だけ。"""

    def plan_hand(self, obs: CpuObservation) -> Optional[HandPlan]: ...
    def replan(self, obs: CpuObservation, plan: Optional[HandPlan], skill: str) -> Optional[HandPlan]: ...
    def choose_skill(self, obs: CpuObservation, plan: Optional[HandPlan]) -> Optional[dict]: ...
    def choose_hand(self, obs: CpuObservation, plan: Optional[HandPlan]) -> list[int]: ...
    def choose_bet(self, obs: CpuObservation, plan: Optional[HandPlan]) -> int: ...
    def choose_discard(self, obs: CpuObservation) -> int: ...


class RuleAgent:
    """よわい／ふつう の考え方（本番の CPU と同じ RuleBrain）。"""

    def __init__(self, brain: RuleBrain, use_skills: bool = True, fixed_bet: Optional[int] = None):
        self.brain = brain
        self.use_skills = use_skills
        self.fixed_bet = fixed_bet

    def plan_hand(self, obs):
        return self.brain.plan_hand(obs)

    def replan(self, obs, plan, skill):
        return self.brain.replan(obs, plan, skill)

    def choose_skill(self, obs, plan):
        return self.brain.next_skill(obs, plan) if self.use_skills else None

    def choose_hand(self, obs, plan):
        return list(plan.hand_indexes) if plan is not None else fallback_hand_indexes(obs)

    def choose_bet(self, obs, plan):
        return self.fixed_bet if self.fixed_bet is not None else self.brain.choose_bet(obs, plan)

    def choose_discard(self, obs):
        return self.brain.choose_discard(obs)


def make_rule_agent(level: str, seed: int, hand_selection: Optional[dict] = None, **kwargs) -> RuleAgent:
    """設定ファイルの値で RuleAgent を作る。hand_selection で手牌の候補探索の設定を上書きできる（学習・テストで軽くする）。"""
    config = load_cpu_config()[level]
    if hand_selection:
        config["hand_selection"].update(hand_selection)
    return RuleAgent(RuleBrain(level, config, random.Random(seed)), **kwargs)


@dataclass
class RoundRecord:
    round_number: int
    is_draw: bool
    winner: Optional[str]
    health_before: dict[str, int]  # 手牌選択の前（スキルを使う前）
    health_after: dict[str, int]
    bets: dict[str, int]
    skills: dict[str, list[str]]
    liquidation: Optional[dict] = None


@dataclass
class GameRecord:
    rounds: list[RoundRecord] = field(default_factory=list)
    end_reason: Optional[str] = None
    final_health: dict[str, int] = field(default_factory=dict)

    @property
    def winner(self) -> Optional[str]:
        """HP の多い方。"""
        if not self.final_health:
            return None
        a, b = PLAYER_IDS
        if self.final_health[a] == self.final_health[b]:
            return None
        return a if self.final_health[a] > self.final_health[b] else b


class SimulationError(RuntimeError):
    pass


class MatchSimulator:
    """1 対局を、エンジンを直接呼んで進める。"""

    def __init__(
        self,
        agents: list[Agent],
        deal_factory: Optional[Callable[[int], RoundDeal]] = None,
        perks: Optional[list[dict]] = None,
        opening_boosts: bool = True,
        max_rounds: int = 1000,
        rng: Optional[random.Random] = None,
    ):
        if len(agents) != 2:
            raise ValueError("打ち手は 2 人")
        self.agents = dict(zip(PLAYER_IDS, agents))
        self.engine = GameEngine(max_rounds=max_rounds, deal_factory=deal_factory)
        self.engine.initialize_players(list(PLAYER_IDS))
        self.public = {pid: PublicInfo() for pid in PLAYER_IDS}

        for pid, seat_perks in zip(PLAYER_IDS, perks or [HUMAN_PERKS, HUMAN_PERKS]):
            # 特典の項目名は PlayerState のフィールド名と同じ（GameSession と同じ反映方法）
            self.engine.update_player_state(pid, **seat_perks)
            if not opening_boosts:
                self.engine.update_player_state(pid, opening_boost_count=0)

        # 開始時の役強化は、相手にとっては公開情報（opening_boost_assigned で知らされる）
        for boost in self.engine.assign_random_opening_boosts(rng or random.Random()):
            known = self.public[self._other(boost["client_id"])].opponent_opening_boosts
            known[boost["yaku_name"]] = known.get(boost["yaku_name"], 0) + boost["bonus_han"]

        self.engine.start_game(max_rounds)  # 1 局目の配牌まで進む（手牌選択フェーズ）

    @property
    def ended(self) -> bool:
        return self.engine.game_end_reason is not None

    @staticmethod
    def _other(pid: str) -> str:
        return PLAYER_IDS[1] if pid == PLAYER_IDS[0] else PLAYER_IDS[0]

    def _obs(self, pid: str) -> CpuObservation:
        return build_observation(self.engine, pid, self.public[pid])

    # ========== 1 局 ==========

    def play_round(
        self,
        before_discard_phase: Optional[Callable[[], None]] = None,
        advance: bool = True,
    ) -> RoundRecord:
        """
        いまの局を最後まで打つ（start_round → advance_discards → finish_round）。

        Args:
            before_discard_phase: 打牌フェーズ（先手の抽選）の直前に呼ぶ。実験で乱数をそろえるのに使う
            advance: 局の後、次の局（配牌）へ進むか。1 局だけ打つ実験では False
        """
        self.start_round(before_discard_phase)
        self.advance_discards()
        return self.finish_round(advance)

    def start_round(self, before_discard_phase: Optional[Callable[[], None]] = None) -> None:
        """手牌選択（スキル → 手牌確定）と掛け金を済ませ、打牌フェーズの入口まで進める。"""
        engine = self.engine
        if engine.state.round_state.status != RoundStatus.HAND_SELECTION:
            raise SimulationError(f"手牌選択フェーズではない: {engine.state.round_state.status}")
        for public in self.public.values():
            public.reset_round()

        self._round_number = engine.state.round_state.round_number
        self._health_before = {p.player_id: p.health for p in engine.state.players}
        self._skills: dict[str, list[str]] = {pid: [] for pid in PLAYER_IDS}
        # 打牌フェーズに入ったか（入らずに決着した局は精算していないので、精算結果を拾わない）
        self._entered_discard = False

        plans = {pid: self._hand_selection(pid) for pid in PLAYER_IDS}
        engine.selected_hand()
        if self.ended:
            return

        # 掛け金（流局の持ち越し中は掛け金のフェーズがない）
        if engine.state.round_state.status == RoundStatus.BETTING:
            for pid in PLAYER_IDS:
                player = engine.get_player_by_id(pid)
                if not engine.place_bet(player, self.agents[pid].choose_bet(self._obs(pid), plans[pid])):
                    engine.place_bet(player, fallback_bet(self._obs(pid)))
            if before_discard_phase is not None:
                before_discard_phase()
            engine.bet()
        elif before_discard_phase is not None:
            # 持ち越し中は selected_hand の中で打牌フェーズが始まっているので、乱数をそろえる効果はない
            before_discard_phase()

    def advance_discards(self, stop_for: Optional[str] = None) -> bool:
        """
        打牌（とロン確認）を進める。ロン確認は必ず受ける（エンジンは満貫以上で和了れるときだけ出す）。

        Args:
            stop_for: このプレイヤーの打牌の手番が来たら、打たずに止まる（学習する打ち手の番）

        Returns:
            stop_for の手番で止まったら True。局が終わったら False
        """
        engine = self.engine
        for _ in range(MAX_DISCARDS_PER_ROUND):
            if self.ended or engine.state.round_state.status != RoundStatus.DISCARD:
                return False
            self._entered_discard = True
            pending = engine.get_pending_agari()
            if pending is not None:
                engine.resolve_pending_agari(pending["winner_id"], True)
                continue
            pid = engine.get_current_player().player_id
            if pid == stop_for:
                return True
            self.discard(pid, self.agents[pid].choose_discard(self._obs(pid)))
        raise SimulationError("打牌が終わらない")

    def discard(self, pid: str, index: int) -> None:
        engine = self.engine
        if engine.get_current_player().player_id != pid:
            raise SimulationError(f"{pid} の手番ではない")
        if index in engine.get_player_by_id(pid).discarded_wall_indexes:
            raise SimulationError(f"{pid} が打牌済みの牌を選んだ: {index}")
        engine.discard(pid, index)

    def finish_round(self, advance: bool = True) -> RoundRecord:
        """局の記録を作り、advance なら次の局（配牌）へ進める。"""
        engine = self.engine
        liquidation = engine.get_last_liquidation_result() if self._entered_discard else None
        record = RoundRecord(
            round_number=self._round_number,
            is_draw=liquidation is None,
            winner=liquidation["winner_id"] if liquidation else None,
            health_before=self._health_before,
            health_after={p.player_id: p.health for p in engine.state.players},
            bets={p.player_id: p.bet for p in engine.state.players},
            skills=self._skills,
            liquidation=liquidation,
        )
        if advance and not self.ended and engine.state.round_state.status == RoundStatus.ROUND_END_WAITING:
            for pid in PLAYER_IDS:
                engine.confirm_next_round(pid)
        return record

    def _hand_selection(self, pid: str) -> Optional[HandPlan]:
        """スキル → 手牌確定（CpuController._act_hand_selection と同じ流れ）。"""
        agent = self.agents[pid]
        engine = self.engine
        player = engine.get_player_by_id(pid)
        obs = self._obs(pid)
        plan = agent.plan_hand(obs)
        for _ in range(MAX_SKILLS_PER_ROUND):
            data = agent.choose_skill(obs, plan)
            if data is None:
                break
            skill = data["skill_type"]
            # 弾かれても同じスキルを繰り返さないよう、使う前に数える（CpuController と同じ）
            mine = self.public[pid].my_skills_this_round
            mine[skill] = mine.get(skill, 0) + 1
            if engine.execute_skill(player, SkillType(skill), data) is None:
                continue
            self._skills[pid].append(skill)
            other = self.public[self._other(pid)]
            other.opponent_skills_this_round.append(skill)
            if skill == SkillType.BOOST_HAND.value:
                other.opponent_boost_use_count += 1
            obs = self._obs(pid)
            plan = agent.replan(obs, plan, skill)

        indexes = agent.choose_hand(obs, plan)
        # GameSession と同じく、満貫未満・非聴牌の手牌は確認のうえで確定する（select_confirm）
        if not engine.select_hand(indexes, player):
            engine.select_hand(indexes, player, force=True)
        return plan

    # ========== 1 対局 ==========

    def play_game(self, max_rounds: Optional[int] = None) -> GameRecord:
        record = GameRecord()
        while not self.ended:
            if max_rounds is not None and len(record.rounds) >= max_rounds:
                break
            record.rounds.append(self.play_round())
        record.end_reason = self.engine.game_end_reason
        record.final_health = {p.player_id: p.health for p in self.engine.state.players}
        return record
