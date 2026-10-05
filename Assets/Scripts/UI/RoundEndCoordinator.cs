namespace KillingMahjong.UI
{
    /// <summary>局終了の重複・準備表示・OK一回を管理。勝敗と次局の承認はサーバーが決める。</summary>
    public sealed class RoundEndCoordinator
    {
        public enum Outcome { Agari, Draw }
        public enum Stage { Idle, Presenting, Waiting, Confirmed }
        public sealed class Ticket
        {
            public Outcome Result { get; private set; }
            internal Ticket(Outcome result) { Result = result; }
        }
        private Ticket current;
        public Stage CurrentStage { get; private set; }
        public bool LocalReady { get; private set; }
        public bool EnemyReady { get; private set; }
        public void Reset() { current = null; CurrentStage = Stage.Idle; LocalReady = EnemyReady = false; }
        public Ticket Begin(Outcome result)
        {
            if (CurrentStage != Stage.Idle) return null;
            current = new Ticket(result);
            CurrentStage = Stage.Presenting;
            return current;
        }
        public bool IsCurrent(Ticket ticket) => ticket != null && ticket == current;
        public bool FinishPresentation(Ticket ticket)
        {
            if (!IsCurrent(ticket) || CurrentStage != Stage.Presenting) return false;
            CurrentStage = Stage.Waiting;
            return true;
        }
        public bool Confirm(Ticket ticket)
        {
            if (!IsCurrent(ticket) || CurrentStage != Stage.Waiting) return false;
            CurrentStage = Stage.Confirmed;
            LocalReady = true;
            return true;
        }
        public void SetReady(bool local, bool enemy)
        {
            LocalReady = local || CurrentStage == Stage.Confirmed;
            EnemyReady = enemy;
        }
    }
}
