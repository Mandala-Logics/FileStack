using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace MandalaLogics.Locking
{
    public sealed class SyncLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private readonly ThreadedCounter _counter = new ThreadedCounter();
        private bool _disposed;

        public bool Disposed
        {
            get
            {
                lock (_stateLock)
                    return _disposed;
            }
        }

        public IDisposable Take()
        {
            ThrowIfDisposed();

            if (_counter.Inc() == 1)
            {
                _semaphore.Wait();
            }
            
            return new Releaser(this);
        }

        public bool TryTake(TimeSpan timeout, out Releaser releaser)
        {
            ThrowIfDisposed();

            var count = _counter.Inc();

            if (count == 1)
            {
                if (!_semaphore.Wait(timeout))
                {
                    _counter.Dec(); // undo the increment, since we never acquired the lock
                    releaser = new Releaser();
                    return false;
                }
            }
    
            releaser = new Releaser(this);
            return true;
        }

        private void Release()
        {
            if (_counter.Dec() == 0)
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_disposed) return;
            
                _semaphore.Dispose();

                _disposed = true;
            }
        }

        public static ReleaserBundle TakeLocks(SyncLock first, SyncLock second)
        {
            var l1 = first.Take();
            var l2 = second.Take();

            return new ReleaserBundle(l1, l2);
        }
        
        public static ReleaserBundle TakeLocks(SyncLock first, SyncLock second, SyncLock third)
        {
            var l1 = first.Take();
            var l2 = second.Take();
            var l3 = third.Take();

            return new ReleaserBundle(l1, l2, l3);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SyncLock));
        }

        public sealed class Releaser : IDisposable
        {
            private SyncLock? _owner;

            internal Releaser() { }

            internal Releaser(SyncLock owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                _owner?.Release();
                _owner = null;
            }
        }
        
        public sealed class ReleaserBundle : IDisposable
        {
            private readonly IEnumerable<IDisposable> _releasers;

            internal ReleaserBundle(params IDisposable[] releasers)
            {
                _releasers = releasers;
            }
            
            public void Dispose()
            {
                foreach (var releaser in _releasers)
                {
                    releaser.Dispose();
                }
            }
        }
    }
}