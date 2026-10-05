using System;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    /// <summary>解除は取得したチケットだけに効く。同じ所有者名でも別の取得を解除しない。</summary>
    public sealed class TransitionLockSet
    {
        public sealed class Lease : IDisposable
        {
            private readonly TransitionLockSet source;
            private readonly int generation;
            public string Owner { get; private set; }
            internal Lease(TransitionLockSet source, string owner, int generation)
            { this.source = source; Owner = owner; this.generation = generation; }
            public bool IsActive => generation == source.generation && source.active.Contains(this);
            public void Dispose() { source.Release(this); }
        }

        private readonly HashSet<Lease> active = new HashSet<Lease>();
        private readonly Action changed;
        private int generation;
        public bool IsLocked => active.Count > 0;
        public TransitionLockSet(Action changed = null) { this.changed = changed; }
        public Lease Acquire(string owner)
        {
            if (string.IsNullOrEmpty(owner)) throw new ArgumentException("Lock owner is required", nameof(owner));
            bool wasLocked = IsLocked;
            var lease = new Lease(this, owner, generation);
            active.Add(lease);
            if (!wasLocked) changed?.Invoke();
            return lease;
        }
        private void Release(Lease lease)
        {
            if (!active.Remove(lease)) return;
            if (!IsLocked) changed?.Invoke();
        }
        public void Reset()
        {
            bool wasLocked = IsLocked;
            active.Clear();
            generation++;
            if (wasLocked) changed?.Invoke();
        }
    }
}
