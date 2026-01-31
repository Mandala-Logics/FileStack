using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using MandalaLogics.Encoding;

namespace MandalaLogics.Weave
{
    public partial class Weave
    {
        public class WeaveEnumerator : IEnumerator<IEncodable>
        {
            public IEncodable Current { get; private set; } = null!;

            object? IEnumerator.Current => Current;
            private readonly Weave _owner;
            private readonly int _threadId;

            private int _pos;

            internal WeaveEnumerator(Weave owner)
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
                    Current = _owner[_pos];
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
                CheckThreadId();
                
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