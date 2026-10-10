using System;
using System.Collections;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    // 一つの演出が作った物と復元処理を、終了・例外・中止で同じ順序で片付ける。
    public sealed class PresentationScope : IDisposable
    {
        private readonly List<Action> cleanups = new List<Action>();
        private readonly Action<Exception> reportError;
        public bool IsActive { get; private set; } = true;
        public PresentationScope(Action<Exception> reportError = null) { this.reportError = reportError; }
        public void AddCleanup(Action cleanup)
        {
            if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
            if (IsActive) cleanups.Add(cleanup);
            else Clean(cleanup);
        }
        private void Clean(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception error) { reportError?.Invoke(error); }
        }
        public void Dispose()
        {
            if (!IsActive) return;
            IsActive = false;
            var pending = cleanups.ToArray();
            cleanups.Clear();
            for (int i = pending.Length - 1; i >= 0; i--) Clean(pending[i]);
        }
        // 子IEnumeratorも同じ失効条件で実行する。別のMonoBehaviourからyieldされても中止が効く。
        public IEnumerator Run(IEnumerator routine)
        {
            try
            {
                while (IsActive && Advance(routine))
                {
                    var child = routine.Current as IEnumerator;
                    yield return child != null ? Run(child) : routine.Current;
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
        }
        private bool Advance(IEnumerator routine)
        {
            try { return routine.MoveNext(); }
            // Unityが親コルーチンのfinallyを呼ぶかに依存せず、子の例外でも直ちに片付ける。
            catch { Dispose(); throw; }
        }
    }

    public sealed class PresentationScopeSet
    {
        private readonly List<PresentationScope> active = new List<PresentationScope>();
        private readonly Action<Exception> reportError;
        public PresentationScopeSet(Action<Exception> reportError = null) { this.reportError = reportError; }
        public PresentationScope Begin()
        {
            var scope = new PresentationScope(reportError);
            active.Add(scope);
            scope.AddCleanup(() => active.Remove(scope));
            return scope;
        }
        public void CancelAll()
        {
            // Dispose中の登録解除で列挙を壊さない。新しく始まった演出はこの中止の対象外。
            var pending = active.ToArray();
            for (int i = pending.Length - 1; i >= 0; i--) pending[i].Dispose();
        }
    }
}
