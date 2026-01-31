using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace MandalaLogics.Splice
{
    public sealed partial class Splice<T>
    {
        public class SpliceEnumerator : IEnumerator<T>
        {
            public T Current { get; private set; } = null!;

            object? IEnumerator.Current => Current;

            private readonly Splice<T> _owner;
            private readonly int _threadId;
            private int _pos;
            
            public SpliceEnumerator(Splice<T> owner)
            {
                _owner = owner;
                _threadId = Thread.CurrentThread.ManagedThreadId;
                
                Reset();
            }
            
            public bool MoveNext()
            {
                CheckThreadId();
                
                _pos++;

                if (_pos >= _owner.Count) return false;
                else
                {

                    if (_owner._cache.TryGet(_pos) is { } ie)
                    {
                        Current = (T)ie;
                    }
                    else
                    {
                        Current = _owner.ReadFromChain(_pos);
                    }
                    
                    return true;
                }
            }

            public void Reset()
            {
                CheckThreadId();

                if (!_owner._enumLock.IsReadLockHeld)
                {
                    _owner._enumLock.EnterReadLock();
                }
                
                _pos = -1;
            }

            public void Dispose()
            {
                _owner._enumLock.ExitReadLock();
            }
            
            private void CheckThreadId()
            {
                if (Thread.CurrentThread.ManagedThreadId != _threadId)
                {
                    throw new InvalidOperationException("Weave enumerator cannot be moved between threads.");
                }
            }
        }
    }
}