using System;
using KillingMahjong.EngineData;

namespace KillingMahjong.Network
{
    public enum CpuDifficulty { Easy, Normal, Hard, Special }

    /// <summary>受付契約と表示名だけ。CPUの判断や特典の計算はサーバーが持つ。</summary>
    public static class CpuDifficultyCatalog
    {
        public static string WireName(CpuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case CpuDifficulty.Easy: return "easy";
                case CpuDifficulty.Normal: return "normal";
                case CpuDifficulty.Hard: return "hard";
                case CpuDifficulty.Special: return "special";
                default: throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }
        public static string Label(CpuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case CpuDifficulty.Easy: return "やさしい";
                case CpuDifficulty.Normal: return "ふつう";
                case CpuDifficulty.Hard: return "つよい";
                case CpuDifficulty.Special: return "とくしゅ";
                default: throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }
    }

    public enum CpuJoinState { Pending, Matched, Failed, Cancelled }

    /// <summary>CPU席と指定の強さが確定した開始通知だけを受理する。一度終わった試行は変化しない。</summary>
    public sealed class CpuMatchJoinGate
    {
        public const float TimeoutSeconds = 15f;
        private readonly float deadline;
        public CpuDifficulty Difficulty { get; }
        public CpuJoinState State { get; private set; }
        public string Error { get; private set; }
        public bool IsPending => State == CpuJoinState.Pending;
        public CpuMatchJoinGate(CpuDifficulty difficulty, float now)
        {
            CpuDifficultyCatalog.WireName(difficulty);
            Difficulty = difficulty; deadline = now + TimeoutSeconds;
        }
        public void Accept(GameStartedData data)
        {
            if (!IsPending) return;
            if (data == null || data.mode != "cpu" || string.IsNullOrEmpty(data.match_id)
                || data.players == null || data.players.Length != 2)
            { Fail("CPU対戦の開始情報を確認できませんでした"); return; }
            PlayerRef cpu = null, human = null;
            foreach (var player in data.players)
            {
                if (player == null || string.IsNullOrEmpty(player.client_id))
                { Fail("CPU対戦の開始情報を確認できませんでした"); return; }
                if (player.is_cpu) { if (cpu != null) { Fail("CPU対戦の開始情報を確認できませんでした"); return; } cpu = player; }
                else human = player;
            }
            if (cpu == null || human == null || cpu.client_id == human.client_id || cpu.cpu == null
                || cpu.cpu.difficulty != CpuDifficultyCatalog.WireName(Difficulty))
            { Fail("選んだ強さでCPU対戦を開始できませんでした"); return; }
            State = CpuJoinState.Matched;
        }
        public void Fail(string error)
        {
            if (!IsPending) return;
            Error = string.IsNullOrEmpty(error) ? "CPU対戦を開始できませんでした" : error;
            State = CpuJoinState.Failed;
        }
        public void Tick(float now)
        { if (IsPending && now >= deadline) Fail("サーバーから応答がありません。もう一度お試しください"); }
        public void Cancel() { if (IsPending) State = CpuJoinState.Cancelled; }
    }
}
